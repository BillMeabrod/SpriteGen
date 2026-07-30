using SpriteGen.Domain.Models;

namespace SpriteGen.Domain.Ports;

public interface IAnimationPlayerPort
{
    void Play(IReadOnlyList<SpriteGrid> frames, int fps = 8);
}