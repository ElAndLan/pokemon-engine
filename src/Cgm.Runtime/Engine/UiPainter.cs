namespace Cgm.Runtime.Engine;

/// <summary>Builds the built-in 5x7 glyph atlas as RGBA bytes (ENGINE_RUNTIME_SPEC 16C bitmap text).
/// Procedural and content-neutral: it ships no asset and names no game content, so the UI kit works
/// before asset loading exists. An authored font replaces it by supplying the same atlas shape.</summary>
public static class FontAtlas
{
    public const int Columns = 16;
    public const int GlyphWidth = 5;
    public const int GlyphHeight = 7;

    /// <summary>Each glyph is seven rows of a five-bit mask, most significant bit leftmost.</summary>
    private static readonly IReadOnlyDictionary<char, int[]> Glyphs = new Dictionary<char, int[]>
    {
        [' '] = [0, 0, 0, 0, 0, 0, 0],
        ['?'] = [14, 17, 1, 2, 4, 0, 4],
        ['>'] = [16, 8, 4, 2, 4, 8, 16],
        [':'] = [0, 4, 4, 0, 4, 4, 0],
        ['/'] = [1, 1, 2, 4, 8, 16, 16],
        ['-'] = [0, 0, 0, 31, 0, 0, 0],
        ['+'] = [0, 4, 4, 31, 4, 4, 0],
        ['.'] = [0, 0, 0, 0, 0, 12, 12],
        ['0'] = [14, 17, 19, 21, 25, 17, 14],
        ['1'] = [4, 12, 4, 4, 4, 4, 14],
        ['2'] = [14, 17, 1, 2, 4, 8, 31],
        ['3'] = [30, 1, 1, 14, 1, 1, 30],
        ['4'] = [2, 6, 10, 18, 31, 2, 2],
        ['5'] = [31, 16, 16, 30, 1, 1, 30],
        ['6'] = [14, 16, 16, 30, 17, 17, 14],
        ['7'] = [31, 1, 2, 4, 8, 8, 8],
        ['8'] = [14, 17, 17, 14, 17, 17, 14],
        ['9'] = [14, 17, 17, 15, 1, 1, 14],
        ['A'] = [14, 17, 17, 31, 17, 17, 17],
        ['B'] = [30, 17, 17, 30, 17, 17, 30],
        ['C'] = [14, 17, 16, 16, 16, 17, 14],
        ['D'] = [30, 17, 17, 17, 17, 17, 30],
        ['E'] = [31, 16, 16, 30, 16, 16, 31],
        ['F'] = [31, 16, 16, 30, 16, 16, 16],
        ['G'] = [14, 17, 16, 23, 17, 17, 15],
        ['H'] = [17, 17, 17, 31, 17, 17, 17],
        ['I'] = [14, 4, 4, 4, 4, 4, 14],
        ['J'] = [7, 2, 2, 2, 18, 18, 12],
        ['K'] = [17, 18, 20, 24, 20, 18, 17],
        ['L'] = [16, 16, 16, 16, 16, 16, 31],
        ['M'] = [17, 27, 21, 21, 17, 17, 17],
        ['N'] = [17, 25, 21, 19, 17, 17, 17],
        ['O'] = [14, 17, 17, 17, 17, 17, 14],
        ['P'] = [30, 17, 17, 30, 16, 16, 16],
        ['Q'] = [14, 17, 17, 17, 21, 18, 13],
        ['R'] = [30, 17, 17, 30, 20, 18, 17],
        ['S'] = [15, 16, 16, 14, 1, 1, 30],
        ['T'] = [31, 4, 4, 4, 4, 4, 4],
        ['U'] = [17, 17, 17, 17, 17, 17, 14],
        ['V'] = [17, 17, 17, 17, 17, 10, 4],
        ['W'] = [17, 17, 17, 21, 21, 21, 10],
        ['X'] = [17, 17, 10, 4, 10, 17, 17],
        ['Y'] = [17, 17, 10, 4, 4, 4, 4],
        ['Z'] = [31, 1, 2, 4, 8, 16, 31],
    };

    /// <summary>Glyphs in atlas order. Index 0 is a solid cell used for panels and bars, so the whole
    /// UI draws from one texture and never breaks the batch to switch.</summary>
    public static IReadOnlyList<char> Order { get; } = [.. Glyphs.Keys];

    public static int Rows => (Order.Count + Columns) / Columns + 1;

    public static int Width => Columns * GlyphWidth;

    public static int Height => Rows * GlyphHeight;

    /// <summary>Source rectangle for a character, or the replacement glyph when unsupported.</summary>
    public static RectI Source(char value)
    {
        int index = IndexOf(char.ToUpperInvariant(value));
        if (index < 0)
            index = IndexOf(BitmapFont.Replacement);
        // Row 0 holds the solid cell; glyphs start on row 1.
        int cell = index + Columns;
        return new RectI(cell % Columns * GlyphWidth, cell / Columns * GlyphHeight, GlyphWidth, GlyphHeight);
    }

    /// <summary>The solid cell, tinted for panels, bars, cursors, and fades.</summary>
    public static RectI Solid => new(0, 0, 1, 1);

    public static bool Supports(char value) => Glyphs.ContainsKey(char.ToUpperInvariant(value));

    /// <summary>Premultiplied white-on-transparent RGBA, matching the renderer's blend mode.</summary>
    public static byte[] Rgba()
    {
        var pixels = new byte[Width * Height * 4];
        for (int x = 0; x < GlyphWidth; x++)
            for (int y = 0; y < GlyphHeight; y++)
                Set(pixels, x, y);   // row 0: the solid cell

        for (int i = 0; i < Order.Count; i++)
        {
            int[] rows = Glyphs[Order[i]];
            int cell = i + Columns;
            int originX = cell % Columns * GlyphWidth;
            int originY = cell / Columns * GlyphHeight;
            for (int row = 0; row < rows.Length; row++)
                for (int col = 0; col < GlyphWidth; col++)
                    if ((rows[row] & (1 << (GlyphWidth - 1 - col))) != 0)
                        Set(pixels, originX + col, originY + row);
        }
        return pixels;
    }

    private static int IndexOf(char value)
    {
        for (int i = 0; i < Order.Count; i++)
            if (Order[i] == value)
                return i;
        return -1;
    }

    private static void Set(byte[] pixels, int x, int y)
    {
        int at = (y * Width + x) * 4;
        pixels[at] = pixels[at + 1] = pixels[at + 2] = pixels[at + 3] = 255;
    }
}

/// <summary>Draws the 16C primitives through one <see cref="QuadBatch"/> and one atlas: panel,
/// text, cursor, and fade. Every primitive shares the atlas, so a whole screen batches into one
/// draw call unless a scissor or layer breaks it.</summary>
public sealed class UiPainter(QuadBatch batch, TextureHandle atlas, BitmapFont font)
{
    public BitmapFont Font => font;

    /// <summary>A filled panel. 9-slice borders arrive with an authored frame; a tinted solid keeps
    /// the layout honest until then.</summary>
    // ponytail: solid fill until a 9-slice frame asset exists; the call site does not change.
    public void Panel(RectI bounds, Rgba fill, int layer = 0) =>
        batch.Ui(atlas, FontAtlas.Solid, bounds, layer, Flip.None, fill);

    public void Fill(RectI bounds, Rgba colour, int layer = 0) => Panel(bounds, colour, layer);

    /// <summary>Draws one line at a virtual-pixel origin. Unsupported characters draw the font's
    /// replacement glyph rather than a gap, so a missing glyph is visible instead of silent.</summary>
    public void Text(string text, int x, int y, Rgba colour, int layer = 1)
    {
        ArgumentNullException.ThrowIfNull(text);
        int cursor = x;
        foreach (char raw in text)
        {
            char value = char.ToUpperInvariant(raw);
            if (value != ' ')
                batch.Ui(atlas, FontAtlas.Source(value),
                    new RectI(cursor, y, FontAtlas.GlyphWidth, FontAtlas.GlyphHeight), layer, Flip.None, colour);
            cursor += font.Advance;
        }
    }

    /// <summary>Draws pre-wrapped lines top-down from an origin.</summary>
    public void TextBlock(IReadOnlyList<string> lines, int x, int y, Rgba colour, int layer = 1)
    {
        ArgumentNullException.ThrowIfNull(lines);
        for (int i = 0; i < lines.Count; i++)
            Text(lines[i], x, y + i * font.LineHeight, colour, layer);
    }

    /// <summary>The selection cursor, drawn to the left of an entry.</summary>
    public void Cursor(int x, int y, Rgba colour, int layer = 1) => Text(">", x, y, colour, layer);

    /// <summary>Full-screen fade. Alpha is premultiplied to match the renderer's blend mode.</summary>
    public void Fade(int virtualWidth, int virtualHeight, double alpha, int layer = 100)
    {
        byte a = (byte)Math.Clamp((int)Math.Round(alpha * 255), 0, 255);
        if (a == 0)
            return;
        Panel(new RectI(0, 0, virtualWidth, virtualHeight), new Rgba(0, 0, 0, a), layer);
    }
}
