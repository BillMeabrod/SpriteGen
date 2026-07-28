using System.Threading.Tasks;
using SpriteGen.Domain.Models;
using SpriteGen.Domain.Ports;

namespace SpriteGen.Application.Services;

public class SpriteGenerationService<TOutput>
{
    private readonly IGenerationPort _generator;
    private readonly IRendererPort<TOutput> _renderer;

    public SpriteGenerationService(IGenerationPort generator, IRendererPort<TOutput> renderer)
    {
        _generator = generator;
        _renderer = renderer;
    }

    public async Task<(Sprite? Sprite, TOutput? Output, string? Error)> GenerateAsync(string prompt)
    {
        var (sprite, error) = await _generator.GenerateAsync(prompt);
        if (sprite is null)
            return (null, default, error);

        var output = _renderer.Render(sprite);
        return (sprite, output, null);
    }

    public async Task<(Sprite? Sprite, TOutput? Output, string? Error)> RefineAsync(string prompt, Sprite current)
    {
        var (sprite, error) = await _generator.GenerateAsync(prompt, current);
        if (sprite is null)
            return (null, default, error);

        var output = _renderer.Render(sprite);
        return (sprite, output, null);
    }
}