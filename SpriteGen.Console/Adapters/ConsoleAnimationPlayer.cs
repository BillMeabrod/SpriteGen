using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using SpriteGen.Domain.Models;
using SpriteGen.Domain.Ports;

namespace SpriteGen.Console.Adapters;

public class ConsoleAnimationPlayer : IAnimationPlayerPort
{
    private const string Block = "██";
    private const string Reset = "\x1b[0m";
    private const string ClearToEndOfLine = "\x1b[0K";

    public void Play(IReadOnlyList<SpriteGrid> frames, int fps = 8)
    {
        if (frames.Count == 0)
            return;

        var delayMs = Math.Max(1, 1000 / fps);
        var height = frames[0].Height;

        var rendered = new string[frames.Count][];
        for (int i = 0; i < frames.Count; i++)
            rendered[i] = RenderFrameRows(frames[i]);

        TryEnsureHeight(height + 2);

        if (height >= System.Console.BufferHeight)
        {
            System.Console.WriteLine($"[Warning] Sprite is {height} rows but the console fits {System.Console.BufferHeight}.");
            System.Console.WriteLine("Resize the window taller, then press any key to continue.");
            System.Console.ReadKey(intercept: true);
            TryEnsureHeight(height + 2);
        }

        var visibleRows = Math.Min(height, System.Console.BufferHeight);

        System.Console.Clear();
        System.Console.CursorVisible = false;

        var showFooter = visibleRows < System.Console.BufferHeight;

        try
        {
            int frame = 0;

            if (showFooter)
            {
                System.Console.SetCursorPosition(0, visibleRows);
                System.Console.Write("Playing — press any key to stop.");
            }

            while (!System.Console.KeyAvailable)
            {
                var rows = rendered[frame];

                for (int row = 0; row < visibleRows; row++)
                {
                    System.Console.SetCursorPosition(0, row);
                    System.Console.Write(rows[row]);
                }

                frame = (frame + 1) % rendered.Length;
                Thread.Sleep(delayMs);
            }

            System.Console.ReadKey(intercept: true);
        }
        finally
        {
            System.Console.CursorVisible = true;
            System.Console.Clear();
        }
    }

    private static void TryEnsureHeight(int required)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            if (System.Console.BufferHeight < required)
                System.Console.SetBufferSize(System.Console.BufferWidth, required);

            if (System.Console.WindowHeight < required)
                System.Console.SetWindowSize(System.Console.WindowWidth, required);
        }
        catch (Exception)
        {
        }
    }

    private static string[] RenderFrameRows(SpriteGrid grid)
    {
        var rows = new string[grid.Height];

        for (int row = 0; row < grid.Height; row++)
        {
            var sb = new StringBuilder();

            for (int col = 0; col < grid.Width; col++)
            {
                var (r, g, b) = HexToRgb(grid.GetColor(row, col));
                sb.Append($"\x1b[38;2;{r};{g};{b}m");
                sb.Append(Block);
            }

            sb.Append(Reset);
            sb.Append(ClearToEndOfLine);

            rows[row] = sb.ToString();
        }

        return rows;
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