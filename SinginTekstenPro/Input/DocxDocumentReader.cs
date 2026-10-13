using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace SinginTekstenPro.Input;

static class DocxDocumentReader
{
    public static DocumentContent Read(Stream stream)
    {
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document?.Body
            ?? throw new InvalidDataException("The document has no body.");

        return ReadBody(body);
    }

    public static async Task<DocumentContent> ReadAsync(Stream stream)
    {
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document?.Body
            ?? throw new InvalidDataException("The document has no body.");
        var blocks = new List<ContentBlock>();
        foreach (var child in body.ChildElements)
        {
            AddChild(blocks, child);
            await Task.Delay(1);
        }

        return new DocumentContent(blocks);
    }

    static DocumentContent ReadBody(Body body)
    {
        var blocks = new List<ContentBlock>();
        foreach (var child in body.ChildElements)
        {
            AddChild(blocks, child);
        }

        return new DocumentContent(blocks);
    }

    static void AddChild(List<ContentBlock> blocks, DocumentFormat.OpenXml.OpenXmlElement child)
    {
        switch (child)
        {
            case Paragraph paragraph:
                AddBlock(blocks, ContentKind.Text, LinesOf(paragraph));
                break;
            case Table table:
                foreach (var row in table.Elements<TableRow>())
                {
                    var cell = row.Elements<TableCell>().FirstOrDefault();
                    if (cell is null)
                    {
                        continue;
                    }

                    var lines = cell.Elements<Paragraph>().SelectMany(LinesOf).ToList();
                    AddBlock(blocks, ContentKind.Row, lines);
                }

                break;
        }
    }

    static void AddBlock(List<ContentBlock> blocks, ContentKind kind, IEnumerable<string> lines)
    {
        var cleaned = lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

        if (cleaned.Count > 0)
        {
            blocks.Add(new ContentBlock(kind, cleaned));
        }
    }

    static IEnumerable<string> LinesOf(Paragraph paragraph)
    {
        var current = new System.Text.StringBuilder();
        foreach (var element in paragraph.Descendants())
        {
            if (element is Text text)
            {
                current.Append(text.Text);
            }
            else if (element is Break lineBreak && IsTextBreak(lineBreak))
            {
                yield return current.ToString();
                current.Clear();
            }
            else if (element is CarriageReturn)
            {
                yield return current.ToString();
                current.Clear();
            }
            else if (element is TabChar)
            {
                current.Append(' ');
            }
        }

        yield return current.ToString();
    }

    static bool IsTextBreak(Break lineBreak)
    {
        return lineBreak.Type is null || !lineBreak.Type.HasValue || lineBreak.Type.Value == BreakValues.TextWrapping;
    }
}
