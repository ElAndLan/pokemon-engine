using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;

namespace Cgm.Runtime.Engine;

public sealed record ExportedGame(RuntimeConfig Config, PackManifest Manifest, GameDb Db, Map StartMap, BattleScene ShowcaseBattle);

public static class ExportedGameBoot
{
    public const string RuntimeVersion = "1.0.0";

    public static ExportedGame Load(string baseFolder, string? configPath = null)
    {
        string configFile = configPath ?? Path.Combine(baseFolder, Exporter.ConfigFileName);
        string configDir = Path.GetDirectoryName(Path.GetFullPath(configFile)) ?? baseFolder;
        RuntimeConfig config = CgmJson.Deserialize<RuntimeConfig>(File.ReadAllText(configFile));
        if (config.VirtualWidth <= 0 || config.VirtualHeight <= 0)
            throw new InvalidDataException("Runtime config virtual resolution must be positive.");

        string packPath = Path.IsPathRooted(config.PackPath)
            ? config.PackPath
            : Path.Combine(configDir, config.PackPath);

        PackManifest manifest;
        using (FileStream fs = File.OpenRead(packPath))
            manifest = CgmPack.ReadManifest(fs);
        if (!string.Equals(manifest.RequiredRuntimeVersion, RuntimeVersion, StringComparison.Ordinal))
            throw new InvalidDataException(
                $"Pack requires runtime {manifest.RequiredRuntimeVersion}; this runtime is {RuntimeVersion}.");

        GameDb db;
        using (FileStream fs = File.OpenRead(packPath))
            db = CgmPack.Read(fs);

        EntityId startMapId = db.Settings.StartMap
            ?? throw new InvalidDataException("Project has no start map.");
        Map startMap = db.Find<Map>(startMapId)
            ?? throw new InvalidDataException($"Start map '{startMapId}' is missing from the pack.");

        return new ExportedGame(config, manifest, db, startMap, BuildShowcaseBattle(db));
    }

    public static void Smoke(string baseFolder, string? configPath = null)
    {
        ExportedGame game = Load(baseFolder, configPath);
        if (game.ShowcaseBattle.Menu.Count == 0)
            throw new InvalidDataException("Smoke battle has no legal player actions.");

        BattleMenuItem action = game.ShowcaseBattle.Menu.FirstOrDefault(i => i.Action is ActivateForm)
            ?? game.ShowcaseBattle.Menu[0];
        game.ShowcaseBattle.Submit(action.Action);
    }

    private static BattleScene BuildShowcaseBattle(GameDb db)
    {
        IReadOnlyList<BattleCreature> players = BuildPlayerParty(db);
        Trainer trainer = db.Find<Trainer>(EntityId.Parse("trainer:expert_rematch_mira"))
            ?? db.All<Trainer>().FirstOrDefault(t => t.Party.Count > 0)
            ?? throw new InvalidDataException("Smoke battle needs a trainer with a party.");
        IReadOnlyList<BattleCreature> enemies = trainer.Party.Select(p => BuildCreature(db, p)).ToList();
        var chart = new TypeChart(db.All<TypeDef>());
        IReadOnlyDictionary<EntityId, Item> itemData = db.All<Item>().ToDictionary(item => item.Id);
        IReadOnlyDictionary<EntityId, BattleMove> moveData = db.All<Move>()
            .Select(MoveCompiler.ToBattleMove).ToDictionary(move => move.Move);
        var battle = new BattleController(players, enemies, chart, new Rng(1), itemData: itemData.Values,
            moveData: moveData.Values, abilityData: db.All<Ability>());
        AddTemporaryFormTrainerItems(db, battle, players.Select(p => p.Species));

        var aiRng = new Rng(2);
        var memory = new SmartAiMemory();
        BattleAction EnemyAction(BattleController b, BattleAction playerAction)
        {
            memory.ObservePlayerAction(playerAction, b.Active(BattleSide.Player));
            return TrainerAi.ChooseAction(trainer.AiProfile, new SmartAiContext(
                b.Party(BattleSide.Enemy),
                b.ActiveIndex(BattleSide.Enemy),
                b.Party(BattleSide.Player),
                b.ActiveIndex(BattleSide.Player),
                chart,
                aiRng,
                Turn: b.Turn,
                Memory: memory,
                ItemData: itemData,
                Conditions: b.ConditionSnapshot,
                Ruleset: b.Ruleset,
                MoveData: moveData,
                Overlays: b.Overlays,
                ItemState: b.Items,
                AbilityState: b.Abilities));
        }

        return new BattleScene(battle, EnemyAction, FormChoices(db, players.Select(p => p.Species)), id => NameOf(db, id));
    }

    private static IReadOnlyList<BattleCreature> BuildPlayerParty(GameDb db)
    {
        EntityId speciesId = db.Settings.StarterParty.FirstOrDefault();
        if (speciesId.Equals(default(EntityId)))
            speciesId = db.All<Species>().FirstOrDefault()?.Id
                ?? throw new InvalidDataException("Smoke battle needs at least one species.");

        Species species = db.Find<Species>(speciesId)
            ?? throw new InvalidDataException($"Starter species '{speciesId}' is missing.");
        EntityId[] preferredMoves =
        [
            EntityId.Parse("move:leaf_jab"),
            EntityId.Parse("move:root_guard"),
            EntityId.Parse("move:cinder_burst"),
        ];
        IReadOnlyList<EntityId> moves = preferredMoves
            .Where(id => db.Find<Move>(id) is not null)
            .ToList();
        if (moves.Count == 0)
        {
            moves = species.Learnset
                .Where(l => l.Level <= 24)
                .OrderBy(l => l.Level)
                .Select(l => l.Move)
                .TakeLast(4)
                .ToList();
        }
        if (moves.Count == 0)
            throw new InvalidDataException($"Species '{species.Id}' has no showcase moves.");

        EntityId bloomStone = EntityId.Parse("item:bloom_stone");
        EntityId stormBand = EntityId.Parse("item:storm_band");
        EntityId surgeSash = EntityId.Parse("item:surge_sash");
        EntityId? formItem = species.Forms.FirstOrDefault(f => f.RequiredHeldItem is not null)?.RequiredHeldItem;
        PartyMember[] party =
        [
            new() { Species = speciesId, Level = 24, Moves = moves, HeldItem = db.Find<Item>(formItem ?? bloomStone) is null ? null : formItem ?? bloomStone },
            new() { Species = speciesId, Level = 24, Moves = moves, HeldItem = db.Find<Item>(stormBand) is null ? null : stormBand },
            new() { Species = speciesId, Level = 24, Moves = moves, HeldItem = db.Find<Item>(surgeSash) is null ? null : surgeSash },
        ];
        return party.Select(p => BuildCreature(db, p)).ToList();
    }

    private static BattleCreature BuildCreature(GameDb db, PartyMember partyMember)
    {
        Species species = db.Find<Species>(partyMember.Species)
            ?? throw new InvalidDataException($"Unknown species '{partyMember.Species}'.");
        IReadOnlyList<EntityId> moveIds = partyMember.Moves is { Count: > 0 }
            ? partyMember.Moves
            : species.Learnset.Where(l => l.Level <= partyMember.Level).OrderBy(l => l.Level).Select(l => l.Move).TakeLast(4).ToList();
        if (moveIds.Count == 0)
            throw new InvalidDataException($"Species '{species.Id}' has no smoke-test moves.");

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

    private static void AddTemporaryFormTrainerItems(GameDb db, BattleController battle, IEnumerable<EntityId> speciesIds)
    {
        // ponytail: smoke grants only the key item needed to prove the showcase action path.
        foreach (EntityId item in speciesIds
            .Select(db.Find<Species>)
            .OfType<Species>()
            .SelectMany(s => s.Forms)
            .Select(f => f.RequiredTrainerItem)
            .OfType<EntityId>()
            .Distinct())
            battle.SetBattleItemStock(BattleSide.Player, item, 1);
    }

    private static IReadOnlyList<BattleFormChoice> FormChoices(GameDb db, IEnumerable<EntityId> speciesIds) =>
        speciesIds
            .Select(db.Find<Species>)
            .OfType<Species>()
            .SelectMany(s => s.Forms)
            .Where(f => f.Activation is FormActivation.BattleTemporary or FormActivation.BattleTimed)
            .Select(f => new BattleFormChoice(f.FormId, 0))
            .Distinct()
            .ToList();

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
