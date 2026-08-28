using HCS.Identity;
using HCS.PlatformService.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCS.PlatformService.Controllers;

[ApiController, Authorize, Route("api/identity")]
public sealed class ProfileAvatarController(UserAvatarAppService avatars) : ControllerBase
{
    [HttpGet("profile/avatar")]
    public Task<IActionResult> GetMine(CancellationToken cancellationToken) =>
        GetAvatarFileAsync(userId: null, cancellationToken);

    [HttpGet("users/{userId:guid}/avatar")]
    public Task<IActionResult> GetUser(Guid userId, CancellationToken cancellationToken) =>
        GetAvatarFileAsync(userId, cancellationToken);

    [HttpPost("profile/avatar")]
    [RequestSizeLimit(UserAvatar.MaxSizeBytes)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length <= 0)
        {
            return BadRequest("A non-empty image file is required.");
        }

        await using var stream = file.OpenReadStream();
        await avatars.UploadAsync(file.FileName, file.ContentType, stream, file.Length, cancellationToken);
        return NoContent();
    }

    [HttpDelete("profile/avatar")]
    public async Task<IActionResult> Delete(CancellationToken cancellationToken)
    {
        await avatars.DeleteAsync(cancellationToken);
        return NoContent();
    }

    private async Task<IActionResult> GetAvatarFileAsync(Guid? userId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await avatars.GetAsync(userId, cancellationToken);
            return File(result.Content, result.ContentType, result.FileName, enableRangeProcessing: true);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
