using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Runtime.Engine;

/// <summary>Why a battle could not be started. A refusal is always a content or state defect, never
/// something Runtime papers over by inventing a combatant.</summary>
public enum BattleStartRefusal { None, EmptyParty, PartyFainted, UnknownSpecies, EmptyOpponent }

public sealed record BattleStart(BattleController? Battle, BattleStartRefusal Refusal)
{
    public bool Started => Battle is not null;
}

/// <summary>Builds a Core <see cref="BattleController"/> from live session state, and writes the
/// result back to the party afterwards (ENGINE_RUNTIME_SPEC 16D battle entry and return). Runtime
/// composes the participants; every rule inside the battle stays in Core.</summary>
public static class BattleLauncher
{
    /// <summary>Starts a wild battle against one generated creature.</summary>
    public static BattleStart Wild(GameDb db, WorldSession session, EntityId speciesId, int level, IRng rng)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(rng);

        if (Refuse(session) is { } refusal)
            return new BattleStart(null, refusal);
        if (db.Find<Species>(speciesId) is not { } species)
            return new BattleStart(null, BattleStartRefusal.UnknownSpecies);

        IReadOnlyList<MoveSlot> moves = LearnsetMoves(db, species, level);
        CreatureInstance wild = InstanceGen.Create(speciesId, species.BaseStats, species.GrowthRate,
            level, moves, rng, species.Abilities);

        return Build(db, session, [wild], rng, isWild: true);
    }

    /// <summary>Starts a trainer battle against the trainer's authored party.</summary>
    public static BattleStart Trainer(GameDb db, WorldSession session, Trainer trainer, IRng rng)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(trainer);
        ArgumentNullException.ThrowIfNull(rng);

        if (Refuse(session) is { } refusal)
            return new BattleStart(null, refusal);
        if (trainer.Party.Count == 0)
            return new BattleStart(null, BattleStartRefusal.EmptyOpponent);

        var opponents = new List<CreatureInstance>();
        foreach (PartyMember member in trainer.Party)
        {
            if (db.Find<Species>(member.Species) is not { } species)
                return new BattleStart(null, BattleStartRefusal.UnknownSpecies);

            IReadOnlyList<MoveSlot> moves = member.Moves is { Count: > 0 }
                ? member.Moves.Select(id => new MoveSlot(id, db.Find<Move>(id)?.Pp ?? 1)).ToList()
                : LearnsetMoves(db, species, member.Level);

            opponents.Add(InstanceGen.Create(member.Species, species.BaseStats, species.GrowthRate,
                member.Level, moves, rng, species.Abilities) with { HeldItem = member.HeldItem });
        }

        return Build(db, session, opponents, rng, isWild: false);
    }

    /// <summary>Writes a finished battle back to the session: current HP, status, and PP for every
    /// party member. Core owns the values; this only copies them into the saved instances.</summary>
    public static void ApplyResult(WorldSession session, BattleController battle)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(battle);

        IReadOnlyList<BattleCreature> fought = battle.Party(BattleSide.Player);
        for (int i = 0; i < session.Party.Count && i < fought.Count; i++)
            session.Party[i] = session.Party[i] with
            {
                CurHp = fought[i].CurrentHp,
                Status = fought[i].Status,
                Moves = fought[i].Moves.Select(move => new MoveSlot(move.Move, move.Pp)).ToList(),
            };
    }

    /// <summary>Awards experience for a won battle through Core's formula. Only participants that
    /// are still standing gain it, matching the Core rule that a fainted member earns nothing.</summary>
    public static void AwardExperience(GameDb db, WorldSession session, BattleController battle, bool trainer)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(battle);

        List<int> eligible = session.Party
            .Select((member, index) => (member, index))
            .Where(entry => entry.member.CurHp > 0)
            .Select(entry => entry.index)
            .ToList();
        if (eligible.Count == 0)
            return;

        foreach (BattleCreature defeated in battle.Party(BattleSide.Enemy).Where(c => c.CurrentHp <= 0))
        {
            if (db.Find<Species>(defeated.Species) is not { } species)
                continue;

            int yield = ExpCalc.Yield(species.BaseExp, defeated.Level, trainer, eligible.Count);
            foreach (int index in eligible)
            {
                CreatureInstance member = session.Party[index];
                long total = member.Exp + yield;
                // The level curve is the *earner's* growth rate, not the defeated creature's.
                session.Party[index] = member with
                {
                    Exp = total,
                    Level = ExpCurve.LevelForExp(MemberGrowth(db, member), total),
                };
            }
        }
    }

    /// <summary>Routes a captured creature into the party, or a box when the party is full, through
    /// Core's storage rule. Its battle-current HP and status carry over, so a creature caught at low
    /// health arrives at low health.</summary>
    public static DepositResult? DepositCaptured(GameDb db, WorldSession session,
        BattleController battle, Action<string>? report = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(battle);

        IReadOnlyList<BattleCreature> wild = battle.Party(BattleSide.Enemy);
        if (wild.Count == 0)
            return null;

        BattleCreature caught = wild[0];
        if (db.Find<Species>(caught.Species) is not { } species)
        {
            report?.Invoke($"Captured unknown species '{caught.Species}'.");
            return null;
        }

        var instance = new CreatureInstance
        {
            Species = caught.Species,
            Level = caught.Level,
            Exp = ExpCurve.TotalExp(species.GrowthRate, caught.Level),
            CurHp = caught.CurrentHp,
            Status = caught.Status,
            Moves = caught.Moves.Select(move => new MoveSlot(move.Move, move.Pp)).ToList(),
        };

        DepositResult? result = session.Deposit(instance);
        if (result is null)
            report?.Invoke("Party and storage are full; the capture could not be stored.");
        return result;
    }

    private static string MemberGrowth(GameDb db, CreatureInstance member) =>
        db.Find<Species>(member.Species)?.GrowthRate ?? "medium-fast";

    /// <summary><paramref name="isWild"/> is not cosmetic: Core refuses capture and applies wild-only
    /// rules from it, so a wild battle built without it silently behaves as a trainer battle.</summary>
    private static BattleStart Build(GameDb db, WorldSession session,
        IReadOnlyList<CreatureInstance> opponents, IRng rng, bool isWild)
    {
        IReadOnlyList<BattleCreature> players = session.Party
            .Select(member => BattleCreature.FromInstance(member, db))
            .ToList();
        IReadOnlyList<BattleCreature> enemies = opponents
            .Select(member => BattleCreature.FromInstance(member, db))
            .ToList();

        var chart = new TypeChart(db.All<TypeDef>());
        var battle = new BattleController(players, enemies, chart, rng,
            isWild: isWild,
            itemData: db.All<Item>(),
            moveData: db.All<Move>().Select(MoveCompiler.ToBattleMove),
            abilityData: db.All<Ability>());

        return new BattleStart(battle, BattleStartRefusal.None);
    }

    private static BattleStartRefusal? Refuse(WorldSession session) => session.Party.Count switch
    {
        0 => BattleStartRefusal.EmptyParty,
        _ when session.PartyIsWhitedOut => BattleStartRefusal.PartyFainted,
        _ => null,
    };

    private static IReadOnlyList<MoveSlot> LearnsetMoves(GameDb db, Species species, int level) =>
        species.Learnset
            .Where(entry => entry.Level <= level)
            .OrderBy(entry => entry.Level)
            .TakeLast(4)
            .Select(entry => new MoveSlot(entry.Move, db.Find<Move>(entry.Move)?.Pp ?? 1))
            .ToList();
}
