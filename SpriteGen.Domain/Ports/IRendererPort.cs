using SpriteGen.Domain.Models;

namespace SpriteGen.Domain.Ports;

public interface IRendererPort<TOutput>
{
    TOutput Render(Sprite sprite);
}