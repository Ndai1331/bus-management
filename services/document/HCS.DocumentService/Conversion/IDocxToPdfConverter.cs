namespace HCS.DocumentService.Conversion;

public interface IDocxToPdfConverter
{
    bool IsAvailable { get; }
    Task<byte[]?> ConvertAsync(byte[] docxBytes, CancellationToken cancellationToken = default);
}
