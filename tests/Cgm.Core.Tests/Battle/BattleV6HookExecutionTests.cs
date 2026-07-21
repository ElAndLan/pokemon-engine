using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleV6HookExecutionTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Water = EntityId.Parse("type:water");
    private static readonly EntityId Fire = EntityId.Parse("type:fire");
    private static readonly EntityId SpeciesId = EntityId.Parse("species:hookmon");
    private static readonly EntityId AbilityId = EntityId.Parse("ability:drizzle_root");
    private static readonly EntityId FormAbilityId = EntityId.Parse("ability:stone_skin");
    private static readonly EntityId QuietAbilityId = EntityId.Parse("ability:quiet_root");
    private static readonly EntityId ItemId = EntityId.Parse("item:river_band");
    private static readonly EntityId BerryId = EntityId.Parse("item:form_berry");
    private static readonly EntityId TrainerKeyId = EntityId.Parse("item:form_key");
    private static readonly EntityId MoveId = EntityId.Parse("move:splash_hit");
    private static readonly EntityId FormMoveId = EntityId.Parse("move:stone_hit");
    private static readonly EntityId RainMoveId = EntityId.Parse("move:rain_call");

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }, new TypeDef { Id = Water }, new TypeDef { Id = Fire }]);

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 25, 0, 0);

    private static BattleMove WaterHit() =>
        new(MoveId, Water, DamageClass.Special, 60, 100, 25, 0, 0);

    private static BattleMove PhysicalHit() =>
        new(EntityId.Parse("move:body_check"), Normal, DamageClass.Physical, 60, 100, 25, 0, 0);

    private static BattleMove ContactHit() =>
        new(EntityId.Parse("move:contact_hit"), Normal, DamageClass.Physical, 60, 100, 25, 0, 0,
            makesContact: true);

    private static BattleCreature Fast(params BattleMove[] moves) =>
        new(EntityId.Parse("species:fast"), "Fast", 50, [Normal], new Stats(300, 100, 100, 100, 100, 100), moves);

    private static BattleCreature Slow(params BattleMove[] moves) =>
        new(EntityId.Parse("species:slow"), "Slow", 50, [Normal], new Stats(9999, 100, 100, 100, 100, 1), moves);

    private static BattleCreature Weak(params BattleMove[] moves) =>
        new(EntityId.Parse("species:weak"), "Weak", 50, [Normal], new Stats(10, 10, 10, 10, 10, 1), moves);

    [Fact]
    public void FromInstance_WiresAbilityAndHeldItemBattleEffectsFromGameDb()
    {
        BattleCreature creature = BattleCreature.FromInstance(new CreatureInstance
        {
            Species = SpeciesId,
            Level = 50,
            Nature = "hardy",
            CurHp = 999,
            Moves = [new MoveSlot(MoveId, 7)],
            HeldItem = ItemId,
        }, Db());

        Assert.Single(creature.AbilityHooks);
        Assert.Equal("weatherSummon", creature.AbilityHooks.Single().Effects.Single().Op);
        Assert.Single(creature.HeldItemBattleEffects);
        Assert.Equal("typeDamageBoost", creature.HeldItemBattleEffects.Single().Op);
        Assert.Equal(7, creature.Moves.Single().Pp);
    }

    [Fact]
    public void FromInstance_AppliesPermanentFormOverrides()
    {
        BattleCreature creature = BattleCreature.FromInstance(new CreatureInstance
        {
            Species = SpeciesId,
            Form = "stone",
            Level = 50,
            Nature = "hardy",
            CurHp = 999,
            Moves = [new MoveSlot(MoveId, 3)],
        }, Db());

        Assert.Equal([Fire], creature.Types);
        Assert.Equal(180, creature.MaxHp);
        Assert.Equal(145, creature.Stats.Spa);
        Assert.Equal("residualHeal", creature.AbilityHooks.Single().Effects.Single().Op);
        Assert.Equal(FormMoveId, creature.Moves.Single().Move);
        Assert.Equal(3, creature.Moves.Single().Pp);
    }

    [Fact]
    public void WeatherConditionForm_ActivatesAndPreservesHpRatioAndStages()
    {
        BattleCreature player = FormCandidate();
        player.TakeDamage(80);
        player.ChangeStage(StatKind.Atk, 2);
        var battle = new BattleController(player, Slow(Inert()), Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal([Water], player.Types);
        Assert.Equal(180, player.MaxHp);
        Assert.Equal(90, player.CurrentHp);
        Assert.Equal(2, player.Stage(StatKind.Atk));
        Assert.Equal("weatherSummon", player.AbilityHooks.Single().Effects.Single().Op);
        Assert.Contains(events, e => e is FormChanged { Side: BattleSide.Player, FormId: "rain" });
    }

    [Fact]
    public void WeatherConditionForm_RevertsWhenWeatherExpires()
    {
        BattleCreature player = FormCandidate();
        player.TakeDamage(80);
        player.ChangeStage(StatKind.Atk, 2);
        var battle = new BattleController(player, Slow(Inert()), Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        IReadOnlyList<BattleEvent> events = [];
        for (int i = 0; i < 4; i++)
            events = battle.ResolveTurn(new UseMove(1), new UseMove(0));

        Assert.Equal([Normal], player.Types);
        Assert.Equal(160, player.MaxHp);
        Assert.Equal(80, player.CurrentHp);
        Assert.Equal(2, player.Stage(StatKind.Atk));
        Assert.Equal("weatherSummon", player.AbilityHooks.Single().Effects.Single().Op);
        Assert.Contains(events, e => e is FormChanged { Side: BattleSide.Player, FormId: null });
    }

    [Fact]
    public void HeldItemConditionForm_ActivatesOnSwitchIn()
    {
        BattleCreature reserve = HeldItemFormCandidate(ItemId);
        var battle = new BattleController([Fast(Inert()), reserve], [Slow(Inert())], Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new Switch(1), new UseMove(0));

        Assert.Equal([Fire], reserve.Types);
        Assert.Equal(180, reserve.MaxHp);
        Assert.Equal(180, reserve.CurrentHp);
        Assert.Contains(events, e => e is FormChanged { Side: BattleSide.Player, FormId: "held" });
    }

    [Fact]
    public void HeldItemConditionForm_RevertsAfterConsumableHeldItemFires()
    {
        BattleCreature reserve = HeldItemFormCandidate(BerryId);
        var battle = new BattleController([Fast(Inert()), reserve], [Slow(Inert())], Chart(), new Rng(1));

        battle.ResolveTurn(new Switch(1), new UseMove(0));
        reserve.TakeDamage(120);
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal([Normal], reserve.Types);
        Assert.Equal(160, reserve.MaxHp);
        Assert.Equal(80, reserve.CurrentHp);
        Assert.Contains(events, e => e is HeldItemConsumed { Side: BattleSide.Player, Op: "thresholdHeal" });
        Assert.Contains(events, e => e is FormChanged { Side: BattleSide.Player, FormId: null });
    }

    [Fact]
    public void BattleTemporaryForm_ActivatesBeforeSelectedMove()
    {
        BattleCreature player = TemporaryFormCandidate();
        BattleCreature enemy = Slow(Inert());
        var battle = new BattleController(player, enemy, Chart(), new Rng(1));
        battle.SetBattleItemStock(BattleSide.Player, TrainerKeyId, 1);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new ActivateForm("burst", 0), new UseMove(0));

        Assert.Equal([Fire], player.Types);
        Assert.Contains(events, e => e is FormChanged { Side: BattleSide.Player, FormId: "burst" });
        Assert.Contains(events.OfType<MoveUsed>(), e => e.Side == BattleSide.Player && e.Move == MoveId);
    }

    [Fact]
    public void BattleTemporaryForm_RequiresTrainerKeyItem()
    {
        var battle = new BattleController(TemporaryFormCandidate(), Slow(Inert()), Chart(), new Rng(1));

        Assert.Throws<ArgumentException>(() => battle.ResolveTurn(new ActivateForm("burst", 0), new UseMove(0)));
    }

    [Fact]
    public void BattleTemporaryForm_ActivatesOncePerSide()
    {
        var battle = new BattleController(TemporaryFormCandidate(), Slow(Inert()), Chart(), new Rng(1));
        battle.SetBattleItemStock(BattleSide.Player, TrainerKeyId, 1);

        battle.ResolveTurn(new ActivateForm("burst", 0), new UseMove(0));

        Assert.Throws<ArgumentException>(() => battle.ResolveTurn(new ActivateForm("burst", 0), new UseMove(0)));
    }

    [Fact]
    public void BattleTemporaryForm_RevertsAfterFaint()
    {
        BattleCreature player = TemporaryFormCandidate();
        var battle = new BattleController(player, Slow(FixedHit(999)), Chart(), new Rng(1));
        battle.SetBattleItemStock(BattleSide.Player, TrainerKeyId, 1);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new ActivateForm("burst", 0), new UseMove(0));

        Assert.True(player.IsFainted);
        Assert.Equal([Normal], player.Types);
        Assert.Contains(events, e => e is FormChanged { Side: BattleSide.Player, FormId: "burst" });
        Assert.Contains(events, e => e is FormChanged { Side: BattleSide.Player, FormId: null });
    }

    [Fact]
    public void BattleTemporaryForm_RevertsWhenBattleEnds()
    {
        BattleCreature player = TemporaryFormCandidate();
        var battle = new BattleController(player, Weak(Inert()), Chart(), new Rng(1));
        battle.SetBattleItemStock(BattleSide.Player, TrainerKeyId, 1);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new ActivateForm("burst", 0), new UseMove(0));

        Assert.Equal([Normal], player.Types);
        Assert.Contains(events, e => e is FormChanged { Side: BattleSide.Player, FormId: "burst" });
        Assert.Contains(events, e => e is FormChanged { Side: BattleSide.Player, FormId: null });
        Assert.Contains(events, e => e is BattleEnded { Winner: BattleSide.Player });
    }

    [Fact]
    public void BattleTemporaryForm_RevertsWhenCaptureEndsBattle()
    {
        BattleCreature player = TemporaryFormCandidate();
        var battle = new BattleController(player, Slow(Inert()), Chart(), new Rng(1), isWild: true);
        battle.SetBattleItemStock(BattleSide.Player, TrainerKeyId, 1);
        battle.ResolveTurn(new ActivateForm("burst", 0), new UseMove(0));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new ThrowBall(255.0, 1.0), new UseMove(0));

        Assert.Equal([Normal], player.Types);
        Assert.Contains(events, e => e is FormChanged { Side: BattleSide.Player, FormId: null });
        Assert.Contains(events, e => e is BattleEnded { Winner: BattleSide.Player });
    }

    [Fact]
    public void BattleTimedForm_ActivatesBeforeSelectedMoveAndAppliesHpMultiplier()
    {
        BattleCreature player = TimedFormCandidate();
        var battle = new BattleController(player, Slow(Inert()), Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new ActivateForm("giant", 0), new UseMove(0));

        Assert.Equal([Fire], player.Types);
        Assert.Equal(320, player.MaxHp);
        Assert.Equal(320, player.CurrentHp);
        Assert.Contains(events, e => e is FormChanged { Side: BattleSide.Player, FormId: "giant" });
        Assert.Contains(events.OfType<MoveUsed>(), e => e.Side == BattleSide.Player && e.Move == FormMoveId);
    }

    [Fact]
    public void BattleTimedForm_ExpiresAfterAuthoredTurns()
    {
        BattleCreature player = TimedFormCandidate();
        var battle = new BattleController(player, Slow(Inert()), Chart(), new Rng(1));

        battle.ResolveTurn(new ActivateForm("giant", 0), new UseMove(0));
        player.TakeDamage(160);
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal([Normal], player.Types);
        Assert.Equal(160, player.MaxHp);
        Assert.Equal(80, player.CurrentHp);
        Assert.Contains(events, e => e is FormChanged { Side: BattleSide.Player, FormId: null });
    }

    [Fact]
    public void BattleTimedForm_RevertsAfterFaint()
    {
        BattleCreature player = TimedFormCandidate();
        var battle = new BattleController(player, Slow(FixedHit(999)), Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new ActivateForm("giant", 0), new UseMove(0));

        Assert.True(player.IsFainted);
        Assert.Equal([Normal], player.Types);
        Assert.Contains(events, e => e is FormChanged { Side: BattleSide.Player, FormId: "giant" });
        Assert.Contains(events, e => e is FormChanged { Side: BattleSide.Player, FormId: null });
    }

    [Fact]
    public void BattleTimedForm_RevertsWhenBattleEnds()
    {
        BattleCreature player = TimedFormCandidate();
        var battle = new BattleController(player, Weak(Inert()), Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new ActivateForm("giant", 0), new UseMove(0));

        Assert.Equal([Normal], player.Types);
        Assert.Equal(160, player.MaxHp);
        Assert.Contains(events, e => e is FormChanged { Side: BattleSide.Player, FormId: "giant" });
        Assert.Contains(events, e => e is FormChanged { Side: BattleSide.Player, FormId: null });
        Assert.Contains(events, e => e is BattleEnded { Winner: BattleSide.Player });
    }

    [Fact]
    public void BattleTimedForm_MoveRemapUsesOriginalSlotPpAndReverts()
    {
        BattleCreature player = TimedFormCandidate();
        var battle = new BattleController(player, Slow(Inert()), Chart(), new Rng(1));

        battle.ResolveTurn(new ActivateForm("giant", 0), new UseMove(0));
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(MoveId, player.Moves[0].Move);
        Assert.Equal(23, player.Moves[0].Pp);
        Assert.Equal(25, player.Moves[0].MaxPp);
        Assert.Contains(events.OfType<MoveUsed>(), e => e.Side == BattleSide.Player && e.Move == FormMoveId);
        Assert.Contains(events, e => e is FormChanged { Side: BattleSide.Player, FormId: null });
    }

    [Fact]
    public void FormHpRatio_UsesWideFloorArithmeticAndKeepsLivingMinimum()
    {
        BattleCreature player = TimedFormCandidate();
        player.TakeDamage(player.MaxHp - 1);
        var battle = new BattleController(player, Slow(Inert()), Chart(), new Rng(1));

        battle.ResolveTurn(new ActivateForm("giant", 0), new UseMove(0));

        Assert.Equal(320, player.MaxHp);
        Assert.Equal(2, player.CurrentHp);
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(160, player.MaxHp);
        Assert.Equal(1, player.CurrentHp);

        BattleCreature wide = TimedFormCandidate(20_000_000);
        var wideBattle = new BattleController(wide, Slow(Inert()), Chart(), new Rng(1));
        wideBattle.ResolveTurn(new ActivateForm("giant", 0), new UseMove(0));
        Assert.Equal(32_000_000, wide.MaxHp);
        Assert.Equal(32_000_000, wide.CurrentHp);
    }

    [Fact]
    public void BattleTemporaryForm_OncePerSideSurvivesSwitchToAnotherEligibleOwner()
    {
        BattleCreature first = TemporaryFormCandidate();
        BattleCreature second = TemporaryFormCandidate();
        var battle = new BattleController([first, second], [Slow(Inert())], Chart(), new Rng(1));
        battle.SetBattleItemStock(BattleSide.Player, TrainerKeyId, 1);

        battle.ResolveTurn(new ActivateForm("burst", 0), new UseMove(0));
        battle.ResolveTurn(new Switch(1), new UseMove(0));

        Assert.Throws<ArgumentException>(() =>
            battle.ResolveTurn(new ActivateForm("burst", 0), new UseMove(0)));
        Assert.Equal("held", second.FormId);
    }

    [Fact]
    public void BattleTemporaryForm_DoublesConflictRejectsWholeTurnBeforeMutation()
    {
        BattleCreature first = TemporaryFormCandidate();
        BattleCreature second = TemporaryFormCandidate();
        var battle = new BattleController([first, second], [Slow(Inert()), Slow(Inert())],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new Rng(1));
        battle.SetBattleItemStock(BattleSide.Player, TrainerKeyId, 1);
        BattleSlot enemy0 = new(BattleSide.Enemy, 0);
        BattleSlot enemy1 = new(BattleSide.Enemy, 1);

        var actions = new BattleTurnActions(BattleTopology.Doubles,
        [
            new(new(BattleSide.Player, 0), new ActivateForm("burst", 0), new ActiveSlotSelection(enemy0)),
            new(new(BattleSide.Player, 1), new ActivateForm("burst", 0), new ActiveSlotSelection(enemy1)),
            new(enemy0, new Pass()),
            new(enemy1, new Pass()),
        ]);

        Assert.Throws<ArgumentException>(() => battle.ResolveTurn(actions));
        Assert.Equal([Normal], first.Types);
        Assert.Equal([Normal], second.Types);
        Assert.DoesNotContain(battle.Overlays.Snapshot(), item => item.Payload is FormOverlay);
    }

    [Fact]
    public void FormTransition_UsesSharedOverlaySequenceAndMatchesGolden()
    {
        static string Run()
        {
            BattleCreature player = TimedFormCandidate();
            player.TakeDamage(80);
            var battle = new BattleController(player, Slow(Inert()), Chart(), new Rng(1));
            BattleSlot slot = new(BattleSide.Player, 0);
            var owner = new BattleOverlayOwner(BattleSide.Player, 0, slot);
            battle.Overlays.Apply(new BattleOverlayApplication(owner, new(slot, 0),
                BattleOverlayLayer.FormOrSnapshot, new CreatureTypesOverlay([Water]), 0, 0,
                Cleanup: BattleOverlayCleanup.Switch | BattleOverlayCleanup.Faint | BattleOverlayCleanup.BattleEnd));

            IReadOnlyList<BattleEvent> first = battle.ResolveTurn(new ActivateForm("giant", 0), new UseMove(0));
            BattleEffectiveResult during = battle.Overlays.Resolve(owner,
                PhysicalMetricFormulas.BaseEffectiveValues(player));
            int duringMaxHp = player.MaxHp;
            battle.Overlays.Apply(new BattleOverlayApplication(owner, new(slot, 0),
                BattleOverlayLayer.FormOrSnapshot, new CreatureTypesOverlay([Water]), battle.Turn, 0,
                Cleanup: BattleOverlayCleanup.Switch | BattleOverlayCleanup.Faint | BattleOverlayCleanup.BattleEnd));
            BattleEffectiveResult later = battle.Overlays.Resolve(owner,
                PhysicalMetricFormulas.BaseEffectiveValues(player));
            IReadOnlyList<BattleEvent> second = battle.ResolveTurn(new UseMove(0), new UseMove(0));

            return string.Join('\n',
            [
                .. first.Concat(second).Select(item => item switch
                {
                    FormChanged changed when changed.Slot.Side == BattleSide.Player
                        => $"form:{changed.FormId ?? "base"}",
                    MoveUsed used when used.Slot.Side == BattleSide.Player => $"move:{used.Move}",
                    _ => null,
                }).Where(item => item is not null),
                $"during:{during.Values.FormId}:{during.Values.CreatureTypes.Single()}:{duringMaxHp}",
                $"later:{later.Values.FormId}:{later.Values.CreatureTypes.Single()}",
                $"reverted:{player.FormId ?? "base"}:{player.CurrentHp}/{player.MaxHp}",
                $"form-overlays:{battle.Overlays.Snapshot().Count(item => item.Payload is StatsOverlay
                    or AbilityOverlay or MoveListOverlay or FormOverlay)}",
            ]);
        }

        string first = Run();
        Assert.Equal(first, Run());
        Assert.Equal(Golden("form-transition"), first);
    }

    [Fact]
    public void WeatherSummon_AbilitySetsWeatherOnSwitchIn()
    {
        var reserve = new BattleCreature(SpeciesId, "Rain", 50, [Normal], new Stats(300, 100, 100, 100, 100, 100),
            [Inert()], abilityHooks:
            [
                new AbilityHook
                {
                    Hook = AbilityHookPoint.OnSwitchIn,
                    Effects = [new Effect { Op = "weatherSummon", Params = Params(("weather", "rain")) }],
                },
            ]);
        var battle = new BattleController([Fast(Inert()), reserve], [Slow(Inert())], Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new Switch(1), new UseMove(0));

        Assert.Contains(events, e => e is SwitchedIn { Side: BattleSide.Player, PartyIndex: 1 });
        Assert.Contains(events, e => e is WeatherChanged { Weather: Weather.Rain });
    }

    [Fact]
    public void WeatherChangeHook_RunsAfterWeatherChanged()
    {
        var reserve = new BattleCreature(SpeciesId, "Rain", 50, [Normal], new Stats(300, 100, 100, 100, 100, 100),
            [Inert()], abilityHooks:
            [
                new AbilityHook
                {
                    Hook = AbilityHookPoint.OnSwitchIn,
                    Effects = [new Effect { Op = "weatherSummon", Params = Params(("weather", "rain")) }],
                },
                new AbilityHook
                {
                    Hook = AbilityHookPoint.OnWeatherChange,
                    Effects = [new Effect { Op = "weatherSummon", Params = Params(("weather", "sun")) }],
                },
            ]);
        var battle = new BattleController([Fast(Inert()), reserve], [Slow(Inert())], Chart(), new Rng(1));

        IReadOnlyList<Weather> weather = battle.ResolveTurn(new Switch(1), new UseMove(0))
            .OfType<WeatherChanged>()
            .Select(e => e.Weather)
            .ToArray();

        Assert.Equal([Weather.Rain, Weather.Sun], weather);
    }

    [Fact]
    public void WeatherDurationExtend_ExtendsHolderSummonedWeather()
    {
        var reserve = WeatherSummoner(heldEffects:
        [
            new Effect { Op = "weatherDurationExtend", Params = Params(("turns", 2)) },
        ]);
        var battle = new BattleController([Fast(Inert()), reserve], [Slow(Inert())], Chart(), new Rng(1));

        battle.ResolveTurn(new Switch(1), new UseMove(0));
        for (int i = 0; i < 5; i++)
        {
            IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));
            Assert.DoesNotContain(events, e => e is WeatherEnded);
        }
        IReadOnlyList<BattleEvent> last = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Contains(last, e => e is WeatherEnded { Weather: Weather.Rain });
    }

    [Fact]
    public void WeatherDurationExtend_DoesNotAffectWeatherWithoutHeldItem()
    {
        var battle = new BattleController([Fast(Inert()), WeatherSummoner()], [Slow(Inert())], Chart(), new Rng(1));

        battle.ResolveTurn(new Switch(1), new UseMove(0));
        for (int i = 0; i < 3; i++)
            battle.ResolveTurn(new UseMove(0), new UseMove(0));
        IReadOnlyList<BattleEvent> last = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Contains(last, e => e is WeatherEnded { Weather: Weather.Rain });
    }

    [Fact]
    public void Phase15Integration_SwitchInOrdersFormAbilityWeatherAndHeldItemEvents()
    {
        BattleCreature reserve = BattleCreature.FromInstance(new CreatureInstance
        {
            Species = SpeciesId,
            Level = 50,
            Nature = "hardy",
            CurHp = 999,
            HeldItem = BerryId,
            Moves = [new MoveSlot(MoveId, 25)],
        }, Db());
        reserve.TakeDamage(100);
        var battle = new BattleController([Fast(Inert()), reserve], [Slow(Inert())], Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new Switch(1), new UseMove(0));

        Assert.True(IndexOf<SwitchedIn>(events) < IndexOf<FormChanged>(events, e => e.FormId == "berry"));
        Assert.True(IndexOf<FormChanged>(events, e => e.FormId == "berry") < IndexOf<WeatherChanged>(events));
        Assert.True(IndexOf<WeatherChanged>(events) < IndexOf<FormChanged>(events, e => e.FormId == "rain"));
        Assert.True(IndexOf<FormChanged>(events, e => e.FormId == "rain") < IndexOf<HeldItemConsumed>(events));
        Assert.Contains(events, e => e is HeldItemConsumed { Side: BattleSide.Player, Op: "thresholdHeal" });
    }

    [Fact]
    public void TypeDamageModify_AbilityMultipliesMatchingMoveDamage()
    {
        int baseline = DamageWith();
        int boosted = DamageWith(hooks:
        [
            new AbilityHook
            {
                Hook = AbilityHookPoint.OnModifyOutgoingDamage,
                Effects =
                [
                    new Effect
                    {
                        Op = "typeDamageModify",
                        Params = Params(("type", "water"), ("multiplierPercent", 200)),
                    },
                ],
            },
        ]);

        Assert.Equal(baseline * 2, boosted);
    }

    [Fact]
    public void TypeDamageBoost_HeldItemMultipliesMatchingMoveDamage()
    {
        int baseline = DamageWith();
        int boosted = DamageWith(heldEffects:
        [
            new Effect
            {
                Op = "typeDamageBoost",
                Params = Params(("type", "water"), ("multiplierPercent", 200)),
            },
        ]);

        Assert.Equal(baseline * 2, boosted);
    }

    [Fact]
    public void ChoiceLock_MultipliesMatchingDamageClass()
    {
        int baseline = DamageWith();
        int boosted = DamageWith(heldEffects:
        [
            new Effect
            {
                Op = "choiceLock",
                Params = Params(("damageClass", "special"), ("multiplierPercent", 200)),
            },
        ]);

        Assert.Equal(baseline * 2, boosted);
    }

    [Fact]
    public void ChoiceLock_RejectsDifferentMoveUntilSwitchOut()
    {
        var player = new BattleCreature(SpeciesId, "Choice", 50, [Normal],
            new Stats(300, 100, 100, 100, 100, 100), [WaterHit(), PhysicalHit(), Inert()],
            heldItemBattleEffects:
            [
                new Effect
                {
                    Op = "choiceLock",
                    Params = Params(("damageClass", "special"), ("multiplierPercent", 150)),
                },
            ]);
        var battle = new BattleController([player, Fast(Inert())], [Slow(Inert())], Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Throws<ArgumentException>(() => battle.ResolveTurn(new UseMove(1), new UseMove(0)));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new Switch(1), new UseMove(0));
        battle.ResolveTurn(new Switch(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(1), new UseMove(0));
    }

    [Fact]
    public void StatModify_OutgoingSpecialAttackChangesDamage()
    {
        int baseline = DamageWith();
        int boosted = DamageWith(hooks:
        [
            new AbilityHook
            {
                Hook = AbilityHookPoint.OnModifyOutgoingDamage,
                Effects =
                [
                    new Effect
                    {
                        Op = "statModify",
                        Params = Params(("stat", "spa"), ("multiplierPercent", 200)),
                    },
                ],
            },
        ]);

        Assert.True(boosted > baseline);
    }

    [Fact]
    public void StatModify_IncomingSpecialDefenseChangesDamage()
    {
        int baseline = DamageWith();
        int reduced = DamageWith(enemyHooks:
        [
            new AbilityHook
            {
                Hook = AbilityHookPoint.OnModifyIncomingDamage,
                Effects =
                [
                    new Effect
                    {
                        Op = "statModify",
                        Params = Params(("stat", "spd"), ("add", 100)),
                    },
                ],
            },
        ]);

        Assert.True(reduced < baseline);
    }

    [Fact]
    public void ContactChanceEffect_StatusesContactingAttacker()
    {
        var player = Fast(ContactHit());
        var enemy = WithContactAbility(new Effect
        {
            Op = "contactChanceEffect",
            Chance = 100,
            Params = Params(("status", "burn")),
        });
        var battle = new BattleController(player, enemy, Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(PersistentStatus.Burn, player.Status);
        Assert.Contains(events, e => e is StatusApplied { Side: BattleSide.Player, Status: PersistentStatus.Burn });
    }

    [Fact]
    public void ContactChanceEffect_ChangesContactingAttackerStage()
    {
        var player = Fast(ContactHit());
        var enemy = WithContactAbility(new Effect
        {
            Op = "contactChanceEffect",
            Chance = 100,
            Params = Params(("stat", "atk"), ("delta", -1)),
        });
        var battle = new BattleController(player, enemy, Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(-1, player.Stage(StatKind.Atk));
        Assert.Contains(events, e => e is StatStageChanged { Side: BattleSide.Player, Stat: StatKind.Atk, Delta: -1 });
    }

    [Fact]
    public void ContactChanceEffect_DoesNotTriggerWithoutContact()
    {
        var player = Fast(PhysicalHit());
        var enemy = WithContactAbility(new Effect
        {
            Op = "contactChanceEffect",
            Chance = 100,
            Params = Params(("status", "burn")),
        });
        var battle = new BattleController(player, enemy, Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Null(player.Status);
        Assert.DoesNotContain(events, e => e is StatusApplied { Side: BattleSide.Player });
    }

    [Fact]
    public void ThresholdHeal_ConsumesOnce_WhenBelowThreshold()
    {
        var player = new BattleCreature(SpeciesId, "Berry", 50, [Normal], new Stats(300, 100, 100, 100, 100, 100),
            [Inert()], heldItemBattleEffects:
            [
                new Effect
                {
                    Op = "thresholdHeal",
                    Params = Params(("thresholdPercent", 50), ("healAmount", 30)),
                },
            ]);
        player.TakeDamage(180);
        var battle = new BattleController(player, Slow(Inert()), Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> first = battle.ResolveTurn(new UseMove(0), new UseMove(0));
        player.TakeDamage(60);
        IReadOnlyList<BattleEvent> second = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(90, player.CurrentHp);
        Assert.Contains(first, e => e is HeldItemConsumed { Side: BattleSide.Player, Op: "thresholdHeal" });
        Assert.Contains(first, e => e is Healed { Side: BattleSide.Player, Amount: 30 });
        Assert.DoesNotContain(second, e => e is HeldItemConsumed);
        Assert.DoesNotContain(second, e => e is Healed { Side: BattleSide.Player });
    }

    [Fact]
    public void StatusCure_ConsumesOnce_WhenStatusMatches()
    {
        var player = WithHeldEffects(new Effect
        {
            Op = "statusCure",
            Params = Params(("status", "poison")),
        });
        player.SetStatus(PersistentStatus.Poison);
        var battle = new BattleController(player, Slow(Inert()), Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> first = battle.ResolveTurn(new UseMove(0), new UseMove(0));
        player.SetStatus(PersistentStatus.Poison);
        IReadOnlyList<BattleEvent> second = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(PersistentStatus.Poison, player.Status);
        Assert.Contains(first, e => e is HeldItemConsumed { Side: BattleSide.Player, Op: "statusCure" });
        Assert.Contains(first, e => e is StatusCured { Side: BattleSide.Player, Status: PersistentStatus.Poison });
        Assert.DoesNotContain(second, e => e is HeldItemConsumed);
        Assert.DoesNotContain(second, e => e is StatusCured);
    }

    [Fact]
    public void StatusCure_DoesNotConsume_WhenStatusDoesNotMatch()
    {
        var player = WithHeldEffects(new Effect
        {
            Op = "statusCure",
            Params = Params(("status", "burn")),
        });
        player.SetStatus(PersistentStatus.Poison);
        var battle = new BattleController(player, Slow(Inert()), Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(PersistentStatus.Poison, player.Status);
        Assert.DoesNotContain(events, e => e is HeldItemConsumed);
        Assert.DoesNotContain(events, e => e is StatusCured);
    }

    [Fact]
    public void SurviveFromFull_ConsumesAndPreventsKoFromFullHp()
    {
        var enemy = WithHeldEffects(new Effect { Op = "surviveFromFull" });
        var battle = new BattleController(Fast(FixedHit(999)), enemy, Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(1, enemy.CurrentHp);
        Assert.Contains(events, e => e is HeldItemConsumed { Side: BattleSide.Enemy, Op: "surviveFromFull" });
        Assert.Contains(events, e => e is DamageDealt { Target: BattleSide.Enemy, Amount: 299 });
        Assert.DoesNotContain(events, e => e is Fainted { Side: BattleSide.Enemy });
    }

    [Fact]
    public void SurviveFromFull_DoesNotTriggerWhenAlreadyDamaged()
    {
        var enemy = WithHeldEffects(new Effect { Op = "surviveFromFull" });
        enemy.TakeDamage(1);
        var battle = new BattleController(Fast(FixedHit(999)), enemy, Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(enemy.IsFainted);
        Assert.DoesNotContain(events, e => e is HeldItemConsumed);
        Assert.Contains(events, e => e is Fainted { Side: BattleSide.Enemy });
    }

    [Fact]
    public void SurviveFromFull_ConsumesOnceThenMultiHitCanKo()
    {
        var enemy = WithHeldEffects(new Effect { Op = "surviveFromFull" });
        var battle = new BattleController(Fast(MultiHit()), enemy, Chart(),
            new FakeRng(ints: [0, 0, 15, 15], doubles: [0.99, 0.99]));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(enemy.IsFainted);
        Assert.Single(events.OfType<HeldItemConsumed>());
        Assert.Equal([299, 1], events.OfType<DamageDealt>().Where(e => e.Target == BattleSide.Enemy).Select(e => e.Amount).ToArray());
        Assert.Contains(events, e => e is Fainted { Side: BattleSide.Enemy });
    }

    [Fact]
    public void ResidualHeal_AbilityHealsFractionAndCapsAtMax()
    {
        var player = WithEndTurnAbility(new Effect
        {
            Op = "residualHeal",
            Params = Params(("num", 1), ("den", 8)),
        });
        player.TakeDamage(20);
        var battle = new BattleController(player, Slow(Inert()), Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(player.MaxHp, player.CurrentHp);
        Assert.Contains(events, e => e is Healed { Side: BattleSide.Player, Amount: 20 });
    }

    [Fact]
    public void ResidualDamage_AbilityCanFaintAndStopsLaterHooksForCreature()
    {
        var player = WithEndTurnAbility(
            new Effect { Op = "residualDamage", Params = Params(("num", 1), ("den", 8)) },
            new Effect { Op = "residualHeal", Params = Params(("num", 1), ("den", 8)) });
        player.TakeDamage(player.MaxHp - 10);
        var battle = new BattleController(player, Slow(Inert()), Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(player.IsFainted);
        Assert.Contains(events, e => e is ResidualDamage { Side: BattleSide.Player });
        Assert.Contains(events, e => e is Fainted { Side: BattleSide.Player });
        Assert.DoesNotContain(events, e => e is Healed { Side: BattleSide.Player });
    }

    [Fact]
    public void ResidualHeal_AtFullHpDoesNotEmitHeal()
    {
        var player = WithEndTurnAbility(new Effect
        {
            Op = "residualHeal",
            Params = Params(("num", 1), ("den", 8)),
        });
        var battle = new BattleController(player, Slow(Inert()), Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(player.MaxHp, player.CurrentHp);
        Assert.DoesNotContain(events, e => e is Healed { Side: BattleSide.Player });
    }

    [Fact]
    public void ResidualHeal_HeldItemHealsAtEndOfTurn()
    {
        var player = WithHeldEffects(new Effect
        {
            Op = "residualHeal",
            Params = Params(("num", 1), ("den", 8)),
        });
        player.TakeDamage(50);
        var battle = new BattleController(player, Slow(Inert()), Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(player.MaxHp - 13, player.CurrentHp);
        Assert.Contains(events, e => e is Healed { Side: BattleSide.Player, Amount: 37 });
    }

    [Fact]
    public void StatusImmunity_AbilityBlocksMatchingStatus()
    {
        var enemy = WithStatusAttemptAbility(new Effect
        {
            Op = "statusImmunity",
            Params = Params(("status", "burn")),
        });
        var battle = new BattleController(Fast(Burner()), enemy, Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Null(enemy.Status);
        Assert.DoesNotContain(events, e => e is StatusApplied { Side: BattleSide.Enemy });
    }

    [Fact]
    public void StatusImmunity_NonMatchingStatusDoesNotBlock()
    {
        var enemy = WithStatusAttemptAbility(new Effect
        {
            Op = "statusImmunity",
            Params = Params(("status", "poison")),
        });
        var battle = new BattleController(Fast(Burner()), enemy, Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(PersistentStatus.Burn, enemy.Status);
        Assert.Contains(events, e => e is StatusApplied { Side: BattleSide.Enemy, Status: PersistentStatus.Burn });
    }

    [Fact]
    public void StatusImmunity_TypeImmunityStillBlocksWhenAbilityDoesNot()
    {
        var enemy = WithStatusAttemptAbility(
            [new Effect { Op = "statusImmunity", Params = Params(("status", "poison")) }],
            [Fire]);
        var battle = new BattleController(Fast(Burner()), enemy, Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Null(enemy.Status);
        Assert.DoesNotContain(events, e => e is StatusApplied { Side: BattleSide.Enemy });
    }

    private static int DamageWith(AbilityHook[]? hooks = null, Effect[]? heldEffects = null, AbilityHook[]? enemyHooks = null)
    {
        var player = new BattleCreature(SpeciesId, "A", 50, [Normal], new Stats(300, 100, 100, 100, 100, 100),
            [WaterHit()], abilityHooks: hooks, heldItemBattleEffects: heldEffects);
        var enemy = new BattleCreature(SpeciesId, "B", 50, [Normal], new Stats(9999, 100, 100, 100, 100, 1),
            [Inert()], abilityHooks: enemyHooks);
        new BattleController(player, enemy, Chart(), new FakeRng(ints: [0, 100], doubles: [0.99]))
            .ResolveTurn(new UseMove(0), new UseMove(0));
        return enemy.MaxHp - enemy.CurrentHp;
    }

    private static BattleCreature WithEndTurnAbility(params Effect[] effects) =>
        new(SpeciesId, "A", 50, [Normal], new Stats(300, 100, 100, 100, 100, 100), [Inert()], abilityHooks:
        [
            new AbilityHook { Hook = AbilityHookPoint.OnEndOfTurn, Effects = effects },
        ]);

    private static BattleCreature WithHeldEffects(params Effect[] effects) =>
        new(SpeciesId, "A", 50, [Normal], new Stats(300, 100, 100, 100, 100, 100), [Inert()],
            heldItemBattleEffects: effects);

    private static BattleMove Burner() =>
        new(EntityId.Parse("move:wisp"), Normal, DamageClass.Status, null, 100, 25, 0, 0,
            ailment: PersistentStatus.Burn, ailmentChance: 100);

    private static BattleMove FixedHit(int amount) =>
        new(EntityId.Parse("move:fixed"), Normal, DamageClass.Physical, null, 100, 25, 0, 0,
            fixedDamage: amount);

    private static BattleMove MultiHit() =>
        new(EntityId.Parse("move:multi"), Normal, DamageClass.Physical, 999, 100, 25, 0, 0,
            multiHitMin: 2, multiHitMax: 2);

    private static BattleCreature WithStatusAttemptAbility(params Effect[] effects) =>
        WithStatusAttemptAbility(effects, [Normal]);

    private static BattleCreature WithStatusAttemptAbility(Effect[] effects, IReadOnlyList<EntityId> types) =>
        new(SpeciesId, "A", 50, types, new Stats(300, 100, 100, 100, 100, 1), [Inert()], abilityHooks:
        [
            new AbilityHook { Hook = AbilityHookPoint.OnStatusAttempt, Effects = effects },
        ]);

    private static BattleCreature WithContactAbility(params Effect[] effects) =>
        new(SpeciesId, "A", 50, [Normal], new Stats(300, 100, 100, 100, 100, 1), [Inert()], abilityHooks:
        [
            new AbilityHook { Hook = AbilityHookPoint.OnContactReceived, Effects = effects },
        ]);

    private static BattleCreature WeatherSummoner(Effect[]? heldEffects = null) =>
        new(SpeciesId, "Rain", 50, [Normal], new Stats(300, 100, 100, 100, 100, 100),
            [Inert()], abilityHooks:
            [
                new AbilityHook
                {
                    Hook = AbilityHookPoint.OnSwitchIn,
                    Effects = [new Effect { Op = "weatherSummon", Params = Params(("weather", "rain")) }],
                },
            ],
            heldItemBattleEffects: heldEffects);

    private static BattleCreature FormCandidate() => BattleCreature.FromInstance(new CreatureInstance
    {
        Species = SpeciesId,
        Level = 50,
        Nature = "hardy",
        CurHp = 999,
        Moves = [new MoveSlot(RainMoveId, 25), new MoveSlot(MoveId, 25)],
    }, Db());

    private static BattleCreature HeldItemFormCandidate(EntityId heldItem) => BattleCreature.FromInstance(new CreatureInstance
    {
        Species = SpeciesId,
        Level = 50,
        Nature = "hardy",
        CurHp = 999,
        Ability = QuietAbilityId.ToString(),
        HeldItem = heldItem,
        Moves = [new MoveSlot(MoveId, 25)],
    }, Db());

    private static BattleCreature TemporaryFormCandidate() => BattleCreature.FromInstance(new CreatureInstance
    {
        Species = SpeciesId,
        Level = 50,
        Nature = "hardy",
        CurHp = 999,
        Ability = QuietAbilityId.ToString(),
        HeldItem = ItemId,
        Moves = [new MoveSlot(MoveId, 25)],
    }, Db());

    private static BattleCreature TimedFormCandidate(int? hpMultiplierPercent = null) => BattleCreature.FromInstance(new CreatureInstance
    {
        Species = SpeciesId,
        Level = 50,
        Nature = "hardy",
        CurHp = 999,
        Ability = QuietAbilityId.ToString(),
        Moves = [new MoveSlot(MoveId, 25)],
    }, Db(hpMultiplierPercent));

    private static GameDb Db(int? timedHpMultiplierPercent = null) => new(new ProjectSettings { Name = "T" },
    [
        new TypeDef { Id = Normal, Name = "Normal" },
        new TypeDef { Id = Water, Name = "Water" },
        new Move
        {
            Id = MoveId,
            Name = "Splash Hit",
            Type = Water,
            DamageClass = DamageClass.Special,
            Power = 60,
            Accuracy = 100,
            Pp = 25,
        },
        new Move
        {
            Id = FormMoveId,
            Name = "Stone Hit",
            Type = Fire,
            DamageClass = DamageClass.Special,
            Power = 80,
            Accuracy = 100,
            Pp = 20,
        },
        new Move
        {
            Id = RainMoveId,
            Name = "Rain Call",
            Type = Normal,
            DamageClass = DamageClass.Status,
            Accuracy = 100,
            Pp = 25,
            Effects = [new Effect { Op = "weather", Params = Params(("weather", "rain")) }],
        },
        new Species
        {
            Id = SpeciesId,
            Name = "Hookmon",
            Types = [Normal],
            BaseStats = new Stats(100, 100, 100, 100, 100, 100),
            GrowthRate = "medium-fast",
            Abilities = [AbilityId],
            Forms =
            [
                new Form
                {
                    FormId = "stone",
                    Activation = FormActivation.Permanent,
                    StatOverrides = new Stats(120, 100, 100, 140, 100, 100),
                    TypeOverrides = [Fire],
                    AbilityOverride = FormAbilityId,
                    MoveRemap = new Dictionary<EntityId, EntityId> { [MoveId] = FormMoveId },
                    Sprites = new SpeciesSprites
                    {
                        Front = EntityId.Parse("sprite:stone_front"),
                        Back = EntityId.Parse("sprite:stone_back"),
                        Icon = EntityId.Parse("sprite:stone_icon"),
                    },
                },
                new Form
                {
                    FormId = "rain",
                    Activation = FormActivation.Condition,
                    StatOverrides = new Stats(120, 100, 100, 140, 100, 100),
                    TypeOverrides = [Water],
                    Condition = new FormCondition { Weather = "rain" },
                    Sprites = new SpeciesSprites
                    {
                        Front = EntityId.Parse("sprite:rain_front"),
                        Back = EntityId.Parse("sprite:rain_back"),
                        Icon = EntityId.Parse("sprite:rain_icon"),
                    },
                },
                new Form
                {
                    FormId = "held",
                    Activation = FormActivation.Condition,
                    StatOverrides = new Stats(120, 100, 100, 140, 100, 100),
                    TypeOverrides = [Fire],
                    Condition = new FormCondition { HeldItem = ItemId },
                    Sprites = new SpeciesSprites
                    {
                        Front = EntityId.Parse("sprite:held_front"),
                        Back = EntityId.Parse("sprite:held_back"),
                        Icon = EntityId.Parse("sprite:held_icon"),
                    },
                },
                new Form
                {
                    FormId = "berry",
                    Activation = FormActivation.Condition,
                    StatOverrides = new Stats(120, 100, 100, 140, 100, 100),
                    TypeOverrides = [Fire],
                    Condition = new FormCondition { HeldItem = BerryId },
                    Sprites = new SpeciesSprites
                    {
                        Front = EntityId.Parse("sprite:berry_front"),
                        Back = EntityId.Parse("sprite:berry_back"),
                        Icon = EntityId.Parse("sprite:berry_icon"),
                    },
                },
                new Form
                {
                    FormId = "burst",
                    Activation = FormActivation.BattleTemporary,
                    StatOverrides = new Stats(120, 100, 100, 140, 100, 100),
                    TypeOverrides = [Fire],
                    RequiredHeldItem = ItemId,
                    RequiredTrainerItem = TrainerKeyId,
                    Sprites = new SpeciesSprites
                    {
                        Front = EntityId.Parse("sprite:burst_front"),
                        Back = EntityId.Parse("sprite:burst_back"),
                        Icon = EntityId.Parse("sprite:burst_icon"),
                    },
                },
                new Form
                {
                    FormId = "giant",
                    Activation = FormActivation.BattleTimed,
                    Turns = 2,
                    HpMultiplierPercent = timedHpMultiplierPercent ?? 200,
                    TypeOverrides = [Fire],
                    MoveRemap = new Dictionary<EntityId, EntityId> { [MoveId] = FormMoveId },
                    Sprites = new SpeciesSprites
                    {
                        Front = EntityId.Parse("sprite:giant_front"),
                        Back = EntityId.Parse("sprite:giant_back"),
                        Icon = EntityId.Parse("sprite:giant_icon"),
                    },
                },
            ],
        },
        new Ability
        {
            Id = AbilityId,
            Name = "Drizzle Root",
            Hooks =
            [
                new AbilityHook
                {
                    Hook = AbilityHookPoint.OnSwitchIn,
                    Effects = [new Effect { Op = "weatherSummon", Params = Params(("weather", "rain")) }],
                },
            ],
        },
        new Ability
        {
            Id = FormAbilityId,
            Name = "Stone Skin",
            Hooks =
            [
                new AbilityHook
                {
                    Hook = AbilityHookPoint.OnEndOfTurn,
                    Effects = [new Effect { Op = "residualHeal", Params = Params(("num", 1), ("den", 16)) }],
                },
            ],
        },
        new Ability
        {
            Id = QuietAbilityId,
            Name = "Quiet Root",
        },
        new Item
        {
            Id = ItemId,
            Name = "River Band",
            Holdable = true,
            BattleEffects =
            [
                new Effect
                {
                    Op = "typeDamageBoost",
                    Params = Params(("type", "water"), ("multiplierPercent", 120)),
                },
            ],
        },
        new Item
        {
            Id = BerryId,
            Name = "Form Berry",
            Holdable = true,
            BattleEffects =
            [
                new Effect
                {
                    Op = "thresholdHeal",
                    Params = Params(("thresholdPercent", 50), ("healAmount", 30)),
                },
            ],
        },
        new Item
        {
            Id = TrainerKeyId,
            Name = "Form Key",
            KeyItem = true,
        },
    ]);

    private static string Golden(string name) => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Battle", "Goldens", $"{name}.golden"))
        .Replace("\r\n", "\n").TrimEnd();

    private static IReadOnlyDictionary<string, JsonElement> Params(params (string Key, object Value)[] values) =>
        values.ToDictionary(v => v.Key, v => JsonSerializer.SerializeToElement(v.Value));

    private static int IndexOf<T>(IReadOnlyList<BattleEvent> events, Func<T, bool>? predicate = null)
        where T : BattleEvent
    {
        for (int i = 0; i < events.Count; i++)
            if (events[i] is T e && (predicate is null || predicate(e)))
                return i;
        return -1;
    }
}
