using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleHpStatusFormulaTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    [Theory]
    [InlineData(49, true, true)]
    [InlineData(50, true, true)]
    [InlineData(51, true, false)]
    [InlineData(49, false, true)]
    [InlineData(50, false, false)]
    [InlineData(51, false, false)]
    public void Threshold_UsesLockedInclusivity(int hp, bool inclusive, bool expected) =>
        Assert.Equal(expected, HpStatusFormulas.AtThreshold(hp, 100, new Fraction(1, 2), inclusive));

    [Theory]
    [InlineData(1, 100, HpRatioPowerBasis.Current, 120, 1, 2)]
    [InlineData(100, 100, HpRatioPowerBasis.Current, 120, 1, 121)]
    [InlineData(1, 100, HpRatioPowerBasis.Missing, 100, 0, 99)]
    [InlineData(100, 100, HpRatioPowerBasis.Missing, 100, 0, 0)]
    public void RatioAmount_FloorsCurrentAndMissingHp(int hp, int max, HpRatioPowerBasis basis,
        int scale, int offset, int expected) =>
        Assert.Equal(expected, HpStatusFormulas.RatioAmount(hp, max, basis, scale, offset));

    [Theory]
    [InlineData(1, 200)]
    [InlineData(2, 150)]
    [InlineData(5, 150)]
    [InlineData(6, 100)]
    [InlineData(12, 100)]
    [InlineData(13, 80)]
    [InlineData(21, 80)]
    [InlineData(22, 40)]
    [InlineData(42, 40)]
    [InlineData(43, 20)]
    [InlineData(64, 20)]
    public void BandPower_CoversEveryBoundary(int hp, int expected)
    {
        HpPowerBand[] bands = [new(1, 200), new(5, 150), new(12, 100), new(21, 80), new(42, 40), new(64, 20)];
        Assert.Equal(expected, HpStatusFormulas.BandPower(hp, 64, 64, bands));
    }

    [Fact]
    public void FormulaHelpers_RejectInvalidMaximumHpAndTreatItAsNoThresholdMatch()
    {
        Assert.False(HpStatusFormulas.AtThreshold(1, 0, new Fraction(1, 2), true));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HpStatusFormulas.RatioAmount(1, 0, HpRatioPowerBasis.Current, 1, 0));
    }

    [Theory]
    [InlineData(BattleVolatileStatus.Confusion)]
    [InlineData(BattleVolatileStatus.Flinch)]
    [InlineData(BattleVolatileStatus.Bound)]
    [InlineData(BattleVolatileStatus.Seeded)]
    [InlineData(BattleVolatileStatus.Protected)]
    public void StatusPredicate_RecognizesEveryVolatile(BattleVolatileStatus status)
    {
        BattleCreature creature = Creature("volatile", 100, 1, Inert());
        switch (status)
        {
            case BattleVolatileStatus.Confusion: creature.SetConfusion(1); break;
            case BattleVolatileStatus.Flinch: creature.SetFlinch(); break;
            case BattleVolatileStatus.Bound: creature.SetTrap(1); break;
            case BattleVolatileStatus.Seeded: creature.SetSeeded(true); break;
            case BattleVolatileStatus.Protected: break;
        }
        Assert.True(HpStatusFormulas.Matches(creature, null, status,
            protectedStatus: status == BattleVolatileStatus.Protected));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HpStatusFormulas.Matches(creature, null, (BattleVolatileStatus)999));
    }

    [Fact]
    public void StatusPredicateAndCount_HandleAbsentMismatchAndPersistentMatches()
    {
        BattleCreature creature = Creature("status", 100, 1, Inert());
        Assert.False(HpStatusFormulas.Matches(creature, null, null));
        Assert.Equal(0, HpStatusFormulas.Count(creature, [PersistentStatus.Burn], []));
        creature.SetStatus(PersistentStatus.Burn);
        Assert.True(HpStatusFormulas.Matches(creature, null, null));
        Assert.True(HpStatusFormulas.Matches(creature, PersistentStatus.Burn, null));
        Assert.False(HpStatusFormulas.Matches(creature, PersistentStatus.Poison, null));
        Assert.Equal(1, HpStatusFormulas.Count(creature, [PersistentStatus.Burn], [BattleVolatileStatus.Confusion]));
        Assert.Equal(0, HpStatusFormulas.Count(creature, [PersistentStatus.Poison], []));
    }

    [Fact]
    public void PowerQuery_RejectsUnknownStatusCountSubject()
    {
        BattleMove move = new(EntityId.Parse("move:invalidsubject"), Normal, DamageClass.Special,
            null, 100, 10, 0, 0, secondaryEffects:
            [new StatusCountPowerEffect((StatusCountSubject)999, [], [], 10, 10)]);
        BattleCreature source = Creature("source", 100, 100, move);
        BattleCreature target = Creature("target", 100, 1, Inert());

        Assert.Throws<ArgumentOutOfRangeException>(() => HpStatusFormulas.PowerQuery(move, source, target));
    }

    [Fact]
    public void Compiler_ProducesStrictTypedFormulaRows()
    {
        BattleMove move = Compile(DamageClass.Special, null,
            Op("hpBandPower", ("source", "user"), ("scale", 64), ("bands", "1:200,5:150,12:100,21:80,42:40,64:20")),
            Op("statusPower", ("subject", "target"), ("volatile", "confusion"),
                ("multiplierNum", 2), ("multiplierDen", 1), ("ignoreSourceBurnPenalty", false)),
            Op("cannotKo", ("floor", 1)));

        Assert.NotNull(move.HpBandPower);
        Assert.Equal(HpRatioPowerSource.User, move.HpBandPower.Source);
        Assert.Equal(64, move.HpBandPower.Scale);
        Assert.Equal([new(1, 200), new(5, 150), new(12, 100), new(21, 80), new(42, 40), new(64, 20)],
            move.HpBandPower.Bands);
        Assert.Contains(move.SecondaryEffects, effect => effect is StatusPowerEffect
            { Subject: StatusPowerSubject.Target, Volatile: BattleVolatileStatus.Confusion });
        Assert.Contains(move.SecondaryEffects, effect => effect is CannotKoEffect { Floor: 1 });
    }

    [Theory]
    [InlineData("hpBandPower", "bands", "1:20,1:40")]
    [InlineData("hpBandPower", "bands", "bad")]
    [InlineData("statusPower", "status", "burn")]
    [InlineData("statusCountPower", "statuses", "burn,burn")]
    public void Compiler_RejectsMalformedOrAmbiguousRows(string op, string key, string value)
    {
        Effect effect = op switch
        {
            "hpBandPower" => Op(op, ("source", "user"), ("scale", 64), (key, value)),
            "statusPower" => Op(op, ("subject", "user"), (key, value), ("volatile", "confusion"),
                ("multiplierNum", 2), ("multiplierDen", 1)),
            _ => Op(op, ("subject", "user"), (key, value), ("base", 10), ("perStatus", 10)),
        };
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Special, 50, effect));
    }

    [Fact]
    public void Compiler_RejectsInvalidFormulaCompositionAndChanceBindings()
    {
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Special, 50,
            Op("hpRatioPower", ("source", "user"), ("scale", 0), ("offset", 0))));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Special, null,
            Op("hpRatioPower", ("source", "user"), ("scale", 100)),
            Op("hpBandPower", ("source", "user"), ("scale", 1), ("bands", "1:20"))));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Special, 50,
            Op("statusCountPower", ("subject", "user"), ("base", 10), ("perStatus", 10))));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Status, null,
            Op("statusChance", ("subject", "target"), ("status", "poison"), ("num", 2), ("den", 1))));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Status, null,
            new Effect { Op = "cannotKo", Chance = 50 }));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Special, null,
            Op("hpEqualize", ("mode", "average"))));
    }

    [Fact]
    public void Compiler_RejectsEveryFormulaTrustBoundary()
    {
        Effect Threshold() => Op("targetHpThresholdPower", ("thresholdNum", 1), ("thresholdDen", 2),
            ("multiplierNum", 2), ("multiplierDen", 1));
        Effect Ratio() => Op("hpRatioPower", ("source", "user"));
        Effect Band() => Op("hpBandPower", ("source", "user"), ("scale", 64), ("bands", "64:20"));
        Effect Equalize() => Op("hpEqualize", ("mode", "average"));
        Effect Floor() => Op("cannotKo", ("floor", 1));

        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Special, 50, Threshold(), Threshold()));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Special, 50, Ratio(), Ratio()));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Special, null, Band(), Band()));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Special, 50,
            new Effect { Op = "hpBandPower", Chance = 50, Params = Band().Params }));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Special, 50,
            Op("hpBandPower", ("source", "user"), ("scale", 64), ("bands", "63:20"))));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Special, 50,
            Op("statusPower", ("subject", "target"), ("status", "poison"),
                ("multiplierNum", 2), ("multiplierDen", 1), ("ignoreSourceBurnPenalty", true))));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Special, 50,
            new Effect { Op = "statusCountPower", Chance = 50, Params = Op("statusCountPower",
                ("subject", "user"), ("statuses", "burn"), ("base", 1), ("perStatus", 1)).Params }));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Special, 50,
            Op("statusCountPower", ("subject", "user"), ("statuses", "burn"), ("base", 0), ("perStatus", 0))));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Status, null,
            new Effect { Op = "statusChance", Chance = 50, Params = Op("statusChance",
                ("subject", "target"), ("status", "poison"), ("num", 2), ("den", 1)).Params },
            new Effect { Op = "flinch", Chance = 20 }));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Status, null,
            Op("statusChance", ("subject", "target"), ("status", "poison"), ("num", 0), ("den", 1)),
            new Effect { Op = "flinch", Chance = 20 }));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Status, null, Equalize(), Equalize()));
        Assert.Throws<ArgumentException>(() => CompileAt(MoveTarget.AllOpponents, DamageClass.Status, null, Equalize()));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Status, null,
            new Effect { Op = "hpEqualize", Chance = 50, Params = Equalize().Params }));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Physical, 50, Floor(), Floor()));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Status, null, Floor()));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Physical, 50,
            new Effect { Op = "cannotKo", Chance = 50 }));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Physical, 50, Op("cannotKo", ("floor", 0))));
    }

    [Fact]
    public void LinearRatioPower_ReplacesMissingAuthoredPowerAndTracesTheQuery()
    {
        BattleMove move = Compile(DamageClass.Special, null,
            Op("hpRatioPower", ("source", "target"), ("basis", "current"), ("scale", 120), ("offset", 1)));
        BattleCreature source = Creature("source", 100, 100, move);
        BattleCreature target = Creature("target", 200, 1, Inert());
        target.TakeDamage(100);
        var battle = Battle(source, target);

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        BattleQueryResult query = Assert.Single(battle.QueryTrace, entry => entry.Result.Query == BattleQueryId.BasePower).Result;
        Assert.Equal(61, query.FinalValue.ToInt32());
        Assert.Contains(query.Steps, step => step.Operation == BattleQueryOperation.Replace && step.Output.ToInt32() == 61);
    }

    [Fact]
    public void BandPower_ReplacesMissingAuthoredPowerInTheResolver()
    {
        BattleMove move = Compile(DamageClass.Physical, null,
            Op("hpBandPower", ("source", "user"), ("scale", 64),
                ("bands", "1:200,5:150,12:100,21:80,42:40,64:20")));
        BattleCreature source = Creature("source", 64, 100, move);
        source.TakeDamage(63);
        BattleCreature target = Creature("target", 300, 1, Inert());
        BattleController battle = Battle(source, target);

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(200, Assert.Single(battle.QueryTrace,
            entry => entry.Result.Query == BattleQueryId.BasePower).Result.FinalValue.ToInt32());
    }

    [Theory]
    [InlineData("user")]
    [InlineData("target")]
    public void StatusCountPower_AddressesEachSubject(string subject)
    {
        BattleMove move = Compile(DamageClass.Special, null,
            Op("statusCountPower", ("subject", subject), ("statuses", "burn"),
                ("base", 10), ("perStatus", 15)));
        BattleCreature source = Creature("source", 100, 100, move);
        BattleCreature target = Creature("target", 300, 1, Inert());
        (subject == "user" ? source : target).SetStatus(PersistentStatus.Burn);
        BattleController battle = Battle(source, target);

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(25, Assert.Single(battle.QueryTrace,
            entry => entry.Result.Query == BattleQueryId.BasePower).Result.FinalValue.ToInt32());
    }

    [Fact]
    public void VolatileAndStatusCountPower_UseLiveSourceAndTargetState()
    {
        BattleMove volatilePower = Compile(DamageClass.Special, 40,
            Op("statusPower", ("subject", "target"), ("volatile", "confusion"),
                ("multiplierNum", 2), ("multiplierDen", 1)));
        BattleCreature source = Creature("source", 100, 100, volatilePower);
        BattleCreature target = Creature("target", 300, 1, Inert());
        target.SetConfusion(2);
        BattleController battle = Battle(source, target);
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(80, Assert.Single(battle.QueryTrace, e => e.Result.Query == BattleQueryId.BasePower).Result.FinalValue.ToInt32());

        BattleMove countPower = Compile(DamageClass.Special, null,
            Op("statusCountPower", ("subject", "both"), ("statuses", "burn,poison"),
                ("volatiles", "confusion"), ("base", 10), ("perStatus", 15)));
        source = Creature("source2", 100, 100, countPower);
        source.SetStatus(PersistentStatus.Burn);
        target = Creature("target2", 300, 1, Inert());
        target.SetStatus(PersistentStatus.Poison);
        target.SetConfusion(2);
        battle = Battle(source, target);
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(55, Assert.Single(battle.QueryTrace, e => e.Result.Query == BattleQueryId.BasePower).Result.FinalValue.ToInt32());
    }

    [Fact]
    public void HpEqualize_AveragesOddHpFromOneSnapshotAndClampsPerCreature()
    {
        BattleMove move = Compile(DamageClass.Status, null, Op("hpEqualize", ("mode", "average")));
        BattleCreature source = Creature("source", 101, 100, move);
        BattleCreature target = Creature("target", 300, 1, Inert());
        source.TakeDamage(100);
        target.TakeDamage(100);
        BattleController battle = Battle(source, target);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(100, source.CurrentHp);
        Assert.Equal(100, target.CurrentHp);
        Assert.Equal(2, events.OfType<HpFormulaChanged>().Count());
        Assert.Contains(battle.Trace, entry => entry.Kind == EffectTraceKind.HpFormula && entry.Value == 100);
    }

    [Theory]
    [InlineData(20, 50, 20, true)]
    [InlineData(50, 20, 20, false)]
    public void HpEqualize_MatchSourceChangesOnlyHigherTarget(int sourceHp, int targetHp, int expected, bool changed)
    {
        BattleMove move = Compile(DamageClass.Status, null, Op("hpEqualize", ("mode", "matchSource")));
        BattleCreature source = Creature("source", 100, 100, move);
        BattleCreature target = Creature("target", 100, 1, Inert());
        source.TakeDamage(100 - sourceHp);
        target.TakeDamage(100 - targetHp);
        BattleController battle = Battle(source, target);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(expected, target.CurrentHp);
        Assert.Equal(changed, events.Any(item => item is HpFormulaChanged { Slot.Side: BattleSide.Enemy }));
        Assert.Contains(battle.Trace, entry => entry.Kind == EffectTraceKind.HpFormula
            && (changed ? entry.EventEndIndex > entry.EventStartIndex : entry.EventEndIndex == entry.EventStartIndex));
    }

    [Fact]
    public void HpEqualize_MatchSource_UsesDamageBookkeepingAndObservesTypeImmunity()
    {
        EntityId ground = EntityId.Parse("type:ground");
        EntityId flying = EntityId.Parse("type:flying");
        BattleMove move = new(EntityId.Parse("move:equalize"), ground, DamageClass.Status,
            null, 100, 10, 0, 0, secondaryEffects: [new HpEqualizeEffect(HpEqualizeMode.MatchSource)]);
        BattleCreature source = Creature("source", 100, 100, move);
        source.TakeDamage(80);
        BattleCreature target = new(EntityId.Parse("species:target"), "target", 50, [flying],
            new Stats(100, 100, 100, 100, 100, 100), [Inert()]);
        var chart = new TypeChart([new TypeDef { Id = ground, NoDamageTo = [flying] }, new TypeDef { Id = flying }]);
        var battle = new BattleController(source, target, chart,
            new FakeRng(ints: [0, 0], doubles: [0.99]));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(100, target.CurrentHp);
        Assert.DoesNotContain(events, item => item is DamageDealt or HpFormulaChanged);
        Assert.Contains(battle.Trace, entry => entry.Kind == EffectTraceKind.HpFormula
            && entry.EventEndIndex == entry.EventStartIndex);
    }

    [Fact]
    public void HpEqualize_MatchSource_RecordsExactDamageForLaterEffects()
    {
        BattleMove move = new(EntityId.Parse("move:equalizedrain"), Normal, DamageClass.Status,
            null, 100, 10, 0, 0, secondaryEffects:
            [new HpEqualizeEffect(HpEqualizeMode.MatchSource), new DrainEffect(new Fraction(1, 2))]);
        BattleCreature source = Creature("source", 100, 100, move);
        source.TakeDamage(80);
        BattleCreature target = Creature("target", 100, 1, Inert());
        BattleController battle = Battle(source, target);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(20, target.CurrentHp);
        Assert.Equal(60, source.CurrentHp);
        Assert.Equal(80, Assert.Single(events.OfType<DamageDealt>()).Amount);
    }

    [Fact]
    public void CannotKo_CapsEveryHitAtConfiguredFloor()
    {
        BattleMove move = Compile(DamageClass.Physical, 250, Op("cannotKo", ("floor", 1)));
        BattleCreature source = Creature("source", 100, 100, move);
        BattleCreature target = Creature("target", 100, 1, Inert());
        BattleController battle = Battle(source, target);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(1, target.CurrentHp);
        Assert.DoesNotContain(events, item => item is Fainted { Slot.Side: BattleSide.Enemy });
        Assert.Equal(99, Assert.Single(events.OfType<DamageDealt>()).Amount);
    }

    [Fact]
    public void StatusChance_ChangesTheExistingDrawBoundWithoutAddingADraw()
    {
        BattleMove move = Compile(DamageClass.Status, null,
            Op("statusChance", ("subject", "target"), ("status", "poison"), ("num", 2), ("den", 1)),
            new Effect { Op = "flinch", Chance = 40 });
        BattleCreature source = Creature("source", 100, 100, move);
        BattleCreature target = Creature("target", 100, 1, Inert());
        target.SetStatus(PersistentStatus.Poison);
        var rng = new CountingRng(70);
        var battle = new BattleController(source, target, Chart(), rng);

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(target.Flinched);
        Assert.Equal(1, rng.HundredDraws);
        Assert.Contains(battle.Trace, entry => entry.Kind == EffectTraceKind.EffectChance && entry.DrawResult == 70);
        Assert.Equal(80, Assert.Single(battle.QueryTrace,
            entry => entry.Result.Query == BattleQueryId.SecondaryChance).Result.FinalValue.ToInt32());
    }

    [Fact]
    public void StatusChance_MismatchUsesAuthoredChance()
    {
        BattleMove move = Compile(DamageClass.Status, null,
            Op("statusChance", ("subject", "target"), ("status", "poison"), ("num", 2), ("den", 1)),
            new Effect { Op = "flinch", Chance = 40 });
        BattleCreature source = Creature("source", 100, 100, move);
        BattleCreature target = Creature("target", 100, 1, Inert());
        var rng = new CountingRng(70);
        var battle = new BattleController(source, target, Chart(), rng);

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.False(target.Flinched);
        Assert.Equal(1, rng.HundredDraws);
        Assert.Equal(40, Assert.Single(battle.QueryTrace,
            entry => entry.Result.Query == BattleQueryId.SecondaryChance).Result.FinalValue.ToInt32());
    }

    [Fact]
    public void CurrentHpFraction_ComposesWithCannotKoAtOneHp()
    {
        BattleMove move = Compile(DamageClass.Physical, null,
            Op("hpFraction", ("recipient", "target"), ("operation", "damage"),
                ("basis", "currentHp"), ("num", 1), ("den", 2)),
            Op("cannotKo", ("floor", 1)));
        BattleCreature source = Creature("source", 100, 100, move);
        BattleCreature target = Creature("target", 100, 1, Inert());
        target.TakeDamage(99);
        BattleController battle = Battle(source, target);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(1, target.CurrentHp);
        Assert.DoesNotContain(events, item => item is HpFractionDamaged or Fainted);
    }

    [Fact]
    public void TargetHpFormula_EvaluatesEachDoublesTargetFromItsOwnSnapshot()
    {
        BattleMove spread = MoveCompiler.ToBattleMove(new Move
        {
            Id = EntityId.Parse("move:spread_formula"), Name = "Spread Formula", Type = Normal,
            DamageClass = DamageClass.Special, Power = 50, Accuracy = null, Pp = 10,
            Target = MoveTarget.AllOpponents,
            Effects = [Op("targetHpThresholdPower", ("thresholdNum", 1), ("thresholdDen", 2),
                ("multiplierNum", 2), ("multiplierDen", 1), ("inclusive", true))],
        });
        BattleCreature source = Creature("source", 100, 100, spread);
        BattleCreature ally = Creature("ally", 100, 90, Inert());
        BattleCreature low = Creature("low", 100, 1, Inert());
        BattleCreature full = Creature("full", 100, 2, Inert());
        low.TakeDamage(50);
        var battle = new BattleController([source, ally], [low, full], BattleTopology.Doubles,
            [0, 1], [0, 1], Chart(), new FakeRng(ints: [15, 15], doubles: [0.99, 0.99]));
        var actions = new BattleTurnActions(BattleTopology.Doubles,
        [
            new(new BattleSlot(BattleSide.Player, 0), new UseMove(0)),
            new(new BattleSlot(BattleSide.Player, 1), new Pass()),
            new(new BattleSlot(BattleSide.Enemy, 0), new Pass()),
            new(new BattleSlot(BattleSide.Enemy, 1), new Pass()),
        ]);

        battle.ResolveTurn(actions);

        Assert.Equal([100, 50], battle.QueryTrace.Where(entry => entry.Result.Query == BattleQueryId.BasePower)
            .Select(entry => entry.Result.FinalValue.ToInt32()).ToArray());
    }

    private static BattleMove Compile(DamageClass damageClass, int? power, params Effect[] effects) =>
        CompileAt(MoveTarget.Selected, damageClass, power, effects);

    private static BattleMove CompileAt(MoveTarget target, DamageClass damageClass, int? power, params Effect[] effects) =>
        MoveCompiler.ToBattleMove(new Move
        {
            Id = EntityId.Parse("move:formula"), Name = "Formula", Type = Normal,
            DamageClass = damageClass, Power = power, Accuracy = null, Pp = 20,
            Target = target, Effects = effects,
        });

    private static Effect Op(string op, params (string Key, object Value)[] values) => new()
    {
        Op = op,
        Params = values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value)),
    };

    private static BattleCreature Creature(string id, int hp, int speed, params BattleMove[] moves) =>
        new(EntityId.Parse($"species:{id}"), id, 50, [Normal], new Stats(hp, 100, 100, 100, 100, speed), moves);

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 20, 0, 0);

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);
    private static BattleController Battle(BattleCreature source, BattleCreature target) =>
        new(source, target, Chart(), new FakeRng(ints: [0, 15], doubles: [0.99, 0.99, 0.99]));

    private sealed class CountingRng(int draw) : IRng
    {
        public int HundredDraws { get; private set; }
        public int Next(int maxExclusive)
        {
            if (maxExclusive == 100) HundredDraws++;
            return maxExclusive == 100 ? draw : maxExclusive - 1;
        }
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
        public double NextDouble() => 0.99;
    }
}
