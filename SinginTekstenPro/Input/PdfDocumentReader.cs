using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace SinginTekstenPro.Input;

static class PdfDocumentReader
{
    const double LineTolerance = 3;
    const double ColumnGap = 30;

    sealed record PdfLine(string Text, double Left);

    public static DocumentContent Read(Stream stream)
    {
        using var document = PdfDocument.Open(stream);
        var lines = new List<PdfLine>();
        foreach (var page in document.GetPages())
        {
            Collect(page, lines);
        }

        return Finish(lines);
    }

    public static async Task<DocumentContent> ReadAsync(Stream stream)
    {
        using var document = PdfDocument.Open(stream);
        var lines = new List<PdfLine>();
        foreach (var page in document.GetPages())
        {
            Collect(page, lines);
            await Task.Delay(1);
        }

        return Finish(lines);
    }

    static void Collect(Page page, List<PdfLine> lines)
    {
        var words = NearestNeighbourWordExtractor.Instance
            .GetWords(page.Letters)
            .Where(word => !string.IsNullOrWhiteSpace(word.Text))
            .ToList();

        foreach (var line in GroupLines(words))
        {
            var left = line.Min(word => word.BoundingBox.Left);
            var text = FirstColumnText(line, page.Width);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            lines.Add(new PdfLine(text.Trim(), left));
        }
    }

    static DocumentContent Finish(List<PdfLine> lines)
    {
        if (lines.Count == 0)
        {
            return new DocumentContent([]);
        }

        var bodyLeft = lines
            .GroupBy(line => Math.Round(line.Left))
            .MaxBy(group => group.Count())!
            .Key;

        var blocks = lines.Select((line, index) => new ContentBlock(KindFor(lines, index, bodyLeft), [line.Text])).ToList();
        return new DocumentContent(blocks);
    }

    static ContentKind KindFor(IReadOnlyList<PdfLine> lines, int index, double bodyLeft)
    {
        var line = lines[index];
        if (line.Left <= bodyLeft + 12 || line.Text.StartsWith("LEAD:", StringComparison.Ordinal))
        {
            return ContentKind.Row;
        }

        if (index + 1 >= lines.Count)
        {
            return ContentKind.Text;
        }

        var next = lines[index + 1];
        var nextIsRow = next.Text.StartsWith("LEAD:", StringComparison.Ordinal) || next.Left <= bodyLeft + 12;
        return nextIsRow ? ContentKind.Row : ContentKind.Text;
    }

    static string FirstColumnText(IReadOnlyList<Word> line, double pageWidth)
    {
        var sorted = line.OrderBy(word => word.BoundingBox.Left).ToList();
        var rightColumn = pageWidth * 0.45;
        if (sorted[0].BoundingBox.Left >= pageWidth * 0.58)
        {
            return "";
        }

        var kept = new List<Word>();
        foreach (var word in sorted)
        {
            if (kept.Count > 0)
            {
                var gap = word.BoundingBox.Left - kept[^1].BoundingBox.Right;
                if (gap > ColumnGap && word.BoundingBox.Left >= rightColumn)
                {
                    break;
                }
            }

            kept.Add(word);
        }

        return string.Join(' ', kept.Select(word => word.Text.Trim()));
    }

    static List<List<Word>> GroupLines(IReadOnlyList<Word> words)
    {
        var lines = new List<List<Word>>();
        foreach (var word in words.OrderByDescending(word => word.BoundingBox.Bottom))
        {
            var line = lines.LastOrDefault();
            if (line is null || Math.Abs(line[0].BoundingBox.Bottom - word.BoundingBox.Bottom) > LineTolerance)
            {
                line = [];
                lines.Add(line);
            }

            line.Add(word);
        }

        return lines;
    }
}
