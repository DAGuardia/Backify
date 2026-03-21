namespace Backify.Api.Models;

public class PreviewItem
{
    public int Rank { get; set; }
    public string Name { get; set; } = "";
    public string Artist { get; set; } = "";
    public int PlayCount { get; set; }
    public string? SpotifyId { get; set; }
    public bool Found { get; set; }
}
