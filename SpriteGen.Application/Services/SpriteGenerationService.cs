using System.Text.Json;
using System.Text.Json.Serialization;
using SpriteGen.Domain.Models;
using SpriteGen.Domain.Ports;

namespace SpriteGen.Application.Services;

public class SpriteGenerationService
{
    private readonly ILlmClient _llm;
    private readonly SpriteDimensions _dimensions;
    private const int MaxRetries = 2;

    public SpriteGenerationService(ILlmClient llm, SpriteDimensions dimensions)
    {
        _llm = llm;
        _dimensions = dimensions;
    }

    private class SpriteResponse
    {
        [JsonPropertyName("palette")]
        public string[] Palette { get; set; } = [];

        [JsonPropertyName("rows")]
        public int[][] Rows { get; set; } = [];
    }

    private string BuildSystemPrompt() => $"""
        You are a pixel art generator for {_dimensions.Width}x{_dimensions.Height} sprites.
        - Index 0 in the palette is always "#000000" and represents empty/transparent pixels.
        - Center the subject in the grid.
        - Use full RGB color with subtle shading (base, highlight, shadow per region).
        - Keep the palette cohesive and naturally limited.
        - Apply left-right symmetry where appropriate.
        - Outlines should use dark, slightly colored shadows rather than pure black.
        - Output exactly {_dimensions.Height} rows. Each row is an array of exactly {_dimensions.Width} palette indices.
        - The subject's height should roughly match the size of the frame ( {_dimensions.Width} x {_dimensions.Height} pixels). It does not need to be exact.
        - The subject's lowest pixels should be touching the bottom edge of the frame.
        - Allow a 2 pixel empty space on the left or right to allow movement.
        - All pixels should be a direct part of the subject. No detached ground or objects.
        """;

    public async Task<(Sprite? Sprite, string? Error)> GenerateAsync(string prompt, Sprite? previous = null)
    {
        var userMessage = BuildUserMessage(prompt, previous);
        return await GenerateWithRetryAsync(userMessage, prompt, retryCount: 0);
    }

    private async Task<(Sprite? Sprite, string? Error)> GenerateWithRetryAsync(
        string userMessage, string originalPrompt, int retryCount)
    {
        SpriteResponse response;
        try
        {
            response = await _llm.CompleteAsync<SpriteResponse>(BuildSystemPrompt(), userMessage);
        }
        catch (Exception ex)
        {
            return (null, $"LLM call failed: {ex.Message}");
        }

        var (sprite, error) = Interpret(response, originalPrompt);

        if (sprite is null && retryCount < MaxRetries)
        {
            System.Console.WriteLine($"[Retry {retryCount + 1}/{MaxRetries}] {error}");
            var corrective = $"Your previous response had an error: {error}. Return corrected JSON only.";
            return await GenerateWithRetryAsync(corrective, originalPrompt, retryCount + 1);
        }

        return (sprite, error);
    }

    private (Sprite? Sprite, string? Error) Interpret(SpriteResponse response, string prompt)
    {
        if (response.Palette is null)
            return (null, "Missing 'palette'");
        if (response.Rows is null)
            return (null, "Missing 'rows'");

        if (response.Rows.Length != _dimensions.Height)
            return (null, $"Expected {_dimensions.Height} rows, got {response.Rows.Length}");

        var pixels = new int[_dimensions.PixelCount];
        for (int row = 0; row < response.Rows.Length; row++)
        {
            if (response.Rows[row].Length != _dimensions.Width)
                return (null, $"Row {row} has {response.Rows[row].Length} values, expected {_dimensions.Width}");

            Array.Copy(response.Rows[row], 0, pixels, row * _dimensions.Width, _dimensions.Width);
        }

        var (grid, gridError) = SpriteGrid.TryCreate(_dimensions, response.Palette, pixels);
        if (grid is null)
            return (null, gridError);

        return (new Sprite(prompt, grid), null);
    }

    private string BuildUserMessage(string prompt, Sprite? previous)
    {
        if (previous is null)
            return prompt;

        var paletteJson = JsonSerializer.Serialize(previous.Grid.Palette.Colors);
        var rows = ToRows(previous.Grid.Indices, previous.Grid.Width, previous.Grid.Height);
        var rowsJson = JsonSerializer.Serialize(rows);
        return $"Current sprite:\n{{\"palette\":{paletteJson},\"rows\":{rowsJson}}}\n\nRefinement: {prompt}";
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