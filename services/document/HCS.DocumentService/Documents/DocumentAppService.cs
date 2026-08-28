using System.Security.Claims;
using HCS.DocumentService.Integration;
using HCS.IntegrationEvents.Auditing;
using HCS.IntegrationEvents.Documents;
using Microsoft.EntityFrameworkCore;

namespace HCS.DocumentService.Documents;

public sealed class DocumentAppService(DocumentServiceDbContext db, IHttpContextAccessor httpContext) : IDocumentAppService
{
    public async Task<PagedDocumentsDto> GetListAsync(string? filter = null, DocumentStatus? status = null,
        bool mine = false, int skip = 0, int take = 50, int? sourceType = null,
        Guid? documentTypeId = null, Guid? sectorId = null, Guid? urgencyId = null, Guid? confidentialityId = null,
        DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var principal = Principal;
        var userId = DocumentAccess.RequireUser(principal);
        DocumentAccess.RequirePermission(principal, DocumentPermissions.View);
        take = Math.Clamp(take, 1, 100);
        skip = Math.Max(skip, 0);
        var query = Query().Where(x => x.SourceType != DocumentSourceType.Workflow || sourceType == 3);
        query = sourceType switch
        {
            1 => query.Where(x => x.SourceType == DocumentSourceType.Personal),
            2 => query.Where(x => x.Assignments.Any(a => a.AssigneeUserId == userId && a.IsCurrent &&
                                  a.Responsibility == "VIEW" && a.StepCode == null)),
            0 when !DocumentAccess.IsElevated(principal) =>
                query.Where(x => x.SourceType == DocumentSourceType.Archive &&
                                 (x.Assignments.Any(a => a.AssigneeUserId == userId) ||
                                  x.History.Any(h => h.Action == "Created" && h.ActorUserId == userId))),
            0 => query.Where(x => x.SourceType == DocumentSourceType.Archive),
            3 when !DocumentAccess.IsElevated(principal) =>
                query.Where(x => x.SourceType == DocumentSourceType.Workflow &&
                                 (x.Assignments.Any(a => a.AssigneeUserId == userId) ||
                                  x.History.Any(h => h.Action == "Created" && h.ActorUserId == userId))),
            3 => query.Where(x => x.SourceType == DocumentSourceType.Workflow),
            _ when mine || !DocumentAccess.IsElevated(principal) =>
                query.Where(x => x.Assignments.Any(a => a.AssigneeUserId == userId) ||
                                 x.History.Any(h => h.Action == "Created" && h.ActorUserId == userId)),
            _ => query
        };
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var value = filter.Trim().ToLowerInvariant();
            query = query.Where(x => EF.Functions.ILike(x.Number, $"%{value}%") ||
                                     EF.Functions.ILike(x.Title, $"%{value}%"));
        }
        if (status.HasValue) query = query.Where(x => x.Status == status);
        if (documentTypeId.HasValue) query = query.Where(x => x.DocumentTypeId == documentTypeId);
        if (sectorId.HasValue) query = query.Where(x => x.SectorId == sectorId);
        if (urgencyId.HasValue) query = query.Where(x => x.UrgencyId == urgencyId);
        if (confidentialityId.HasValue) query = query.Where(x => x.ConfidentialityId == confidentialityId);
        if (from.HasValue) query = query.Where(x => x.CreationTime >= from.Value.ToUniversalTime());
        if (to.HasValue) query = query.Where(x => x.CreationTime < to.Value.ToUniversalTime().AddDays(1));
        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreationTime).Skip(skip).Take(take)
            .ToListAsync(cancellationToken);
        return new PagedDocumentsDto(totalCount, items.Select(Map).ToList());
    }

    public async Task<DocumentDto> CreateAsync(CreateDocumentRequest input, CancellationToken cancellationToken = default)
    {
        var principal = Principal;
        var userId = DocumentAccess.RequireUser(principal);
        DocumentAccess.RequirePermission(principal, DocumentPermissions.Create);
        var now = DateTime.UtcNow;
        var sourceType = input.SourceType is DocumentSourceType.Personal ? DocumentSourceType.Personal : DocumentSourceType.Archive;
        var number = await ResolveNumberAsync(input.Number, now, cancellationToken);
        var document = new DocumentAggregate(Guid.NewGuid(), number, input.Title, input.Description, userId, now, sourceType);
        if (input.DocumentTypeId is not null || input.SectorId is not null || input.UrgencyId is not null || input.ConfidentialityId is not null)
            document.Classify(input.DocumentTypeId, input.SectorId, input.UrgencyId, input.ConfidentialityId, userId, now);
        db.Documents.Add(document);
        AddAudit("DocumentCreated", document.Id, 201, null, now);
        await db.SaveChangesAsync(cancellationToken);
        return Map(document);
    }

    public async Task<DocumentDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var principal = Principal;
        var userId = DocumentAccess.RequireUser(principal);
        DocumentAccess.RequirePermission(principal, DocumentPermissions.View);
        var document = await Query().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document is not null) DocumentAccess.EnsureCanView(document, userId, principal);
        return document is null ? null : Map(document);
    }

    public async Task<DocumentDto> UpdateAsync(Guid id, UpdateDocumentRequest input, CancellationToken cancellationToken = default)
    {
        var principal = Principal;
        var userId = DocumentAccess.RequireUser(principal);
        DocumentAccess.RequirePermission(principal, DocumentPermissions.Update);
        var document = await LoadAsync(id, cancellationToken);
        DocumentAccess.EnsureCanManage(document, userId, principal);
        var existingAssignmentIds = document.Assignments.Select(x => x.Id).ToHashSet();
        var existingHistoryIds = document.History.Select(x => x.Id).ToHashSet();
        document.Update(input.Title, input.Description, userId, DateTime.UtcNow);
        document.Classify(input.DocumentTypeId, input.SectorId, input.UrgencyId, input.ConfidentialityId, userId, DateTime.UtcNow);
        TrackNewChildren(db, document, existingAssignmentIds, existingHistoryIds);
        AddAudit("DocumentUpdated", id, 200, null, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return Map(document);
    }

    public async Task<DocumentDto> AssignAsync(Guid id, AssignDocumentRequest input, CancellationToken cancellationToken = default)
    {
        var principal = Principal;
        var userId = DocumentAccess.RequireUser(principal);
        DocumentAccess.RequirePermission(principal, DocumentPermissions.Assign);
        var document = await LoadAsync(id, cancellationToken);
        DocumentAccess.EnsureCanManage(document, userId, principal);
        var existingAssignmentIds = document.Assignments.Select(x => x.Id).ToHashSet();
        var existingHistoryIds = document.History.Select(x => x.Id).ToHashSet();
        var now = DateTime.UtcNow;
        var before = document.Assignments.Count;
        var assignment = document.Assign(Guid.NewGuid(), input.AssigneeUserId, input.Responsibility, userId, now);
        if (document.Assignments.Count != before)
        {
            var integrationEvent = new DocumentAssignedEto(Guid.NewGuid(), now, CorrelationId, id,
                assignment.Id, input.AssigneeUserId, null, assignment.Responsibility);
            db.OutboxMessages.Add(OutboxFactory.CreateCanonical(integrationEvent, CorrelationId, now));
        }
        TrackNewChildren(db, document, existingAssignmentIds, existingHistoryIds);
        AddAudit("DocumentAssigned", id, 200, input.Responsibility, now);
        await db.SaveChangesAsync(cancellationToken);
        return Map(document);
    }

    public async Task<DocumentDto> SubmitAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var principal = Principal;
        var userId = DocumentAccess.RequireUser(principal);
        DocumentAccess.RequirePermission(principal, DocumentPermissions.Update);
        var document = await LoadAsync(id, cancellationToken);
        DocumentAccess.EnsureCanManage(document, userId, principal);
        var existingAssignmentIds = document.Assignments.Select(x => x.Id).ToHashSet();
        var existingHistoryIds = document.History.Select(x => x.Id).ToHashSet();
        document.Submit(userId, DateTime.UtcNow);
        TrackNewChildren(db, document, existingAssignmentIds, existingHistoryIds);
        AddAudit("DocumentSubmitted", id, 200, null, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return Map(document);
    }

    public async Task<DocumentDto> SendAsync(Guid id, SendDocumentRequest input, CancellationToken cancellationToken = default)
    {
        var principal = Principal;
        var userId = DocumentAccess.RequireUser(principal);
        DocumentAccess.RequirePermission(principal, DocumentPermissions.Assign);
        var document = await LoadAsync(id, cancellationToken);
        DocumentAccess.EnsureCanManage(document, userId, principal);
        var existingAssignmentIds = document.Assignments.Select(x => x.Id).ToHashSet();
        var existingHistoryIds = document.History.Select(x => x.Id).ToHashSet();
        document.Send(input.ReceiverUserId, input.OrganizationUnitId, userId, DateTime.UtcNow);
        TrackNewChildren(db, document, existingAssignmentIds, existingHistoryIds);
        AddAudit("DocumentSent", id, 200, input.ReceiverUserId?.ToString() ?? input.OrganizationUnitId?.ToString(), DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return Map(document);
    }

    public async Task<DocumentDto> RevokeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var principal = Principal;
        var userId = DocumentAccess.RequireUser(principal);
        DocumentAccess.RequirePermission(principal, DocumentPermissions.Assign);
        var document = await LoadAsync(id, cancellationToken);
        DocumentAccess.EnsureCanManage(document, userId, principal);
        var existingAssignmentIds = document.Assignments.Select(x => x.Id).ToHashSet();
        var existingHistoryIds = document.History.Select(x => x.Id).ToHashSet();
        document.RevokeInbox(userId, DateTime.UtcNow);
        TrackNewChildren(db, document, existingAssignmentIds, existingHistoryIds);
        AddAudit("DocumentRevoked", id, 200, null, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return Map(document);
    }

    private IQueryable<DocumentAggregate> Query() => db.Documents.AsNoTracking().AsSplitQuery()
        .Include(x => x.Files).Include(x => x.Assignments).Include(x => x.History);
    private async Task<DocumentAggregate> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Documents.AsSplitQuery().Include(x => x.Files).Include(x => x.Assignments).Include(x => x.History)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new KeyNotFoundException("Document not found.");
    private ClaimsPrincipal Principal => httpContext.HttpContext?.User ?? new ClaimsPrincipal();
    private Guid? UserId => Guid.TryParse(httpContext.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private string CorrelationId => httpContext.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");

    internal static void TrackNewChildren(DocumentServiceDbContext db, DocumentAggregate document,
        IReadOnlySet<Guid> existingAssignmentIds, IReadOnlySet<Guid> existingHistoryIds)
    {
        // Aggregate methods append client-generated Guid children through private
        // backing fields. Track only children created by this request so EF inserts
        // them instead of treating non-empty keys as existing rows.
        db.DocumentAssignments.AddRange(document.Assignments.Where(x => !existingAssignmentIds.Contains(x.Id)));
        db.DocumentHistories.AddRange(document.History.Where(x => !existingHistoryIds.Contains(x.Id)));
    }

    private async Task<string> ResolveNumberAsync(string? requested, DateTime now, CancellationToken cancellationToken)
    {
        var normalized = requested?.Trim();
        if (!string.IsNullOrWhiteSpace(normalized)) return normalized;

        string number;
        do
        {
            number = GenerateNumber(now);
        }
        while (await db.Documents.AnyAsync(x => x.Number == number, cancellationToken));

        return number;
    }

    internal static string GenerateNumber(DateTime now) =>
        $"VB-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    private void AddAudit(string action, Guid id, int status, string? detail, DateTime now)
    {
        var audit = new AuditRecordCapturedEto(Guid.NewGuid(), "HCS.DocumentService", "HCS.DocumentService",
            UserId, Principal.Identity?.Name, now, 0, action, httpContext.HttpContext?.Request.Method,
            httpContext.HttpContext?.Request.Path, status, CorrelationId,
            httpContext.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            httpContext.HttpContext?.Request.Headers.UserAgent, null, detail, [],
            [new AuditEntityChangeCapturedEto(Guid.NewGuid(), now, action, id.ToString(), nameof(DocumentAggregate))]);
        db.OutboxMessages.Add(OutboxFactory.CreateAudit(audit, CorrelationId, now));
    }
    internal static DocumentDto Map(DocumentAggregate x) => new(x.Id, x.Number, x.Title, x.Description, x.Status,
        x.DocumentTypeId, x.SectorId, x.UrgencyId, x.ConfidentialityId,
        x.Files.Select(f => new DocumentFileDto(f.Id, f.FileName, f.ContentType, f.Size, f.Sha256, f.CreationTime, f.PairedFileId)).ToList(),
        x.Assignments.Select(a => new DocumentAssignmentDto(a.Id, a.AssigneeUserId, a.Responsibility, a.AssignedAt, a.IsCurrent, a.StepCode)).ToList(),
        x.History.OrderBy(h => h.OccurredAt).Select(h => new DocumentHistoryDto(h.Id, h.Action, h.ActorUserId, h.Detail, h.OccurredAt)).ToList(),
        x.CreationTime, x.SourceType, x.ParentDocumentId, x.FromUserId, x.OrganizationUnitId);
}
