namespace SpriteGen.Domain.Models;

public class Palette
{
    public const int MaxColors = 24;
    public string[] Colors { get; }

    private Palette(string[] colors)
    {
        Colors = colors;
    }

    public static (Palette? Palette, string? Error) TryCreate(string[] colors)
    {
        if (colors.Length == 0)
            return (null, "Palette must contain at least one color");

        if (colors.Length > MaxColors)
            return (null, $"Palette exceeds maximum of {MaxColors} colors, got {colors.Length}");

        for (int i = 0; i < colors.Length; i++)
        {
            if (!IsValidHex(colors[i]))
                return (null, $"Invalid hex color at index {i}: '{colors[i]}'");
        }

        return (new Palette(colors), null);
    }

    public string Resolve(int index)
    {
        if (index < 0 || index >= Colors.Length)
            return "#000000";

        return Colors[index];
    }

    private static bool IsValidHex(string value) =>
        value is { Length: 7 } && value[0] == '#' && value[1..].All(Uri.IsHexDigit);
}