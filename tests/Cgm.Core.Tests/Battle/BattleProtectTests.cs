using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>Protect/Detect volatile (catalog §7.2): a priority self-shield that blocks the opponent's
/// move for a turn, with success-chain decay on consecutive use.</summary>
public sealed class BattleProtectTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Potion = EntityId.Parse("item:potion");

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private static BattleMove Hit() =>
        new(EntityId.Parse("move:hit"), Normal, DamageClass.Physical, 60, 100, 25, 0, 0);

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 25, 0, 0);

    // Protect: +4 priority so the user shields before the attacker strikes.
    private static BattleMove Protect() =>
        new(EntityId.Parse("move:protect"), Normal, DamageClass.Status, null, null, 25, priority: 4, 0,
            target: MoveTarget.User,
            secondaryEffects: [new ProtectEffect(ProtectionConditions.LegacyPersonal)]);

    private static BattleCreature Slower(int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:s"), "S", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 50), moves);

    private static BattleCreature Faster(int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:f"), "F", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 100), moves);

    [Fact]
    public void Protect_BlocksIncomingDamage()
    {
        // Player is slower, but Protect's +4 priority makes it resolve first.
        var player = Slower(200, Protect());
        var enemy = Faster(200, Hit());
        var battle = new BattleController(player, enemy, Chart(), new Rng(1));
        var events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(200, player.CurrentHp); // shielded
        Assert.Contains(events, e => e is Protected { Side: BattleSide.Player });
        Assert.Contains(events, e => e is MoveBlocked { Side: BattleSide.Enemy });
        Assert.Contains(events, e => e is ProtectionBlocked
            { Source.Side: BattleSide.Enemy, Target.Side: BattleSide.Player });
        Assert.Contains(battle.HookTrace, entry => entry.Checkpoint == BattleConditionHook.TryHit
            && entry.Scope == BattleHookScope.Creature
            && entry.PayloadKind == BattleHookPayloadKind.Filter);
    }

    [Fact]
    public void Protect_BlocksAllOpponentsTargetInSingles()
    {
        var spreadHit = new BattleMove(EntityId.Parse("move:spread"), Normal, DamageClass.Physical, 60, 100, 25, 0, 0,
            target: MoveTarget.AllOpponents);
        var player = Slower(200, Protect());
        var enemy = Faster(200, spreadHit);
        var events = new BattleController(player, enemy, Chart(), new Rng(1)).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(200, player.CurrentHp);
        Assert.Contains(events, e => e is MoveBlocked { Side: BattleSide.Enemy });
    }

    [Fact]
    public void Protect_BlocksAllOtherPokemonTargetInSingles()
    {
        var spreadHit = new BattleMove(EntityId.Parse("move:spread"), Normal, DamageClass.Physical, 60, 100, 25, 0, 0,
            target: MoveTarget.AllOtherPokemon);
        var player = Slower(200, Protect());
        var enemy = Faster(200, spreadHit);
        var events = new BattleController(player, enemy, Chart(), new Rng(1)).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(200, player.CurrentHp);
        Assert.Contains(events, e => e is MoveBlocked { Side: BattleSide.Enemy });
    }

    [Fact]
    public void Protect_ExpiresNextTurn()
    {
        var player = Slower(200, Protect(), Inert());
        var enemy = Faster(200, Hit());
        var battle = new BattleController(player, enemy, Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // protect blocks
        Assert.Equal(200, player.CurrentHp);
        battle.ResolveTurn(new UseMove(1), new UseMove(0)); // no protect this turn → takes the hit
        Assert.True(player.CurrentHp < 200);
    }

    [Fact]
    public void Protect_DoesNotBlockSelfTargetedMoves()
    {
        // A self-buff (stat change on self) is not aimed at the protected creature, so it isn't blocked.
        var buff = new BattleMove(EntityId.Parse("move:swordsdance"), Normal, DamageClass.Status, null, null, 25, 0, 0,
            stageEffect: new StageEffect(StatKind.Atk, 2, OnSelf: true, Chance: 100), target: MoveTarget.User);
        var player = Slower(200, Protect());
        var enemy = Faster(200, buff);
        new BattleController(player, enemy, Chart(), new Rng(1)).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(2, enemy.Stage(StatKind.Atk)); // enemy still buffed itself through the shield
    }

    [Fact]
    public void ConsecutiveProtect_ChainCanFail()
    {
        // With a rigged RNG: first protect (chain 0) always succeeds; a later roll ≥ chance fails.
        var player = Slower(400, Protect());
        var enemy = Faster(400, Hit());
        // Draw order per turn: (turn order — no tie), protect success double, enemy accuracy/crit/roll if it hits.
        // Turn 1: protect chain 0 → NextDouble()<1.0 always true. Enemy blocked (no draws).
        // Turn 2: protect chain 1 → success chance 0.5; feed 0.9 (≥0.5) → fail; enemy hit lands.
        var rng = new FakeRng(
            ints: [50, 15],                 // turn 2 enemy accuracy(hit=50<100), damage roll(85+15)
            doubles: [0.0, 0.9, 0.5]);      // t1 protect ok(0.0), t2 protect fail(0.9), t2 enemy crit(0.5 no crit)
        var battle = new BattleController(player, enemy, Chart(), rng);

        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // t1: protect succeeds, blocks
        Assert.Equal(400, player.CurrentHp);
        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // t2: protect fails, enemy hits
        Assert.True(player.CurrentHp < 400);

        EffectTraceEntry[] protectTraces = battle.Trace.Where(entry => entry.Kind == EffectTraceKind.Protect).ToArray();
        Assert.Equal(2, protectTraces.Length);
        Assert.All(protectTraces, entry =>
        {
            Assert.Equal(new BattleSlot(BattleSide.Player, 0), entry.SourceSlot);
            Assert.Null(entry.TargetSlot);
            Assert.True(entry.Performed);
            Assert.Equal(1d, entry.DrawBound);
            Assert.True(entry.EventEndIndex > entry.EventStartIndex);
        });
        Assert.Equal([0d, 0.9d], protectTraces.Select(entry => entry.DrawResult));
        Assert.Equal([1, 0], protectTraces.Select(entry => entry.Value));
    }

    [Fact]
    public void Compiler_AdmitsClosedProtectionProfilesAndBypass()
    {
        BattleMove personal = MoveCompiler.ToBattleMove(DataMove(MoveTarget.User,
            Op("protection", ("key", "contact_guard"), ("scope", "personal"),
                ("filter", "all"), ("chain", "shared"), ("drawGuaranteed", true),
                ("contact", "damage:1/8;status:Poison;stage:Atk/-1"))));
        ProtectEffect effect = Assert.Single(personal.SecondaryEffects.OfType<ProtectEffect>());
        Assert.Equal(new BattleConditionId("protection:contact_guard"), effect.Profile.Id);
        Assert.Collection(effect.Profile.ContactEffects,
            row => Assert.IsType<ProtectionContactDamage>(row),
            row => Assert.IsType<ProtectionContactStatus>(row),
            row => Assert.IsType<ProtectionContactStage>(row));

        BattleMove side = MoveCompiler.ToBattleMove(DataMove(MoveTarget.UsersField,
            Op("protection", ("key", "wide_guard"), ("scope", "side"),
                ("filter", "multiTarget"), ("chain", "classicOnly"),
                ("drawGuaranteed", false))));
        Assert.Equal(ProtectionScope.Side,
            Assert.Single(side.SecondaryEffects.OfType<ProtectEffect>()).Profile.Scope);

        BattleMove bypass = MoveCompiler.ToBattleMove(DataMove(MoveTarget.Selected,
            Op("protectionBypass"), DamageClass.Physical, 40));
        Assert.Single(bypass.SecondaryEffects.OfType<ProtectionBypassEffect>());
    }

    [Fact]
    public void Compiler_RejectsMalformedProtectionSiblings()
    {
        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(DataMove(MoveTarget.User,
            Op("protect", ("extra", 1)))));
        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(DataMove(MoveTarget.User,
            Op("protection", ("key", "missing")))));
        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(DataMove(MoveTarget.Selected,
            Op("protection", ("key", "wrong_target"), ("scope", "personal"),
                ("filter", "all"), ("chain", "none"), ("drawGuaranteed", false)))));
        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(DataMove(MoveTarget.UsersField,
            Op("protection", ("key", "bad_contact"), ("scope", "side"),
                ("filter", "priority"), ("chain", "none"), ("drawGuaranteed", false),
                ("contact", "damage:1/8")))));
        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(DataMove(MoveTarget.Selected,
            Op("protectionBypass", ("extra", 1)), DamageClass.Physical, 40)));
    }

    [Fact]
    public void ProtectionProfile_CapturesImmutablePayloadAndRejectsInvalidRows()
    {
        var authored = new List<ProtectionContactEffect>
        {
            new ProtectionContactDamage(new Fraction(1, 8)),
        };
        ProtectionProfile profile = ProtectionConditions.Personal(
            "immutable", ProtectionChainMode.None, false, authored);
        authored.Add(new ProtectionContactStatus(PersistentStatus.Poison));
        Assert.Single(profile.ContactEffects);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ProtectionContactEffect>)profile.ContactEffects).Add(
                new ProtectionContactStatus(PersistentStatus.Poison)));

        Assert.Throws<ArgumentException>(() => ProtectionConditions.Validate(profile with
        {
            ContactEffects = [new ProtectionContactDamage(new Fraction(2, 1))],
        }));
        Assert.Throws<ArgumentException>(() => ProtectionConditions.Validate(profile with
        {
            ContactEffects = [new ProtectionContactStage(StatKind.Atk, 1)],
        }));
    }

    [Fact]
    public void ProtectionChance_UsesExactRulesetFactorsAndGuaranteedDrawPolicy()
    {
        ProtectionProfile sharedDraw = ProtectionConditions.Personal(
            "shared_draw", ProtectionChainMode.Shared, true, []);
        ProtectionProfile sharedNoGuaranteedDraw = ProtectionConditions.Personal(
            "shared_skip", ProtectionChainMode.Shared, false, []);
        ProtectionProfile classic = ProtectionConditions.Side(
            "classic", ProtectionFilter.Priority, ProtectionChainMode.ClassicOnly, false);

        Assert.Equal(0.5, ProtectionConditions.SuccessChance(sharedDraw, 1, BattleRulesets.Gen4Like));
        Assert.Equal(1d / 3d, ProtectionConditions.SuccessChance(sharedDraw, 1,
            BattleRulesets.ModernReference), 12);
        Assert.Equal(1, ProtectionConditions.SuccessChance(classic, 7, BattleRulesets.ModernReference));

        var rng = new CountingProtectionRng();
        Assert.True(ProtectionConditions.Succeeds(sharedDraw, 0, BattleRulesets.Gen4Like, rng, out double? drawn));
        Assert.Equal(0d, drawn);
        Assert.Equal(1, rng.Draws);
        Assert.True(ProtectionConditions.Succeeds(sharedNoGuaranteedDraw, 0,
            BattleRulesets.Gen4Like, rng, out double? skipped));
        Assert.Null(skipped);
        Assert.Equal(1, rng.Draws);
    }

    [Fact]
    public void GenericSideProtection_UsesClassicChainButIsGuaranteedWithoutDrawInModern()
    {
        ProtectionProfile profile = ProtectionConditions.Side("wide_guard",
            ProtectionFilter.MultiTarget, ProtectionChainMode.ClassicOnly, false);
        BattleMove guard = new(EntityId.Parse("move:wide_guard"), Normal, DamageClass.Status,
            null, null, 10, 4, 0, target: MoveTarget.UsersField,
            secondaryEffects: [new ProtectEffect(profile)]);
        BattleMove spread = new(EntityId.Parse("move:spread"), Normal, DamageClass.Physical,
            60, null, 10, 0, 0, target: MoveTarget.AllOpponents);

        BattleCreature classicUser = Slower(400, guard);
        var classic = new BattleController(classicUser, Faster(400, spread), Chart(),
            new CountingProtectionRng(0.99), fieldInputs: new(BattleRulesets.Gen4Like));
        classic.ResolveTurn(new UseMove(0), new UseMove(0));
        classic.ResolveTurn(new UseMove(0), new UseMove(0));
        EffectTraceEntry[] classicTraces = classic.Trace
            .Where(entry => entry.Kind == EffectTraceKind.Protect).ToArray();
        Assert.Equal([null, 0.99d], classicTraces.Select(entry => entry.DrawResult));
        Assert.Equal([1, 0], classicTraces.Select(entry => entry.Value));
        Assert.Equal(0, classicUser.ProtectChain);

        BattleCreature modernUser = Slower(400, guard);
        var modernRng = new CountingProtectionRng(0.99);
        var modern = new BattleController(modernUser, Faster(400, spread), Chart(), modernRng,
            fieldInputs: new(BattleRulesets.ModernReference));
        modern.ResolveTurn(new UseMove(0), new UseMove(0));
        modern.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(400, modernUser.CurrentHp);
        Assert.Equal(0, modernRng.Draws);
        Assert.Equal(0, modernUser.ProtectChain);
    }

    [Fact]
    public void ProtectChain_ResetsAtPassOrdinaryItemSwitchAndPreventionBoundaries()
    {
        BattleCreature player = Slower(200, Protect(), Inert());
        BattleCreature enemy = Faster(200, Inert());
        var battle = new BattleController([player, Slower(200, Inert())], [enemy], Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(1, player.ProtectChain);
        battle.ResolveTurn(new Pass(), new UseMove(0));
        Assert.Equal(0, player.ProtectChain);

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(1), new UseMove(0));
        Assert.Equal(0, player.ProtectChain);

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        player.TakeDamage(1);
        battle.SetBattleItemStock(BattleSide.Player, Potion, 1);
        battle.ResolveTurn(new UseBattleItem(Potion, 0, 1), new UseMove(0));
        Assert.Equal(0, player.ProtectChain);

        player.AdvanceProtectChain();
        battle.ResolveTurn(new Switch(1), new UseMove(0));
        Assert.Equal(0, player.ProtectChain);

        BattleCreature prevented = Slower(200, Protect());
        prevented.AdvanceProtectChain();
        BattleMove flinch = new(EntityId.Parse("move:flinch"), Normal, DamageClass.Physical,
            1, null, 10, 5, 0, flinchChance: 100);
        var preventedBattle = new BattleController(prevented, Faster(200, flinch), Chart(), new Rng(1));
        preventedBattle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(0, prevented.ProtectChain);
    }

    [Fact]
    public void ContactProtection_AppliesOrderedPayloadAndMoveBypassHitsThroughIt()
    {
        ProtectionProfile profile = ProtectionConditions.Personal("contact_guard",
            ProtectionChainMode.None, false,
            [new ProtectionContactDamage(new Fraction(1, 8)),
             new ProtectionContactStatus(PersistentStatus.Poison),
             new ProtectionContactStage(StatKind.Atk, -1)]);
        BattleMove guard = new(EntityId.Parse("move:contact_guard"), Normal, DamageClass.Status,
            null, null, 10, 4, 0, target: MoveTarget.User,
            secondaryEffects: [new ProtectEffect(profile)]);
        BattleMove contact = new(EntityId.Parse("move:contact"), Normal, DamageClass.Physical,
            60, null, 10, 0, 0, makesContact: true);
        BattleCreature defender = Slower(400, guard);
        BattleCreature attacker = Faster(400, contact);
        BattleController blocked = new(defender, attacker, Chart(), new Rng(1));

        blocked.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(400, defender.CurrentHp);
        Assert.Equal(300, attacker.CurrentHp); // 1/8 contact damage, then 1/8 poison at turn end.
        Assert.Equal(PersistentStatus.Poison, attacker.Status);
        Assert.Equal(-1, attacker.Stage(StatKind.Atk));
        Assert.Contains(blocked.Log, entry => entry is ProtectionContactDamaged
            { Amount: 50, Condition.Value: "protection:contact_guard" });

        BattleMove bypass = new(EntityId.Parse("move:bypass"), Normal, DamageClass.Physical,
            60, null, 10, 0, 0, makesContact: true,
            secondaryEffects: [new ProtectionBypassEffect()]);
        defender = Slower(400, guard);
        attacker = Faster(400, bypass);
        new BattleController(defender, attacker, Chart(), new Rng(1))
            .ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.True(defender.CurrentHp < 400);
        Assert.Equal(400, attacker.CurrentHp);
    }

    [Fact]
    public void ContactProtection_SourceFaintStopsLaterPayloadButSpreadContinues()
    {
        ProtectionProfile profile = ProtectionConditions.Personal("faint_guard",
            ProtectionChainMode.None, false,
            [new ProtectionContactDamage(new Fraction(1, 1)),
             new ProtectionContactStatus(PersistentStatus.Poison),
             new ProtectionContactStage(StatKind.Atk, -1)]);
        BattleMove guard = new(EntityId.Parse("move:faint_guard"), Normal, DamageClass.Status,
            null, null, 10, 4, 0, target: MoveTarget.User,
            secondaryEffects: [new ProtectEffect(profile)]);
        BattleMove spread = new(EntityId.Parse("move:contact_spread"), Normal, DamageClass.Physical,
            60, null, 10, 0, 0, makesContact: true, target: MoveTarget.AllOpponents);
        BattleCreature source = Faster(100, spread);
        BattleCreature ally = Slower(100, Inert());
        BattleCreature protectedTarget = Slower(200, guard);
        BattleCreature openTarget = Slower(200, Inert());
        var battle = new BattleController([source, ally], [protectedTarget, openTarget],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new Rng(1));

        battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new(new BattleSlot(BattleSide.Player, 0), new UseMove(0)),
            new(new BattleSlot(BattleSide.Player, 1), new Pass()),
            new(new BattleSlot(BattleSide.Enemy, 0), new UseMove(0)),
            new(new BattleSlot(BattleSide.Enemy, 1), new Pass()),
        ]));

        Assert.True(source.IsFainted);
        Assert.Null(source.Status);
        Assert.Equal(0, source.Stage(StatKind.Atk));
        Assert.Equal(200, protectedTarget.CurrentHp);
        Assert.True(openTarget.CurrentHp < 200);
        Assert.Single(battle.Log.OfType<ProtectionContactDamaged>());
        Assert.DoesNotContain(battle.Log, entry => entry is StatusApplied
            { Slot.Side: BattleSide.Player });
    }

    [Fact]
    public void ProtectionFamily_MatchesDeterministicGolden()
    {
        static string Run()
        {
            ProtectionProfile profile = ProtectionConditions.Personal("golden_guard",
                ProtectionChainMode.None, false,
                [new ProtectionContactDamage(new Fraction(1, 8)),
                 new ProtectionContactStatus(PersistentStatus.Poison),
                 new ProtectionContactStage(StatKind.Atk, -1)]);
            BattleMove guard = new(EntityId.Parse("move:golden_guard"), Normal, DamageClass.Status,
                null, null, 10, 4, 0, target: MoveTarget.User,
                secondaryEffects: [new ProtectEffect(profile)]);
            BattleMove contact = new(EntityId.Parse("move:golden_contact"), Normal, DamageClass.Physical,
                60, null, 10, 0, 0, makesContact: true);
            var battle = new BattleController(Slower(400, guard), Faster(400, contact), Chart(), new Rng(1));
            IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));
            return string.Join('\n',
            [
                "events",
                .. events.Select(EventGolden).Where(value => value is not null)!,
                "condition-trace",
                .. battle.ConditionTrace.Select(entry =>
                    $"{entry.Kind}:{entry.Condition}:{entry.Scope}:{entry.DurationBefore}->{entry.DurationAfter}"),
                "effect-trace",
                .. battle.Trace.Where(entry => entry.Kind is EffectTraceKind.Protect or EffectTraceKind.ProtectionBlock)
                    .Select(entry => $"{entry.Kind}:{Slot(entry.SourceSlot)}:{entry.TargetSlot?.ToString() ?? "none"}:" +
                        $"{entry.Condition}:{entry.Performed}:{entry.DrawResult?.ToString() ?? "none"}:" +
                        $"{entry.ResolvedChance?.ToString() ?? "none"}:{entry.Value}"),
                "hook-trace",
                .. battle.HookTrace.Where(entry => entry.PayloadKind == BattleHookPayloadKind.Filter)
                    .Select(entry => $"{entry.Checkpoint}:{entry.Scope}:{entry.PayloadKind}:{entry.Invoked}"),
            ]);
        }

        string first = Run();
        Assert.Equal(first, Run());
        Assert.Equal(Golden("protection"), first);
    }

    private static Move DataMove(MoveTarget target, Effect effect,
        DamageClass damageClass = DamageClass.Status, int? power = null) => new()
    {
        Id = EntityId.Parse("move:data_protection"),
        Name = "Data Protection",
        Type = Normal,
        DamageClass = damageClass,
        Power = power,
        Pp = 10,
        Priority = damageClass == DamageClass.Status ? 4 : 0,
        Target = target,
        Effects = [effect],
    };

    private static Effect Op(string op, params (string Key, object Value)[] values) => new()
    {
        Op = op,
        Params = values.ToDictionary(value => value.Key,
            value => JsonSerializer.SerializeToElement(value.Value)),
    };

    private static string Golden(string name) => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Battle", "Goldens", $"{name}.golden"))
        .Replace("\r\n", "\n").TrimEnd();

    private static string? EventGolden(BattleEvent item) => item switch
    {
        MoveUsed e => $"MoveUsed:{Slot(e.Slot)}:{e.Move}",
        ConditionApplied e => $"ConditionApplied:{e.Condition}:{e.Scope}",
        ConditionExpired e => $"ConditionExpired:{e.Condition}:{e.Scope}",
        Protected e => $"Protected:{Slot(e.Slot)}",
        MoveBlocked e => $"MoveBlocked:{Slot(e.Slot)}",
        ProtectionBlocked e => $"ProtectionBlocked:{Slot(e.Source)}:{Slot(e.Target)}:{e.Condition}",
        ProtectionContactDamaged e => $"ProtectionContactDamaged:{Slot(e.Slot)}:{e.Condition}:{e.Amount}",
        StatusApplied e => $"StatusApplied:{Slot(e.Slot)}:{e.Status}",
        StatStageChanged e => $"StatStageChanged:{Slot(e.Slot)}:{e.Stat}:{e.Delta}",
        _ => null,
    };

    private static string Slot(BattleSlot slot) => $"{slot.Side}:{slot.Position}";

    private sealed class CountingProtectionRng : IRng
    {
        private readonly double _draw;
        public CountingProtectionRng(double draw = 0) => _draw = draw;
        public int Draws { get; private set; }
        public int Next(int maxExclusive) => 0;
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
        public double NextDouble() { Draws++; return _draw; }
    }
}
