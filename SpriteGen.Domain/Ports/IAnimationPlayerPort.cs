using SpriteGen.Domain.Models;

namespace SpriteGen.Domain.Ports;

public interface IAnimationPlayerPort
{
    void Play(Animation animation, int fps = 8);
}