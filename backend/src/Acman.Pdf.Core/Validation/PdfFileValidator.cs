using Acman.Pdf.Abstractions.Exceptions;

namespace Acman.Pdf.Core.Validation;

public static class PdfFileValidator
{
    private static readonly byte[] PdfMagic = "%PDF"u8.ToArray();

    public const long DefaultMaxUploadBytes = 50 * 1024 * 1024;

    public static void EnsurePdfMagic(ReadOnlySpan<byte> header)
    {
        if (header.Length < 5 || !header[..4].SequenceEqual(PdfMagic))
        {
            throw new PdfOpenException("File content is not a valid PDF (missing %PDF header).");
        }
    }

    public static async Task EnsurePdfMagicAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        if (!stream.CanSeek)
        {
            throw new PdfOpenException("PDF stream must be seekable for validation.");
        }

        var original = stream.Position;
        stream.Position = 0;
        var buffer = new byte[8];
        var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
        stream.Position = original;

        if (read < 5)
        {
            throw new PdfOpenException("File is too small to be a PDF.");
        }

        EnsurePdfMagic(buffer.AsSpan(0, read));
    }

    public static void EnsureWithinSizeLimit(long sizeBytes, long maxBytes = DefaultMaxUploadBytes)
    {
        if (sizeBytes <= 0)
        {
            throw new PdfOpenException("PDF file is empty.");
        }

        if (sizeBytes > maxBytes)
        {
            throw new PdfOpenException($"PDF exceeds maximum allowed size of {maxBytes} bytes.");
        }
    }
}
