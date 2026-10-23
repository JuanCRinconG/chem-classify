#nullable enable
using System;
using QRCoder;

/// <summary>
/// Low-level QR encoding via QRCoder. Produces PNG bytes for an arbitrary string payload.
/// No Godot or UI dependencies — intended for file export and external print workflows.
/// </summary>
public static class QREncoder
{
    /// <summary>
    /// Encodes <paramref name="payload"/> as a PNG image and returns the raw bytes.
    /// </summary>
    /// <exception cref="ArgumentException">Payload is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Options contain invalid values.</exception>
    public static byte[] EncodeToPng(string payload, QrEncodeOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Payload cannot be empty.", nameof(payload));

        QrEncodeOptions opts = options ?? QrEncodeOptions.Default;
        opts.Validate();

        using var generator = new QRCodeGenerator();
        using QRCodeData data = generator.CreateQrCode(
            payload,
            MapErrorCorrection(opts.ErrorCorrection));

        var png = new PngByteQRCode(data);
        return png.GetGraphic(opts.PixelsPerModule, drawQuietZones: opts.DrawQuietZones);
    }

    private static QRCodeGenerator.ECCLevel MapErrorCorrection(QrErrorCorrectionLevel level) =>
        level switch
        {
            QrErrorCorrectionLevel.Low => QRCodeGenerator.ECCLevel.L,
            QrErrorCorrectionLevel.Medium => QRCodeGenerator.ECCLevel.M,
            QrErrorCorrectionLevel.Quartile => QRCodeGenerator.ECCLevel.Q,
            QrErrorCorrectionLevel.High => QRCodeGenerator.ECCLevel.H,
            _ => QRCodeGenerator.ECCLevel.M
        };
}

/// <summary>Reed–Solomon error correction tier for printed labels.</summary>
public enum QrErrorCorrectionLevel
{
    Low,
    Medium,
    Quartile,
    High
}

/// <summary>Rendering options passed to <see cref="QREncoder"/>.</summary>
public readonly record struct QrEncodeOptions(
    QrErrorCorrectionLevel ErrorCorrection = QrErrorCorrectionLevel.Medium,
    int PixelsPerModule = 8,
    bool DrawQuietZones = true)
{
    public const int MinPixelsPerModule = 1;
    public const int MaxPixelsPerModule = 64;

    public static QrEncodeOptions Default { get; } = new();

    internal void Validate()
    {
        if (PixelsPerModule < MinPixelsPerModule || PixelsPerModule > MaxPixelsPerModule)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PixelsPerModule),
                PixelsPerModule,
                $"Must be between {MinPixelsPerModule} and {MaxPixelsPerModule}.");
        }
    }
}
