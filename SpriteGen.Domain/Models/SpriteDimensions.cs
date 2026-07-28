namespace SpriteGen.Domain.Models;

public readonly record struct SpriteDimensions(int Width, int Height)
{
    public int PixelCount => Width * Height;

    public static (SpriteDimensions? Dimensions, string? Error) TryCreate(int width, int height)
    {
        if (width <= 0)
            return (null, $"Width must be positive, got {width}");
        if (height <= 0)
            return (null, $"Height must be positive, got {height}");
        return (new SpriteDimensions(width, height), null);
    }
}