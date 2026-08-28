using Volo.Abp.BlobStoring;

namespace HCS.DocumentService.Storage;

[BlobContainerName("hcs-documents")]
public sealed class DocumentBlobContainer;

[BlobContainerName("hcs-signing")]
public sealed class SigningBlobContainer;

public static class BlobNamePolicy
{
    public static string Document(Guid documentId, Guid fileId) => $"documents/{documentId:N}/{fileId:N}";
    public static string Signing(Guid documentId, Guid attemptId) => $"signing/{documentId:N}/{attemptId:N}";
    public static string UserSignature(Guid userId, Guid signatureId) => $"signatures/{userId:N}/{signatureId:N}";
    public static string WorkflowTemplate(Guid templateId, Guid fileId) => $"workflows/{templateId:N}/{fileId:N}";
}
