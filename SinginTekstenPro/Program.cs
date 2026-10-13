using SinginTekstenPro.Input;
using SinginTekstenPro.Output;
using System.Text;

namespace SinginTekstenPro;

static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: SinginTekstenPro <file.pdf|file.docx>");
            return 1;
        }

        var path = args[0];
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"File not found: {path}");
            return 1;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath) ?? ".";
            var bundlePath = Path.Combine(directory, Path.GetFileNameWithoutExtension(fullPath) + ".proBundle");
            Console.WriteLine(ProPresenterExporter.Export(SongParser.Parse(Read(path)), bundlePath));

            return 0;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    static DocumentContent Read(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".docx" => DocxDocumentReader.Read(path),
            ".pdf" => PdfDocumentReader.Read(path),
            var extension => throw new ArgumentException($"Unsupported file type: {extension}"),
        };
    }
}
