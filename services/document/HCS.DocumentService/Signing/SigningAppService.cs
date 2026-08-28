using System.Security.Claims;
using System.Text.RegularExpressions;
using Volo.Abp;
using HCS.DocumentService.Conversion;
using HCS.DocumentService.Integration;
using HCS.DocumentService.Documents;
using HCS.DocumentService.Storage;
using HCS.DocumentService.Workflows;
using HCS.IntegrationEvents.Documents;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Authorization;
using Volo.Abp.BlobStoring;

namespace HCS.DocumentService.Signing;

public sealed class SigningAppService(
    DocumentServiceDbContext db,
    IHttpContextAccessor httpContext,
    IConfiguration configuration,
    ISigningSecretProtector secretProtector,
    ISigningProviderFactory providerFactory,
    IBlobContainer<DocumentBlobContainer> documentBlobs,
    IBlobContainer<SigningBlobContainer> signingBlobs,
    IDocxToPdfConverter converter,
    DocumentFileService documentFiles) : ISigningAppService
{
    private const long MaxSignatureSize = 2 * 1024 * 1024;
    private static readonly HashSet<string> AllowedSignatureContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif"
    };

    private sealed record ResolvedSignatureMetadata(string ProviderCode, string TokenRef,
        string? ProtectedSecret, string? SealImageBase64);

    public Task<IReadOnlyList<SigningProviderDefinitionDto>> GetProviderDefinitionsAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SigningProviderDefinitionDto>>(
            providerFactory.Definitions.Select(definition => new SigningProviderDefinitionDto(
                definition.Code,
                definition.DisplayName,
                definition.SupportedKinds.ToList(),
                definition.DefaultEndpoint,
                definition.RequiresLayoutImage,
                definition.RequiresSealImage,
                definition.RequiresBase64Secret,
                definition.DefaultApiTimeoutSeconds,
                definition.DefaultSignWidth,
                definition.DefaultSignHeight)).ToList());

    public async Task<IReadOnlyList<SigningQueueItemDto>> GetQueueAsync(CancellationToken cancellationToken = default)
    {
        var principal = Principal;
        var userId = DocumentAccess.RequireUser(principal);
        DocumentAccess.RequirePermission(principal, DocumentPermissions.SigningExecute);

        var query = db.WorkflowInstances.AsNoTracking().Include(x => x.Tasks)
            .Where(x => x.Status == WorkflowInstanceStatus.Running
                && x.Tasks.Any(task => task.Status == ApprovalTaskStatus.Pending)
                && db.Documents.Any(document => document.Id == x.DocumentId
                    && document.SourceType == DocumentSourceType.Workflow));
        if (!DocumentAccess.IsElevated(principal))
        {
            query = query.Where(instance =>
                db.Documents.Any(document => document.Id == instance.DocumentId &&
                    (document.Assignments.Any(a => a.AssigneeUserId == userId) ||
                     document.History.Any(h => h.Action == "Created" && h.ActorUserId == userId))));
        }

        var instances = await query.OrderByDescending(x => x.CreationTime).Take(200).ToListAsync(cancellationToken);
        if (instances.Count == 0) return [];

        var documentIds = instances.Select(x => x.DocumentId).Distinct().ToArray();
        var documents = await db.Documents.AsNoTracking()
            .Where(x => documentIds.Contains(x.Id))
            .Include(x => x.Files)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var definitionIds = instances.Select(x => x.DefinitionId).Distinct().ToArray();
        var definitions = await db.WorkflowDefinitions.AsNoTracking()
            .Where(x => definitionIds.Contains(x.Id)).Include(x => x.Steps)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        return instances.SelectMany(instance =>
                instance.Tasks.Where(task => task.Status == ApprovalTaskStatus.Pending)
                    .Select(task => (instance, task)))
            .Where(x => documents.ContainsKey(x.instance.DocumentId)
                && definitions.TryGetValue(x.instance.DefinitionId, out var definition)
                && definition.Steps.Any(step => step.Code == x.task.StepCode
                    && (step.Type is "SIGN" or "PROCESS")))
            .Select(x => new SigningQueueItemDto(
                MapQueueDocument(documents[x.instance.DocumentId]),
                new ApprovalTaskDto(x.task.Id, x.task.InstanceId, x.task.StepCode, x.task.Status,
                    x.task.DecidedBy, x.task.DecidedAt, x.task.AssigneeUserId, x.task.DueAt, x.task.Comment),
                WorkflowAppService.Map(x.instance),
                WorkflowAppService.MapDefinition(definitions[x.instance.DefinitionId])))
            .ToList();
    }

    private static SigningQueueDocumentDto MapQueueDocument(DocumentAggregate document) =>
        new(document.Id, document.Number, document.Title, document.Description, document.Status,
            document.Files.Select(file => new DocumentFileDto(file.Id, file.FileName, file.ContentType,
                file.Size, file.Sha256, file.CreationTime, file.PairedFileId)).ToList(),
            document.CreationTime, document.SourceType, document.FromUserId);

    public async Task<IReadOnlyList<SigningCredentialDto>> GetCredentialsAsync(Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var principal = Principal;
        var targetUserId = ResolveTargetUser(userId, DocumentPermissions.SigningConfigure);
        var credentials = await db.SigningCredentials.AsNoTracking().Where(x => x.UserId == targetUserId)
            .OrderBy(x => x.Kind).ToListAsync(cancellationToken);
        return credentials.Select(Map).ToList();
    }

    public async Task<SigningCredentialDto> ConfigureCredentialAsync(ConfigureSigningCredentialRequest input, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var principal = Principal;
        var targetUserId = ResolveTargetUser(userId, DocumentPermissions.SigningConfigure);
        if (!Enum.IsDefined(input.Kind)) throw new ArgumentOutOfRangeException(nameof(input.Kind));
        var providerCode = input.Kind == SigningKind.Electronic
            ? input.ProviderCode.Trim()
            : SigningProviderCodes.Normalize(input.ProviderCode);
        var providerDefaults = providerFactory.GetDefinition(input.Kind, providerCode);
        if (input.Kind != SigningKind.Electronic && string.IsNullOrWhiteSpace(providerCode))
            throw new ArgumentException("Provider code is required for external signing.", nameof(input));
        var endpointValue = string.IsNullOrWhiteSpace(input.Endpoint)
            ? providerDefaults.DefaultEndpoint
            : input.Endpoint.Trim();
        if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttps && endpoint.Scheme != Uri.UriSchemeHttp))
            throw new ArgumentException("Signing endpoint must be an absolute HTTP or HTTPS URI.");
        EnsureEndpointAllowed(endpoint);
        var credential = await db.SigningCredentials.SingleOrDefaultAsync(x => x.UserId == targetUserId && x.Kind == input.Kind, cancellationToken);
        var rawSecret = input.ConsumeSecret();
        if (credential is null && input.Kind != SigningKind.Electronic && string.IsNullOrWhiteSpace(rawSecret))
            throw new ArgumentException("A signing secret is required for a new provider configuration.", nameof(input));
        var protectedSecret = string.IsNullOrWhiteSpace(rawSecret)
            ? credential?.ProtectedSecret ?? string.Empty
            : secretProtector.Protect(rawSecret);
        var layoutImage = input.LayoutImageBase64 ?? credential?.LayoutImageBase64;
        ValidateOptionalImage(layoutImage);
        var timeoutSeconds = input.ApiTimeoutSeconds > 0
            ? input.ApiTimeoutSeconds
            : providerDefaults.DefaultApiTimeoutSeconds;
        if (providerDefaults.RequiresBase64Secret)
            timeoutSeconds = Math.Clamp(timeoutSeconds, 30, 240);
        var signWidth = input.SignWidth > 0 ? input.SignWidth : providerDefaults.DefaultSignWidth;
        var signHeight = input.SignHeight > 0 ? input.SignHeight : providerDefaults.DefaultSignHeight;
        if (credential is null)
        {
            credential = new SigningCredential(Guid.NewGuid(), targetUserId, input.Kind, endpoint.ToString(), protectedSecret, DateTime.UtcNow,
                providerCode, layoutImage, timeoutSeconds, signWidth, signHeight,
                input.AllowElectronicSign, input.AllowDigitalSign, input.RequireOtp);
            db.SigningCredentials.Add(credential);
        }
        else credential.Replace(endpoint.ToString(), protectedSecret, DateTime.UtcNow,
            providerCode, layoutImage, timeoutSeconds, signWidth, signHeight,
            input.AllowElectronicSign, input.AllowDigitalSign, input.RequireOtp);
        await db.SaveChangesAsync(cancellationToken);
        return Map(credential);
    }

    public async Task<SigningAttemptDto> SignAsync(SignDocumentRequest input, CancellationToken cancellationToken = default)
    {
        var principal = Principal;
        var userId = DocumentAccess.RequireUser(principal);
        DocumentAccess.RequirePermission(principal, DocumentPermissions.SigningExecute);
        if (!Enum.IsDefined(input.Kind)) throw new ArgumentOutOfRangeException(nameof(input.Kind));
        var key = input.IdempotencyKey?.Trim();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128)
            throw new ArgumentException("A valid idempotency key is required.", nameof(input));
        var document = await db.Documents.AsNoTracking().Include(x => x.Assignments).Include(x => x.History)
            .SingleOrDefaultAsync(x => x.Id == input.DocumentId, cancellationToken)
            ?? throw new KeyNotFoundException("Document not found.");
        DocumentAccess.EnsureCanView(document, userId, principal);
        var activeSignTask = await (
            from task in db.ApprovalTasks.AsNoTracking()
            join instance in db.WorkflowInstances.AsNoTracking() on task.InstanceId equals instance.Id
            join step in db.WorkflowSteps.AsNoTracking()
                on new { instance.DefinitionId, Code = task.StepCode }
                equals new { DefinitionId = step.DefinitionId, Code = step.Code }
            where instance.DocumentId == input.DocumentId
                && instance.Status == WorkflowInstanceStatus.Running
                && task.Status == ApprovalTaskStatus.Pending
                && step.Type == WorkflowStepTypes.Sign
            select new { task.AssigneeUserId })
            .FirstOrDefaultAsync(cancellationToken);
        if (activeSignTask?.AssigneeUserId is { } assignee
            && assignee != userId
            && !DocumentAccess.IsElevated(principal))
            throw new UnauthorizedAccessException("Only the assigned user can sign this document.");
        var existing = await FindAttemptAsync(userId, input, key, cancellationToken);
        if (existing is not null) return Map(existing);
        var file = await db.DocumentFiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.FileId && x.DocumentId == input.DocumentId && !x.IsPendingDeletion, cancellationToken)
            ?? throw new KeyNotFoundException("Document file not found.");
        if (!IsPdf(file))
            throw new InvalidOperationException("Only the prepared PDF file can be signed.");
        var credential = await db.SigningCredentials.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId && x.Kind == input.Kind, cancellationToken);
        if (input.Kind != SigningKind.Electronic && credential is null) throw new InvalidOperationException("Signing provider is not configured.");
        if (input.Kind != SigningKind.Electronic && string.IsNullOrWhiteSpace(credential!.ProviderCode))
            throw new InvalidOperationException("The selected signing provider has no provider code.");
        if (input.Kind == SigningKind.Electronic && credential is { AllowElectronicSign: false })
            throw new InvalidOperationException("Electronic signing is disabled for this provider.");
        if (input.Kind != SigningKind.Electronic && credential is { AllowDigitalSign: false })
            throw new InvalidOperationException("Digital signing is disabled for this provider.");
        var expectedType = input.Kind == SigningKind.Electronic ? UserSignatureType.Electronic : UserSignatureType.Digital;
        var signatureQuery = db.UserSignatures.AsNoTracking()
            .Where(x => x.UserId == userId && x.Type == expectedType && x.IsActive);
        UserSignature? signature;
        if (input.SignatureId is { } signatureId)
        {
            signature = await signatureQuery.SingleOrDefaultAsync(x => x.Id == signatureId, cancellationToken)
                ?? throw new KeyNotFoundException("Selected user signature was not found or is inactive.");
        }
        else
        {
            signature = await signatureQuery.OrderByDescending(x => x.IsDefault).ThenByDescending(x => x.CreationTime)
                .FirstOrDefaultAsync(cancellationToken);
        }
        if (signature is null) throw new InvalidOperationException("A matching user signature is not configured.");
        if (credential is not null && !string.IsNullOrWhiteSpace(signature.ProviderCode)
            && !string.Equals(signature.ProviderCode, credential.ProviderCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The selected signature is configured for a different provider.");
        var now = DateTime.UtcNow;
        if (signature.ValidFrom.HasValue && signature.ValidFrom > now) throw new InvalidOperationException("The selected signature is not yet valid.");
        if (signature.ValidTo.HasValue && signature.ValidTo < now) throw new InvalidOperationException("The selected signature has expired.");
        var pairedWordFile = file.PairedFileId is { } pairedFileId
            ? await QueryPairedFile(db.DocumentFiles.AsNoTracking(), pairedFileId, input.DocumentId)
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        if (pairedWordFile is not null && !IsWord(pairedWordFile))
            pairedWordFile = null;
        byte[] bytes;
        byte[]? preparedWordBytes = null;
        var wordPrepared = false;
        string actualInputHash;
        string secret;
        string placeholder;
        SigningProviderRequest providerRequest;
        IDigitalSigningAdapter adapter;
        try
        {
            await using var inputStream = await documentBlobs.GetAsync(file.BlobName, cancellationToken: cancellationToken);
            await using var buffer = new MemoryStream();
            await inputStream.CopyToAsync(buffer, cancellationToken);
            bytes = buffer.ToArray();
            var storedInputHash = ContentHash.Sha256(bytes);
            if (!storedInputHash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Stored file hash does not match its immutable metadata.");
            var providerDefaults = providerFactory.GetDefinition(input.Kind, credential?.ProviderCode);
            if (providerDefaults.RequiresLayoutImage && string.IsNullOrWhiteSpace(credential?.LayoutImageBase64))
                throw new InvalidOperationException("The selected signing provider requires a layout image.");
            adapter = providerFactory.GetAdapter(input.Kind, credential?.ProviderCode);
            var signatureImage = await ReadSigningBlobAsync(signature.BlobName, cancellationToken);
            var layoutImage = DecodeImage(credential?.LayoutImageBase64);
            var protectedSecret = !string.IsNullOrWhiteSpace(signature.ProtectedSecret)
                ? signature.ProtectedSecret
                : credential?.ProtectedSecret;
            secret = string.IsNullOrWhiteSpace(protectedSecret)
                ? string.Empty
                : secretProtector.Unprotect(protectedSecret);
            placeholder = ResolvePlaceholder(bytes, input.Placeholder);
            var signerName = NormalizeBounded(input.SignerName, 256);
            if (string.IsNullOrWhiteSpace(signerName))
                signerName = ResolveCurrentUserName(principal, userId);
            var note = NormalizeBounded(input.Note, 2000);

            var stepOrder = ResolveSigningStepOrder(placeholder);
            if (pairedWordFile is not null)
            {
                await using var wordInputStream = await documentBlobs.GetAsync(pairedWordFile.BlobName,
                    cancellationToken: cancellationToken);
                await using var wordBuffer = new MemoryStream();
                await wordInputStream.CopyToAsync(wordBuffer, cancellationToken);
                var sourceWordBytes = wordBuffer.ToArray();
                var electronicImage = input.Kind == SigningKind.Electronic
                    ? layoutImage is { Length: > 0 }
                        ? PdfSigningDrawing.ComposeSignatureWithLayout(signatureImage, layoutImage)
                        : ElectronicSignatureLayoutComposer.Compose(signatureImage)
                    : signatureImage;
                preparedWordBytes = WordFirstSigningDocumentBuilder.Replace(sourceWordBytes, input.Kind,
                    electronicImage, stepOrder, signerName, note);
                if (!converter.IsAvailable)
                    throw new InvalidOperationException("LibreOffice is required to prepare the Word signing document.");
                bytes = await converter.ConvertAsync(preparedWordBytes, cancellationToken)
                    ?? throw new InvalidOperationException("The Word signing document could not be converted to PDF.");
                if (bytes.Length == 0)
                    throw new InvalidOperationException("The Word signing document could not be converted to PDF.");
                wordPrepared = true;
            }
            else
            {
                // Legacy PDF-only documents keep the compatibility overlay path.
                bytes = PdfPlaceholderReplacer.ReplaceApprovalText(bytes, stepOrder, signerName, note);
            }

            actualInputHash = ContentHash.Sha256(bytes);
            providerRequest = new SigningProviderRequest(
                bytes,
                credential?.Endpoint ?? "https://electronic.local",
                secret,
                signature.TokenRef,
                signatureImage,
                DecodeImage(signature.SealImageBase64),
                layoutImage,
                placeholder,
                signerName,
                note,
                credential?.SignWidth is > 0 and var width ? width : 150,
                credential?.SignHeight is > 0 and var height ? height : 70,
                credential?.ApiTimeoutSeconds is > 0 and var timeout ? timeout : 30,
                wordPrepared);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException("The document could not be prepared for signing.", exception);
        }

        var attempt = new SigningAttempt(Guid.NewGuid(), input.DocumentId, input.FileId, userId, input.Kind,
            actualInputHash, key, DateTime.UtcNow);
        db.SigningAttempts.Add(attempt);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.Entry(attempt).State = EntityState.Detached;
            var concurrent = await FindAttemptAsync(userId, input, key, cancellationToken);
            if (concurrent is not null) return Map(concurrent);
            throw;
        }
        string? signingBlobName = null;
        var documentBlobNames = new List<string>();
        try
        {
            var result = await adapter.SignAsync(new SigningAdapterRequest(bytes, actualInputHash,
                providerRequest.Endpoint, secret, providerRequest), cancellationToken);
            var signedContent = result.SignedContent;
            if (signedContent is not { Length: > 0 })
                throw new InvalidDataException("The signing provider returned an empty signed document.");
            var outputHash = ContentHash.Sha256(signedContent);
            var outputFile = await db.DocumentFiles.SingleAsync(x => x.Id == input.FileId, cancellationToken);
            if (!string.Equals(outputFile.Sha256, file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The document changed while it was being signed. Please retry.");

            signingBlobName = BlobNamePolicy.Signing(input.DocumentId, attempt.Id);
            await signingBlobs.SaveAsync(signingBlobName, new MemoryStream(signedContent), overrideExisting: false, cancellationToken: cancellationToken);
            if (wordPrepared)
            {
                var trackedDocument = await db.Documents.Include(x => x.Files).Include(x => x.History)
                    .SingleAsync(x => x.Id == input.DocumentId, cancellationToken);
                var pair = await documentFiles.AddDocxPdfPairAsync(trackedDocument, preparedWordBytes!, signedContent,
                    BuildDerivedFileName(pairedWordFile!.FileName, "-Sign", ResolveSigningStepOrder(placeholder), ".docx"),
                    BuildDerivedFileName(file.FileName, "-Sign", ResolveSigningStepOrder(placeholder), ".pdf"),
                    userId, DateTime.UtcNow, cancellationToken);
                documentBlobNames.Add(pair.WordBlobName);
                documentBlobNames.Add(pair.PdfBlobName);
            }
            else
            {
                var documentBlobName = BlobNamePolicy.Document(input.DocumentId, Guid.NewGuid());
                await documentBlobs.SaveAsync(documentBlobName, new MemoryStream(signedContent),
                    overrideExisting: false, cancellationToken: cancellationToken);
                documentBlobNames.Add(documentBlobName);
                outputFile.ReplaceContent(signedContent.Length, outputHash, documentBlobName);
            }
            attempt.Complete(outputHash, signingBlobName, DateTime.UtcNow);
            var integrationEvent = new DocumentSignedEto(Guid.NewGuid(), DateTimeOffset.UtcNow, CorrelationId, input.DocumentId,
                input.FileId, actualInputHash, outputHash, result.AdapterId);
            db.OutboxMessages.Add(OutboxFactory.CreateCanonical(integrationEvent, CorrelationId, DateTime.UtcNow));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await DeleteBlobIfCreatedAsync(signingBlobs, signingBlobName, cancellationToken);
            foreach (var documentBlobName in documentBlobNames)
                await DeleteBlobIfCreatedAsync(documentBlobs, documentBlobName, cancellationToken);
            attempt.Fail(SigningFailureSanitizer.ToPublicMessage(exception), DateTime.UtcNow);
        }
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await DeleteBlobIfCreatedAsync(signingBlobs, signingBlobName, cancellationToken);
            foreach (var documentBlobName in documentBlobNames)
                await DeleteBlobIfCreatedAsync(documentBlobs, documentBlobName, cancellationToken);
            throw;
        }
        return Map(attempt);
    }

    public async Task<SigningReportDto> GetReportAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var principal = Principal;
        var userId = DocumentAccess.RequireUser(principal);
        DocumentAccess.RequirePermission(principal, DocumentPermissions.SigningReport);
        var document = await db.Documents.AsNoTracking().Include(x => x.Assignments).Include(x => x.History)
            .SingleOrDefaultAsync(x => x.Id == documentId, cancellationToken)
            ?? throw new KeyNotFoundException("Document not found.");
        DocumentAccess.EnsureCanView(document, userId, principal);
        var attempts = await db.SigningAttempts.AsNoTracking().Where(x => x.DocumentId == documentId)
            .OrderByDescending(x => x.CreationTime).ToListAsync(cancellationToken);
        return new SigningReportDto(documentId, attempts.Count(x => x.Status == SigningStatus.Completed),
            attempts.Count(x => x.Status == SigningStatus.Failed), attempts.Select(Map).ToList());
    }

    public async Task<IReadOnlyList<UserSignatureDto>> GetSignaturesAsync(Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var targetUserId = ResolveTargetUser(userId, DocumentPermissions.SigningExecute);
        var items = await db.UserSignatures.AsNoTracking().Where(x => x.UserId == targetUserId)
            .OrderByDescending(x => x.IsDefault).ThenByDescending(x => x.CreationTime).ToListAsync(cancellationToken);
        return items.Select(MapSignature).ToList();
    }

    public async Task<UserSignatureDto> UploadSignatureAsync(string fileName, string contentType, Stream content, long size,
        UserSignatureType type = UserSignatureType.Electronic, Guid? userId = null, string? providerCode = null,
        string? tokenRef = null, string? secret = null, string? sealImageBase64 = null,
        DateTime? validFrom = null, DateTime? validTo = null, bool isActive = true,
        CancellationToken cancellationToken = default)
    {
        var targetUserId = ResolveTargetUser(userId, DocumentPermissions.SigningExecute);
        ValidateSignatureFile(fileName, contentType, size);
        ValidateSignatureType(type);
        var metadata = await ResolveSignatureMetadataAsync(targetUserId, type, providerCode, tokenRef, secret,
            sealImageBase64, current: null, cancellationToken);
        var id = Guid.NewGuid();
        var blobName = BlobNamePolicy.UserSignature(targetUserId, id);
        await signingBlobs.SaveAsync(blobName, content, overrideExisting: false, cancellationToken: cancellationToken);
        var signature = new UserSignature(id, targetUserId, Path.GetFileName(fileName), NormalizeSignatureContentType(contentType), blobName, size, DateTime.UtcNow, type,
            metadata.ProviderCode, metadata.TokenRef, metadata.ProtectedSecret, metadata.SealImageBase64,
            NormalizeUtc(validFrom), NormalizeUtc(validTo), isActive);
        if (!await db.UserSignatures.AnyAsync(x => x.UserId == targetUserId, cancellationToken)) signature.MarkDefault();
        db.UserSignatures.Add(signature);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch { await signingBlobs.DeleteAsync(blobName, cancellationToken: cancellationToken); throw; }
        return MapSignature(signature);
    }

    public async Task<UserSignatureDto> UpdateSignatureAsync(Guid id, string? fileName, string? contentType, Stream? content, long? size,
        UserSignatureType? type = null, Guid? userId = null, string? providerCode = null,
        string? tokenRef = null, string? secret = null, string? sealImageBase64 = null,
        DateTime? validFrom = null, DateTime? validTo = null, bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var targetUserId = ResolveTargetUser(userId, DocumentPermissions.SigningExecute);
        var signature = await db.UserSignatures.SingleOrDefaultAsync(x => x.Id == id && x.UserId == targetUserId, cancellationToken)
            ?? throw new KeyNotFoundException("Signature not found.");

        var normalizedFileName = string.IsNullOrWhiteSpace(fileName) ? signature.FileName : Path.GetFileName(fileName.Replace('\\', '/'));
        var normalizedType = type ?? signature.Type;
        ValidateSignatureType(normalizedType);
        var metadata = await ResolveSignatureMetadataAsync(targetUserId, normalizedType, providerCode, tokenRef, secret,
            sealImageBase64, signature, cancellationToken);
        var hasMetadata = providerCode is not null || tokenRef is not null || secret is not null || sealImageBase64 is not null
            || validFrom.HasValue || validTo.HasValue || isActive.HasValue;
        if (content is null)
        {
            var fileNameChanged = !string.Equals(normalizedFileName, signature.FileName, StringComparison.Ordinal);
            var typeChanged = normalizedType != signature.Type;
            if (!fileNameChanged && !typeChanged && !hasMetadata)
                throw new ArgumentException("A file name, replacement image, or signature metadata is required.");

            if (fileNameChanged) signature.Rename(normalizedFileName);
            if (typeChanged) signature.ChangeType(normalizedType);
            if (normalizedType == UserSignatureType.Electronic) signature.ClearDigitalMetadata();
            else if (hasMetadata || typeChanged)
                signature.UpdateMetadata(metadata.ProviderCode, metadata.TokenRef, metadata.ProtectedSecret,
                    metadata.SealImageBase64, NormalizeUtc(validFrom), NormalizeUtc(validTo), isActive);
            await db.SaveChangesAsync(cancellationToken);
            return MapSignature(signature);
        }

        var uploadSize = size ?? 0;
        ValidateSignatureFile(normalizedFileName, contentType, uploadSize);
        var normalizedContentType = NormalizeSignatureContentType(contentType);
        var oldBlobName = signature.BlobName;
        var newBlobName = BlobNamePolicy.UserSignature(targetUserId, Guid.NewGuid());
        await signingBlobs.SaveAsync(newBlobName, content, overrideExisting: false, cancellationToken: cancellationToken);
        signature.ReplaceContent(normalizedFileName, normalizedContentType, newBlobName, uploadSize);
        signature.ChangeType(normalizedType);
        if (normalizedType == UserSignatureType.Electronic) signature.ClearDigitalMetadata();
        else signature.UpdateMetadata(metadata.ProviderCode, metadata.TokenRef, metadata.ProtectedSecret,
            metadata.SealImageBase64, NormalizeUtc(validFrom), NormalizeUtc(validTo), isActive);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await signingBlobs.DeleteAsync(newBlobName, cancellationToken: cancellationToken);
            throw;
        }

        await signingBlobs.DeleteAsync(oldBlobName, cancellationToken: cancellationToken);
        return MapSignature(signature);
    }

    public async Task<UserSignatureDto> SetDefaultSignatureAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var targetUserId = ResolveTargetUser(userId, DocumentPermissions.SigningExecute);
        var signature = await db.UserSignatures.SingleOrDefaultAsync(x => x.Id == id && x.UserId == targetUserId, cancellationToken)
            ?? throw new KeyNotFoundException("Signature not found.");
        var signatures = await db.UserSignatures.Where(x => x.UserId == targetUserId).ToListAsync(cancellationToken);
        foreach (var item in signatures)
        {
            if (item.Id == signature.Id) item.MarkDefault();
            else item.ClearDefault();
        }

        await db.SaveChangesAsync(cancellationToken);
        return MapSignature(signature);
    }

    public async Task DeleteSignatureAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var targetUserId = ResolveTargetUser(userId, DocumentPermissions.SigningExecute);
        var signature = await db.UserSignatures.SingleOrDefaultAsync(x => x.Id == id && x.UserId == targetUserId, cancellationToken)
            ?? throw new KeyNotFoundException("Signature not found.");
        List<UserSignature> siblings = signature.IsDefault
            ? await db.UserSignatures.Where(x => x.UserId == targetUserId && x.Id != id)
                .OrderByDescending(x => x.CreationTime).ToListAsync(cancellationToken)
            : [];
        var replacement = siblings.FirstOrDefault();
        db.UserSignatures.Remove(signature);
        if (signature.IsDefault)
        {
            foreach (var sibling in siblings)
            {
                if (sibling.Id == replacement?.Id) sibling.MarkDefault();
                else sibling.ClearDefault();
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        await signingBlobs.DeleteAsync(signature.BlobName, cancellationToken: cancellationToken);
    }

    public async Task<(Stream Content, string ContentType, string FileName)> OpenSignatureContentAsync(
        Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var targetUserId = ResolveTargetUser(userId, DocumentPermissions.SigningExecute);
        var signature = await db.UserSignatures.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.UserId == targetUserId, cancellationToken)
            ?? throw new KeyNotFoundException("Signature not found.");
        var stream = await signingBlobs.GetAsync(signature.BlobName, cancellationToken);
        return (stream, signature.ContentType, signature.FileName);
    }

    private Guid ResolveTargetUser(Guid? userId, string permission)
    {
        var principal = Principal;
        var current = DocumentAccess.RequireUser(principal);
        if (userId is null || userId == current)
        {
            return current;
        }

        DocumentAccess.RequirePermission(principal, permission);
        if (!DocumentAccess.IsElevated(principal))
        {
            throw new AbpAuthorizationException("Managing another user's signatures requires an administrator.");
        }

        return userId.Value;
    }

    private ClaimsPrincipal Principal => httpContext.HttpContext?.User ?? new ClaimsPrincipal();

    private static void ValidateSignatureFile(string fileName, string? contentType, long size)
    {
        if (size is <= 0 or > MaxSignatureSize) throw new ArgumentOutOfRangeException(nameof(size));
        var normalizedFileName = Path.GetFileName(fileName.Replace('\\', '/')).Trim();
        if (string.IsNullOrWhiteSpace(normalizedFileName)) throw new ArgumentException("A file name is required.", nameof(fileName));
        if (normalizedFileName.Length > 256) throw new ArgumentException("The file name is too long.", nameof(fileName));
        if (!AllowedSignatureContentTypes.Contains(contentType ?? string.Empty))
            throw new InvalidDataException("Signature files must be JPEG, PNG, WebP, or GIF images.");
    }

    private static string NormalizeSignatureContentType(string? contentType) =>
        contentType?.Trim().ToLowerInvariant() ?? throw new InvalidDataException("A signature image content type is required.");

    private static DateTime? NormalizeUtc(DateTime? value) => value?.ToUniversalTime();

    private static bool IsPdf(DocumentFile file) =>
        file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
        || file.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase);

    private static bool IsWord(DocumentFile file) =>
        file.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
        || string.Equals(file.ContentType,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            StringComparison.OrdinalIgnoreCase);

    internal static IQueryable<DocumentFile> QueryPairedFile(IQueryable<DocumentFile> files,
        Guid fileId, Guid documentId) => files.Where(x => x.Id == fileId
            && x.DocumentId == documentId && !x.IsPendingDeletion);

    private static string BuildDerivedFileName(string fileName, string suffix, int stepOrder, string extension)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(stem)) stem = "workflow";
        var stepSuffix = $"{suffix}{stepOrder:D2}";
        var maxStemLength = Math.Max(1, 256 - stepSuffix.Length - extension.Length);
        if (stem.Length > maxStemLength) stem = stem[..maxStemLength];
        return $"{stem}{stepSuffix}{extension}";
    }

    private static string NormalizeBounded(string? value, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static int ResolveSigningStepOrder(string placeholder)
    {
        var match = Regex.Match(placeholder ?? string.Empty, "^<<Sign(?<order>\\d{1,3})>>$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups["order"].Value, out var order) && order > 0
            ? order
            : 1;
    }

    private static string ResolveCurrentUserName(ClaimsPrincipal principal, Guid userId)
    {
        var direct = principal.FindFirst("name")?.Value
            ?? principal.FindFirst(ClaimTypes.Name)?.Value;
        if (!string.IsNullOrWhiteSpace(direct)) return NormalizeBounded(direct, 256);

        var given = principal.FindFirst("given_name")?.Value
            ?? principal.FindFirst(ClaimTypes.GivenName)?.Value;
        var family = principal.FindFirst("family_name")?.Value
            ?? principal.FindFirst(ClaimTypes.Surname)?.Value;
        var composed = string.Join(' ', new[] { family, given }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        return NormalizeBounded(string.IsNullOrWhiteSpace(composed) ? userId.ToString("N") : composed, 256);
    }

    private async Task<byte[]> ReadSigningBlobAsync(string blobName, CancellationToken cancellationToken)
    {
        await using var stream = await signingBlobs.GetAsync(blobName, cancellationToken);
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private static async Task DeleteBlobIfCreatedAsync<TContainer>(IBlobContainer<TContainer> container,
        string? blobName, CancellationToken cancellationToken)
        where TContainer : class
    {
        if (string.IsNullOrWhiteSpace(blobName)) return;
        try
        {
            await container.DeleteAsync(blobName, cancellationToken: cancellationToken);
        }
        catch
        {
            // Blob cleanup is best effort; the signing attempt remains the audit source of truth.
        }
    }

    private static string ResolvePlaceholder(byte[] pdfBytes, string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested)) return NormalizeBounded(requested, 128);
        if (pdfBytes is { Length: > 0 })
        {
            using var document = UglyToad.PdfPig.PdfDocument.Open(pdfBytes);
            foreach (var page in document.GetPages())
            {
                var text = string.Concat(page.Letters.Select(x => x.Value));
                var match = Regex.Match(text, @"<<Sign\d{1,3}>>", RegexOptions.CultureInvariant);
                if (match.Success) return match.Value;
            }
        }
        return "<<Sign01>>";
    }

    private static byte[] DecodeImage(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return [];
        var value = base64.Trim();
        var separator = value.IndexOf(',');
        if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && separator >= 0)
            value = value[(separator + 1)..];
        try { return Convert.FromBase64String(value); }
        catch (FormatException) { throw new InvalidDataException("An image configuration is not valid Base64."); }
    }

    private static void ValidateOptionalImage(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return;
        var bytes = DecodeImage(base64);
        if (bytes.Length > 3 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(base64), "The configured image is too large.");
    }

    private static void ValidateSignatureType(UserSignatureType type)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
    }

    private async Task<ResolvedSignatureMetadata> ResolveSignatureMetadataAsync(Guid userId, UserSignatureType type,
        string? providerCode, string? tokenRef, string? secret, string? sealImageBase64, UserSignature? current,
        CancellationToken cancellationToken)
    {
        if (type == UserSignatureType.Electronic)
            return new ResolvedSignatureMetadata(string.Empty, string.Empty, null, null);

        var normalizedProvider = SigningProviderCodes.Normalize(string.IsNullOrWhiteSpace(providerCode)
            ? current?.ProviderCode?.Trim() ?? string.Empty
            : providerCode);
        if (string.IsNullOrWhiteSpace(normalizedProvider))
            throw new ArgumentException("A configured provider must be selected for a digital signature.", nameof(providerCode));

        var credential = await db.SigningCredentials.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId
            && x.Kind != SigningKind.Electronic
            && x.ProviderCode.ToUpper() == normalizedProvider, cancellationToken);
        if (credential is null)
            throw new InvalidOperationException("The selected provider is not configured for this user.");
        var providerDefaults = providerFactory.GetDefinition(credential.Kind, normalizedProvider);

        var normalizedToken = string.IsNullOrWhiteSpace(tokenRef)
            ? current?.TokenRef?.Trim() ?? string.Empty
            : tokenRef.Trim();
        if (string.IsNullOrWhiteSpace(normalizedToken))
            throw new ArgumentException("A token or API key is required for a digital signature.", nameof(tokenRef));

        string? protectedSecret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            protectedSecret = current?.ProtectedSecret;
        }
        else
        {
            protectedSecret = secretProtector.Protect(secret);
        }
        if (string.IsNullOrWhiteSpace(protectedSecret))
            throw new ArgumentException("A secret key is required for a digital signature.", nameof(secret));

        var normalizedSeal = string.IsNullOrWhiteSpace(sealImageBase64)
            ? current?.SealImageBase64
            : sealImageBase64.Trim();
        ValidateOptionalImage(normalizedSeal);
        if (providerDefaults.RequiresSealImage && string.IsNullOrWhiteSpace(normalizedSeal))
            throw new ArgumentException("A seal image is required for a digital signature.", nameof(sealImageBase64));

        return new ResolvedSignatureMetadata(normalizedProvider, normalizedToken, protectedSecret, normalizedSeal);
    }

    private Task<SigningAttempt?> FindAttemptAsync(Guid userId, SignDocumentRequest input, string key,
        CancellationToken cancellationToken) => db.SigningAttempts.AsNoTracking().SingleOrDefaultAsync(x =>
        x.UserId == userId && x.DocumentId == input.DocumentId && x.FileId == input.FileId &&
        x.Kind == input.Kind && x.IdempotencyKey == key, cancellationToken);
    private void EnsureEndpointAllowed(Uri endpoint)
    {
        var hosts = configuration.GetSection("Signing:AllowedEndpointHosts").Get<string[]>() ?? [];
        if (hosts.Length == 0 || !hosts.Any(host => string.Equals(host?.Trim(), endpoint.Host,
                StringComparison.OrdinalIgnoreCase)))
            throw new BusinessException("Signing:EndpointNotAllowed");
    }
    private string CorrelationId => httpContext.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
    private static SigningCredentialDto Map(SigningCredential x) => new(x.Id, x.Kind, x.ProviderCode, x.Endpoint, "********",
        x.ApiTimeoutSeconds, x.SignWidth, x.SignHeight, x.AllowElectronicSign, x.AllowDigitalSign, x.RequireOtp,
        x.UpdatedAt, !string.IsNullOrWhiteSpace(x.LayoutImageBase64));
    private static SigningAttemptDto Map(SigningAttempt x) => new(x.Id, x.DocumentId, x.FileId, x.Kind, x.Status,
        x.InputSha256, x.OutputSha256, x.Error, x.CreationTime, x.CompletedAt);
    private static UserSignatureDto MapSignature(UserSignature x) =>
        new(x.Id, x.FileName, x.ContentType, x.Size, x.IsDefault, x.CreationTime, x.Type,
            x.ProviderCode, string.Empty, x.ValidFrom, x.ValidTo, x.IsActive, !string.IsNullOrWhiteSpace(x.SealImageBase64));
}

internal static class SigningFailureSanitizer
{
    public static string ToPublicMessage(Exception exception) => exception switch
    {
        NotSupportedException => "The selected signing method is not available.",
        InvalidDataException => "The source document failed integrity verification.",
        System.Security.Cryptography.CryptographicException => "The signing operation failed cryptographic verification.",
        _ => "The signing operation failed. Use the correlation id to investigate server logs."
    };
}
