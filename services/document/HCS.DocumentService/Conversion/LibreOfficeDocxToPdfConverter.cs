using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HCS.DocumentService.Conversion;

public sealed class LibreOfficeDocxToPdfConverter(IConfiguration configuration, ILogger<LibreOfficeDocxToPdfConverter> logger)
    : IDocxToPdfConverter
{
    private readonly string _sofficePath = ResolveSofficePath(configuration);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool IsAvailable => File.Exists(_sofficePath);

    public async Task<byte[]?> ConvertAsync(byte[] docxBytes, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable || docxBytes.Length == 0) return null;
        await _gate.WaitAsync(cancellationToken);
        var workDir = Path.Combine(Path.GetTempPath(), "hcs-lo-convert", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            var inputPath = Path.Combine(workDir, "input.docx");
            await File.WriteAllBytesAsync(inputPath, docxBytes, cancellationToken);
            var start = new ProcessStartInfo
            {
                FileName = _sofficePath,
                Arguments = $"--headless --nologo --nodefault --norestore --nolockcheck --nofirststartwizard --convert-to pdf --outdir \"{workDir}\" \"{inputPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(start);
            if (process is null) return null;
            await process.WaitForExitAsync(cancellationToken);
            var pdf = Directory.EnumerateFiles(workDir, "*.pdf").FirstOrDefault();
            if (process.ExitCode != 0 || pdf is null)
            {
                logger.LogWarning("LibreOffice convert failed with exit {ExitCode} for {Path}", process.ExitCode, inputPath);
                return null;
            }
            return await File.ReadAllBytesAsync(pdf, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LibreOffice convert threw.");
            return null;
        }
        finally
        {
            _gate.Release();
            try { Directory.Delete(workDir, recursive: true); } catch { /* temp cleanup */ }
        }
    }

    private static string ResolveSofficePath(IConfiguration configuration)
    {
        var configured = configuration["LibreOffice:SofficePath"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (File.Exists(configured)) return configured;
            if (LooksLikeCommand(configured) && FindOnPath(configured) is { } found) return found;
            return "";
        }
        foreach (var candidate in CandidatePaths())
        {
            if (File.Exists(candidate)) return candidate;
        }
        return FindOnPath("soffice") ?? FindOnPath("soffice.bin") ?? "";
    }

    private static IEnumerable<string> CandidatePaths()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            yield return "/opt/homebrew/bin/soffice";
            yield return "/Applications/LibreOffice.app/Contents/MacOS/soffice";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            yield return "/usr/bin/soffice";
            yield return "/usr/lib/libreoffice/program/soffice";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            yield return @"C:\Program Files\LibreOffice\program\soffice.exe";
        }
    }

    private static bool LooksLikeCommand(string path) =>
        path.IndexOfAny(['/', '\\']) < 0;

    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path)) return null;
        foreach (var folder in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(folder.Trim(), fileName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
