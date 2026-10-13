using System.Text;
using System.Text.Json;
using SinginTekstenPro.Models;

namespace SinginTekstenPro.Output;

static class DocumentExporter
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string ToJson(IReadOnlyList<Song> songs)
    {
        return JsonSerializer.Serialize(new ExportFile(songs.Select(ToSong).ToList()), JsonOptions);
    }

    public static string ToMarkdown(string serviceName, IReadOnlyList<Song> songs)
    {
        var builder = new StringBuilder();
        builder.Append("# ").AppendLine(serviceName);
        builder.AppendLine();

        for (var songIndex = 0; songIndex < songs.Count; songIndex++)
        {
            var song = songs[songIndex];
            builder.Append("## ").AppendLine(song.Name);
            builder.AppendLine();
            builder.Append("Order: ").AppendLine(string.Join(", ", song.GroupOrder.Select(group => group.Name)));
            builder.AppendLine();

            for (var groupIndex = 0; groupIndex < song.Groups.Count; groupIndex++)
            {
                var group = song.Groups[groupIndex];
                builder.Append(group.Name).AppendLine(":");
                foreach (var line in group.Slides.SelectMany(slide => slide.Lines))
                {
                    builder.AppendLine(line);
                }

                var isLastGroup = songIndex == songs.Count - 1 && groupIndex == song.Groups.Count - 1;
                if (!isLastGroup)
                {
                    builder.AppendLine();
                }
            }
        }

        return builder.ToString();
    }

    static SongFile ToSong(Song song)
    {
        return new SongFile(
            song.Name,
            song.Groups.Select(group => new GroupFile(group.Name, group.Slides.Select(slide => slide.Lines).ToList())).ToList(),
            song.GroupOrder.Select(group => group.Name).ToList());
    }

    sealed record ExportFile(IReadOnlyList<SongFile> Songs);

    sealed record SongFile(string Name, IReadOnlyList<GroupFile> Groups, IReadOnlyList<string> Order);

    sealed record GroupFile(string Name, IReadOnlyList<string[]> Slides);
}
