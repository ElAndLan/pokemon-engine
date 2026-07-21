using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>Builds a battle from the demo-game sample so the Phase 15 integration script keeps its
/// coverage. This lives in tests on purpose: naming sample content IDs is legitimate for a test
/// asserting against that sample, and forbidden in Runtime, which must stay content-agnostic (16A).
/// Runtime's own battle construction arrives in 16F, driven entirely by Core.</summary>
internal static class ShowcaseBattleFixture
{
    public static BattleScene Build(GameDb db)
    {
        IReadOnlyList<BattleCreature> players = BuildPlayerParty(db);
        Trainer trainer = db.Find<Trainer>(EntityId.Parse("trainer:expert_rematch_mira"))
            ?? throw new InvalidDataException("Showcase fixture needs trainer:expert_rematch_mira.");
        IReadOnlyList<BattleCreature> enemies = trainer.Party.Select(p => BuildCreature(db, p)).ToList();
        var chart = new TypeChart(db.All<TypeDef>());
        IReadOnlyDictionary<EntityId, Item> itemData = db.All<Item>().ToDictionary(item => item.Id);
        IReadOnlyDictionary<EntityId, BattleMove> moveData = db.All<Move>()
            .Select(MoveCompiler.ToBattleMove).ToDictionary(move => move.Move);
        var battle = new BattleController(players, enemies, chart, new Rng(1), itemData: itemData.Values,
            moveData: moveData.Values, abilityData: db.All<Ability>());
        AddFormTrainerItems(db, battle, players.Select(p => p.Species));

        var aiRng = new Rng(2);
        var memory = new SmartAiMemory();
        BattleAction EnemyAction(BattleController b, BattleAction playerAction)
        {
            memory.ObservePlayerAction(playerAction, b.Active(BattleSide.Player));
            return TrainerAi.ChooseAction(trainer.AiProfile, new SmartAiContext(
                b.Party(BattleSide.Enemy), b.ActiveIndex(BattleSide.Enemy),
                b.Party(BattleSide.Player), b.ActiveIndex(BattleSide.Player),
                chart, aiRng, Turn: b.Turn, Memory: memory, ItemData: itemData,
                Conditions: b.ConditionSnapshot, Ruleset: b.Ruleset, MoveData: moveData,
                Overlays: b.Overlays, ItemState: b.Items, AbilityState: b.Abilities));
        }

        return new BattleScene(battle, EnemyAction, FormChoices(db, players.Select(p => p.Species)),
            id => NameOf(db, id));
    }

    private static IReadOnlyList<BattleCreature> BuildPlayerParty(GameDb db)
    {
        EntityId speciesId = db.Settings.StarterParty[0];
        Species species = db.Find<Species>(speciesId)
            ?? throw new InvalidDataException($"Starter species '{speciesId}' is missing.");
        IReadOnlyList<EntityId> moves =
        [
            EntityId.Parse("move:leaf_jab"),
            EntityId.Parse("move:root_guard"),
            EntityId.Parse("move:cinder_burst"),
        ];
        EntityId? formItem = species.Forms.FirstOrDefault(f => f.RequiredHeldItem is not null)?.RequiredHeldItem;
        PartyMember[] party =
        [
            new() { Species = speciesId, Level = 24, Moves = moves, HeldItem = formItem ?? EntityId.Parse("item:bloom_stone") },
            new() { Species = speciesId, Level = 24, Moves = moves, HeldItem = EntityId.Parse("item:storm_band") },
            new() { Species = speciesId, Level = 24, Moves = moves, HeldItem = EntityId.Parse("item:surge_sash") },
        ];
        return party.Select(p => BuildCreature(db, p)).ToList();
    }

    private static BattleCreature BuildCreature(GameDb db, PartyMember partyMember)
    {
        Species species = db.Find<Species>(partyMember.Species)
            ?? throw new InvalidDataException($"Unknown species '{partyMember.Species}'.");
        IReadOnlyList<EntityId> moveIds = partyMember.Moves is { Count: > 0 }
            ? partyMember.Moves
            : species.Learnset.Where(l => l.Level <= partyMember.Level).OrderBy(l => l.Level)
                .Select(l => l.Move).TakeLast(4).ToList();

        Stats ivs = partyMember.Ivs ?? new Stats(10, 10, 10, 10, 10, 10);
        Stats stats = StatCalc.Compute(species.BaseStats, ivs, default, partyMember.Nature ?? "hardy", partyMember.Level);
        var instance = new CreatureInstance
        {
            Species = partyMember.Species,
            Level = partyMember.Level,
            Ivs = ivs,
            Nature = partyMember.Nature ?? "hardy",
            CurHp = stats.Hp,
            Moves = moveIds.Select(id => new MoveSlot(id, db.Find<Move>(id)?.Pp ?? 1)).ToList(),
            HeldItem = partyMember.HeldItem,
        };
        return BattleCreature.FromInstance(instance, db);
    }

    private static void AddFormTrainerItems(GameDb db, BattleController battle, IEnumerable<EntityId> speciesIds)
    {
        foreach (EntityId item in speciesIds.Select(db.Find<Species>).OfType<Species>()
            .SelectMany(s => s.Forms).Select(f => f.RequiredTrainerItem).OfType<EntityId>().Distinct())
            battle.SetBattleItemStock(BattleSide.Player, item, 1);
    }

    private static IReadOnlyList<BattleFormChoice> FormChoices(GameDb db, IEnumerable<EntityId> speciesIds) =>
        speciesIds.Select(db.Find<Species>).OfType<Species>().SelectMany(s => s.Forms)
            .Where(f => f.Activation is FormActivation.BattleTemporary or FormActivation.BattleTimed)
            .Select(f => new BattleFormChoice(f.FormId, 0)).Distinct().ToList();

    private static string NameOf(GameDb db, EntityId id) => id.Category switch
    {
        EntityCategory.Ability => db.Find<Ability>(id)?.Name ?? id.ToString(),
        EntityCategory.Item => db.Find<Item>(id)?.Name ?? id.ToString(),
        EntityCategory.Move => db.Find<Move>(id)?.Name ?? id.ToString(),
        EntityCategory.Species => db.Find<Species>(id)?.Name ?? id.ToString(),
        EntityCategory.Trainer => db.Find<Trainer>(id)?.Name ?? id.ToString(),
        EntityCategory.Type => db.Find<TypeDef>(id)?.Name ?? id.ToString(),
        _ => id.ToString(),
    };
}
