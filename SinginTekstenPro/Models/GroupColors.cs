namespace SinginTekstenPro.Models;

public static class GroupColors
{
    public static bool AllowsLyrics(string name)
    {
        var (family, _) = Parse(name);
        return family is not "intro" and not "interlude";
    }

    public static string Css(string name)
    {
        var (red, green, blue) = Rgb(name);
        return $"#{red:X2}{green:X2}{blue:X2}";
    }

    public static (byte Red, byte Green, byte Blue) Rgb(string name)
    {
        var (family, number) = Parse(name);
        var palette = Palette(family);
        var index = Math.Clamp(Math.Max(number, 1) - 1, 0, palette.Length - 1);
        return palette[index];
    }

    static (byte, byte, byte)[] Palette(string family) => family switch
    {
        "verse" => [(0x00, 0x77, 0xCC), (0x00, 0x59, 0x99), (0x00, 0x3C, 0x66), (0x00, 0x68, 0xB3), (0x00, 0x4A, 0x80), (0x00, 0x2D, 0x4D)],
        "chorus" => [(0xCC, 0x00, 0x4E), (0x99, 0x00, 0x3B), (0x66, 0x00, 0x27), (0xB3, 0x00, 0x44)],
        "bridge" => [(0x76, 0x00, 0xCC), (0x59, 0x00, 0x99), (0x3B, 0x00, 0x66)],
        "prechorus" => [(0xCC, 0x29, 0x8B)],
        "tag" => [(0xCC, 0x29, 0x29)],
        "intro" => [(0xB3, 0xA7, 0x24)],
        "ending" => [(0x99, 0x8F, 0x1F)],
        "outro" => [(0x7E, 0x76, 0x19)],
        "interlude" or "vamp" or "turnaround" => [(0x24, 0xB3, 0x4C)],
        "blank" => [(0x00, 0x00, 0x00)],
        _ => [(0x33, 0xB3, 0x73)],
    };

    static (string Family, int Number) Parse(string name)
    {
        var text = name.Trim();
        var variant = text.LastIndexOf(" (", StringComparison.Ordinal);
        if (variant > 0 && text.EndsWith(')'))
        {
            text = text[..variant];
        }

        var number = 0;
        var split = text.LastIndexOf(' ');
        if (split > 0 && int.TryParse(text[(split + 1)..], out var parsed))
        {
            number = parsed;
            text = text[..split];
        }

        return (text.Replace("-", "", StringComparison.Ordinal).ToLowerInvariant(), number);
    }
}
