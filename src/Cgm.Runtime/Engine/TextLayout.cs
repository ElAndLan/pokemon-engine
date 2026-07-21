namespace Cgm.Runtime.Engine;

/// <summary>Fixed-width bitmap font metrics and wrapping (ENGINE_RUNTIME_SPEC 16C). Measurement is
/// in virtual pixels and never consults the framebuffer, so layout is identical at every scale.
/// </summary>
// ponytail: fixed advance covers the whole Phase 16 kit; per-glyph widths only if a font needs them.
public sealed class BitmapFont(int glyphWidth = 5, int glyphHeight = 7, int spacing = 1, int lineSpacing = 2)
{
    public const char Replacement = '?';

    public int GlyphWidth { get; } = Positive(glyphWidth);
    public int GlyphHeight { get; } = Positive(glyphHeight);

    /// <summary>Pixels between glyphs on a line.</summary>
    public int Spacing { get; } = NonNegative(spacing);

    /// <summary>Pixels between baselines beyond the glyph height.</summary>
    public int LineSpacing { get; } = NonNegative(lineSpacing);

    public int Advance => GlyphWidth + Spacing;

    public int LineHeight => GlyphHeight + LineSpacing;

    /// <summary>Printable ASCII only; anything else draws the replacement glyph.</summary>
    public bool Supports(char value) => value is >= ' ' and <= '~';

    public char Resolve(char value) => Supports(value) ? value : Replacement;

    /// <summary>Width of one line. The trailing spacing after the last glyph is not counted, so a
    /// single character measures its glyph width rather than one advance.</summary>
    public int Measure(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return line.Length == 0 ? 0 : line.Length * Advance - Spacing;
    }

    /// <summary>Block size of already-wrapped lines.</summary>
    public (int Width, int Height) MeasureBlock(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
            return (0, 0);
        return (lines.Max(Measure), lines.Count * LineHeight - LineSpacing);
    }

    /// <summary>Wraps to <paramref name="maxWidth"/> virtual pixels. Newlines are explicit breaks,
    /// wrapping prefers the last whitespace, and a token wider than the line is hard-broken.</summary>
    public IReadOnlyList<string> Wrap(string text, int maxWidth)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (maxWidth < GlyphWidth)
            throw new ArgumentOutOfRangeException(nameof(maxWidth), maxWidth,
                "Wrap width must fit at least one glyph.");

        var lines = new List<string>();
        foreach (string paragraph in text.Replace("\r\n", "\n").Split('\n'))
            WrapParagraph(paragraph, maxWidth, lines);
        return lines;
    }

    /// <summary>Characters that fit in <paramref name="maxWidth"/>, used for hard-breaking.</summary>
    public int FitCount(int maxWidth) => Math.Max(1, (maxWidth + Spacing) / Advance);

    private void WrapParagraph(string paragraph, int maxWidth, List<string> lines)
    {
        if (paragraph.Length == 0)
        {
            lines.Add(string.Empty); // a blank line is deliberate vertical space
            return;
        }

        int perLine = FitCount(maxWidth);
        var line = new System.Text.StringBuilder();
        foreach (string word in SplitKeepingSpaces(paragraph))
        {
            if (word == " ")
            {
                if (line.Length > 0 && line.Length + 1 <= perLine)
                    line.Append(' ');
                continue;
            }

            if (line.Length > 0 && line.Length + word.Length > perLine)
            {
                lines.Add(line.ToString().TrimEnd());
                line.Clear();
            }

            string rest = word;
            while (rest.Length > perLine)
            {
                // Hard-break a token that cannot fit on any line, rather than overflowing.
                int room = perLine - line.Length;
                if (room <= 0)
                {
                    lines.Add(line.ToString().TrimEnd());
                    line.Clear();
                    room = perLine;
                }
                line.Append(rest[..room]);
                lines.Add(line.ToString());
                line.Clear();
                rest = rest[room..];
            }
            line.Append(rest);
        }

        if (line.Length > 0)
            lines.Add(line.ToString().TrimEnd());
    }

    private static IEnumerable<string> SplitKeepingSpaces(string text)
    {
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == ' ')
            {
                yield return " ";
                i++;
                continue;
            }
            int start = i;
            while (i < text.Length && text[i] != ' ')
                i++;
            yield return text[start..i];
        }
    }

    private static int Positive(int value) => value > 0
        ? value
        : throw new ArgumentOutOfRangeException(nameof(value), value, "Font metric must be positive.");

    private static int NonNegative(int value) => value >= 0
        ? value
        : throw new ArgumentOutOfRangeException(nameof(value), value, "Font spacing cannot be negative.");
}
