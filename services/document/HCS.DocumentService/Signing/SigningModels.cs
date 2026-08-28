namespace HCS.DocumentService.Signing;

public sealed class SigningCredential
{
    private SigningCredential() { }
    public SigningCredential(Guid id, Guid userId, SigningKind kind, string endpoint, string protectedSecret, DateTime now,
        string? providerCode = null, string? layoutImageBase64 = null, int apiTimeoutSeconds = 30,
        int signWidth = 150, int signHeight = 70, bool allowElectronicSign = true,
        bool allowDigitalSign = true, bool requireOtp = false)
    {
        Id = id;
        UserId = userId;
        Kind = kind;
        Replace(endpoint, protectedSecret, now, providerCode, layoutImageBase64, apiTimeoutSeconds,
            signWidth, signHeight, allowElectronicSign, allowDigitalSign, requireOtp);
    }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public SigningKind Kind { get; private set; }
    public string Endpoint { get; private set; } = string.Empty;
    public string ProtectedSecret { get; private set; } = string.Empty;
    public string ProviderCode { get; private set; } = string.Empty;
    public string? LayoutImageBase64 { get; private set; }
    public int ApiTimeoutSeconds { get; private set; } = 30;
    public int SignWidth { get; private set; } = 150;
    public int SignHeight { get; private set; } = 70;
    public bool AllowElectronicSign { get; private set; } = true;
    public bool AllowDigitalSign { get; private set; } = true;
    public bool RequireOtp { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public void Replace(string endpoint, string protectedSecret, DateTime now,
        string? providerCode = null, string? layoutImageBase64 = null, int apiTimeoutSeconds = 30,
        int signWidth = 150, int signHeight = 70, bool allowElectronicSign = true,
        bool allowDigitalSign = true, bool requireOtp = false)
    {
        Endpoint = endpoint.Trim();
        ProtectedSecret = protectedSecret;
        ProviderCode = providerCode?.Trim() ?? string.Empty;
        LayoutImageBase64 = string.IsNullOrWhiteSpace(layoutImageBase64) ? null : layoutImageBase64.Trim();
        ApiTimeoutSeconds = Math.Clamp(apiTimeoutSeconds, 5, 600);
        SignWidth = Math.Clamp(signWidth, 40, 1000);
        SignHeight = Math.Clamp(signHeight, 20, 1000);
        AllowElectronicSign = allowElectronicSign;
        AllowDigitalSign = allowDigitalSign;
        RequireOtp = requireOtp;
        UpdatedAt = now;
    }
}

public sealed class UserSignature
{
    private UserSignature() { }
    public UserSignature(Guid id, Guid userId, string fileName, string contentType, string blobName, long size, DateTime now,
        UserSignatureType type = UserSignatureType.Electronic, string? providerCode = null, string? tokenRef = null,
        string? protectedSecret = null, string? sealImageBase64 = null, DateTime? validFrom = null,
        DateTime? validTo = null, bool isActive = true)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User is required.", nameof(userId));
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        Id = id;
        UserId = userId;
        FileName = fileName.Trim();
        ContentType = contentType.Trim();
        BlobName = blobName;
        Size = size;
        Type = type;
        ProviderCode = providerCode?.Trim() ?? string.Empty;
        TokenRef = tokenRef?.Trim() ?? string.Empty;
        ProtectedSecret = protectedSecret;
        SealImageBase64 = string.IsNullOrWhiteSpace(sealImageBase64) ? null : sealImageBase64.Trim();
        ValidFrom = validFrom;
        ValidTo = validTo;
        IsActive = isActive;
        IsDefault = false;
        CreationTime = now;
    }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public string BlobName { get; private set; } = string.Empty;
    public long Size { get; private set; }
    public UserSignatureType Type { get; private set; }
    public string ProviderCode { get; private set; } = string.Empty;
    public string TokenRef { get; private set; } = string.Empty;
    public string? ProtectedSecret { get; private set; }
    public string? SealImageBase64 { get; private set; }
    public DateTime? ValidFrom { get; private set; }
    public DateTime? ValidTo { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsDefault { get; private set; }
    public DateTime CreationTime { get; private set; }
    public void MarkDefault() => IsDefault = true;
    public void ClearDefault() => IsDefault = false;
    public void ChangeType(UserSignatureType type)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        Type = type;
    }
    public void UpdateMetadata(string? providerCode, string? tokenRef, string? protectedSecret,
        string? sealImageBase64, DateTime? validFrom, DateTime? validTo, bool? isActive)
    {
        if (validFrom.HasValue && validTo.HasValue && validFrom > validTo)
            throw new ArgumentException("Signature validity range is invalid.");
        ProviderCode = providerCode?.Trim() ?? ProviderCode;
        TokenRef = tokenRef?.Trim() ?? TokenRef;
        if (protectedSecret is not null) ProtectedSecret = protectedSecret;
        if (sealImageBase64 is not null) SealImageBase64 = string.IsNullOrWhiteSpace(sealImageBase64) ? null : sealImageBase64.Trim();
        ValidFrom = validFrom;
        ValidTo = validTo;
        if (isActive.HasValue) IsActive = isActive.Value;
    }

    public void ClearDigitalMetadata()
    {
        ProviderCode = string.Empty;
        TokenRef = string.Empty;
        ProtectedSecret = null;
        SealImageBase64 = null;
    }
    public void Rename(string fileName)
    {
        var normalized = Path.GetFileName(fileName.Replace('\\', '/')).Trim();
        if (string.IsNullOrWhiteSpace(normalized)) throw new ArgumentException("A file name is required.", nameof(fileName));
        if (normalized.Length > 256) throw new ArgumentException("The file name is too long.", nameof(fileName));
        FileName = normalized;
    }

    public void ReplaceContent(string fileName, string contentType, string blobName, long size)
    {
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        Rename(fileName);
        ContentType = contentType.Trim();
        BlobName = blobName;
        Size = size;
    }
}

public sealed class SigningAttempt
{
    private SigningAttempt() { }
    public SigningAttempt(Guid id, Guid documentId, Guid fileId, Guid userId, SigningKind kind,
        string inputSha256, string idempotencyKey, DateTime now)
    {
        if (documentId == Guid.Empty || fileId == Guid.Empty || userId == Guid.Empty)
            throw new ArgumentException("Document, file, and user identifiers are required.");
        if (!Documents.Hashing.IsSha256(inputSha256)) throw new ArgumentException("Invalid input hash.");
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 128)
            throw new ArgumentException("A valid idempotency key is required.", nameof(idempotencyKey));
        (Id, DocumentId, FileId, UserId, Kind, InputSha256, IdempotencyKey, Status, CreationTime) =
            (id, documentId, fileId, userId, kind, inputSha256.ToLowerInvariant(), idempotencyKey.Trim(), SigningStatus.Pending, now);
    }
    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid FileId { get; private set; }
    public Guid UserId { get; private set; }
    public SigningKind Kind { get; private set; }
    public SigningStatus Status { get; private set; }
    public string InputSha256 { get; private set; } = string.Empty;
    public string? OutputSha256 { get; private set; }
    public string? OutputBlobName { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string? Error { get; private set; }
    public DateTime CreationTime { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public void Complete(string outputSha256, string outputBlobName, DateTime now)
    {
        if (!Documents.Hashing.IsSha256(outputSha256)) throw new ArgumentException("Invalid signed content hash.");
        Status = SigningStatus.Completed;
        OutputSha256 = outputSha256.ToLowerInvariant();
        OutputBlobName = outputBlobName;
        Error = null;
        CompletedAt = now;
    }
    public void Fail(string error, DateTime now)
    {
        Status = SigningStatus.Failed;
        Error = error.Length > 1000 ? error[..1000] : error;
        CompletedAt = now;
    }
}
