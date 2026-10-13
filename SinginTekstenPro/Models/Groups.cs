namespace SinginTekstenPro.Models;

public class Groups
{
    public required string Name { get; set; }
    public required string RawText { get; set; }
    public List<Slide> Slides { get; set; } = [];
}
