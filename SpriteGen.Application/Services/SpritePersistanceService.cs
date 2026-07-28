using System.Text.Json;
using SpriteGen.Application.Persistence;
using SpriteGen.Domain.Models;

namespace SpriteGen.Application.Services;

public record LoadResult(Sprite? Sprite, Animation? Animation, string? Error)
{
    public bool IsAnimation => Animation is not null;
}

public class SpritePersistenceService
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public async Task<string?> SaveSpriteAsync(Sprite sprite, string path, int fps = 8)
    {
        var file = new SpriteFile
        {
            Width = sprite.Grid.Width,
            Height = sprite.Grid.Height,
            Palette = sprite.Grid.Palette.Colors,
            Fps = fps,
            Prompt = sprite.Prompt,
            Frames = [(int[])sprite.Grid.Indices.Clone()]
        };
        return await WriteAsync(file, path);
    }

    public async Task<string?> SaveAnimationAsync(Animation animation, string path, string prompt, int fps = 8)
    {
        var grid0 = animation.Frames.Count > 0 ? animation.Frames[0] : animation.BaseSprite.Grid;

        var frames = new int[animation.Frames.Count][];
        for (int i = 0; i < animation.Frames.Count; i++)
            frames[i] = (int[])animation.Frames[i].Indices.Clone();

        var file = new SpriteFile
        {
            Width = grid0.Width,
            Height = grid0.Height,
            Palette = grid0.Palette.Colors,
            Fps = fps,
            Prompt = prompt,
            Frames = frames
        };
        return await WriteAsync(file, path);
    }

    private static async Task<string?> WriteAsync(SpriteFile file, string path)
    {
        try
        {
            var json = JsonSerializer.Serialize(file, WriteOptions);
            await File.WriteAllTextAsync(path, json);
            return null;
        }
        catch (Exception ex)
        {
            return $"Save failed: {ex.Message}";
        }
    }

    public async Task<LoadResult> LoadAsync(string path)
    {
        if (!File.Exists(path))
            return new LoadResult(null, null, $"File not found: {path}");

        SpriteFile? file;
        try
        {
            var json = await File.ReadAllTextAsync(path);
            file = JsonSerializer.Deserialize<SpriteFile>(json);
        }
        catch (JsonException ex)
        {
            return new LoadResult(null, null, $"JSON parse failed: {ex.Message}");
        }

        if (file is null)
            return new LoadResult(null, null, "Deserialized to null");

        if (file.Frames is null || file.Frames.Length == 0)
            return new LoadResult(null, null, "File contains no frames");

        var (dims, dimError) = SpriteDimensions.TryCreate(file.Width, file.Height);
        if (dims is null)
            return new LoadResult(null, null, dimError);

        var grids = new List<SpriteGrid>(file.Frames.Length);
        for (int i = 0; i < file.Frames.Length; i++)
        {
            var (grid, gridError) = SpriteGrid.TryCreate(dims.Value, file.Palette, file.Frames[i]);
            if (grid is null)
                return new LoadResult(null, null, $"Frame {i}: {gridError}");
            grids.Add(grid);
        }

        if (grids.Count == 1)
        {
            var sprite = new Sprite(file.Prompt, grids[0]);
            return new LoadResult(sprite, null, null);
        }

        var baseSprite = new Sprite(file.Prompt, grids[0]);
        var animation = new Animation(baseSprite, grids);
        return new LoadResult(null, animation, null);
    }
}