using System.Text.Json.Serialization;

namespace SpriteGen.Application.Persistence;

public class SpriteFile
{
    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("palette")]
    public string[] Palette { get; set; } = [];

    [JsonPropertyName("fps")]
    public int Fps { get; set; } = 8;

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = "";

    [JsonPropertyName("frames")]
    public int[][] Frames { get; set; } = [];
}