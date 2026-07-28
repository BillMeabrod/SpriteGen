namespace SpriteGen.Domain.Models;

public class Sprite
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Prompt { get; }
    public SpriteGrid Grid { get; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    public Sprite(string prompt, SpriteGrid grid)
    {
        Prompt = prompt;
        Grid = grid;
    }
}