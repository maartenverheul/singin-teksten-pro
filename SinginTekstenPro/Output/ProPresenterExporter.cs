using System.IO.Compression;
using System.Reflection;
using System.Text;
using Google.Protobuf;
using Pro.SerializationInterop.RVProtoData;
using SinginTekstenPro.Models;

namespace SinginTekstenPro.Output;

static class ProPresenterExporter
{
    const string ParagraphPrefix = @"\pard\li0\fi0\ri0\qc\sb0\sa0\sl240\slmult1\slleading0\f0\b0\i0\ul0\strike0\fs66\expnd0\expndtw0\CocoaLigature1\cf1\strokewidth0\strokec1\nosupersub\ulc0\highlight2\cb2 ";

    static readonly Presentation Template = LoadTemplate();

    public static byte[] Export(IReadOnlyList<Song> songs)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var song in songs)
            {
                var entry = archive.CreateEntry(UniqueName(Sanitize(song.Name), usedNames) + ".pro");
                using var entryStream = entry.Open();
                Build(song).WriteTo(entryStream);
            }

            archive.CreateEntry("Media/");
            archive.CreateEntry("PDF/");
        }

        return stream.ToArray();
    }

    static Presentation Build(Song song)
    {
        var templateCue = Template.Cues[0];
        var slidesByGroup = song.Groups.ToDictionary(group => group, group => CreateSlides(templateCue, group));
        var groupIds = song.Groups.ToDictionary(group => group, _ => NewUuid());

        var presentation = new Presentation
        {
            ApplicationInfo = Template.ApplicationInfo.Clone(),
            Uuid = NewUuid(),
            Name = song.Name,
            LastModifiedDate = new Timestamp { Seconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
            Background = Template.Background.Clone(),
            Ccli = new Presentation.Types.CCLI(),
            Timeline = new Presentation.Types.Timeline { Duration = 300 },
        };

        foreach (var group in song.Groups)
        {
            var cues = slidesByGroup[group];
            foreach (var cue in cues)
            {
                presentation.Cues.Add(cue);
            }

            presentation.CueGroups.Add(new Presentation.Types.CueGroup
            {
                Group = new Group
                {
                    Uuid = groupIds[group],
                    Name = group.Name,
                    Color = ColorFor(group.Name),
                    ApplicationGroupIdentifier = NewUuid(),
                    ApplicationGroupName = group.Name,
                },
                CueIdentifiers = { cues.Select(cue => cue.Uuid.Clone()) },
            });
        }

        var arrangementId = NewUuid();
        presentation.SelectedArrangement = arrangementId.Clone();
        presentation.Arrangements.Add(new Presentation.Types.Arrangement
        {
            Uuid = arrangementId,
            Name = DateTime.Today.ToString("yyyy-MM-dd"),
            GroupIdentifiers = { song.GroupOrder.Select(group => groupIds[group].Clone()) },
        });

        return presentation;
    }

    static List<Cue> CreateSlides(Cue templateCue, Groups group)
    {
        var lines = group.Slides.SelectMany(slide => slide.Lines).ToList();
        var chunks = lines.Count == 0
            ? [Array.Empty<string>()]
            : lines.Chunk(2).Select(chunk => chunk.ToArray()).ToList();

        return chunks.Select(chunk => CreateCue(templateCue, chunk)).ToList();
    }

    static Cue CreateCue(Cue templateCue, IReadOnlyList<string> lines)
    {
        var cue = templateCue.Clone();
        cue.Name = "";
        cue.IsEnabled = true;
        cue.CompletionTargetUuid = EmptyUuid();
        cue.CompletionActionUuid = EmptyUuid();
        cue.CompletionActionType = Cue.Types.CompletionActionType.Last;

        var action = cue.Actions[0];
        action.IsEnabled = true;
        action.Type = Pro.SerializationInterop.RVProtoData.Action.Types.ActionType.PresentationSlide;

        var text = action.Slide.Presentation.BaseSlide.Elements[0].Element_.Text;
        text.RtfData = ByteString.CopyFromUtf8(BuildRtf(lines));
        text.Attributes.CustomAttributes.Clear();
        var offset = 0;
        for (var index = 0; index < lines.Count; index++)
        {
            var length = lines[index].Length;
            text.Attributes.CustomAttributes.Add(new Graphics.Types.Text.Types.Attributes.Types.CustomAttribute
            {
                Range = new IntRange { Start = offset, End = offset + length },
            });
            offset += length + 1;
        }

        AssignIdentifiers(cue);
        return cue;
    }

    static void AssignIdentifiers(Cue cue)
    {
        cue.Uuid = NewUuid();
        var action = cue.Actions[0];
        action.Uuid = NewUuid();
        var slide = action.Slide.Presentation.BaseSlide;
        slide.Uuid = NewUuid();
        slide.Elements[0].Element_.Uuid = NewUuid();
    }

    static string BuildRtf(IReadOnlyList<string> lines)
    {
        var builder = new StringBuilder();
        builder.Append(@"{\rtf0\ansi\ansicpg1252{\fonttbl\f0\fnil ArialMT;}{\colortbl;\red255\green255\blue255;\red255\green255\blue255;}{\*\expandedcolortbl;\csgenericrgb\c100000\c100000\c100000\c100000;\csgenericrgb\c100000\c100000\c100000\c0;}{\*\listtable}{\*\listoverridetable}\uc1\paperw19200\margl0\margr0\margt0\margb0");

        if (lines.Count == 0)
        {
            builder.Append(ParagraphPrefix);
        }
        else
        {
            for (var index = 0; index < lines.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(@"\par");
                }

                builder.Append(ParagraphPrefix);
                builder.Append(EscapeRtf(lines[index]));
            }
        }

        builder.Append('}');
        return builder.ToString();
    }

    static string EscapeRtf(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            switch (character)
            {
                case '\\' or '{' or '}':
                    builder.Append('\\');
                    builder.Append(character);
                    break;
                default:
                    if (character <= 127)
                    {
                        builder.Append(character);
                    }
                    else
                    {
                        var value = character > 32767 ? character - 65536 : character;
                        builder.Append("\\u").Append(value).Append('?');
                    }

                    break;
            }
        }

        return builder.ToString();
    }

    static Color ColorFor(string name)
    {
        if (name.StartsWith("Pre-Chorus", StringComparison.Ordinal))
        {
            return new Color { Red = 0.95f, Green = 0.6f, Blue = 0.15f, Alpha = 1 };
        }

        if (name.StartsWith("Verse", StringComparison.Ordinal))
        {
            return new Color { Green = 0.46666667f, Blue = 0.8f, Alpha = 1 };
        }

        if (name.StartsWith("Chorus", StringComparison.Ordinal))
        {
            return new Color { Red = 0.8f, Blue = 0.30588236f, Alpha = 1 };
        }

        if (name.StartsWith("Bridge", StringComparison.Ordinal))
        {
            return new Color { Red = 0.55f, Blue = 0.85f, Alpha = 1 };
        }

        if (name.Equals("Interlude", StringComparison.Ordinal))
        {
            return new Color { Red = 0.45f, Green = 0.45f, Blue = 0.45f, Alpha = 1 };
        }

        return new Color { Red = 0.2f, Green = 0.7f, Blue = 0.45f, Alpha = 1 };
    }

    static Presentation LoadTemplate()
    {
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SinginTekstenPro.Template.pro")
            ?? throw new InvalidOperationException("The ProPresenter slide template is missing.");
        using (stream)
        {
            return Presentation.Parser.ParseFrom(stream);
        }
    }

    static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Concat(name.Select(character => invalid.Contains(character) ? '_' : character)).Trim();
        return sanitized.Length == 0 ? "song" : sanitized;
    }

    static string UniqueName(string name, HashSet<string> usedNames)
    {
        var candidate = name;
        var suffix = 2;
        while (!usedNames.Add(candidate))
        {
            candidate = $"{name} {suffix}";
            suffix++;
        }

        return candidate;
    }

    static UUID NewUuid() => new() { String = Guid.NewGuid().ToString() };

    static UUID EmptyUuid() => new() { String = "00000000-0000-0000-0000-000000000000" };
}
