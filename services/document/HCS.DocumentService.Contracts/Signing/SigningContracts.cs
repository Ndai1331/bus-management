using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using HCS.DocumentService.Documents;
using HCS.DocumentService.Workflows;

namespace HCS.DocumentService.Signing;

// Keep the provider order compatible with the existing free-license API.
// UsbToken is the licensed provider's USB/token option.
public enum SigningKind { Electronic, RemoteCa, Hsm, UsbToken }
public enum UserSignatureType { Electronic, Digital }
public enum SigningStatus { Pending, Completed, Failed }

public sealed class ConfigureSigningCredentialRequest
{
    public ConfigureSigningCredentialRequest() { }

    public ConfigureSigningCredentialRequest(SigningKind kind, string endpoint, string secret,
        string providerCode = "", string? layoutImageBase64 = null, int apiTimeoutSeconds = 30,
        int signWidth = 150, int signHeight = 70, bool allowElectronicSign = true,
        bool allowDigitalSign = true, bool requireOtp = false)
    {
        Kind = kind;
        Endpoint = endpoint;
        Secret = secret;
        ProviderCode = providerCode;
        LayoutImageBase64 = layoutImageBase64;
        ApiTimeoutSeconds = apiTimeoutSeconds;
        SignWidth = signWidth;
        SignHeight = signHeight;
        AllowElectronicSign = allowElectronicSign;
        AllowDigitalSign = allowDigitalSign;
        RequireOtp = requireOtp;
    }

    public SigningKind Kind { get; init; }
    [StringLength(256)]
    public string ProviderCode { get; init; } = string.Empty;
    [Required, StringLength(1024)]
    public string Endpoint { get; init; } = string.Empty;
    [StringLength(4_000_000)]
    public string? LayoutImageBase64 { get; init; }
    public int ApiTimeoutSeconds { get; init; } = 30;
    public int SignWidth { get; init; } = 150;
    public int SignHeight { get; init; } = 70;
    public bool AllowElectronicSign { get; init; } = true;
    public bool AllowDigitalSign { get; init; } = true;
    public bool RequireOtp { get; init; }
    [JsonPropertyName("secret")]
    public string Secret { private get; init; } = string.Empty;
    public string ConsumeSecret() => Secret;
}

public sealed record SigningCredentialDto(Guid Id, SigningKind Kind, string ProviderCode, string Endpoint,
    string MaskedSecret, int ApiTimeoutSeconds, int SignWidth, int SignHeight,
    bool AllowElectronicSign, bool AllowDigitalSign, bool RequireOtp, DateTime UpdatedAt,
    bool HasLayoutImage = false)
{
    public SigningCredentialDto(Guid id, SigningKind kind, string endpoint, string maskedSecret, DateTime updatedAt)
        : this(id, kind, string.Empty, endpoint, maskedSecret, 30, 150, 70, true, true, false, updatedAt, false) { }
}
public sealed record SigningProviderDefinitionDto(
    string Code,
    string DisplayName,
    IReadOnlyList<SigningKind> SupportedKinds,
    string? DefaultEndpoint,
    bool RequiresLayoutImage,
    bool RequiresSealImage,
    bool RequiresBase64Secret,
    int DefaultApiTimeoutSeconds,
    int DefaultSignWidth,
    int DefaultSignHeight);
public sealed record UserSignatureDto(Guid Id, string FileName, string ContentType, long Size, bool IsDefault, DateTime CreationTime,
    UserSignatureType Type = UserSignatureType.Electronic, string ProviderCode = "", string TokenRef = "",
    DateTime? ValidFrom = null, DateTime? ValidTo = null, bool IsActive = true, bool HasSealImage = false);
public sealed record SignDocumentRequest(Guid DocumentId, Guid FileId, SigningKind Kind,
    [param: Required, StringLength(128, MinimumLength = 1)] string IdempotencyKey,
    Guid? SignatureId = null, string? Placeholder = null, string? SignerName = null, string? Note = null);
public sealed record SigningAttemptDto(Guid Id, Guid DocumentId, Guid FileId, SigningKind Kind, SigningStatus Status,
    string InputSha256, string? OutputSha256, string? Error, DateTime CreationTime, DateTime? CompletedAt);
public sealed record SigningReportDto(Guid DocumentId, int Completed, int Failed, IReadOnlyList<SigningAttemptDto> Attempts);
public sealed record SigningQueueDocumentDto(Guid Id, string Number, string Title, string? Description, DocumentStatus Status,
    IReadOnlyList<DocumentFileDto> Files, DateTime CreationTime, DocumentSourceType SourceType = DocumentSourceType.Workflow,
    Guid? FromUserId = null);
public sealed record SigningQueueItemDto(SigningQueueDocumentDto Document, ApprovalTaskDto Task, WorkflowInstanceDto Instance,
    WorkflowDefinitionDto Definition);

public interface ISigningAppService
{
    Task<IReadOnlyList<SigningQueueItemDto>> GetQueueAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SigningProviderDefinitionDto>> GetProviderDefinitionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SigningCredentialDto>> GetCredentialsAsync(Guid? userId = null, CancellationToken cancellationToken = default);
    Task<SigningCredentialDto> ConfigureCredentialAsync(ConfigureSigningCredentialRequest input, Guid? userId = null, CancellationToken cancellationToken = default);
    Task<SigningAttemptDto> SignAsync(SignDocumentRequest input, CancellationToken cancellationToken = default);
    Task<SigningReportDto> GetReportAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserSignatureDto>> GetSignaturesAsync(Guid? userId = null, CancellationToken cancellationToken = default);
    Task<UserSignatureDto> UploadSignatureAsync(string fileName, string contentType, Stream content, long size,
        UserSignatureType type = UserSignatureType.Electronic, Guid? userId = null, string? providerCode = null,
        string? tokenRef = null, string? secret = null, string? sealImageBase64 = null,
        DateTime? validFrom = null, DateTime? validTo = null, bool isActive = true,
        CancellationToken cancellationToken = default);
    Task<UserSignatureDto> UpdateSignatureAsync(Guid id, string? fileName, string? contentType, Stream? content, long? size,
        UserSignatureType? type = null, Guid? userId = null, string? providerCode = null,
        string? tokenRef = null, string? secret = null, string? sealImageBase64 = null,
        DateTime? validFrom = null, DateTime? validTo = null, bool? isActive = null,
        CancellationToken cancellationToken = default);
    Task<UserSignatureDto> SetDefaultSignatureAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default);
    Task DeleteSignatureAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default);
    Task<(Stream Content, string ContentType, string FileName)> OpenSignatureContentAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default);
}
