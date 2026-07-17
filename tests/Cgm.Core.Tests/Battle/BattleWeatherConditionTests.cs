using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleWeatherConditionTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Water = EntityId.Parse("type:water");
    private static readonly EntityId Fire = EntityId.Parse("type:fire");
    private static readonly EntityId Rock = EntityId.Parse("type:rock");

    private static TypeChart Chart() => new([
        new TypeDef { Id = Normal }, new TypeDef { Id = Water },
        new TypeDef { Id = Fire }, new TypeDef { Id = Rock },
    ]);

    private static BattleMove Inert(string slug = "inert") =>
        new(EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 30, 0, 0);

    private static BattleMove WeatherMove(Weather weather, string slug) =>
        new(EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 30, 0, 0,
            setsWeather: weather);

    private static BattleMove Hit(EntityId type, int power, string slug) =>
        new(EntityId.Parse($"move:{slug}"), type, DamageClass.Special, power, 100, 30, 0, 0);

    private static BattleCreature Creature(string slug, int speed, IReadOnlyList<EntityId>? types = null,
        params BattleMove[] moves) => new(EntityId.Parse($"species:{slug}"), slug, 50, types ?? [Normal],
            new Stats(320, 100, 100, 100, 100, speed), moves);

    [Fact]
    public void Registry_LocksEveryWeatherRow()
    {
        Assert.Equal(4, WeatherConditions.Definitions.Count);
        Assert.All(WeatherConditions.Definitions, definition =>
        {
            Assert.Equal(BattleConditionScope.Weather, definition.Scope);
            Assert.Equal("weather", definition.StackingKey);
            Assert.Equal(BattleConditionStackingPolicy.Replace, definition.StackingPolicy);
            Assert.Equal(WeatherConditions.DefaultTurns, definition.DefaultDuration);
            Assert.Equal(BattleIntentCheckpoint.TurnEnd, definition.DurationCheckpoint);
            Assert.Equal(BattleConditionSwitchPolicy.StayScope, definition.SwitchPolicy);
            Assert.Equal(BattleConditionFaintPolicy.Persist, definition.FaintPolicy);
            Assert.Single(definition.Tags);
        });
        Assert.Equal([BattleConditionHook.AccuracyQuery, BattleConditionHook.ChargeStart,
                BattleConditionHook.MoveTypeQuery, BattleConditionHook.BasePowerQuery,
                BattleConditionHook.DamageQuery, BattleConditionHook.HealingQuery],
            WeatherConditions.For(Weather.Rain).Definition!.Hooks);
        Assert.Equal([BattleConditionHook.AccuracyQuery, BattleConditionHook.ChargeStart,
                BattleConditionHook.MoveTypeQuery, BattleConditionHook.BasePowerQuery,
                BattleConditionHook.DamageQuery, BattleConditionHook.HealingQuery,
                BattleConditionHook.StatusAttempt],
            WeatherConditions.For(Weather.Sun).Definition!.Hooks);
        Assert.Equal([BattleConditionHook.HealingQuery, BattleConditionHook.ChargeStart, BattleConditionHook.MoveTypeQuery,
                BattleConditionHook.BasePowerQuery, BattleConditionHook.TurnEnd],
            WeatherConditions.For(Weather.Sandstorm).Definition!.Hooks);
        Assert.Equal([BattleConditionHook.AccuracyQuery, BattleConditionHook.ChargeStart, BattleConditionHook.MoveTypeQuery,
                BattleConditionHook.BasePowerQuery, BattleConditionHook.HealingQuery, BattleConditionHook.TurnEnd],
            WeatherConditions.For(Weather.Hail).Definition!.Hooks);
        Assert.Equal([PersistentStatus.Freeze], WeatherConditions.For(Weather.Sun).BlockedStatuses);
        Assert.All([Weather.Rain, Weather.Sandstorm, Weather.Hail], weather =>
            Assert.Empty(WeatherConditions.For(weather).BlockedStatuses));
    }

    [Fact]
    public void Start_CapturesSourceAndEmitsTypedState()
    {
        var battle = new BattleController(
            Creature("source", 100, moves: [WeatherMove(Weather.Rain, "wet_field")]),
            Creature("target", 1, moves: [Inert()]), Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        BattleConditionInstance condition = Assert.Single(battle.ConditionSnapshot);
        Assert.Equal(new BattleConditionId("weather:rain"), condition.Definition.Id);
        Assert.Equal(new BattleConditionSource(new BattleSlot(BattleSide.Player, 0), 0), condition.Source);
        Assert.Equal(4, condition.RemainingDuration);
        Assert.Contains(events, entry => entry is ConditionApplied);
        Assert.Contains(events, entry => entry is WeatherChanged { Weather: Weather.Rain });
        Assert.Equal(BattleConditionTraceKind.Applied, battle.ConditionTrace[0].Kind);
    }

    [Fact]
    public void Resolver_WeatherMoveChangesEffectiveTypeAndBasePower()
    {
        WeatherMoveEffect interaction = WeatherInteraction(
            types: [(Weather.Rain, Water)], power: [(Weather.Rain, new Fraction(2, 1))]);
        BattleMove hit = new(EntityId.Parse("move:weather_strike"), Normal, DamageClass.Special,
            50, 100, 30, 0, 0, secondaryEffects: [interaction]);
        var battle = new BattleController(
            Creature("source", 100, moves: [WeatherMove(Weather.Rain, "wet_field"), hit]),
            Creature("target", 1, [Fire], Inert()), Chart(), new Rng(1));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        battle.ResolveTurn(new UseMove(1), new UseMove(0));

        Assert.Equal(Water, Assert.Single(battle.ActionHistory.DamageSnapshot()).DamageType);
        Assert.Equal(100, battle.QueryTrace.Last(entry => entry.Result.Query == BattleQueryId.BasePower
            && entry.SourceSlot.Side == BattleSide.Player).Result.FinalValue.ToInt32());
        Assert.Contains(battle.HookTrace, entry => entry.Checkpoint == BattleConditionHook.MoveTypeQuery);
        Assert.Contains(battle.HookTrace, entry => entry.Checkpoint == BattleConditionHook.BasePowerQuery);
    }

    [Fact]
    public void WeatherMove_AbsentAndUnlistedWeatherAreNeutral()
    {
        WeatherMoveEffect interaction = WeatherInteraction(
            types: [(Weather.Rain, Water)], power: [(Weather.Rain, new Fraction(2, 1))],
            skipCharge: [Weather.Rain]);

        Assert.Empty(WeatherConditions.CollectMoveTypeHooks([], interaction, 0).MoveTypes());
        Assert.Empty(WeatherConditions.CollectBasePowerHooks([], interaction, 0)
            .QueryModifiers(BattleQueryId.BasePower));
        Assert.Empty(WeatherConditions.CollectChargeHooks([], interaction, 0).Filters());
        Assert.Empty(WeatherConditions.CollectMoveTypeHooks(StoresWith(Weather.Sun).Snapshot(), interaction, 0)
            .MoveTypes());
        WeatherMoveEffect hailSkip = WeatherInteraction(skipCharge: [Weather.Hail]);
        Assert.Contains(WeatherConditions.CollectChargeHooks(StoresWith(Weather.Hail).Snapshot(), hailSkip, 0)
            .Filters(), filter => filter.Decision == BattleHookFilterDecision.Deny);
    }

    [Fact]
    public void WeatherMove_AddsNoRngDraws()
    {
        static int HitDraws(bool weatherSensitive)
        {
            var rng = new CountingRng();
            WeatherMoveEffect interaction = WeatherInteraction(power: [(Weather.Rain, new Fraction(2, 1))]);
            BattleMove hit = new(EntityId.Parse("move:weather_strike"), Normal, DamageClass.Special,
                50, 100, 30, 0, 0, secondaryEffects: weatherSensitive ? [interaction] : []);
            var battle = new BattleController(
                Creature("source", 100, moves: [WeatherMove(Weather.Rain, "wet_field"), hit]),
                Creature("target", 1, moves: [Inert()]), Chart(), rng);
            battle.ResolveTurn(new UseMove(0), new UseMove(0));
            int before = rng.Calls;
            battle.ResolveTurn(new UseMove(1), new UseMove(0));
            return rng.Calls - before;
        }

        Assert.Equal(HitDraws(false), HitDraws(true));
    }

    [Fact]
    public void Resolver_DoublesUsesTheSameWeatherMoveRowsPerTarget()
    {
        WeatherMoveEffect interaction = WeatherInteraction(
            types: [(Weather.Rain, Water)], power: [(Weather.Rain, new Fraction(2, 1))]);
        BattleMove spread = new(EntityId.Parse("move:weather_spread"), Normal, DamageClass.Special,
            50, 100, 30, 0, 0, target: MoveTarget.AllOpponents, secondaryEffects: [interaction]);
        var battle = new BattleController(
            [Creature("source", 100, moves: [WeatherMove(Weather.Rain, "wet_field"), spread]),
                Creature("ally", 90, moves: [Inert("ally_move")])],
            [Creature("target_zero", 10, [Fire], Inert("target_zero_move")),
                Creature("target_one", 1, [Fire], Inert("target_one_move"))],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new Rng(1));
        battle.ResolveTurn(DoublesActions(new UseMove(0),
            new ActiveSlotSelection(new BattleSlot(BattleSide.Enemy, 0))));

        battle.ResolveTurn(DoublesActions(new UseMove(1)));

        Assert.Equal(2, battle.ActionHistory.DamageSnapshot().Count(record => record.DamageType == Water));
        Assert.Equal(2, battle.QueryTrace.Count(entry => entry.Result.Query == BattleQueryId.BasePower
            && entry.SourceSlot.Side == BattleSide.Player && entry.Result.FinalValue.ToInt32() == 100));
    }

    [Fact]
    public void Resolver_SunSkipsChargeWhileRainRetainsChargeAndHalvesReleasePower()
    {
        WeatherMoveEffect interaction = WeatherInteraction(
            power: [(Weather.Rain, new Fraction(1, 2))], skipCharge: [Weather.Sun]);
        BattleMove charged = new(EntityId.Parse("move:solar_strike"), Normal, DamageClass.Special,
            120, 100, 10, 0, 0, chargeTurn: true, secondaryEffects: [interaction]);
        BattleCreature sunnySource = Creature("sun_source", 100,
            moves: [WeatherMove(Weather.Sun, "bright_field"), charged]);
        BattleCreature sunnyTarget = Creature("sun_target", 1, moves: [Inert()]);
        var sunny = new BattleController(sunnySource, sunnyTarget, Chart(), new Rng(1));
        sunny.ResolveTurn(new UseMove(0), new UseMove(0));
        int sunnyBefore = sunnyTarget.CurrentHp;

        sunny.ResolveTurn(new UseMove(1), new UseMove(0));

        Assert.False(sunnySource.IsCharging);
        Assert.True(sunnyTarget.CurrentHp < sunnyBefore);
        Assert.Contains(sunny.HookTrace, entry => entry.Checkpoint == BattleConditionHook.ChargeStart);

        BattleMove rainyCharge = new(EntityId.Parse("move:rain_charge"), Normal, DamageClass.Special,
            120, 100, 10, 0, 0, chargeTurn: true, secondaryEffects: [interaction]);
        BattleCreature rainySource = Creature("rain_source", 100,
            moves: [WeatherMove(Weather.Rain, "wet_field"), rainyCharge]);
        BattleCreature rainyTarget = Creature("rain_target", 1, moves: [Inert()]);
        var rainy = new BattleController(rainySource, rainyTarget, Chart(), new Rng(1));
        rainy.ResolveTurn(new UseMove(0), new UseMove(0));
        rainy.ResolveTurn(new UseMove(1), new UseMove(0));
        Assert.True(rainySource.IsCharging);

        rainy.ResolveTurn(new UseMove(1), new UseMove(0));

        Assert.False(rainySource.IsCharging);
        Assert.Equal(60, rainy.QueryTrace.Last(entry => entry.Result.Query == BattleQueryId.BasePower
            && entry.SourceSlot.Side == BattleSide.Player).Result.FinalValue.ToInt32());
    }

    [Fact]
    public void SmartAi_UsesWeatherMoveTypeAndPowerHooks()
    {
        WeatherMoveEffect interaction = WeatherInteraction(
            types: [(Weather.Rain, Water)], power: [(Weather.Rain, new Fraction(2, 1))]);
        BattleMove weatherHit = new(EntityId.Parse("move:weather_strike"), Normal, DamageClass.Special,
            50, 100, 30, 0, 0, secondaryEffects: [interaction]);
        BattleCreature attacker = Creature("ai", 100, moves: [weatherHit, Hit(Normal, 70, "reliable")]);
        BattleCreature target = Creature("ai_target", 1, [Fire], Inert());
        var context = new SmartAiContext([attacker], 0, [target], 0, Chart(), new Rng(1),
            Weights: new SmartAiWeights { NoiseFraction = 0 }, Conditions: StoresWith(Weather.Rain).Snapshot());

        SmartAiDecision decision = SmartAi.ChooseAction(context);

        Assert.Equal(new UseMove(0), decision.Action);
        Assert.True(decision.Scores.Single(score => score.Action == new UseMove(0)).Score
            > decision.Scores.Single(score => score.Action == new UseMove(1)).Score);
    }

    [Fact]
    public void SameWeather_IsNoOpAndDoesNotRestartDuration()
    {
        var battle = new BattleController(
            Creature("source", 100, moves: [WeatherMove(Weather.Rain, "wet_field")]),
            Creature("target", 1, moves: [Inert()]), Chart(), new Rng(1));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(3, Assert.Single(battle.ConditionSnapshot).RemainingDuration);
        Assert.DoesNotContain(events, entry => entry is WeatherChanged or ConditionReplaced or ConditionRefreshed);
        Assert.Single(battle.ConditionTrace, entry => entry.Kind == BattleConditionTraceKind.Applied);
    }

    [Fact]
    public void SameWeatherAbility_IsNoOpAndDoesNotRestartDuration()
    {
        var reserve = new BattleCreature(EntityId.Parse("species:reserve"), "reserve", 50, [Normal],
            new Stats(320, 100, 100, 100, 100, 80), [Inert("reserve")], abilityHooks:
        [
            new AbilityHook
            {
                Hook = AbilityHookPoint.OnSwitchIn,
                Effects = [new Effect { Op = "weatherSummon", Params = Params(("weather", "rain")) }],
            },
        ]);
        var battle = new BattleController(
            [Creature("lead", 100, moves: [WeatherMove(Weather.Rain, "wet_field")]), reserve],
            [Creature("target", 1, moves: [Inert()])], Chart(), new Rng(1));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new Switch(1), new UseMove(0));

        Assert.Equal(3, Assert.Single(battle.ConditionSnapshot).RemainingDuration);
        Assert.DoesNotContain(events, entry => entry is WeatherChanged or ConditionReplaced or ConditionRefreshed);
    }

    [Fact]
    public void DifferentWeather_ReplacesInstanceAndSource()
    {
        var player = Creature("source", 100, moves:
            [WeatherMove(Weather.Rain, "wet_field"), WeatherMove(Weather.Sun, "bright_field")]);
        var battle = new BattleController(player, Creature("target", 1, moves: [Inert()]), Chart(), new Rng(1));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        long firstSequence = Assert.Single(battle.ConditionSnapshot).Sequence;

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(1), new UseMove(0));

        BattleConditionInstance replacement = Assert.Single(battle.ConditionSnapshot);
        Assert.Equal(Weather.Sun, battle.CurrentWeather);
        Assert.True(replacement.Sequence > firstSequence);
        Assert.Equal(4, replacement.RemainingDuration);
        Assert.Contains(events, entry => entry is ConditionReplaced { ReplacedSequence: var old } && old == firstSequence);
        Assert.Contains(events, entry => entry is WeatherChanged { Weather: Weather.Sun });
    }

    [Fact]
    public void DurationOne_RunsCheckpointThenExpires()
    {
        var reserve = new BattleCreature(EntityId.Parse("species:summoner"), "summoner", 50, [Normal],
            new Stats(320, 100, 100, 100, 100, 80), [Inert("reserve")], abilityHooks:
        [
            new AbilityHook
            {
                Hook = AbilityHookPoint.OnSwitchIn,
                Effects = [new Effect { Op = "weatherSummon", Params = Params(("weather", "rain"), ("duration", 1)) }],
            },
        ]);
        var battle = new BattleController(
            [Creature("lead", 100, moves: [Inert("lead")]), reserve],
            [Creature("target", 1, moves: [Inert()])], Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new Switch(1), new UseMove(0));

        Assert.Empty(battle.ConditionSnapshot);
        Assert.Equal(Weather.None, battle.CurrentWeather);
        Assert.Contains(events, entry => entry is ConditionApplied);
        Assert.Contains(events, entry => entry is ConditionExpired);
        Assert.Contains(events, entry => entry is WeatherEnded { Weather: Weather.Rain });
        Assert.Equal(1, battle.ConditionTrace.Count(entry => entry.Kind == BattleConditionTraceKind.Ticked));
    }

    [Fact]
    public void BattleEnd_RemovesWeatherThroughConditionCleanup()
    {
        var exit = new BattleMove(EntityId.Parse("move:exit"), Normal, DamageClass.Physical,
            1, 100, 5, 0, 0, selfDestruct: true);
        var battle = new BattleController(
            Creature("source", 100, moves: [WeatherMove(Weather.Rain, "wet_field")]),
            Creature("target", 1, moves: [exit]), Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Empty(battle.ConditionSnapshot);
        Assert.Contains(events, entry => entry is ConditionRemoved { Reason: BattleConditionCleanupReason.BattleEnd });
        Assert.Contains(battle.ConditionTrace, entry => entry is
            { Kind: BattleConditionTraceKind.Removed, CleanupReason: BattleConditionCleanupReason.BattleEnd });
    }

    [Theory]
    [InlineData(Weather.Rain, "water", 3, 2)]
    [InlineData(Weather.Rain, "fire", 1, 2)]
    [InlineData(Weather.Sun, "fire", 3, 2)]
    [InlineData(Weather.Sun, "water", 1, 2)]
    public void DamageQuery_UsesTypedHook(Weather weather, string type, int numerator, int denominator)
    {
        BattleConditionStores stores = StoresWith(weather);

        BattleHookDispatchSnapshot snapshot = WeatherConditions.CollectDamageHooks(
            stores.Snapshot(), type, actionSequence: 7);

        BattleQueryModifier modifier = Assert.Single(snapshot.QueryModifiers(BattleQueryId.FinalDamage));
        Assert.Equal(new BattleQueryValue(numerator, denominator), modifier.Operand);
        Assert.Equal(BattleQueryOwnerScope.Field, modifier.OwnerScope);
        Assert.Equal(BattleConditionHook.DamageQuery, Assert.Single(snapshot.Trace).Checkpoint);
    }

    [Fact]
    public void DamageQuery_NonMatchingTypeHasNoRegistration()
    {
        BattleHookDispatchSnapshot snapshot = WeatherConditions.CollectDamageHooks(
            StoresWith(Weather.Rain).Snapshot(), "normal", actionSequence: 0);

        Assert.Empty(snapshot.Invocations);
        Assert.Empty(snapshot.Trace);
    }

    [Theory]
    [InlineData(Weather.Rain)]
    [InlineData(Weather.Hail)]
    public void AccuracyQuery_BypassSkipsStagesAndDraw(Weather weather)
    {
        WeatherAccuracyEffect effect = AccuracyEffect([weather]);
        BattleHookDispatchSnapshot snapshot = WeatherConditions.CollectAccuracyHooks(
            StoresWith(weather).Snapshot(), effect, actionSequence: 3);

        BattleHookFilter filter = Assert.Single(snapshot.Filters());
        Assert.Equal("accuracy_bypass", filter.Filter.Value);
        Assert.Equal(BattleHookFilterDecision.Allow, filter.Decision);
        Assert.Equal(new BattleQueryValue(100), Assert.Single(snapshot.QueryModifiers(BattleQueryId.Accuracy)).Operand);
        Assert.All(snapshot.Trace, entry => Assert.Equal(BattleConditionHook.AccuracyQuery, entry.Checkpoint));
        Assert.Equal(2, snapshot.Trace.Count);
    }

    [Theory]
    [InlineData(0, 0, 50)]
    [InlineData(6, 0, 100)]
    [InlineData(-6, 0, 16)]
    public void AccuracyQuery_OverrideReplacesAuthoredBaseBeforeStages(int accuracyStage, int evasionStage, int expected)
    {
        BattleHookDispatchSnapshot snapshot = WeatherConditions.CollectAccuracyHooks(
            StoresWith(Weather.Sun).Snapshot(), AccuracyEffect([], (Weather.Sun, 50)), actionSequence: 4);
        var modifiers = snapshot.QueryModifiers(BattleQueryId.Accuracy).ToList();
        modifiers.Add(new BattleQueryModifier(BattleQueryStage.SourceTargetState, BattleQueryOperation.Multiply,
            BattleQuery.AccuracyStageMultiplier(accuracyStage, evasionStage), InsertionOrder: modifiers.Count));

        Assert.Equal(expected, BattleQuery.ResolveInteger(BattleQueryId.Accuracy, 70, modifiers));
    }

    [Fact]
    public void AccuracyQuery_AbsentOrUnlistedWeatherHasNoRegistration()
    {
        WeatherAccuracyEffect effect = AccuracyEffect([Weather.Rain], (Weather.Sun, 50));
        var empty = new BattleConditionStores(new BattleConditionRegistry(WeatherConditions.Definitions));

        Assert.Empty(WeatherConditions.CollectAccuracyHooks(empty.Snapshot(), effect, 0).Invocations);
        Assert.Empty(WeatherConditions.CollectAccuracyHooks(
            StoresWith(Weather.Sandstorm).Snapshot(), effect, 0).Invocations);
    }

    [Fact]
    public void StatusAttempt_SunDeniesFreezeThroughTypedHook()
    {
        BattleHookDispatchSnapshot snapshot = WeatherConditions.CollectStatusHooks(
            StoresWith(Weather.Sun).Snapshot(), PersistentStatus.Freeze, actionSequence: 5);

        BattleHookFilter filter = Assert.Single(snapshot.Filters());
        Assert.Equal("status_attempt", filter.Filter.Value);
        Assert.Equal(BattleHookFilterDecision.Deny, filter.Decision);
        Assert.Equal(BattleConditionHook.StatusAttempt, Assert.Single(snapshot.Trace).Checkpoint);
    }

    [Theory]
    [InlineData(Weather.None, PersistentStatus.Freeze)]
    [InlineData(Weather.Rain, PersistentStatus.Freeze)]
    [InlineData(Weather.Sun, PersistentStatus.Burn)]
    [InlineData(Weather.Sandstorm, PersistentStatus.Freeze)]
    [InlineData(Weather.Hail, PersistentStatus.Freeze)]
    public void StatusAttempt_AbsentOrUnlistedRowsDoNotRegister(Weather weather, PersistentStatus status)
    {
        IReadOnlyList<BattleConditionInstance> conditions = weather == Weather.None
            ? []
            : StoresWith(weather).Snapshot();

        Assert.Empty(WeatherConditions.CollectStatusHooks(conditions, status, 0).Invocations);
    }

    [Fact]
    public void StatusAttempt_RejectsUnknownStatus()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WeatherConditions.CollectStatusHooks(
            StoresWith(Weather.Sun).Snapshot(), (PersistentStatus)999, 0));
    }

    [Theory]
    [InlineData(Weather.Sun, 2, 3, 67)]
    [InlineData(Weather.Rain, 1, 4, 25)]
    [InlineData(Weather.Sandstorm, 1, 4, 25)]
    [InlineData(Weather.Hail, 1, 4, 25)]
    public void HealingQuery_ReplacesWithTheDirectMaxHpFraction(
        Weather weather, int numerator, int denominator, int expected)
    {
        HealEffect effect = HealingEffect(
            (Weather.Sun, 2, 3), (Weather.Rain, 1, 4),
            (Weather.Sandstorm, 1, 4), (Weather.Hail, 1, 4));

        BattleHookDispatchSnapshot snapshot = WeatherConditions.CollectHealingHooks(
            StoresWith(weather).Snapshot(), effect, maxHp: 101, actionSequence: 6);

        BattleQueryModifier modifier = Assert.Single(snapshot.QueryModifiers(BattleQueryId.Healing));
        Assert.Equal(BattleQueryOperation.Replace, modifier.Operation);
        Assert.Equal(new BattleQueryValue(expected), modifier.Operand);
        Assert.Equal(BattleQueryOwnerScope.Field, modifier.OwnerScope);
        Assert.Equal(BattleConditionHook.HealingQuery, Assert.Single(snapshot.Trace).Checkpoint);
        Assert.Equal(expected, EffectMath.HealAmount(101, numerator, denominator));
    }

    [Fact]
    public void HealingQuery_AbsentOrUnlistedWeatherHasNoRegistration()
    {
        HealEffect effect = HealingEffect((Weather.Sandstorm, 2, 3));

        Assert.Empty(WeatherConditions.CollectHealingHooks([], effect, 101, 0).Invocations);
        Assert.Empty(WeatherConditions.CollectHealingHooks(
            StoresWith(Weather.Rain).Snapshot(), effect, 101, 0).Invocations);
    }

    [Fact]
    public void HealingQuery_RejectsNonPositiveMaximumHp()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WeatherConditions.CollectHealingHooks(
            StoresWith(Weather.Sun).Snapshot(), HealingEffect((Weather.Sun, 2, 3)), 0, 0));
    }

    [Fact]
    public void Resolver_SunHealingUsesDirectRoundingAndDrawsNoRng()
    {
        var rng = new CountingRng();
        BattleCreature source = CreatureWithHp("source", 101, 100,
            WeatherMove(Weather.Sun, "bright_field"),
            HealingMove("weather_recovery", HpFractionRecipient.Self, (Weather.Sun, 2, 3)));
        source.TakeDamage(100);
        var battle = new BattleController(source, Creature("target", 1, moves: [Inert()]), Chart(), rng);
        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(1), new UseMove(0));

        Assert.Equal(68, source.CurrentHp);
        Assert.Contains(events, entry => entry is Healed { Side: BattleSide.Player, Amount: 67 });
        Assert.Equal(67, battle.QueryTrace.Last(entry => entry.Result.Query == BattleQueryId.Healing)
            .Result.FinalValue.ToInt32());
        Assert.Contains(battle.HookTrace, entry => entry.Checkpoint == BattleConditionHook.HealingQuery);
        Assert.Equal(0, rng.Calls);
    }

    [Fact]
    public void Resolver_WeatherHealingUsesTheActualTargetRecipient()
    {
        BattleCreature target = CreatureWithHp("target", 101, 1, Inert());
        target.TakeDamage(100);
        var battle = new BattleController(
            Creature("source", 100, moves:
            [
                WeatherMove(Weather.Sun, "bright_field"),
                HealingMove("shared_recovery", HpFractionRecipient.Target, (Weather.Sun, 2, 3)),
            ]), target, Chart(), new Rng(1));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(1), new UseMove(0));

        Assert.Equal(68, target.CurrentHp);
        Assert.Contains(events, entry => entry is Healed
            { Side: BattleSide.Enemy, Slot: { Side: BattleSide.Enemy }, Amount: 67 });
    }

    [Fact]
    public void Resolver_FullHpWeatherHealingEmitsNoHealEvent()
    {
        BattleCreature source = CreatureWithHp("source", 101, 100,
            WeatherMove(Weather.Sun, "bright_field"),
            HealingMove("weather_recovery", HpFractionRecipient.Self, (Weather.Sun, 2, 3)));
        var battle = new BattleController(source, Creature("target", 1, moves: [Inert()]), Chart(), new Rng(1));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(1), new UseMove(0));

        Assert.DoesNotContain(events, entry => entry is Healed);
        Assert.Equal(101, source.CurrentHp);
        Assert.Equal(67, battle.QueryTrace.Last(entry => entry.Result.Query == BattleQueryId.Healing)
            .Result.FinalValue.ToInt32());
    }

    [Fact]
    public void Resolver_UnlistedWeatherKeepsTheAuthoredHealingFraction()
    {
        BattleCreature source = CreatureWithHp("source", 101, 100,
            WeatherMove(Weather.Rain, "wet_field"),
            HealingMove("sand_recovery", HpFractionRecipient.Self, (Weather.Sandstorm, 2, 3)));
        source.TakeDamage(100);
        var battle = new BattleController(source, Creature("target", 1, moves: [Inert()]), Chart(), new Rng(1));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        battle.ResolveTurn(new UseMove(1), new UseMove(0));

        Assert.Equal(51, source.CurrentHp);
        Assert.DoesNotContain(battle.HookTrace, entry => entry.Checkpoint == BattleConditionHook.HealingQuery);
    }

    [Fact]
    public void Resolver_WeatherReplacementImmediatelyChangesHealingFraction()
    {
        BattleCreature source = CreatureWithHp("source", 120, 100,
            WeatherMove(Weather.Sun, "bright_field"), WeatherMove(Weather.Rain, "wet_field"),
            HealingMove("weather_recovery", HpFractionRecipient.Self,
                (Weather.Sun, 2, 3), (Weather.Rain, 1, 4)));
        source.TakeDamage(119);
        var battle = new BattleController(source, Creature("target", 1, moves: [Inert()]), Chart(), new Rng(1));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(1), new UseMove(0));

        battle.ResolveTurn(new UseMove(2), new UseMove(0));

        Assert.Equal(31, source.CurrentHp);
        Assert.Equal(30, battle.QueryTrace.Last(entry => entry.Result.Query == BattleQueryId.Healing)
            .Result.FinalValue.ToInt32());
    }

    [Fact]
    public void Resolver_WeatherExpiryImmediatelyRestoresAuthoredHealingFraction()
    {
        BattleCreature reserve = new(EntityId.Parse("species:reserve"), "reserve", 50, [Normal],
            new Stats(100, 100, 100, 100, 100, 80),
            [HealingMove("weather_recovery", HpFractionRecipient.Self, (Weather.Sun, 2, 3))], abilityHooks:
        [
            new AbilityHook
            {
                Hook = AbilityHookPoint.OnSwitchIn,
                Effects = [new Effect { Op = "weatherSummon", Params = Params(("weather", "sun"), ("duration", 1)) }],
            },
        ]);
        reserve.TakeDamage(99);
        var battle = new BattleController(
            [Creature("lead", 100, moves: [Inert("lead_move")]), reserve],
            [Creature("target", 1, moves: [Inert()])], Chart(), new Rng(1));
        battle.ResolveTurn(new Switch(1), new UseMove(0));
        Assert.Equal(Weather.None, battle.CurrentWeather);

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(51, reserve.CurrentHp);
        Assert.Equal(50, battle.QueryTrace.Last(entry => entry.Result.Query == BattleQueryId.Healing)
            .Result.FinalValue.ToInt32());
    }

    [Fact]
    public void SmartAi_UsesWeatherHealingAndCapsRecoveryAtMissingHp()
    {
        BattleCreature attacker = CreatureWithHp("ai", 100, 100,
            HealingMove("flat_recovery", HpFractionRecipient.Self),
            HealingMove("weather_recovery", HpFractionRecipient.Self, (Weather.Sun, 2, 3)));
        attacker.TakeDamage(60);
        SmartAiDecision clear = SmartAi.ChooseAction(new SmartAiContext(
            [attacker], 0, [Creature("clear_target", 1, moves: [Inert()])], 0, Chart(), new Rng(1),
            Weights: new SmartAiWeights { NoiseFraction = 0 }));
        SmartAiDecision rain = SmartAi.ChooseAction(new SmartAiContext(
            [attacker], 0, [Creature("rain_target", 1, moves: [Inert()])], 0, Chart(), new Rng(1),
            Weights: new SmartAiWeights { NoiseFraction = 0 },
            Conditions: StoresWith(Weather.Rain).Snapshot()));
        SmartAiDecision sun = SmartAi.ChooseAction(new SmartAiContext(
            [attacker], 0, [Creature("target", 1, moves: [Inert()])], 0, Chart(), new Rng(1),
            Weights: new SmartAiWeights { NoiseFraction = 0 },
            Conditions: StoresWith(Weather.Sun).Snapshot()));

        static double Recovery(SmartAiDecision decision) => decision.Scores
            .Single(score => score.Action == new UseMove(1)).Components
            .Single(component => component.Name == "recovery").Value;
        Assert.Equal(50, Recovery(clear));
        Assert.Equal(50, Recovery(rain));
        Assert.Equal(60, Recovery(sun));
        Assert.Equal(new UseMove(1), sun.Action);
    }

    [Fact]
    public void Resolver_SunBlocksFreezeWithoutDrawingStatusChance()
    {
        var rng = new CountingRng();
        BattleCreature target = Creature("target", 1, moves: [Inert()]);
        var battle = new BattleController(
            Creature("source", 100, moves:
                [WeatherMove(Weather.Sun, "bright_field"), StatusMove(PersistentStatus.Freeze, 10, "cold_snap")]),
            target, Chart(), rng);
        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(1), new UseMove(0));

        Assert.Null(target.Status);
        Assert.DoesNotContain(events, entry => entry is StatusApplied);
        Assert.Contains(battle.HookTrace, entry => entry.Checkpoint == BattleConditionHook.StatusAttempt);
        EffectTraceEntry chance = battle.Trace.Last(entry => entry.Kind == EffectTraceKind.EffectChance
            && entry.SourceSlot.Side == BattleSide.Player);
        Assert.False(chance.Performed);
        Assert.Equal(0, rng.Calls);
    }

    [Fact]
    public void Resolver_SunDoesNotBlockOtherPersistentStatus()
    {
        BattleCreature target = Creature("target", 1, moves: [Inert()]);
        var battle = new BattleController(
            Creature("source", 100, moves:
                [WeatherMove(Weather.Sun, "bright_field"), StatusMove(PersistentStatus.Burn, 100, "heat_mark")]),
            target, Chart(), new Rng(1));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        battle.ResolveTurn(new UseMove(1), new UseMove(0));

        Assert.Equal(PersistentStatus.Burn, target.Status);
        Assert.DoesNotContain(battle.HookTrace, entry => entry.Checkpoint == BattleConditionHook.StatusAttempt);
    }

    [Fact]
    public void Resolver_SunDoesNotThawAnAlreadyFrozenReserve()
    {
        BattleCreature reserve = Creature("reserve", 80, moves: [Inert("reserve_move")]);
        reserve.SetStatus(PersistentStatus.Freeze);
        var battle = new BattleController(
            [Creature("source", 100, moves: [WeatherMove(Weather.Sun, "bright_field")]), reserve],
            [Creature("target", 1, moves: [Inert()])], Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(PersistentStatus.Freeze, reserve.Status);
    }

    [Fact]
    public void Resolver_WeatherReplacementImmediatelyAllowsFreeze()
    {
        BattleCreature target = Creature("target", 1, moves: [Inert()]);
        var battle = new BattleController(
            Creature("source", 100, moves:
            [
                WeatherMove(Weather.Sun, "bright_field"), WeatherMove(Weather.Rain, "wet_field"),
                StatusMove(PersistentStatus.Freeze, 100, "cold_snap"),
            ]), target, Chart(), new Rng(1));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(1), new UseMove(0));

        battle.ResolveTurn(new UseMove(2), new UseMove(0));

        Assert.Equal(PersistentStatus.Freeze, target.Status);
    }

    [Fact]
    public void Resolver_WeatherExpiryImmediatelyAllowsFreeze()
    {
        BattleCreature reserve = new(EntityId.Parse("species:reserve"), "reserve", 50, [Normal],
            new Stats(320, 100, 100, 100, 100, 80), [Inert("reserve_move")], abilityHooks:
        [
            new AbilityHook
            {
                Hook = AbilityHookPoint.OnSwitchIn,
                Effects = [new Effect { Op = "weatherSummon", Params = Params(("weather", "sun"), ("duration", 1)) }],
            },
        ]);
        var battle = new BattleController(
            [Creature("lead", 100, moves: [Inert("lead_move")]), reserve],
            [Creature("source", 1, moves: [StatusMove(PersistentStatus.Freeze, 100, "cold_snap")])],
            Chart(), new Rng(1));

        battle.ResolveTurn(new Switch(1), new UseMove(0));
        Assert.Null(reserve.Status);
        Assert.Equal(Weather.None, battle.CurrentWeather);

        battle.ResolveTurn(new Pass(), new UseMove(0));

        Assert.Equal(PersistentStatus.Freeze, reserve.Status);
    }

    [Fact]
    public void SmartAi_RemovesOnlyTheWeatherBlockedStatusComponent()
    {
        BattleCreature attacker = Creature("ai", 100, moves:
        [
            StatusMove(PersistentStatus.Freeze, 100, "cold_snap"),
            StatusMove(PersistentStatus.Burn, 100, "heat_mark"),
        ]);
        BattleCreature target = Creature("ai_target", 1, moves: [Inert()]);
        var weights = new SmartAiWeights { NoiseFraction = 0 };

        SmartAiDecision clear = SmartAi.ChooseAction(new SmartAiContext(
            [attacker], 0, [target], 0, Chart(), new Rng(1), Weights: weights));
        SmartAiDecision sun = SmartAi.ChooseAction(new SmartAiContext(
            [attacker], 0, [target], 0, Chart(), new Rng(1), Weights: weights,
            Conditions: StoresWith(Weather.Sun).Snapshot()));

        Assert.All(clear.Scores, score => Assert.Contains(score.Components, component =>
            component.Name == "status" && component.Value > 0));
        Assert.DoesNotContain(sun.Scores.Single(score => score.Action == new UseMove(0)).Components,
            component => component.Name == "status");
        Assert.Contains(sun.Scores.Single(score => score.Action == new UseMove(1)).Components,
            component => component.Name == "status" && component.Value > 0);
    }

    [Fact]
    public void Resolver_RainBypassIgnoresStagesAndDoesNotDrawAccuracy()
    {
        BattleMove weatherHit = AccuracyHit(60, 70, "storm_hit", AccuracyEffect([Weather.Rain]));
        BattleCreature source = Creature("source", 100, moves:
            [WeatherMove(Weather.Rain, "wet_field"), weatherHit]);
        BattleCreature target = Creature("target", 1, moves: [Inert()]);
        source.SetStage(StatKind.Accuracy, -6);
        target.SetStage(StatKind.Evasion, 6);
        var battle = new BattleController(source, target, Chart(), new CountingRng());
        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        int before = target.CurrentHp;
        battle.ResolveTurn(new UseMove(1), new UseMove(0));

        EffectTraceEntry accuracy = battle.Trace.Last(entry => entry.Kind == EffectTraceKind.Accuracy
            && entry.SourceSlot.Side == BattleSide.Player);
        Assert.True(target.CurrentHp < before);
        Assert.False(accuracy.Performed);
        Assert.Null(accuracy.DrawResult);
        Assert.Contains(battle.HookTrace, entry => entry.Checkpoint == BattleConditionHook.AccuracyQuery);
        Assert.Equal(100, battle.QueryTrace.Last(entry => entry.Result.Query == BattleQueryId.Accuracy
            && entry.SourceSlot.Side == BattleSide.Player).Result.FinalValue.ToInt32());
    }

    [Fact]
    public void Resolver_SunOverrideRetainsOneAccuracyDraw()
    {
        BattleMove weatherHit = AccuracyHit(60, 70, "storm_hit",
            AccuracyEffect([], (Weather.Sun, 50)));
        BattleCreature source = Creature("source", 100, moves:
            [WeatherMove(Weather.Sun, "bright_field"), weatherHit]);
        var battle = new BattleController(source, Creature("target", 1, moves: [Inert()]),
            Chart(), new CountingRng());
        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        battle.ResolveTurn(new UseMove(1), new UseMove(0));

        EffectTraceEntry accuracy = battle.Trace.Last(entry => entry.Kind == EffectTraceKind.Accuracy
            && entry.SourceSlot.Side == BattleSide.Player);
        Assert.True(accuracy.Performed);
        Assert.Equal(0, accuracy.DrawResult);
        Assert.Equal(100, accuracy.DrawBound);
        Assert.Equal(50, battle.QueryTrace.Last(entry => entry.Result.Query == BattleQueryId.Accuracy
            && entry.SourceSlot.Side == BattleSide.Player).Result.FinalValue.ToInt32());
    }

    [Fact]
    public void SmartAi_UsesTheSameWeatherAccuracyHook()
    {
        BattleCreature setter = Creature("setter", 100, moves: [WeatherMove(Weather.Rain, "wet_field")]);
        var weatherBattle = new BattleController(setter, Creature("dummy", 1, moves: [Inert()]), Chart(), new Rng(1));
        weatherBattle.ResolveTurn(new UseMove(0), new UseMove(0));
        BattleMove weatherHit = AccuracyHit(80, 70, "weather_hit", AccuracyEffect([Weather.Rain]));
        BattleCreature attacker = Creature("ai", 100, moves:
            [weatherHit, Hit(Normal, 70, "reliable_hit")]);
        BattleCreature target = Creature("ai_target", 1, moves: [Inert()]);
        var weights = new SmartAiWeights { NoiseFraction = 0 };

        SmartAiDecision clear = SmartAi.ChooseAction(new SmartAiContext(
            [attacker], 0, [target], 0, Chart(), new Rng(2), Weights: weights));
        SmartAiDecision rain = SmartAi.ChooseAction(new SmartAiContext(
            [attacker], 0, [target], 0, Chart(), new Rng(2), Weights: weights,
            Conditions: weatherBattle.ConditionSnapshot));

        Assert.Equal(new UseMove(1), clear.Action);
        Assert.Equal(new UseMove(0), rain.Action);
    }

    [Fact]
    public void Residual_UsesTopologyOrderAndDrawsNoRng()
    {
        var rng = new CountingRng();
        var battle = new BattleController(
            [Creature("p0", 100, moves: [WeatherMove(Weather.Sandstorm, "rough_field")]),
             Creature("p1", 90, [Rock], Inert("p1"))],
            [Creature("e0", 20, moves: [Inert("e0")]), Creature("e1", 10, moves: [Inert("e1")])],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), rng);
        var actions = new BattleTurnActions(battle.Topology,
        [
            new(new BattleSlot(BattleSide.Player, 0), new UseMove(0),
                new BattleSlot(BattleSide.Enemy, 0)),
            new(new BattleSlot(BattleSide.Player, 1), new Pass()),
            new(new BattleSlot(BattleSide.Enemy, 0), new Pass()),
            new(new BattleSlot(BattleSide.Enemy, 1), new Pass()),
        ]);

        BattleSlot[] damaged = battle.ResolveTurn(actions).OfType<WeatherDamage>().Select(entry => entry.Slot).ToArray();

        Assert.Equal([
            new BattleSlot(BattleSide.Player, 0),
            new BattleSlot(BattleSide.Enemy, 0),
            new BattleSlot(BattleSide.Enemy, 1),
        ], damaged);
        Assert.Equal(0, rng.Calls);
    }

    [Fact]
    public void SmartAi_CollectsTheSameWeatherDamageModifier()
    {
        BattleCreature setter = Creature("setter", 100, moves: [WeatherMove(Weather.Rain, "wet_field")]);
        var weatherBattle = new BattleController(setter, Creature("dummy", 1, moves: [Inert()]), Chart(), new Rng(1));
        weatherBattle.ResolveTurn(new UseMove(0), new UseMove(0));
        var attacker = Creature("ai", 100, [Fire],
            Hit(Water, 80, "water_hit"), Hit(Normal, 100, "plain_hit"));
        var target = Creature("ai_target", 1, moves: [Inert()]);

        SmartAiDecision clear = SmartAi.ChooseAction(new SmartAiContext([attacker], 0, [target], 0,
            Chart(), new Rng(2), Weights: new SmartAiWeights { NoiseFraction = 0 }));
        SmartAiDecision rain = SmartAi.ChooseAction(new SmartAiContext([attacker], 0, [target], 0,
            Chart(), new Rng(2), Weights: new SmartAiWeights { NoiseFraction = 0 },
            Conditions: weatherBattle.ConditionSnapshot));

        Assert.Equal(new UseMove(1), clear.Action);
        Assert.Equal(new UseMove(0), rain.Action);
    }

    [Fact]
    public void Replay_ReproducesEventsConditionAndHookTraces()
    {
        static string Run()
        {
            var battle = new BattleController(
                Creature("source", 100, moves: [WeatherMove(Weather.Rain, "wet_field"), Hit(Water, 60, "water_hit")]),
                Creature("target", 1, moves: [Inert()]), Chart(), new Rng(7));
            battle.ResolveTurn(new UseMove(0), new UseMove(0));
            battle.ResolveTurn(new UseMove(1), new UseMove(0));
            return JsonSerializer.Serialize(new
            {
                Events = battle.Log.Select(entry => entry.ToString()),
                Conditions = battle.ConditionSnapshot,
                battle.ConditionTrace,
                battle.HookTrace,
            });
        }

        Assert.Equal(Run(), Run());
    }

    private static BattleConditionStores StoresWith(Weather weather)
    {
        var stores = new BattleConditionStores(new BattleConditionRegistry(WeatherConditions.Definitions));
        stores.Apply(new BattleConditionApplication(WeatherConditions.For(weather).Definition!.Id,
            WeatherConditions.FieldOwner, new BattleConditionSource(), 0, 0));
        return stores;
    }

    private static BattleTurnActions DoublesActions(BattleAction playerAction,
        BattleActionSelection? selection = null) => new(BattleTopology.Doubles,
    [
        new(new BattleSlot(BattleSide.Player, 0), playerAction, selection),
        new(new BattleSlot(BattleSide.Player, 1), new Pass()),
        new(new BattleSlot(BattleSide.Enemy, 0), new Pass()),
        new(new BattleSlot(BattleSide.Enemy, 1), new Pass()),
    ]);

    private static WeatherAccuracyEffect AccuracyEffect(
        Weather[] bypass, params (Weather Weather, int Accuracy)[] overrides) =>
        new(new HashSet<Weather>(bypass), overrides.ToDictionary(row => row.Weather, row => row.Accuracy));

    private static WeatherMoveEffect WeatherInteraction(
        (Weather Weather, EntityId Type)[]? types = null,
        (Weather Weather, Fraction Fraction)[]? power = null,
        Weather[]? skipCharge = null) => new(
            (types ?? []).ToDictionary(row => row.Weather, row => row.Type),
            (power ?? []).ToDictionary(row => row.Weather, row => row.Fraction),
            new HashSet<Weather>(skipCharge ?? []));

    private static BattleMove AccuracyHit(int power, int accuracy, string slug, WeatherAccuracyEffect effect) =>
        new(EntityId.Parse($"move:{slug}"), Normal, DamageClass.Special, power, accuracy, 30, 0, 0,
            secondaryEffects: [effect]);

    private static BattleMove StatusMove(PersistentStatus status, int chance, string slug) =>
        new(EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 30, 0, 0,
            ailment: status, ailmentChance: chance);

    private static HealEffect HealingEffect(params (Weather Weather, int Num, int Den)[] rows) =>
        new(new Fraction(1, 2), HpFractionRecipient.Self,
            rows.ToDictionary(row => row.Weather, row => new Fraction(row.Num, row.Den)));

    private static BattleMove HealingMove(string slug, HpFractionRecipient recipient,
        params (Weather Weather, int Num, int Den)[] rows)
    {
        HealEffect effect = new(new Fraction(1, 2), recipient,
            rows.Length == 0 ? null : rows.ToDictionary(row => row.Weather, row => new Fraction(row.Num, row.Den)));
        return new BattleMove(EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status,
            null, null, 30, 0, 0, heal: recipient == HpFractionRecipient.Self ? effect.Fraction : null,
            secondaryEffects: [effect]);
    }

    private static BattleCreature CreatureWithHp(string slug, int maxHp, int speed, params BattleMove[] moves) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [Normal],
            new Stats(maxHp, 100, 100, 100, 100, speed), moves);

    private static IReadOnlyDictionary<string, JsonElement> Params(params (string Key, object Value)[] values) =>
        values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value));

    private sealed class CountingRng : IRng
    {
        public int Calls { get; private set; }
        public int Next(int maxExclusive) { Calls++; return 0; }
        public int Next(int minInclusive, int maxExclusive) { Calls++; return minInclusive; }
        public double NextDouble() { Calls++; return 0; }
    }
}
