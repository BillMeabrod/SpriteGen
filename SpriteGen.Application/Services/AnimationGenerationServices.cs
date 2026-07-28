using System.Text.Json;
using System.Text.Json.Serialization;
using SpriteGen.Domain.Models;
using SpriteGen.Domain.Ports;

namespace SpriteGen.Application.Services;

public class AnimationGenerationService
{
    private readonly ILlmClient _llm;
    private const int MaxRetries = 2;

    public AnimationGenerationService(ILlmClient llm)
    {
        _llm = llm;
    }

    private class AnimationResponse
    {
        [JsonPropertyName("frames")]
        public int[][][] Frames { get; set; } = [];
    }

    private static string BuildSystemPrompt(SpriteDimensions dim, int frameCount, string[] lockedPalette) => $"""
        You are a pixel art animation generator for {dim.Width}x{dim.Height} sprite frames.
        You are given a base sprite and its palette. Produce an animation as a sequence of frames.
        - Use ONLY the provided palette indices. Do not introduce new colors.
        - Output exactly {frameCount} frames.
        - Each frame is exactly {dim.Height} rows; each row is exactly {dim.Width} palette indices.
        - Keep the character identical across frames — same proportions, same palette, same silhouette.
        - Only the parts described by the motion should change between frames.
        - The locked palette is: {JsonSerializer.Serialize(lockedPalette)}
        """;

    public async Task<(Animation? Animation, string? Error)> GenerateAsync(
        Sprite baseSprite, string animationPrompt, int frameCount)
    {
        var dim = baseSprite.Grid.Dimensions;
        var systemPrompt = BuildSystemPrompt(dim, frameCount, baseSprite.Grid.Palette.Colors);
        var userMessage = BuildUserMessage(baseSprite, animationPrompt, frameCount);
        return await GenerateWithRetryAsync(baseSprite, systemPrompt, userMessage, frameCount, 0);
    }

    private async Task<(Animation? Animation, string? Error)> GenerateWithRetryAsync(
        Sprite baseSprite, string systemPrompt, string userMessage, int frameCount, int retryCount)
    {
        AnimationResponse response;
        try
        {
            response = await _llm.CompleteAsync<AnimationResponse>(systemPrompt, userMessage);
        }
        catch (Exception ex)
        {
            return (null, $"LLM call failed: {ex.Message}");
        }

        var (animation, error) = Interpret(baseSprite, response, frameCount);

        if (animation is null && retryCount < MaxRetries)
        {
            System.Console.WriteLine($"[Retry {retryCount + 1}/{MaxRetries}] {error}");
            var corrective = $"{userMessage}\n\nYour previous response had an error: {error}. Return corrected JSON only.";
            return await GenerateWithRetryAsync(baseSprite, systemPrompt, corrective, frameCount, retryCount + 1);
        }

        return (animation, error);
    }

    private static (Animation? Animation, string? Error) Interpret(
        Sprite baseSprite, AnimationResponse response, int frameCount)
    {
        if (response.Frames is null)
            return (null, "Missing 'frames'");
        if (response.Frames.Length != frameCount)
            return (null, $"Expected {frameCount} frames, got {response.Frames.Length}");

        var dim = baseSprite.Grid.Dimensions;
        var palette = baseSprite.Grid.Palette.Colors;
        var frames = new List<SpriteGrid>(frameCount);

        for (int f = 0; f < response.Frames.Length; f++)
        {
            var frameRows = response.Frames[f];
            if (frameRows.Length != dim.Height)
                return (null, $"Frame {f} has {frameRows.Length} rows, expected {dim.Height}");

            var pixels = new int[dim.PixelCount];
            for (int row = 0; row < frameRows.Length; row++)
            {
                if (frameRows[row].Length != dim.Width)
                    return (null, $"Frame {f} row {row} has {frameRows[row].Length} values, expected {dim.Width}");

                Array.Copy(frameRows[row], 0, pixels, row * dim.Width, dim.Width);
            }

            var (grid, gridError) = SpriteGrid.TryCreate(dim, palette, pixels);
            if (grid is null)
                return (null, $"Frame {f}: {gridError}");

            frames.Add(grid);
        }

        return (new Animation(baseSprite, frames), null);
    }

    private static string BuildUserMessage(Sprite baseSprite, string animationPrompt, int frameCount)
    {
        var rows = ToRows(baseSprite.Grid.Indices, baseSprite.Grid.Width, baseSprite.Grid.Height);
        var rowsJson = JsonSerializer.Serialize(rows);
        return $"""
            Base sprite (frame reference), as palette indices:
            {rowsJson}

            Animation to produce ({frameCount} frames): {animationPrompt}
            """;
    }

    private static int[][] ToRows(int[] flat, int width, int height)
    {
        var rows = new int[height][];
        for (int row = 0; row < height; row++)
        {
            rows[row] = new int[width];
            Array.Copy(flat, row * width, rows[row], 0, width);
        }
        return rows;
    }
}