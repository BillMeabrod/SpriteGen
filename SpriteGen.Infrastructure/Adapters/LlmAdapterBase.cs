using System.Text.Json;
using System.Text.Json.Serialization;
using SpriteGen.Domain.Models;
using SpriteGen.Domain.Ports;

namespace SpriteGen.Infrastructure.Adapters;

public abstract class LlmAdapterBase : IGenerationPort
{
    protected SpriteDimensions Dimensions { get; }

    protected LlmAdapterBase(SpriteDimensions dimensions)
    {
        Dimensions = dimensions;
    }

    protected string BuildSystemPrompt() => $"""
    You are a pixel art generator for {Dimensions.Width}x{Dimensions.Height} sprites.
    - Index 0 in the palette is always "#000000" and represents empty/transparent pixels.
    - Center the subject in the grid.
    - Use full RGB color with subtle shading (base, highlight, shadow per region).
    - Keep the palette cohesive and naturally limited.
    - Apply left-right symmetry where appropriate.
    - Outlines should use dark, slightly colored shadows rather than pure black.
    - Output exactly {Dimensions.Height} rows. Each row is an array of exactly {Dimensions.Width} palette indices.
    - The subject's widest point must span the full {Dimensions.Width} pixels, touching both the left and right edges. The subject's tallest extent must span the full {Dimensions.Height} pixels, touching both the top and bottom edges.
    - Scale the entire subject up uniformly to meet this. Do not stretch or distort individual parts to reach the edges — enlarge the whole figure so it naturally fills the frame.
    - All pixels should be a direct part of the subject. Do not render ground or other objects detached from the subject.
    """;

    protected class SpriteResponse
    {
        [JsonPropertyName("palette")]
        public string[] Palette { get; set; } = [];

        [JsonPropertyName("rows")]
        public int[][] Rows { get; set; } = [];
    }

    public async Task<(Sprite? Sprite, string? Error)> GenerateAsync(string prompt, Sprite? previous = null)
        => await GenerateWithRetryAsync(prompt, previous, retryCount: 0);

    private async Task<(Sprite? Sprite, string? Error)> GenerateWithRetryAsync(
        string prompt,
        Sprite? previous,
        int retryCount)
    {
        const int maxRetries = 2;

        var userMessage = BuildUserMessage(prompt, previous);

        string? text;
        try
        {
            text = await CallApiAsync(userMessage);
        }
        catch (Exception ex)
        {
            return (null, $"API call failed: {ex.Message}");
        }

        if (string.IsNullOrEmpty(text))
            return (null, "Empty response from model");

        var (sprite, parseError) = TryParseResponse(text, prompt);

        if (sprite is null && retryCount < maxRetries)
        {
            System.Console.WriteLine($"[Retry {retryCount + 1}/{maxRetries}] {parseError}");
            return await GenerateWithRetryAsync(
                $"Your previous response had an error: {parseError}. Return the corrected JSON only.",
                previous,
                retryCount + 1
            );
        }

        return (sprite, parseError);
    }

    protected abstract Task<string?> CallApiAsync(string userMessage);

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

    private (Sprite? Sprite, string? Error) TryParseResponse(string text, string prompt)
    {
        try
        {
            var response = JsonSerializer.Deserialize<SpriteResponse>(text);

            if (response is null)
                return (null, "Deserialized to null");
            if (response.Palette is null)
                return (null, "Missing 'palette'");
            if (response.Rows is null)
                return (null, "Missing 'rows'");

            if (response.Rows.Length != Dimensions.Height)
                return (null, $"Expected {Dimensions.Height} rows, got {response.Rows.Length}");

            var pixels = new int[Dimensions.PixelCount];
            for (int row = 0; row < response.Rows.Length; row++)
            {
                if (response.Rows[row].Length != Dimensions.Width)
                    return (null, $"Row {row} has {response.Rows[row].Length} values, expected {Dimensions.Width}");

                Array.Copy(response.Rows[row], 0, pixels, row * Dimensions.Width, Dimensions.Width);
            }

            var (grid, gridError) = SpriteGrid.TryCreate(Dimensions, response.Palette, pixels);
            if (grid is null)
                return (null, gridError);

            return (new Sprite(prompt, grid), null);
        }
        catch (JsonException ex)
        {
            return (null, $"JSON parse failed: {ex.Message}");
        }
    }
}