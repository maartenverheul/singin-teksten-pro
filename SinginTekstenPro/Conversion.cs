using SinginTekstenPro.Input;
using SinginTekstenPro.Output;

namespace SinginTekstenPro;

public static class Conversion
{
    public static byte[] ToProBundle(string fileName, Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.CanSeek)
        {
            return Convert(fileName, input);
        }

        using var buffer = new MemoryStream();
        input.CopyTo(buffer);
        buffer.Position = 0;
        return Convert(fileName, buffer);
    }

    static byte[] Convert(string fileName, Stream input)
    {
        return ProPresenterExporter.Export(SongParser.Parse(Read(fileName, input)));
    }

    static DocumentContent Read(string fileName, Stream input)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".docx" => DocxDocumentReader.Read(input),
            ".pdf" => PdfDocumentReader.Read(input),
            var extension => throw new ArgumentException($"Unsupported file type: {extension}"),
        };
    }
}
