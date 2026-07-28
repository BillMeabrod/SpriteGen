namespace SpriteGen.Domain.Models;

public class SpriteGrid
{
    public SpriteDimensions Dimensions { get; }
    public Palette Palette { get; }
    public int[] Indices { get; }

    public int Width => Dimensions.Width;
    public int Height => Dimensions.Height;

    private SpriteGrid(SpriteDimensions dimensions, Palette palette, int[] indices)
    {
        Dimensions = dimensions;
        Palette = palette;
        Indices = indices;
    }

    public string GetColor(int row, int col) => Palette.Resolve(Indices[row * Width + col]);

    public static (SpriteGrid? Grid, string? Error) TryCreate(
        SpriteDimensions dimensions,
        string[] paletteColors,
        int[] indices)
    {
        var (palette, paletteError) = Palette.TryCreate(paletteColors);
        if (palette is null)
            return (null, paletteError);

        if (indices.Length != dimensions.PixelCount)
            return (null, $"Expected {dimensions.PixelCount} pixel indices ({dimensions.Width}x{dimensions.Height}), got {indices.Length}");

        for (int i = 0; i < indices.Length; i++)
        {
            if (indices[i] < 0 || indices[i] >= palette.Colors.Length)
                return (null, $"Index {indices[i]} at position {i} is out of palette range (0-{palette.Colors.Length - 1})");
        }

        return (new SpriteGrid(dimensions, palette, indices), null);
    }
}