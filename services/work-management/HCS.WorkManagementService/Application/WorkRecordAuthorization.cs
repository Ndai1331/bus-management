using HCS.WorkManagementService.Data;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace HCS.WorkManagementService.Application;

public static class SurveyAccessRules
{
    public static bool CanSubmit(bool isAdministrator, Guid ownerUserId, Guid userId, string status,
        DateTime startsAt, DateTime endsAt, DateTime nowUtc) =>
        status == "Active" && startsAt <= nowUtc && endsAt >= nowUtc;
    public static bool CanManage(bool isAdministrator, Guid ownerUserId, Guid userId) =>
        isAdministrator || ownerUserId == userId;
}

public sealed class WorkRecordAuthorization(WorkManagementDbContext db, ICurrentUser currentUser) : ITransientDependency
{
    public Guid UserId => currentUser.Id ?? throw new AbpAuthorizationException("Authenticated user required.");
    public bool IsAdministrator => currentUser.IsInRole("admin") || currentUser.IsInRole("bd-admin");

    public IQueryable<Domain.Project> VisibleProjects() => IsAdministrator
        ? db.Projects
        : db.Projects.Where(x => x.OwnerUserId == UserId ||
            db.ProjectMembers.Any(m => m.ProjectId == x.Id && m.UserId == UserId && m.IsActive));

    public async Task DemandProjectMemberAsync(Guid projectId, CancellationToken ct)
    {
        if (IsAdministrator) return;
        if (!await db.Projects.AnyAsync(x => x.Id == projectId && (x.OwnerUserId == UserId ||
                db.ProjectMembers.Any(m => m.ProjectId == x.Id && m.UserId == UserId && m.IsActive)), ct))
            throw new AbpAuthorizationException("Project membership required.");
    }

    public async Task DemandProjectOwnerAsync(Guid projectId, CancellationToken ct)
    {
        if (IsAdministrator) return;
        if (!await db.Projects.AnyAsync(x => x.Id == projectId && x.OwnerUserId == UserId, ct))
            throw new AbpAuthorizationException("Project owner required.");
    }

    public async Task DemandTaskMemberAsync(Guid taskId, CancellationToken ct)
    {
        if (IsAdministrator) return;
        if (!await db.ProjectTasks.AnyAsync(t => t.Id == taskId &&
                (db.Projects.Any(p => p.Id == t.ProjectId && p.OwnerUserId == UserId) ||
                 db.ProjectMembers.Any(m => m.ProjectId == t.ProjectId && m.UserId == UserId && m.IsActive) ||
                 db.ProjectTaskAssignments.Any(a => a.ProjectTaskId == t.Id && a.UserId == UserId)), ct))
            throw new AbpAuthorizationException("Task membership required.");
    }

    public Task DemandTaskOwnerAsync(Guid taskId, CancellationToken ct) => IsAdministrator
        ? Task.CompletedTask
        : DemandTaskOwnerCoreAsync(taskId, ct);

    private async Task DemandTaskOwnerCoreAsync(Guid taskId, CancellationToken ct)
    {
        if (!await db.ProjectTasks.AnyAsync(t => t.Id == taskId &&
                db.Projects.Any(p => p.Id == t.ProjectId && p.OwnerUserId == UserId), ct))
            throw new AbpAuthorizationException("Project owner required.");
    }

    public async Task DemandSurveyOwnerAsync(Guid sessionId, CancellationToken ct)
    {
        if (IsAdministrator) return;
        if (!await db.SurveySessions.AnyAsync(x => x.Id == sessionId && x.OwnerUserId == UserId, ct))
            throw new AbpAuthorizationException("Survey owner required.");
    }

    public async Task DemandSurveyAudienceAsync(Guid sessionId, DateTime nowUtc, CancellationToken ct)
    {
        var session = await db.SurveySessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sessionId, ct)
            ?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Domain.SurveySession), sessionId);
        if (!SurveyAccessRules.CanSubmit(IsAdministrator, session.OwnerUserId, UserId, session.Status,
                session.StartsAt, session.EndsAt, nowUtc))
            throw new AbpAuthorizationException("Survey is outside its active audience window.");
    }
}
