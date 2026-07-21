using System.Security.Cryptography;
using System.Text;
using Cgm.Core.Model;
using Cgm.Core.Serialization;

namespace Cgm.Runtime.Engine;

/// <summary>One scripted tick: the actions held during it. A script is the complete input record for
/// a replay, so two runs of the same script must produce identical results.</summary>
public sealed record ReplayStep(IReadOnlyList<GameAction> Held)
{
    public static ReplayStep Idle { get; } = new([]);

    public static ReplayStep Hold(params GameAction[] actions) => new(actions);
}

/// <summary>A scripted input sequence plus the seed the world runs under. Everything a replay needs
/// is here: no wall-clock, no ambient state.</summary>
public sealed record ReplayScript(IReadOnlyList<ReplayStep> Steps, int Seed = 1)
{
    /// <summary>Repeats one input for a run of ticks, which is how walking is expressed.</summary>
    public static ReplayScript Of(int seed, params (int Ticks, ReplayStep Step)[] runs)
    {
        var steps = new List<ReplayStep>();
        foreach ((int ticks, ReplayStep step) in runs)
            for (int i = 0; i < ticks; i++)
                steps.Add(step);
        return new ReplayScript(steps, seed);
    }
}

/// <summary>The observable result of a replay: the ordered lines describing what happened, the final
/// world state, and the save bytes. Equality of two traces is the parity and determinism check.</summary>
public sealed record ReplayTrace(IReadOnlyList<string> Lines, string SaveJson)
{
    /// <summary>A stable digest of the whole trace, for comparing runs in one assertion.</summary>
    public string Digest
    {
        get
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(
                string.Join('\n', Lines) + '\n' + SaveJson));
            return Convert.ToHexString(hash);
        }
    }
}

/// <summary>Runs a scripted input sequence against a content database headlessly and records what
/// happened (ENGINE_RUNTIME_SPEC 16G). Two runs of one script must produce identical traces, and the
/// same content loaded from a raw folder or a pack must too — that is the parity gate.
///
/// It drives the real scene stack and session, so it exercises the shipping code path rather than a
/// parallel one. Rendering is excluded on purpose: presentation must not affect simulation, and a
/// trace that changed with rendering would prove the opposite.</summary>
public static class ReplayHarness
{
    public static ReplayTrace Run(GameDb db, UiPainter ui, ReplayScript script,
        int tileSize = 16, int virtualWidth = 256, int virtualHeight = 192)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(script);

        var session = new WorldSession(db, ui, tileSize, virtualWidth, virtualHeight, new Rng(script.Seed));
        session.InitialiseNewGame();

        var lines = new List<string> { $"seed {script.Seed}" };
        Record(lines, session, "start");

        if (db.Settings.StartMap is not { } startMap || session.Enter(startMap, db.Settings.StartPos,
                db.Settings.StartFacing) is not { } overworld)
        {
            lines.Add("no start map");
            return new ReplayTrace(lines, CgmJson.Serialize(session.ToSave("")));
        }

        using var scenes = new SceneStack();
        scenes.Push(overworld);

        foreach (ReplayStep step in script.Steps)
        {
            scenes.Tick(Input(step));
            if (scenes.Active is not OverworldScene active)
                continue;

            session.Track(active);
            if (active.TakePending() is { } pending)
                lines.Add(Describe(pending));
        }

        Record(lines, session, "end");
        return new ReplayTrace(lines, CgmJson.Serialize(session.ToSave("")));
    }

    /// <summary>Every held action is also reported as a press edge, because a script records intent
    /// per tick rather than device transitions.</summary>
    private static TickInput Input(ReplayStep step)
    {
        var held = step.Held.ToHashSet();
        return new TickInput(held, held, new HashSet<GameAction>());
    }

    /// <summary>Outcomes are described by kind and identity only. Nothing derived from a memory
    /// address or enumeration order enters the trace, or parity would fail spuriously.</summary>
    private static string Describe(StepOutcome outcome) => outcome switch
    {
        StepOutcome.Warp warp => $"warp {warp.Entity.Key} -> {warp.Entity.Target}",
        StepOutcome.Trigger trigger => $"trigger {trigger.Entity.Key}",
        StepOutcome.TrainerSpotted spotted => $"trainer {spotted.Trainer.Id} via {spotted.Npc.Key}",
        StepOutcome.WildEncounter wild => $"wild {wild.Species} lv{wild.Level}",
        StepOutcome.Interact interact => $"interact {interact.Entity.Key}",
        _ => "none",
    };

    private static void Record(List<string> lines, WorldSession session, string label)
    {
        lines.Add($"{label} map={session.CurrentMap} pos={session.Position.X},{session.Position.Y} "
            + $"facing={session.Facing}");

        for (int i = 0; i < session.Party.Count; i++)
        {
            CreatureInstance member = session.Party[i];
            lines.Add($"{label} party[{i}] {member.Species} lv{member.Level} hp={member.CurHp} "
                + $"exp={member.Exp} status={member.Status?.ToString() ?? "none"}");
        }

        foreach ((string flag, int value) in session.Flags.Snapshot()
            .OrderBy(pair => pair.Key, StringComparer.Ordinal))
            lines.Add($"{label} flag {flag}={value}");

        foreach ((EntityId item, int count) in session.Bag
            .OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
            lines.Add($"{label} bag {item}={count}");
    }
}
