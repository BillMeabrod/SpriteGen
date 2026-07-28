using System.Collections.Generic;

namespace SpriteGen.Domain.Models;

public class Animation
{
    public Sprite BaseSprite { get; }
    public IReadOnlyList<SpriteGrid> Frames { get; }

    public Animation(Sprite baseSprite, IReadOnlyList<SpriteGrid> frames)
    {
        BaseSprite = baseSprite;
        Frames = frames;
    }
}