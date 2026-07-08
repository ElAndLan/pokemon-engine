using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class RecoveryTests
{
    private static readonly EntityId SpeciesId = EntityId.Parse("species:mon");
    private static readonly EntityId MoveId = EntityId.Parse("move:tackle");
    private static readonly EntityId Town = EntityId.Parse("map:town");
    private static readonly EntityId Center = EntityId.Parse("map:center");

    private static GameDb Db() => new(
        new ProjectSettings { Name = "T", StartMap = Town, StartPos = new GridPos(3, 4) },
        [
            new Species { Id = SpeciesId, Name = "Mon", Types = [EntityId.Parse("type:normal")],
                BaseStats = new Stats(45, 45, 45, 45, 45, 45) },
            new Move { Id = MoveId, Name = "Tackle", Type = EntityId.Parse("type:normal"),
                DamageClass = DamageClass.Physical, Power = 40, Accuracy = 100, Pp = 25 },
        ]);

    // base HP 45, IV/EV 0, level 50 → (2*45)*50/100 + 50 + 10 = 105.
    private static CreatureInstance Hurt() => new()
    {
        Species = SpeciesId,
        Level = 50,
        Nature = "hardy",
        CurHp = 1,
        Status = PersistentStatus.Poison,
        StatusCounter = 3,
        Moves = [new MoveSlot(MoveId, 2)], // 2 of 25 PP left
    };

    [Fact]
    public void HealCreature_RestoresHpPpAndClearsStatus()
    {
        CreatureInstance healed = Recovery.HealCreature(Hurt(), Db());

        Assert.Equal(105, healed.CurHp);
        Assert.Null(healed.Status);
        Assert.Equal(0, healed.StatusCounter);
        Assert.Equal(25, healed.Moves[0].Pp);
    }

    [Fact]
    public void HealParty_HealsEveryMember()
    {
        var party = Recovery.HealParty([Hurt(), Hurt()], Db());
        Assert.All(party, c => Assert.Equal(105, c.CurHp));
        Assert.All(party, c => Assert.Null(c.Status));
    }

    [Fact]
    public void VisitCenter_HealsAndSetsRespawn()
    {
        var save = new SaveFile { Party = [Hurt()] };
        SaveFile after = Recovery.VisitCenter(save, Db(), Center, new GridPos(7, 8));

        Assert.Equal(105, after.Party[0].CurHp);
        Assert.Equal(new RespawnPoint(Center, new GridPos(7, 8)), after.Respawn);
    }

    [Fact]
    public void Blackout_WarpsToRespawn_WhenSet()
    {
        var save = new SaveFile
        {
            Party = [Hurt()],
            Map = EntityId.Parse("map:dungeon"),
            Pos = new GridPos(20, 20),
            Respawn = new RespawnPoint(Center, new GridPos(7, 8)),
        };
        SaveFile after = Recovery.Blackout(save, Db());

        Assert.Equal(Center, after.Map);
        Assert.Equal(new GridPos(7, 8), after.Pos);
        Assert.Equal(Facing.Down, after.Facing);
        Assert.Equal(105, after.Party[0].CurHp); // healed on the way down
    }

    [Fact]
    public void Blackout_FallsBackToStartMap_WhenNoRespawn()
    {
        var save = new SaveFile { Party = [Hurt()], Map = EntityId.Parse("map:dungeon"), Pos = new GridPos(20, 20) };
        SaveFile after = Recovery.Blackout(save, Db());

        Assert.Equal(Town, after.Map);             // project start map
        Assert.Equal(new GridPos(3, 4), after.Pos); // project start pos
        Assert.Equal(105, after.Party[0].CurHp);
    }

    [Fact]
    public void HealCreature_ThrowsForUnknownSpecies()
    {
        var orphan = Hurt() with { Species = EntityId.Parse("species:ghost") };
        Assert.Throws<InvalidOperationException>(() => Recovery.HealCreature(orphan, Db()));
    }
}
