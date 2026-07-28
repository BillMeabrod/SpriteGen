using System;
using System.Text;
using System.Threading;
using SpriteGen.Domain.Models;
using SpriteGen.Domain.Ports;

namespace SpriteGen.Console.Adapters;

public class ConsoleAnimationPlayer : IAnimationPlayerPort
{
    private const string Block = "██";
    private const string Reset = "\x1b[0m";
    private readonly ConsoleRenderer _renderer = new();

    public void Play(Animation animation, int fps = 8)
    {
        if (animation.Frames.Count == 0)
            return;

        var delayMs = Math.Max(1, 1000 / fps);
        var height = animation.Frames[0].Height;

        var rendered = new string[animation.Frames.Count];
        for (int i = 0; i < animation.Frames.Count; i++)
            rendered[i] = RenderFrame(animation.Frames[i]);

        System.Console.CursorVisible = false;
        System.Console.WriteLine("Playing animation — press any key to stop.\n");

        try
        {
            int frame = 0;
            bool first = true;

            while (!System.Console.KeyAvailable)
            {
                if (!first)
                    System.Console.Write($"\x1b[{height}A"); // move cursor up `height` lines

                System.Console.Write(rendered[frame]);
                first = false;

                frame = (frame + 1) % rendered.Length;
                Thread.Sleep(delayMs);
            }

            System.Console.ReadKey(intercept: true);
        }
        finally
        {
            System.Console.CursorVisible = true;
            System.Console.WriteLine();
        }
    }

    private string RenderFrame(SpriteGrid grid)
    {
        var sb = new StringBuilder();
        for (int row = 0; row < grid.Height; row++)
        {
            for (int col = 0; col < grid.Width; col++)
            {
                var (r, g, b) = HexToRgb(grid.GetColor(row, col));
                sb.Append($"\x1b[38;2;{r};{g};{b}m");
                sb.Append(Block);
            }
            sb.Append(Reset);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static (int R, int G, int B) HexToRgb(string hex)
    {
        try
        {
            return (
                Convert.ToInt32(hex[1..3], 16),
                Convert.ToInt32(hex[3..5], 16),
                Convert.ToInt32(hex[5..7], 16)
            );
        }
        catch
        {
            return (0, 0, 0);
        }
    }
}