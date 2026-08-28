using System;
using System.IO;

namespace HCS.Identity;

public sealed class UserAvatar
{
    public const long MaxSizeBytes = 2 * 1024 * 1024;

    private UserAvatar()
    {
    }

    public UserAvatar(Guid userId, string fileName, string contentType, string blobName, long size, DateTime now)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User is required.", nameof(userId));
        }

        UserId = userId;
        Replace(fileName, contentType, blobName, size, now);
    }

    public Guid UserId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public string BlobName { get; private set; } = string.Empty;
    public long Size { get; private set; }
    public DateTime CreationTime { get; private set; }
    public DateTime LastModificationTime { get; private set; }

    public void Replace(string fileName, string contentType, string blobName, long size, DateTime now)
    {
        if (size is <= 0 or > MaxSizeBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        FileName = Path.GetFileName(fileName.Trim());
        ContentType = contentType.Trim();
        BlobName = blobName;
        Size = size;
        if (CreationTime == default)
        {
            CreationTime = now;
        }

        LastModificationTime = now;
    }
}
