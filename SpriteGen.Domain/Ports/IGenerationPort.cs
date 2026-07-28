using SpriteGen.Domain.Models;

namespace SpriteGen.Domain.Ports;

public interface IGenerationPort
{
    Task<(Sprite? Sprite, string? Error)> GenerateAsync(string prompt, Sprite? previous = null);
}
