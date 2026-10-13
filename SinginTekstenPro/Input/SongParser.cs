using SinginTekstenPro.Models;
using System.Text.RegularExpressions;

namespace SinginTekstenPro.Input;

static class SongParser
{
    static readonly Regex RepeatSuffix = new(@"\s*\((\d+)\s*[xX]\)\s*$", RegexOptions.Compiled);

    static readonly Regex NumberedName = new(@"^(PRE-REFREIN|REFREIN|COUPLET|BRIDGE|VERS)(?:\s+(\d+))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static readonly Regex CapitalizedTitleWords = new(@"\b(god|hij|uw|you|u)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    static readonly Regex TitleSpecialCharacters = new(@"[^\p{L}\p{N}\s-]", RegexOptions.Compiled);

    static readonly Regex TitleWhitespace = new(@"\s+", RegexOptions.Compiled);

    static readonly string[] LabelPrefixes =
    [
        "COUPLET",
        "PRE-REFREIN",
        "REFREIN",
        "BRIDGE",
        "VERS",
        "OUTRO",
        "INTRO",
        "HERHALING",
        "SLOT",
        "EINDE",
    ];

    public static List<Song> Parse(DocumentContent content)
    {
        var songs = new List<Song>();
        Song? song = null;
        Groups? collecting = null;
        string collectingKey = "";
        var collectingOrderStart = 0;
        List<string>? lyrics = null;
        var discardLyrics = false;
        var variants = new Dictionary<string, List<Groups>>(StringComparer.OrdinalIgnoreCase);
        var blocks = content.Blocks;

        void FlushLyrics()
        {
            if (collecting is not null && lyrics is { Count: > 0 } && song is not null && !IsContentless(collectingKey))
            {
                var lines = lyrics.Select(line => line.Replace(".", "")).ToArray();
                var text = string.Join('\n', lines);
                var family = variants[collectingKey];
                var match = family.FirstOrDefault(group => group.RawText == text);
                if (match is null && family.Count == 1 && family[0].RawText.Length == 0)
                {
                    family[0].RawText = text;
                    family[0].Slides.Add(new Slide { Lines = lines });
                }
                else if (match is null)
                {
                    if (family.Count == 1 && IsUnnumbered(family[0].Name))
                    {
                        family[0].Name = $"{NumberedName.Match(family[0].Name).Groups[1].Value} 1";
                    }

                    var created = new Groups { Name = NextVariantName(song, collectingKey, family.Count + 1), RawText = text };
                    created.Slides.Add(new Slide { Lines = lines });
                    song.Groups.Add(created);
                    family.Add(created);
                    Retarget(created);
                }
                else if (!ReferenceEquals(match, collecting))
                {
                    Retarget(match);
                }
            }

            collecting = null;
            lyrics = null;
        }

        void Retarget(Groups target)
        {
            for (var orderIndex = collectingOrderStart; orderIndex < song!.GroupOrder.Count; orderIndex++)
            {
                if (ReferenceEquals(song.GroupOrder[orderIndex], collecting))
                {
                    song.GroupOrder[orderIndex] = target;
                }
            }
        }

        void EnsureSong()
        {
            if (song is not null)
            {
                return;
            }

            song = new Song { Name = "" };
            songs.Add(song);
        }

        void AddToOrder(Song target, string label)
        {
            var names = ParseCueNames(label, out var repeat);
            var replay = IsReplay(label);
            Groups? defined = null;
            var orderStart = target.GroupOrder.Count;

            for (var time = 0; time < repeat; time++)
            {
                foreach (var name in names)
                {
                    var group = Resolve(target, name);
                    if (IsContentless(name))
                    {
                        group.RawText = "";
                        group.Slides.Clear();
                    }

                    target.GroupOrder.Add(group);
                    defined ??= group;
                }
            }

            if (!replay && names.Count == 1 && defined is not null && !IsContentless(names[0]))
            {
                collecting = defined;
                collectingKey = names[0];
                collectingOrderStart = orderStart;
                lyrics = [];
                discardLyrics = false;
                return;
            }

            discardLyrics = names.Count > 0 && names.All(IsContentless);
        }

        Groups Resolve(Song target, string name)
        {
            if (variants.TryGetValue(name, out var family))
            {
                return family[0];
            }

            var existing = target.Groups.FirstOrDefault(group => group.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                variants[name] = [existing];
                return existing;
            }

            var created = new Groups { Name = name, RawText = "" };
            target.Groups.Add(created);
            variants[name] = [created];
            return created;
        }

        for (var index = 0; index < blocks.Count; index++)
        {
            var block = blocks[index];
            if (IsLead(block))
            {
                continue;
            }

            if (block.Kind == ContentKind.Text)
            {
                FlushLyrics();
                variants.Clear();
                song = null;
                discardLyrics = false;
                continue;
            }

            if (IsSongTitle(blocks, index))
            {
                FlushLyrics();
                variants.Clear();
                song = new Song { Name = HumanizeTitle(block.Lines[0].Trim()) };
                songs.Add(song);
                discardLyrics = false;
                continue;
            }

            for (var lineIndex = 0; lineIndex < block.Lines.Count; lineIndex++)
            {
                var line = block.Lines[lineIndex];
                if (lineIndex == 0 && IsLabel(line))
                {
                    FlushLyrics();
                    EnsureSong();
                    AddToOrder(song!, line);
                    continue;
                }

                if (discardLyrics || collecting is null || lyrics is null)
                {
                    continue;
                }

                lyrics.Add(line);
            }
        }

        FlushLyrics();
        foreach (var parsed in songs)
        {
            TranslateGroupNames(parsed);
            EnsureIntro(parsed);
        }

        return songs;
    }

    static void EnsureIntro(Song song)
    {
        var intro = new Groups { Name = "Intro", RawText = "" };
        intro.Slides.Add(new Slide());
        song.Groups.Insert(0, intro);
        song.GroupOrder.Insert(0, intro);
    }

    static void TranslateGroupNames(Song song)
    {
        var byName = new Dictionary<string, Groups>(StringComparer.OrdinalIgnoreCase);
        var replacements = new Dictionary<Groups, Groups>();

        foreach (var group in song.Groups.ToList())
        {
            group.Name = TranslateName(group.Name);
            if (byName.TryGetValue(group.Name, out var existing))
            {
                replacements[group] = existing;
                song.Groups.Remove(group);
            }
            else
            {
                byName[group.Name] = group;
            }
        }

        song.GroupOrder = song.GroupOrder
            .Select(group => replacements.GetValueOrDefault(group, group))
            .ToList();
    }

    static string TranslateName(string name)
    {
        var suffix = "";
        var variant = Regex.Match(name, @"^(.+?) \((\d+)\)$");
        if (variant.Success)
        {
            name = variant.Groups[1].Value;
            suffix = $" ({variant.Groups[2].Value})";
        }

        var match = NumberedName.Match(name);
        if (match.Success)
        {
            var english = match.Groups[1].Value.ToUpperInvariant() switch
            {
                "PRE-REFREIN" => "Pre-Chorus",
                "REFREIN" => "Chorus",
                "BRIDGE" => "Bridge",
                "COUPLET" or "VERS" => "Verse",
                _ => match.Groups[1].Value,
            };

            var translated = match.Groups[2].Success ? $"{english} {match.Groups[2].Value}" : english;
            return translated + suffix;
        }

        if (name.Contains("TUSSENSPEL", StringComparison.OrdinalIgnoreCase))
        {
            return "Interlude";
        }

        return name.ToUpperInvariant() switch
        {
            "EINDE" => "Ending",
            _ => name,
        } + suffix;
    }

    static bool IsUnnumbered(string name)
    {
        var match = NumberedName.Match(name);
        return match.Success && !match.Groups[2].Success;
    }

    static string NextVariantName(Song song, string label, int number)
    {
        var match = NumberedName.Match(label);
        while (true)
        {
            var candidate = match.Success && !match.Groups[2].Success
                ? $"{match.Groups[1].Value} {number}"
                : $"{label} ({number})";
            if (!song.Groups.Any(group => group.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }

            number++;
        }
    }

    static List<string> ParseCueNames(string label, out int repeat)
    {
        var text = label.Trim();
        repeat = 1;
        var repeatMatch = RepeatSuffix.Match(text);
        if (repeatMatch.Success)
        {
            repeat = int.Parse(repeatMatch.Groups[1].Value);
            text = text[..repeatMatch.Index].Trim();
        }

        if (text.Length >= 2 && text[0] == '(' && text[^1] == ')')
        {
            text = text[1..^1].Trim();
        }

        return text
            .Split([',', '&'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeName)
            .Where(name => name.Length > 0)
            .ToList();
    }

    static bool IsReplay(string label)
    {
        var text = label.Trim();
        var repeatMatch = RepeatSuffix.Match(text);
        if (repeatMatch.Success)
        {
            text = text[..repeatMatch.Index].Trim();
        }

        return text.Length >= 2 && text[0] == '(' && text[^1] == ')'
            || text.Contains('&')
            || text.Contains(',');
    }

    static bool IsContentless(string name)
    {
        return name.Equals("PRE-REFREIN", StringComparison.OrdinalIgnoreCase)
            || name.Contains("TUSSENSPEL", StringComparison.OrdinalIgnoreCase);
    }

    static string HumanizeTitle(string title)
    {
        var chars = StripSpecialCharacters(title).ToLowerInvariant().ToCharArray();
        for (var index = 0; index < chars.Length; index++)
        {
            if (!char.IsLetter(chars[index]))
            {
                continue;
            }

            chars[index] = char.ToUpperInvariant(chars[index]);
            break;
        }

        return CapitalizedTitleWords.Replace(new string(chars), match => match.Value.ToLowerInvariant() switch
        {
            "god" => "God",
            "hij" => "Hij",
            "uw" => "Uw",
            "you" => "You",
            "u" => "U",
            _ => match.Value,
        });
    }

    static string StripSpecialCharacters(string title)
    {
        return TitleWhitespace.Replace(TitleSpecialCharacters.Replace(title, ""), " ").Trim();
    }

    static string NormalizeName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '(' && trimmed[^1] == ')')
        {
            trimmed = trimmed[1..^1].Trim();
        }

        return trimmed.EndsWith(':') ? trimmed[..^1].TrimEnd() : trimmed;
    }

    static bool IsLead(ContentBlock block)
    {
        return block.Kind == ContentKind.Row
            && block.Lines[0].StartsWith("LEAD:", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsSongTitle(IReadOnlyList<ContentBlock> blocks, int index)
    {
        var block = blocks[index];
        if (block.Kind != ContentKind.Row || block.Lines.Count != 1 || IsLead(block) || IsLabel(block.Lines[0]))
        {
            return false;
        }

        for (var nextIndex = index + 1; nextIndex < blocks.Count; nextIndex++)
        {
            var next = blocks[nextIndex];
            if (next.Kind == ContentKind.Text)
            {
                return false;
            }

            if (IsLead(next))
            {
                return true;
            }

            if (next.Lines.Count > 0)
            {
                return false;
            }
        }

        return false;
    }

    static bool IsLabel(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '(' && trimmed[^1] == ')')
        {
            return true;
        }

        if (!LabelPrefixes.Any(prefix => StartsWithKeyword(trimmed, prefix)))
        {
            return false;
        }

        return !trimmed.Any(char.IsLower);
    }

    static bool StartsWithKeyword(string line, string keyword)
    {
        if (!line.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return line.Length == keyword.Length || !char.IsLetter(line[keyword.Length]);
    }

    }
