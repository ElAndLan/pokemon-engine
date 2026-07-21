namespace Cgm.Runtime.Engine;

/// <summary>Reveals wrapped text one glyph at a time on fixed ticks (ENGINE_RUNTIME_SPEC 16C).
/// Progression is tick-driven, never frame-driven, so the same input script reveals identically at
/// any frame rate. <see cref="Confirm"/> is edge-driven: the caller passes a press edge, so holding
/// Confirm cannot repeatedly dismiss pages.</summary>
public sealed class Typewriter
{
    private readonly IReadOnlyList<IReadOnlyList<string>> _pages;
    private int _page;
    private int _revealed;
    private int _tickInGlyph;

    public Typewriter(IReadOnlyList<IReadOnlyList<string>> pages, int ticksPerGlyph = 2)
    {
        ArgumentNullException.ThrowIfNull(pages);
        if (ticksPerGlyph <= 0)
            throw new ArgumentOutOfRangeException(nameof(ticksPerGlyph), ticksPerGlyph, "Cadence must be positive.");
        _pages = pages;
        TicksPerGlyph = ticksPerGlyph;
    }

    public int TicksPerGlyph { get; }

    public int PageIndex => _page;

    public int PageCount => _pages.Count;

    /// <summary>Every glyph on the current page is visible.</summary>
    public bool PageComplete => _pages.Count == 0 || _revealed >= GlyphCount(CurrentPage);

    /// <summary>The last page is complete and has been confirmed.</summary>
    public bool Finished { get; private set; }

    private IReadOnlyList<string> CurrentPage =>
        _pages.Count == 0 ? [] : _pages[Math.Min(_page, _pages.Count - 1)];

    /// <summary>Advances one fixed tick, revealing a glyph when the cadence elapses. Newlines are
    /// structure, not content: they consume no beat and appear with the glyph that follows.</summary>
    public void Tick()
    {
        if (Finished || PageComplete)
            return;
        if (++_tickInGlyph < TicksPerGlyph)
            return;
        _tickInGlyph = 0;
        _revealed++;
    }

    /// <summary>Completes the current page, or advances to the next once complete. Call only on a
    /// press edge.</summary>
    public void Confirm()
    {
        if (Finished)
            return;
        if (!PageComplete)
        {
            _revealed = GlyphCount(CurrentPage);
            _tickInGlyph = 0;
            return;
        }
        if (_page + 1 < _pages.Count)
        {
            _page++;
            _revealed = 0;
            _tickInGlyph = 0;
            return;
        }
        Finished = true;
    }

    /// <summary>The visible portion of the current page, line by line. Lines beyond the reveal point
    /// are omitted rather than blank, so the block grows downward as text arrives.</summary>
    public IReadOnlyList<string> VisibleLines()
    {
        if (_revealed == 0)
            return []; // nothing revealed yet: no text block, rather than one empty line
        var visible = new List<string>();
        int budget = _revealed;
        foreach (string line in CurrentPage)
        {
            if (budget <= 0 && visible.Count > 0)
                break;
            if (line.Length <= budget)
            {
                visible.Add(line);
                budget -= line.Length;
                continue;
            }
            visible.Add(line[..Math.Max(0, budget)]);
            budget = 0;
            break;
        }
        return visible;
    }

    private static int GlyphCount(IReadOnlyList<string> page) => page.Sum(line => line.Length);
}
