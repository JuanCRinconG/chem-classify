#nullable enable
using System;
using System.IO;

/// <summary>
/// High-level QR generation for arbitrary string payloads (e.g. SDS ids).
/// Returns PNG bytes or writes a PNG file — never surfaces Godot textures or in-app previews.
/// </summary>
public static class QRGenerator
{
    /// <summary>
    /// Encodes <paramref name="payload"/> and returns PNG bytes on success.
    /// </summary>
    public static QrGenerateResult Generate(string payload, QrEncodeOptions? options = null)
    {
        try
        {
            byte[] pngBytes = QREncoder.EncodeToPng(payload, options);
            return QrGenerateResult.Success(pngBytes);
        }
        catch (ArgumentException ex)
        {
            return QrGenerateResult.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            return QrGenerateResult.Fail($"QR encoding failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Encodes <paramref name="payload"/> and writes a PNG to <paramref name="outputPath"/>.
    /// Parent directories are created when missing.
    /// </summary>
    public static QrGenerateResult GenerateToFile(
        string payload,
        string outputPath,
        QrEncodeOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return QrGenerateResult.Fail("Output path is required.");

        QrGenerateResult encoded = Generate(payload, options);
        if (!encoded.Ok)
            return encoded;

        try
        {
            string fullPath = Path.GetFullPath(outputPath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllBytes(fullPath, encoded.PngBytes!);
            return QrGenerateResult.Success(encoded.PngBytes!, fullPath);
        }
        catch (Exception ex)
        {
            return QrGenerateResult.Fail($"Could not write QR PNG: {ex.Message}");
        }
    }
}

/// <summary>
/// Outcome of <see cref="QRGenerator"/>. Mirrors <see cref="SdsRegisterResult"/> for UI handling.
/// </summary>
public readonly record struct QrGenerateResult(byte[]? PngBytes, string? SavedFilePath, string? ErrorMessage)
{
    public bool Ok => PngBytes is { Length: > 0 };

    public static QrGenerateResult Success(byte[] pngBytes, string? savedFilePath = null)
        => new(pngBytes, savedFilePath, null);

    public static QrGenerateResult Fail(string message)
        => new(null, null, message);
}
