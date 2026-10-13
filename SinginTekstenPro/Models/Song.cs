namespace SinginTekstenPro.Models;

public class Song
{
    public required string Name { get; set; }
    public List<Groups> Groups { get; set; } = [];
    public List<Groups> GroupOrder { get; set; } = [];
}
