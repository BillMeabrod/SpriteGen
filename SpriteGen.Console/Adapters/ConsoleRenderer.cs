using System;
using System.Text;
using SpriteGen.Domain.Models;
using SpriteGen.Domain.Ports;

namespace SpriteGen.Console.Adapters;

public class ConsoleRenderer : IRendererPort<StringBuilder>
{
    private const string Block = "██";
    private const string Reset = "\x1b[0m";

    public StringBuilder Render(Sprite sprite)
    {
        var sb = new StringBuilder();
        var grid = sprite.Grid;

        for (int row = 0; row < grid.Height; row++)
        {
            for (int col = 0; col < grid.Width; col++)
            {
                var (r, g, b) = HexToRgb(grid.GetColor(row, col));
                sb.Append(AnsiRgbForeground(r, g, b));
                sb.Append(Block);
            }

            sb.Append(Reset);
            sb.AppendLine();
        }

        return sb;
    }

    private static string AnsiRgbForeground(int r, int g, int b) => $"\x1b[38;2;{r};{g};{b}m";

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