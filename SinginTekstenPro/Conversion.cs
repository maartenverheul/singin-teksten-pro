using SinginTekstenPro.Input;
using SinginTekstenPro.Models;
using SinginTekstenPro.Output;

namespace SinginTekstenPro;

public static class Conversion
{
    public static List<Song> ReadSongs(string fileName, Stream input, int linesPerSlide = 2, bool addBlankIntro = true)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.CanSeek)
        {
            return Parse(fileName, input, linesPerSlide, addBlankIntro);
        }

        using var buffer = new MemoryStream();
        input.CopyTo(buffer);
        buffer.Position = 0;
        return Parse(fileName, buffer, linesPerSlide, addBlankIntro);
    }

    static List<Song> Parse(string fileName, Stream input, int linesPerSlide, bool addBlankIntro)
    {
        var songs = SongParser.Parse(Read(fileName, input), addBlankIntro);
        SplitSlides(songs, linesPerSlide);
        return songs;
    }

    static void SplitSlides(List<Song> songs, int linesPerSlide)
    {
        var size = Math.Clamp(linesPerSlide, 1, 8);
        foreach (var group in songs.SelectMany(song => song.Groups))
        {
            var lines = group.Slides.SelectMany(slide => slide.Lines).ToList();
            group.Slides = lines.Count == 0
                ? [new Slide()]
                : lines.Chunk(size).Select(chunk => new Slide { Lines = chunk.ToArray() }).ToList();
        }
    }

    public static byte[] ToProBundle(string fileName, Stream input)
    {
        return ToProBundle(ReadSongs(fileName, input));
    }

    public static async Task<List<Song>> ReadSongsAsync(string fileName, Stream input, int linesPerSlide = 2, bool addBlankIntro = true)
    {
        ArgumentNullException.ThrowIfNull(input);
        MemoryStream? buffer = null;
        if (!input.CanSeek)
        {
            buffer = new MemoryStream();
            await input.CopyToAsync(buffer);
            buffer.Position = 0;
            input = buffer;
        }

        using (buffer)
        {
            var songs = SongParser.Parse(await ReadAsync(fileName, input), addBlankIntro);
            SplitSlides(songs, linesPerSlide);
            return songs;
        }
    }

    public static byte[] ToProBundle(IReadOnlyList<Song> songs)
    {
        return ProPresenterExporter.Export(songs);
    }

    public static Task<byte[]> ToProBundleAsync(IReadOnlyList<Song> songs)
    {
        return ProPresenterExporter.ExportAsync(songs);
    }

    public static string ToMarkdown(string serviceName, IReadOnlyList<Song> songs)
    {
        return DocumentExporter.ToMarkdown(serviceName, songs);
    }

    public static string ToJson(IReadOnlyList<Song> songs)
    {
        return DocumentExporter.ToJson(songs);
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

    static Task<DocumentContent> ReadAsync(string fileName, Stream input)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".docx" => DocxDocumentReader.ReadAsync(input),
            ".pdf" => PdfDocumentReader.ReadAsync(input),
            var extension => throw new ArgumentException($"Unsupported file type: {extension}"),
        };
    }
}
