using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SpriteGen.Application.Services;
using SpriteGen.Console.Adapters;
using SpriteGen.Domain.Models;
using SpriteGen.Domain.Ports;
using SpriteGen.Infrastructure.Llm;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var dimensions = ResolveDimensions();
var generatorName = config["Generator"] ?? "Claude";
var llm = CreateLlmClient(generatorName);

var spriteService = new SpriteGenerationService(llm, dimensions);
var animationService = new AnimationGenerationService(llm);
var renderer = new ConsoleRenderer();
var player = new ConsoleAnimationPlayer();

Sprite? current = null;

Console.WriteLine("=== SpriteGen ===");
Console.WriteLine($"Model: {generatorName}");
Console.WriteLine($"Size: {dimensions.Width}x{dimensions.Height}\n");

while (true)
{
    current = await ShowMainMenuAsync();
}

SpriteDimensions ResolveDimensions()
{
    var width = int.TryParse(config["SpriteWidth"], out var w) ? w : 32;
    var height = int.TryParse(config["SpriteHeight"], out var h) ? h : 32;

    var (dims, error) = SpriteDimensions.TryCreate(width, height);
    if (dims is null)
        throw new InvalidOperationException($"Invalid sprite dimensions: {error}");

    return dims.Value;
}

ILlmClient CreateLlmClient(string name) => name.ToLower() switch
{
    "claude" => new ClaudeLlmClient(
        ResolveKey("Claude:ApiKey", "ANTHROPIC_API_KEY", "Claude"),
        model: config["Claude:Model"] ?? "claude-fable-5"),
    "gemini" => new GeminiLlmClient(
        ResolveKey("Gemini:ApiKey", "GEMINI_API_KEY", "Gemini"),
        model: config["Gemini:Model"] ?? "gemini-3.5-flash"),
    _ => throw new InvalidOperationException($"Unknown generator '{name}'. Use 'Claude' or 'Gemini'.")
};

string ResolveKey(string configKey, string envVar, string label) =>
    config[configKey]
    ?? Environment.GetEnvironmentVariable(envVar)
    ?? throw new InvalidOperationException($"{label} API key not found in appsettings.json ('{configKey}') or environment variable '{envVar}'.");

async Task<Sprite?> ShowMainMenuAsync()
{
    Console.WriteLine("1. Prompt");
    Console.WriteLine("2. Load");
    if (current is not null)
        Console.WriteLine("3. Animate");
    Console.WriteLine("0. Quit");
    Console.Write("\nSelect: ");

    var choice = Console.ReadLine()?.Trim();

    return choice switch
    {
        "1" => await RunPromptLoopAsync(current),
        "2" => await RunLoadAsync(),
        "3" when current is not null => await RunAnimateAsync(current),
        "0" => Quit(),
        _ => Invalid(current)
    };
}

async Task<Sprite?> RunPromptLoopAsync(Sprite? previous)
{
    Console.WriteLine();

    if (previous is not null)
        Console.WriteLine("Continuing from loaded/previous sprite. Type 'new' to reset.\n");

    while (true)
    {
        var label = previous is null ? "Describe your sprite" : "Refine (or 'new' to reset, 'menu' to return)";
        Console.Write($"{label}: ");
        var input = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(input))
            continue;

        if (input.Equals("menu", StringComparison.OrdinalIgnoreCase))
            return previous;

        if (input.Equals("new", StringComparison.OrdinalIgnoreCase))
        {
            previous = null;
            Console.WriteLine("Starting fresh.\n");
            continue;
        }

        var (sprite, error) = previous is null
            ? await WithLoadingAsync(spriteService.GenerateAsync(input))
            : await WithLoadingAsync(spriteService.GenerateAsync(input, previous));

        if (sprite is null)
        {
            Console.WriteLine($"[Error] {error}\n");
            continue;
        }

        Console.WriteLine();
        Console.WriteLine(renderer.Render(sprite));
        previous = sprite;
    }
}

async Task<Sprite?> RunAnimateAsync(Sprite baseSprite)
{
    Console.Write("\nDescribe the animation (e.g. 'walk cycle facing down'): ");
    var animPrompt = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(animPrompt))
    {
        Console.WriteLine("[Error] No animation description entered.\n");
        return baseSprite;
    }

    Console.Write("Frame count [4]: ");
    var frameInput = Console.ReadLine()?.Trim();
    var frameCount = int.TryParse(frameInput, out var fc) && fc > 0 ? fc : 4;

    var (animation, error) = await WithLoadingAsync(
        animationService.GenerateAsync(baseSprite, animPrompt, frameCount));

    if (animation is null)
    {
        Console.WriteLine($"[Error] {error}\n");
        return baseSprite;
    }

    Console.WriteLine();
    player.Play(animation, fps: 8);

    return baseSprite;
}

async Task<Sprite?> RunLoadAsync()
{
    Console.Write("\nFile path: ");
    var path = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(path))
    {
        Console.WriteLine("[Error] No path entered.\n");
        return current;
    }

    if (!File.Exists(path))
    {
        Console.WriteLine($"[Error] File not found: {path}\n");
        return current;
    }

    try
    {
        var json = await File.ReadAllTextAsync(path);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("width", out var widthEl) || !root.TryGetProperty("height", out var heightEl))
        {
            Console.WriteLine("[Error] File must include 'width' and 'height'.\n");
            return current;
        }

        var (dims, dimError) = SpriteDimensions.TryCreate(widthEl.GetInt32(), heightEl.GetInt32());
        if (dims is null)
        {
            Console.WriteLine($"[Error] {dimError}\n");
            return current;
        }

        var palette = JsonSerializer.Deserialize<string[]>(root.GetProperty("palette").GetRawText());
        var pixels = JsonSerializer.Deserialize<int[]>(root.GetProperty("pixels").GetRawText());

        if (palette is null || pixels is null)
        {
            Console.WriteLine("[Error] Invalid file format.\n");
            return current;
        }

        var (grid, gridError) = SpriteGrid.TryCreate(dims.Value, palette, pixels);
        if (grid is null)
        {
            Console.WriteLine($"[Error] {gridError}\n");
            return current;
        }

        var sprite = new Sprite(path, grid);
        Console.WriteLine();
        Console.WriteLine(renderer.Render(sprite));

        return sprite;
    }
    catch (JsonException ex)
    {
        Console.WriteLine($"[Error] JSON parse failed: {ex.Message}\n");
        return current;
    }
}

Sprite? Quit()
{
    Console.WriteLine("Goodbye.");
    Environment.Exit(0);
    return null;
}

Sprite? Invalid(Sprite? previous)
{
    Console.WriteLine("Invalid option.\n");
    return previous;
}

static async Task<T> WithLoadingAsync<T>(Task<T> task)
{
    var cts = new CancellationTokenSource();

    var animation = Task.Run(async () =>
    {
        var states = new[] { ".  ", ".. ", "..." };
        int i = 0;
        while (!cts.Token.IsCancellationRequested)
        {
            Console.Write($"\r{states[i % states.Length]}");
            i++;
            await Task.Delay(400, cts.Token).ContinueWith(_ => { });
        }
    });

    var result = await task;

    cts.Cancel();
    await animation;
    Console.Write("\r   \r");

    return result;
}