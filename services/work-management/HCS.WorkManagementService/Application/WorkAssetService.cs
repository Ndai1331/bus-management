using HCS.WorkManagementService.Contracts;
using HCS.WorkManagementService.Data;
using HCS.WorkManagementService.Domain;
using HCS.WorkManagementService.Storage;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Authorization;

namespace HCS.WorkManagementService.Application;

public sealed class WorkAssetService(IBlobContainer<WorkAssetBlobContainer> blobs, WorkManagementDbContext db,
    WorkRecordAuthorization access) : ITransientDependency
{
    public const long MaxFileSize = 25 * 1024 * 1024;

    public async Task<SurveyFileReferenceDto> SaveSurveyFileAsync(Guid sessionId, Stream stream, string fileName,
        string contentType, long size, CancellationToken ct)
    {
        if (size is <= 0 or > MaxFileSize) throw new BusinessException("Work:InvalidAssetSize");
        await access.DemandSurveyOwnerAsync(sessionId, ct);
        if (!await db.SurveySessions.AnyAsync(x => x.Id == sessionId, ct)) throw new EntityNotFoundException(typeof(SurveySession), sessionId);
        var id = Guid.NewGuid(); var blobName = WorkAssetBlobNamePolicy.Survey(sessionId, id);
        await blobs.SaveAsync(blobName, stream, overrideExisting: false, cancellationToken: ct);
        var item = new SurveyFileReference(id, sessionId, access.UserId, blobName, Path.GetFileName(fileName), contentType, size);
        db.SurveyFiles.Add(item);
        try { await db.SaveChangesAsync(ct); }
        catch { await blobs.DeleteAsync(blobName, cancellationToken: ct); throw; }
        return Map(item);
    }

    public async Task<SurveyFileReferenceDto> SavePublicSurveyFileAsync(Guid sessionId, Stream stream, string fileName,
        string contentType, long size, CancellationToken ct)
    {
        if (size is <= 0 or > MaxFileSize) throw new BusinessException("Work:InvalidAssetSize");
        if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("Work:SurveyImageOnly");
        if (!await db.SurveySessions.AnyAsync(x => x.Id == sessionId && x.IsPublic, ct))
            throw new EntityNotFoundException(typeof(SurveySession), sessionId);
        var id = Guid.NewGuid(); var blobName = WorkAssetBlobNamePolicy.Survey(sessionId, id);
        await blobs.SaveAsync(blobName, stream, overrideExisting: false, cancellationToken: ct);
        var item = new SurveyFileReference(id, sessionId, Guid.Empty, blobName, Path.GetFileName(fileName), contentType, size);
        db.SurveyFiles.Add(item);
        try { await db.SaveChangesAsync(ct); }
        catch { await blobs.DeleteAsync(blobName, cancellationToken: ct); throw; }
        return Map(item);
    }

    public async Task<(Stream Stream, SurveyFileReferenceDto File)> GetSurveyFileAsync(Guid fileId, CancellationToken ct)
    {
        var item = await db.SurveyFiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == fileId, ct)
            ?? throw new EntityNotFoundException(typeof(SurveyFileReference), fileId);
        if (!access.IsAdministrator && item.UploadedByUserId != access.UserId &&
            !await db.SurveySessions.AnyAsync(x => x.Id == item.SessionId && x.OwnerUserId == access.UserId, ct))
            throw new AbpAuthorizationException("Survey asset owner required.");
        var stream = await blobs.GetAsync(item.BlobName, cancellationToken: ct);
        return (stream, Map(item));
    }

    private static SurveyFileReferenceDto Map(SurveyFileReference item) =>
        new(item.Id, item.SessionId, item.FileName, item.ContentType, item.Size);
}
