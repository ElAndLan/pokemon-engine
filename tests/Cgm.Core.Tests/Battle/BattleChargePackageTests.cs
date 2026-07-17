using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleChargePackageTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    [Fact]
    public void Compiler_ProducesTypedChargeHitAndStartEffects()
    {
        BattleMove move = Compile(
            Op("damage"),
            Op("chargeTurn", ("state", "underground"), ("targetPolicy", "snapshotSlot")),
            Op("chargeStartStat", ("stat", "def"), ("delta", 1)),
            Op("semiInvulnerableHit", ("states", "air,underwater"), ("powerNum", 2), ("powerDen", 1)));

        Assert.Equal(new ChargeMoveEffect(SemiInvulnerableState.Underground,
            BattleIntentTargetPolicy.SnapshotSlot), move.Charge);
        Assert.Contains(new ChargeStartStatEffect(StatKind.Def, 1), move.SecondaryEffects);
        SemiInvulnerableHitEffect hit = Assert.Single(move.SecondaryEffects.OfType<SemiInvulnerableHitEffect>());
        Assert.Equal([SemiInvulnerableState.Air, SemiInvulnerableState.Underwater], hit.States.Order());
        Assert.Equal(new Fraction(2, 1), hit.PowerMultiplier);
    }

    [Fact]
    public void Compiler_RejectsMalformedChargeVocabulary()
    {
        Assert.Throws<ArgumentException>(() => Compile(Op("chargeTurn", ("state", "unknown"))));
        Assert.Throws<ArgumentException>(() => Compile(Op("chargeTurn", ("targetPolicy", "field"))));
        Assert.Throws<ArgumentException>(() => Compile(Op("chargeTurn"), Op("chargeTurn")));
        Assert.Throws<ArgumentException>(() => Compile(Op("semiInvulnerableHit", ("states", ""))));
        Assert.Throws<ArgumentException>(() => Compile(Op("semiInvulnerableHit",
            ("states", "air"), ("powerNum", 2))));
        Assert.Throws<ArgumentException>(() => Compile(Op("chargeStartStat", ("stat", "def"), ("delta", 1))));
        Assert.Throws<ArgumentException>(() => Compile(Op("chargeTurn"),
            Op("chargeStartStat", ("stat", "def"), ("delta", 0))));
        Assert.Throws<ArgumentException>(() => CompileTarget(MoveTarget.SpecificMove, Op("chargeTurn")));
        Assert.Throws<ArgumentException>(() => Move("invalid_charge",
            charge: new((SemiInvulnerableState)99)));
        Assert.Throws<ArgumentException>(() => Move("invalid_hit", effects:
            [new SemiInvulnerableHitEffect(new HashSet<SemiInvulnerableState>())]));
        Assert.Throws<ArgumentException>(() => Move("orphan_start", effects:
            [new ChargeStartStatEffect(StatKind.Def, 1)]));
    }

    [Fact]
    public void ChargeStart_QueuesTypedReleaseAndAppliesStartEffectOnce()
    {
        BattleMove charged = Move("charged", charge: new(SemiInvulnerableState.Air,
            BattleIntentTargetPolicy.SnapshotSlot), effects: [new ChargeStartStatEffect(StatKind.Def, 1)]);
        BattleCreature source = Creature("source", 100, charged);
        BattleCreature target = Creature("target", 1, Inert());
        var battle = Battle(source, target);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Equal(9, charged.Pp);
        Assert.Equal(1, source.Stage(StatKind.Def));
        Assert.Equal(SemiInvulnerableState.Air, source.SemiInvulnerableState);
        BattleIntentDebugEntry queued = Assert.Single(battle.IntentQueueSnapshot);
        Assert.Equal(BattleIntentPayloadKind.ReleaseMove, queued.Payload);
        Assert.Equal(0, queued.PayloadMoveIndex);
        Assert.Equal(SemiInvulnerableState.Air, queued.PayloadSemiInvulnerableState);
        Assert.Equal(BattleIntentTargetPolicy.SnapshotSlot, queued.TargetPolicy);
        Assert.Contains(events, e => e is Charging);
        Assert.DoesNotContain(events, e => e is MoveUsed);
        Assert.Single(events.OfType<StatStageChanged>());
    }

    [Fact]
    public void SkipChargeHook_AppliesStartEffectAndResolvesImmediately()
    {
        BattleMove setWeather = new(EntityId.Parse("move:set_weather"), Normal, DamageClass.Status,
            null, null, 10, 0, 0, setsWeather: Weather.Sun, target: MoveTarget.EntireField);
        BattleMove charged = Move("weather_charge", charge: new(), effects:
        [
            new ChargeStartStatEffect(StatKind.Spa, 1),
            new WeatherMoveEffect(new Dictionary<Weather, EntityId>(),
                new Dictionary<Weather, Fraction>(), new HashSet<Weather> { Weather.Sun }),
        ]);
        BattleCreature source = Creature("source", 100, setWeather, charged);
        BattleCreature target = Creature("target", 1, Inert());
        var battle = Battle(source, target);
        battle.ResolveTurn(new UseMove(0), new Pass());

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(1), new Pass());

        Assert.False(source.IsCharging);
        Assert.Empty(battle.IntentQueueSnapshot);
        Assert.Equal(1, source.Stage(StatKind.Spa));
        Assert.Equal(9, charged.Pp);
        Assert.Contains(events, e => e is MoveUsed);
        Assert.DoesNotContain(events, e => e is Charging);
    }

    [Theory]
    [InlineData(BattleIntentTargetPolicy.LiveSlot, true)]
    [InlineData(BattleIntentTargetPolicy.SnapshotSlot, false)]
    public void Release_UsesAuthoredTargetReplacementPolicy(BattleIntentTargetPolicy policy,
        bool hitsReplacement)
    {
        BattleMove charged = Move("charged", target: MoveTarget.Selected, charge: new(null, policy));
        BattleCreature source = Creature("source", 100, charged);
        BattleCreature ally = Creature("ally", 90, Inert());
        BattleCreature original = Creature("original", 1, Inert());
        BattleCreature other = Creature("other", 1, Inert());
        BattleCreature replacement = Creature("replacement", 1, Inert());
        var battle = new BattleController([source, ally], [original, other, replacement],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new Rng(1));
        BattleSlot targetSlot = new(BattleSide.Enemy, 0);

        battle.ResolveTurn(Doubles(new UseMove(0), new Pass(), new ActiveSlotSelection(targetSlot)));
        int before = replacement.CurrentHp;
        IReadOnlyList<BattleEvent> release = battle.ResolveTurn(Doubles(new Pass(), new Switch(2)));

        Assert.Equal(hitsReplacement, replacement.CurrentHp < before);
        Assert.Equal(!hitsReplacement,
            release.Any(e => e is MoveFailed { Reason: MoveFailureReason.TargetUnavailable }));
        Assert.False(source.IsCharging);
        Assert.Equal(9, charged.Pp);
    }

    [Theory]
    [InlineData(BattleIntentTargetPolicy.LiveSlot, true)]
    [InlineData(BattleIntentTargetPolicy.SnapshotSlot, false)]
    public void SinglesRelease_UsesAuthoredTargetReplacementPolicy(BattleIntentTargetPolicy policy,
        bool hitsReplacement)
    {
        BattleMove charged = Move("charged", charge: new(null, policy));
        BattleCreature source = Creature("source", 100, charged);
        BattleCreature original = Creature("original", 1, Inert());
        BattleCreature replacement = Creature("replacement", 1, Inert());
        var battle = new BattleController([source], [original, replacement], Chart(), new Rng(1));
        battle.ResolveTurn(new UseMove(0), new Pass());
        int before = replacement.CurrentHp;

        IReadOnlyList<BattleEvent> release = battle.ResolveTurn(new Pass(), new Switch(1));

        Assert.Equal(hitsReplacement, replacement.CurrentHp < before);
        Assert.Equal(!hitsReplacement,
            release.Any(e => e is MoveFailed { Reason: MoveFailureReason.TargetUnavailable }));
        Assert.Contains(release, e => e is ChargeReleased);
        Assert.False(source.IsCharging);
        Assert.Equal(9, charged.Pp);
    }

    [Fact]
    public void RandomTarget_DrawsOnlyWhenReleaseMaterializesTargets()
    {
        BattleMove charged = Move("random_charge", target: MoveTarget.RandomOpponent, charge: new());
        BattleCreature source = Creature("source", 100, charged);
        BattleCreature ally = Creature("ally", 90, Inert());
        BattleCreature enemy0 = Creature("enemy0", 1, Inert());
        BattleCreature enemy1 = Creature("enemy1", 1, Inert());
        var rng = new CountingRng();
        var battle = new BattleController([source, ally], [enemy0, enemy1], BattleTopology.Doubles,
            [0, 1], [0, 1], Chart(), rng);

        battle.ResolveTurn(Doubles(new UseMove(0), new Pass()));
        Assert.Equal(0, rng.Calls);

        battle.ResolveTurn(Doubles(new Pass(), new Pass()));
        Assert.Equal(3, rng.Calls);
    }

    [Fact]
    public void ForcedSwitch_CancelsChargeAndQueuedRelease()
    {
        BattleMove charged = Move("charged", charge: new());
        BattleMove force = new(EntityId.Parse("move:force"), Normal, DamageClass.Status,
            null, null, 10, 0, 0, secondaryEffects: [new ForceSwitchEffect()]);
        BattleCreature source = Creature("source", 100, charged);
        BattleCreature reserve = Creature("reserve", 50, Inert());
        BattleCreature target = Creature("target", 1, force);
        var battle = new BattleController([source, reserve], [target], Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.False(source.IsCharging);
        Assert.Empty(battle.IntentQueueSnapshot);
        Assert.Same(reserve, battle.Active(BattleSide.Player));
        Assert.Contains(events, e => e is ChargeCancelled);
    }

    [Fact]
    public void FailedRelease_CancelsChargeWithoutSecondPpSpend()
    {
        BattleMove charged = Move("gated_charge", charge: new(), effects:
            [new MoveGateEffect(MoveGateKind.FirstAction)]);
        BattleCreature source = Creature("source", 100, charged);
        BattleCreature target = Creature("target", 1, Inert());
        var battle = Battle(source, target);
        battle.ResolveTurn(new UseMove(0), new Pass());

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new Pass(), new Pass());

        Assert.False(source.IsCharging);
        Assert.Equal(9, charged.Pp);
        Assert.Contains(events, e => e is ChargeCancelled);
        Assert.Contains(events, e => e is MoveFailed { Reason: MoveFailureReason.FirstActionOnly });
        Assert.DoesNotContain(events, e => e is ChargeReleased);
    }

    [Fact]
    public void StatusInterruption_CancelsQueuedRelease()
    {
        BattleMove charged = Move("status_interrupted", charge: new());
        BattleCreature source = Creature("source", 100, charged);
        var battle = Battle(source, Creature("target", 1, Inert()));
        battle.ResolveTurn(new UseMove(0), new Pass());
        source.SetStatus(PersistentStatus.Sleep, counter: 2);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new Pass(), new Pass());

        Assert.False(source.IsCharging);
        Assert.Empty(battle.IntentQueueSnapshot);
        Assert.Equal(9, charged.Pp);
        Assert.Contains(events, e => e is ChargeCancelled);
    }

    [Fact]
    public void ConfusionSelfHit_CancelsQueuedRelease()
    {
        BattleMove charged = Move("confusion_interrupted", charge: new());
        BattleCreature source = Creature("source", 100, charged);
        var battle = new BattleController(source, Creature("target", 1, Inert()), Chart(),
            new CountingRng());
        battle.ResolveTurn(new UseMove(0), new Pass());
        source.SetConfusion(2);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new Pass(), new Pass());

        Assert.False(source.IsCharging);
        Assert.Empty(battle.IntentQueueSnapshot);
        Assert.Equal(9, charged.Pp);
        Assert.Contains(events, e => e is HurtInConfusion);
        Assert.Contains(events, e => e is ChargeCancelled);
    }

    [Fact]
    public void FlinchBeforeRelease_CancelsQueuedRelease()
    {
        BattleMove charged = Move("flinch_interrupted", charge: new());
        BattleMove flinch = Move("flinch", effects: [new FlinchEffect { Chance = 100 }]);
        BattleCreature source = Creature("source", 1, charged);
        BattleCreature target = Creature("target", 100, flinch);
        var battle = Battle(source, target);
        battle.ResolveTurn(new UseMove(0), new Pass());

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new Pass(), new UseMove(0));

        Assert.False(source.IsCharging);
        Assert.Empty(battle.IntentQueueSnapshot);
        Assert.Equal(9, charged.Pp);
        Assert.Contains(events, e => e is Flinched { Side: BattleSide.Player });
        Assert.Contains(events, e => e is ChargeCancelled);
    }

    [Fact]
    public void FaintAndBattleEnd_CancelChargeAndReleaseIntent()
    {
        BattleMove charged = Move("charged", charge: new());
        BattleMove knockout = Move("knockout", power: 10_000);
        BattleCreature source = Creature("source", 100, charged);
        BattleCreature target = Creature("target", 1, knockout);
        var battle = Battle(source, target);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(source.IsFainted);
        Assert.False(source.IsCharging);
        Assert.Empty(battle.IntentQueueSnapshot);
        Assert.NotNull(battle.Outcome);
        Assert.Contains(events, e => e is ChargeCancelled);
        Assert.Contains(battle.Trace, entry => entry.Kind == EffectTraceKind.IntentCancelled
            && entry.IntentPayload == BattleIntentPayloadKind.ReleaseMove);
    }

    [Fact]
    public void ChargeLifecycle_MatchesGolden()
    {
        BattleMove charged = Move("golden_charge", charge: new(SemiInvulnerableState.Air));
        BattleCreature source = Creature("source", 100, charged);
        BattleCreature target = Creature("target", 1, Inert());
        var battle = Battle(source, target);

        IReadOnlyList<BattleEvent> first = battle.ResolveTurn(new UseMove(0), new Pass());
        IReadOnlyList<BattleEvent> second = battle.ResolveTurn(new Pass(), new Pass());
        string actual = string.Join('\n',
        [
            .. first.Concat(second).Where(e => e is Charging or ChargeReleased or MoveUsed or DamageDealt)
                .Select(e => $"event:{e.GetType().Name}"),
            .. battle.Trace.Where(entry => entry.Kind is EffectTraceKind.IntentEnqueued
                    or EffectTraceKind.IntentConsumed or EffectTraceKind.Charge)
                .Select(entry => $"trace:{entry.Kind}:{entry.IntentPayload?.ToString() ?? "-"}:{entry.Value}"),
            $"pp:{charged.Pp}",
        ]);

        Assert.Equal(Golden("charge"), actual);
    }

    [Theory]
    [InlineData(SemiInvulnerableState.Air)]
    [InlineData(SemiInvulnerableState.Underground)]
    [InlineData(SemiInvulnerableState.Underwater)]
    [InlineData(SemiInvulnerableState.Vanished)]
    public void SemiInvulnerability_BlocksOrdinaryMoveWithoutRng(SemiInvulnerableState state)
    {
        BattleCreature source = Creature("source", 100, Move("charge", charge: new(state)));
        BattleCreature target = Creature("target", 1, Move("ordinary"));
        var rng = new CountingRng();
        var battle = new BattleController(source, target, Chart(), rng);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(0, rng.Calls);
        Assert.Contains(events, e => e is SemiInvulnerableAvoided { State: var actual } && actual == state);
        Assert.Contains(events, e => e is MoveMissed { Side: BattleSide.Enemy });
    }

    [Fact]
    public void MatchingException_HitsAndAppliesPowerMultiplierAfterAccuracy()
    {
        BattleMove charge = Move("charge", charge: new(SemiInvulnerableState.Underground));
        BattleMove ordinary = Move("ordinary");
        BattleMove unboostedException = Move("unboosted_exception", effects:
            [new SemiInvulnerableHitEffect(new HashSet<SemiInvulnerableState>
                { SemiInvulnerableState.Underground })]);
        BattleMove boostedException = Move("boosted_exception", effects:
            [new SemiInvulnerableHitEffect(new HashSet<SemiInvulnerableState>
                { SemiInvulnerableState.Underground }, new Fraction(2, 1))]);

        int blockedDamage = DamageAgainstChargingTarget(charge, ordinary);
        int unboostedDamage = DamageAgainstChargingTarget(
            Move("charge_copy_1", charge: new(SemiInvulnerableState.Underground)), unboostedException);
        int boostedDamage = DamageAgainstChargingTarget(
            Move("charge_copy_2", charge: new(SemiInvulnerableState.Underground)), boostedException);

        Assert.Equal(0, blockedDamage);
        Assert.True(unboostedDamage > 0);
        Assert.True(boostedDamage > unboostedDamage);
    }

    [Fact]
    public void MatchingException_RunsAccuracyBeforeBasePower()
    {
        BattleMove charge = Move("charge", charge: new(SemiInvulnerableState.Air));
        BattleMove attack = new(EntityId.Parse("move:ordered_exception"), Normal, DamageClass.Physical,
            40, 100, 10, 0, 0, secondaryEffects:
            [new SemiInvulnerableHitEffect(new HashSet<SemiInvulnerableState>
                { SemiInvulnerableState.Air }, new Fraction(2, 1))]);
        BattleCreature charging = Creature("charging", 100, charge);
        BattleCreature attacker = Creature("attacker", 1, attack);
        var battle = Battle(charging, attacker);

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        BattleQueryId[] queryOrder = battle.QueryTrace
            .Where(entry => entry.SourceSlot.Side == BattleSide.Enemy
                && entry.Result.Query is BattleQueryId.Accuracy or BattleQueryId.BasePower)
            .Select(entry => entry.Result.Query)
            .ToArray();
        Assert.Equal([BattleQueryId.Accuracy, BattleQueryId.BasePower], queryOrder);
    }

    [Fact]
    public void SmartAi_UsesForcedReleaseAndAvoidsKnownSemiInvulnerableMiss()
    {
        BattleMove charge = Move("charge", charge: new(SemiInvulnerableState.Underwater));
        BattleMove ordinary = Move("ordinary");
        BattleMove exception = Move("exception", effects:
            [new SemiInvulnerableHitEffect(new HashSet<SemiInvulnerableState>
                { SemiInvulnerableState.Underwater })]);
        BattleCreature chargingAi = Creature("charging_ai", 100, charge, ordinary);
        chargingAi.StartCharging(0, SemiInvulnerableState.Underwater);
        BattleCreature target = Creature("target", 1, ordinary);

        SmartAiDecision forced = SmartAi.ChooseAction(new SmartAiContext(
            [chargingAi], 0, [target], 0, Chart(), new Rng(1)));
        Assert.Equal(new UseMove(0), forced.Action);
        Assert.Contains(forced.Scores.Single().Components, component => component.Name == "chargeRelease");

        BattleCreature attacker = Creature("attacker", 100, ordinary, exception);
        target.StartCharging(0, SemiInvulnerableState.Underwater);
        SmartAiDecision choice = SmartAi.ChooseAction(new SmartAiContext(
            [attacker], 0, [target], 0, Chart(), new Rng(1)));
        Assert.Equal(new UseMove(1), choice.Action);
    }

    private static int DamageAgainstChargingTarget(BattleMove charge, BattleMove attack)
    {
        BattleCreature charging = Creature("charging", 100, charge);
        BattleCreature attacker = Creature("attacker", 1, attack);
        var battle = Battle(charging, attacker);
        int before = charging.CurrentHp;
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        return before - charging.CurrentHp;
    }

    private static BattleTurnActions Doubles(BattleAction source, BattleAction enemy0,
        BattleActionSelection? selection = null) => new(BattleTopology.Doubles,
    [
        new BattleActionSubmission(new(BattleSide.Player, 0), source, selection),
        new BattleActionSubmission(new(BattleSide.Player, 1), new Pass()),
        new BattleActionSubmission(new(BattleSide.Enemy, 0), enemy0),
        new BattleActionSubmission(new(BattleSide.Enemy, 1), new Pass()),
    ]);

    private static BattleController Battle(BattleCreature player, BattleCreature enemy) =>
        new(player, enemy, Chart(), new Rng(1));

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private static BattleCreature Creature(string slug, int speed, params BattleMove[] moves) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [Normal],
            new Stats(1000, 100, 100, 100, 100, speed), moves);

    private static BattleMove Inert() => new(EntityId.Parse("move:inert"), Normal,
        DamageClass.Status, null, null, 20, 0, 0);

    private static BattleMove Move(string slug, DamageClass damageClass = DamageClass.Physical,
        int? power = 40, MoveTarget target = MoveTarget.Selected, ChargeMoveEffect? charge = null,
        IReadOnlyList<MoveEffect>? effects = null) =>
        new(EntityId.Parse($"move:{slug}"), Normal, damageClass, power, null, 10, 0, 0,
            target: target, secondaryEffects: effects, charge: charge);

    private static BattleMove Compile(params Effect[] effects) => MoveCompiler.ToBattleMove(new Move
    {
        Id = EntityId.Parse("move:compiled_charge"),
        Name = "Compiled Charge",
        Type = Normal,
        DamageClass = DamageClass.Physical,
        Power = 40,
        Accuracy = 100,
        Pp = 10,
        Effects = effects,
    });

    private static BattleMove CompileTarget(MoveTarget target, params Effect[] effects) =>
        MoveCompiler.ToBattleMove(new Move
        {
            Id = EntityId.Parse("move:compiled_charge_target"),
            Name = "Compiled Charge Target",
            Type = Normal,
            DamageClass = DamageClass.Physical,
            Power = 40,
            Accuracy = 100,
            Pp = 10,
            Target = target,
            Effects = effects,
        });

    private static Effect Op(string op, params (string Key, object Value)[] values) => new()
    {
        Op = op,
        Params = values.Length == 0 ? null : values.ToDictionary(value => value.Key,
            value => JsonSerializer.SerializeToElement(value.Value)),
    };

    private static string Golden(string name) => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Battle", "Goldens", $"{name}.golden"))
        .Replace("\r\n", "\n").TrimEnd();

    private sealed class CountingRng : IRng
    {
        public int Calls { get; private set; }
        public int Next(int maxExclusive) { Calls++; return 0; }
        public int Next(int minInclusive, int maxExclusive) { Calls++; return minInclusive; }
        public double NextDouble() { Calls++; return 0; }
    }
}
