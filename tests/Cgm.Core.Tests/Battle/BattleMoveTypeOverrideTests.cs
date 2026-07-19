using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>15F-4 move-type override precedence: an effective per-slot move type (a
/// <see cref="MoveTypeOverlay"/>) drives STAB and effectiveness, not the authored move type.</summary>
public sealed class BattleMoveTypeOverrideTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Water = EntityId.Parse("type:water");

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }, new TypeDef { Id = Water }]);

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 25, 0, 0);

    private static BattleCreature Creature(string slug, EntityId type, Stats stats, params BattleMove[] moves) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [type], stats, moves);

    [Fact]
    public void OverridingAMoveSlotTypeGrantsStabInTheDamagePipeline()
    {
        // Attacker is Water-typed; the move is authored Normal (no STAB) until its slot type is overridden to Water.
        var strike = new BattleMove(EntityId.Parse("move:strike"), Normal, DamageClass.Physical, 80, 100, 25, 0, 0);

        int baseline = Damage(strike, overrideToWater: false);
        int overridden = Damage(strike, overrideToWater: true);

        Assert.True(overridden > baseline,
            $"expected the overridden Water move type to add STAB, got {overridden} vs {baseline}");
    }

    private static int Damage(BattleMove strike, bool overrideToWater)
    {
        var battle = new BattleController(
            Creature("attacker", Water, new Stats(300, 120, 100, 120, 100, 100), strike),
            Creature("target", Normal, new Stats(300, 100, 100, 100, 100, 1), Inert()),
            Chart(), new FakeRng(ints: [0, 100], doubles: [0.99]));
        if (overrideToWater)
            battle.Overlays.Apply(new BattleOverlayApplication(
                new BattleOverlayOwner(BattleSide.Player, 0, new BattleSlot(BattleSide.Player, 0)),
                new BattleOverlaySource(), BattleOverlayLayer.PermanentInstance,
                new MoveTypeOverlay(0, Water), 0, 0));
        return battle.ResolveTurn(new UseMove(0), new UseMove(0))
            .OfType<DamageDealt>().Single(e => e.Target == BattleSide.Enemy).Amount;
    }
}
