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
        BattleCreature player = BuildPlayerCreature(db);
        Trainer trainer = db.All<Trainer>().FirstOrDefault(t => t.Party.Count > 0)
            ?? throw new InvalidDataException("Smoke battle needs a trainer with a party.");
        IReadOnlyList<BattleCreature> enemies = trainer.Party.Select(p => BuildCreature(db, p)).ToList();
        var battle = new BattleController([player], enemies, new TypeChart(db.All<TypeDef>()), new Rng(1));
        AddTemporaryFormTrainerItems(db, battle, player.Species);
        return new BattleScene(battle, _ => new UseMove(0), FormChoices(db, player.Species));
    }

    private static BattleCreature BuildPlayerCreature(GameDb db)
    {
        EntityId speciesId = db.Settings.StarterParty.FirstOrDefault();
        if (speciesId.Equals(default(EntityId)))
            speciesId = db.All<Species>().FirstOrDefault()?.Id
                ?? throw new InvalidDataException("Smoke battle needs at least one species.");

        Species species = db.Find<Species>(speciesId)
            ?? throw new InvalidDataException($"Starter species '{speciesId}' is missing.");
        EntityId? held = species.Forms.FirstOrDefault(f => f.RequiredHeldItem is not null)?.RequiredHeldItem;
        return BuildCreature(db, new PartyMember { Species = speciesId, Level = 24, HeldItem = held });
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

    private static void AddTemporaryFormTrainerItems(GameDb db, BattleController battle, EntityId speciesId)
    {
        Species? species = db.Find<Species>(speciesId);
        if (species is null)
            return;

        // ponytail: smoke grants only the key item needed to prove the showcase action path.
        foreach (EntityId item in species.Forms.Select(f => f.RequiredTrainerItem).OfType<EntityId>())
            battle.SetBattleItemStock(BattleSide.Player, item, 1);
    }

    private static IReadOnlyList<BattleFormChoice> FormChoices(GameDb db, EntityId speciesId) =>
        db.Find<Species>(speciesId)?.Forms
            .Where(f => f.Activation is FormActivation.BattleTemporary or FormActivation.BattleTimed)
            .Select(f => new BattleFormChoice(f.FormId, 0))
            .ToList() ?? [];
}