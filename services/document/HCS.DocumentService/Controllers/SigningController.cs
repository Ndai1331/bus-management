using HCS.DocumentService.Signing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCS.DocumentService.Controllers;

[ApiController, Authorize, Route("api/signing")]
public sealed class SigningController(ISigningAppService signing) : ControllerBase
{
    [HttpGet("queue"), Authorize(Policy = Documents.DocumentPermissions.SigningExecute)]
    public Task<IReadOnlyList<SigningQueueItemDto>> GetQueue(CancellationToken cancellationToken) =>
        signing.GetQueueAsync(cancellationToken);

    [HttpGet("provider-definitions")]
    public Task<IReadOnlyList<SigningProviderDefinitionDto>> GetProviderDefinitions(CancellationToken cancellationToken) =>
        signing.GetProviderDefinitionsAsync(cancellationToken);

    [HttpGet("credentials/current")]
    public Task<IReadOnlyList<SigningCredentialDto>> GetCurrent([FromQuery] Guid? userId, CancellationToken cancellationToken) =>
        signing.GetCredentialsAsync(userId, cancellationToken);
    [HttpPut("credentials/current"), Authorize(Policy = Documents.DocumentPermissions.SigningConfigure)]
    public Task<SigningCredentialDto> Configure(ConfigureSigningCredentialRequest input, [FromQuery] Guid? userId, CancellationToken cancellationToken) =>
        signing.ConfigureCredentialAsync(input, userId, cancellationToken);

    [HttpPut("credentials/current/upload"), Authorize(Policy = Documents.DocumentPermissions.SigningConfigure)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<SigningCredentialDto> ConfigureWithLayout(
        [FromForm] SigningKind kind, [FromForm] string endpoint, [FromForm] string secret,
        [FromForm] string? providerCode, [FromForm] int apiTimeoutSeconds, [FromForm] int signWidth,
        [FromForm] int signHeight, [FromForm] bool allowElectronicSign, [FromForm] bool allowDigitalSign,
        [FromForm] bool requireOtp, [FromForm] IFormFile? layoutImage, [FromQuery] Guid? userId,
        CancellationToken cancellationToken)
    {
        var layoutImageBase64 = await ReadLayoutImageAsync(layoutImage, cancellationToken);
        var input = new ConfigureSigningCredentialRequest(kind, endpoint, secret, providerCode ?? string.Empty,
            layoutImageBase64, apiTimeoutSeconds, signWidth, signHeight, allowElectronicSign, allowDigitalSign, requireOtp);
        return await signing.ConfigureCredentialAsync(input, userId, cancellationToken);
    }
    [HttpPost("attempts"), Authorize(Policy = Documents.DocumentPermissions.SigningExecute)]
    public Task<SigningAttemptDto> Sign(SignDocumentRequest input, CancellationToken cancellationToken) => signing.SignAsync(input, cancellationToken);
    [HttpGet("reports/documents/{documentId:guid}"), Authorize(Policy = Documents.DocumentPermissions.SigningReport)]
    public Task<SigningReportDto> Report(Guid documentId, CancellationToken cancellationToken) => signing.GetReportAsync(documentId, cancellationToken);
    [HttpGet("signatures")]
    public Task<IReadOnlyList<UserSignatureDto>> GetSignatures([FromQuery] Guid? userId, CancellationToken cancellationToken) =>
        signing.GetSignaturesAsync(userId, cancellationToken);
    [HttpPost("signatures")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<UserSignatureDto> UploadSignature(IFormFile file, [FromForm] UserSignatureType? signatureType,
        [FromForm] string? providerCode, [FromForm] string? tokenRef, [FromForm] string? secret,
        IFormFile? sealImage, [FromForm] DateTime? validFrom, [FromForm] DateTime? validTo,
        [FromForm] bool? isActive, [FromQuery] Guid? userId, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var sealImageBase64 = await ReadImageAsDataUrlAsync(sealImage, "Seal image", cancellationToken);
        return await signing.UploadSignatureAsync(file.FileName, file.ContentType, stream, file.Length,
            signatureType ?? UserSignatureType.Electronic, userId, providerCode, tokenRef, secret, sealImageBase64,
            validFrom, validTo, isActive ?? true, cancellationToken);
    }
    [HttpPut("signatures/{id:guid}")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<UserSignatureDto> UpdateSignature(Guid id, [FromForm] string? fileName, [FromForm] UserSignatureType? signatureType,
        [FromForm] string? providerCode, [FromForm] string? tokenRef, [FromForm] string? secret,
        IFormFile? sealImage, [FromForm] DateTime? validFrom, [FromForm] DateTime? validTo,
        [FromForm] bool? isActive, [FromForm] IFormFile? file,
        [FromQuery] Guid? userId, CancellationToken cancellationToken)
    {
        var sealImageBase64 = await ReadImageAsDataUrlAsync(sealImage, "Seal image", cancellationToken);
        if (file is null)
        {
            return await signing.UpdateSignatureAsync(id, fileName, null, null, null, signatureType, userId,
                providerCode, tokenRef, secret, sealImageBase64, validFrom, validTo, isActive, cancellationToken);
        }

        await using var stream = file.OpenReadStream();
        return await signing.UpdateSignatureAsync(id, string.IsNullOrWhiteSpace(fileName) ? file.FileName : fileName,
            file.ContentType, stream, file.Length, signatureType, userId, providerCode, tokenRef, secret,
            sealImageBase64, validFrom, validTo, isActive, cancellationToken);
    }
    [HttpPut("signatures/{id:guid}/default")]
    public Task<UserSignatureDto> SetDefaultSignature(Guid id, [FromQuery] Guid? userId, CancellationToken cancellationToken) =>
        signing.SetDefaultSignatureAsync(id, userId, cancellationToken);
    [HttpGet("signatures/{id:guid}/content")]
    public async Task<IActionResult> DownloadSignature(Guid id, [FromQuery] Guid? userId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await signing.OpenSignatureContentAsync(id, userId, cancellationToken);
            return File(result.Content, result.ContentType, result.FileName, enableRangeProcessing: true);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
    [HttpDelete("signatures/{id:guid}")]
    public async Task<IActionResult> DeleteSignature(Guid id, [FromQuery] Guid? userId, CancellationToken cancellationToken)
    {
        await signing.DeleteSignatureAsync(id, userId, cancellationToken);
        return NoContent();
    }

    private static async Task<string?> ReadLayoutImageAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        return await ReadImageAsDataUrlAsync(file, "Layout image", cancellationToken, 3 * 1024 * 1024);
    }

    private static async Task<string?> ReadImageAsDataUrlAsync(IFormFile? file, string name,
        CancellationToken cancellationToken, long maxBytes = 2 * 1024 * 1024)
    {
        if (file is null) return null;
        var allowed = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
        if (!allowed.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException($"{name} must be a JPEG, PNG, WebP, or GIF image.");
        if (file.Length <= 0 || file.Length > maxBytes)
            throw new ArgumentOutOfRangeException(nameof(file), $"{name} must be between 1 byte and {maxBytes / (1024 * 1024)} MB.");

        await using var stream = file.OpenReadStream();
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return $"data:{file.ContentType};base64,{Convert.ToBase64String(buffer.ToArray())}";
    }
}
