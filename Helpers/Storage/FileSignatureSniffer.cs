namespace StayBook.Helpers.Storage;

/// <summary>
/// Detects the allowed upload types from their magic bytes (v1 used <c>finfo</c>); the client-supplied
/// content type and file name are never trusted.
/// </summary>
public static class FileSignatureSniffer
{
    public const int HeaderLength = 12;

    /// <summary>Allowed MIME types and the extension used for the stored file.</summary>
    public static readonly IReadOnlyDictionary<string, string> Allowed = new Dictionary<string, string>
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp",
        ["application/pdf"] = "pdf",
    };

    public static string? Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return "image/jpeg";
        }

        if (header.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return "image/png";
        }

        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        return header.StartsWith("%PDF-"u8) ? "application/pdf" : null;
    }
}
