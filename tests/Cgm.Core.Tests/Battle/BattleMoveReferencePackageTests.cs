using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleMoveReferencePackageTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Alternate = EntityId.Parse("type:alternate");

    [Fact]
    public void Compiler_ProducesClosedMoveReferenceAndTurnOrderVocabulary()
    {
        BattleMove call = Compile("call", MoveTarget.Selected,
            Op("callMove", ("selector", "authoredPool"), ("pool", "move:first,move:second"),
                ("excludeTags", "uncallable,scripted")));
        CallMoveEffect compiledCall = Assert.Single(call.SecondaryEffects.OfType<CallMoveEffect>());
        Assert.Equal(MoveReferenceSelector.AuthoredPool, compiledCall.Profile.Selector);
        Assert.Equal([EntityId.Parse("move:first"), EntityId.Parse("move:second")],
            compiledCall.Profile.AuthoredPool);
        Assert.Equal(["scripted", "uncallable"], compiledCall.Profile.ExcludedTags.Order());

        BattleMove order = Compile("order", MoveTarget.Ally,
            Op("turnOrderIntent", ("kind", "boostPower"), ("num", 5), ("den", 4)));
        Assert.Equal(new TurnOrderIntentProfile(TurnOrderIntentKind.BoostPower, new Fraction(5, 4)),
            Assert.Single(order.SecondaryEffects.OfType<TurnOrderIntentEffect>()).Profile);

        Assert.Throws<ArgumentException>(() => Compile("bad_call", MoveTarget.Selected,
            Op("callMove", ("selector", "authoredPool"))));
        Assert.Throws<ArgumentException>(() => Compile("bad_order", MoveTarget.EntireField,
            Op("turnOrderIntent", ("kind", "actNext"))));

        BattleMove pair = CompileDamage("pair", Op("pairedAction", ("key", "duet"),
            ("member", "first"), ("mode", "combine"),
            ("pairs", "second:normal:speedReduction"), ("num", 2), ("den", 1)));
        PairedActionProfile pairProfile = Assert.Single(pair.SecondaryEffects.OfType<PairedActionEffect>()).Profile;
        Assert.Equal(("duet", "first", PairedActionMode.Combine),
            (pairProfile.Key, pairProfile.Member, pairProfile.Mode));
        Assert.Equal(new PairedActionOption("second", Normal, PairedActionSideEffect.SpeedReduction),
            Assert.Single(pairProfile.Options));
    }

    [Fact]
    public void Selector_PreservesAuthoredOrderFiltersAndUsesOnlyRequiredDraw()
    {
        BattleMove excluded = Move("excluded", tags: ["uncallable"]);
        BattleMove first = Move("first");
        BattleMove second = Move("second");
        var rng = new CountingRng(1);
        MoveReferenceCandidate[] candidates =
        [
            new(excluded, null, 0, 0), new(first, null, 0, 1),
            new(first, null, 1, 0), new(second, null, 0, 2),
        ];

        MoveReferenceCandidate? selected = MoveReferenceResolver.Select(candidates,
            MoveReferenceResolver.DefaultExcludedTags, false, rng, out int? draw, out int count);

        Assert.Same(second, selected!.Move);
        Assert.Equal(1, draw);
        Assert.Equal(2, count);
        Assert.Equal(1, rng.Calls);
        Assert.Same(first, MoveReferenceResolver.Select([candidates[1]],
            MoveReferenceResolver.DefaultExcludedTags, false, rng, out draw, out count)!.Move);
        Assert.Null(draw);
        Assert.Equal(1, rng.Calls);
        Assert.Null(MoveReferenceResolver.Select([candidates[0]],
            MoveReferenceResolver.DefaultExcludedTags, false, rng, out draw, out count));
        Assert.Equal(0, count);
        Assert.Equal(1, rng.Calls);
    }

    [Theory]
    [InlineData(CalledMovePpOwner.Caller, 9, 10)]
    [InlineData(CalledMovePpOwner.Called, 10, 9)]
    public void CalledMove_UsesDeclaredPpOwnerAndAttributesCallAndDamage(
        CalledMovePpOwner ppOwner, int callerPp, int calledPp)
    {
        BattleMove called = Move("called");
        BattleMove caller = Call("caller", MoveReferenceSelector.AuthoredPool, ppOwner,
            [called.Move]);
        BattleCreature source = Creature("source", 100, caller, called);
        BattleCreature target = Creature("target", 1, Inert());
        var battle = new BattleController(source, target, Chart(), new CountingRng());

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Equal(callerPp, caller.Pp);
        Assert.Equal(calledPp, called.Pp);
        Assert.Equal([caller.Move, called.Move], events.OfType<MoveUsed>().Select(e => e.Move));
        MoveCalled edge = Assert.Single(events.OfType<MoveCalled>());
        Assert.Equal((caller.Move, called.Move, 1), (edge.Caller, edge.Called, edge.Depth));
        Assert.Equal(called.Move, Assert.Single(battle.ActionHistory.Snapshot()).Move);
        Assert.Contains(battle.ActionHistory.DamageSnapshot(),
            entry => entry.Move == called.Move && entry.ActualHpRemoved > 0);
    }

    [Fact]
    public void UserKnownSelector_ReadsTheEffectiveOverriddenMoveList()
    {
        // A UserKnown caller selects from the creature's live move list; ADR-011 OverrideMoves (Transform/
        // Mimic) replaces that list, so the newly injected move becomes selectable (15F-7 over 15F-6).
        BattleMove caller = new(EntityId.Parse("move:caller"), Normal, DamageClass.Status, null, null, 10, 0, 0,
            target: MoveTarget.Selected, tags: ["uncallable"],
            secondaryEffects: [new CallMoveEffect(new CallMoveProfile(MoveReferenceSelector.UserKnown,
                CalledMovePpOwner.Caller, [], new Dictionary<BattleEnvironment, EntityId>(),
                MoveReferenceResolver.DefaultExcludedTags))]);
        BattleMove chosen = Inert("chosen"); // status move -> no execution RNG, isolating the selection draw
        BattleCreature source = Creature("source", 100, caller);
        source.OverrideMoves([caller, chosen]); // as if Transform/Mimic injected 'chosen'
        BattleCreature target = Creature("target", 1, Inert());
        var rng = new CountingRng();
        var battle = new BattleController(source, target, Chart(), rng);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Equal([caller.Move, chosen.Move], events.OfType<MoveUsed>().Select(e => e.Move));
        Assert.Contains(events, e => e is MoveCalled { Called: var called } && called == chosen.Move);
        Assert.Equal(0, rng.Calls); // the caller is excluded, leaving one candidate -> no draw
    }

    [Fact]
    public void CallLoop_StopsAtDepthEightWithoutAnUnneededDraw()
    {
        BattleMove loop = Call("loop", MoveReferenceSelector.ExplicitReference,
            CalledMovePpOwner.Caller, []);
        BattleCreature source = Creature("source", 100, loop);
        BattleCreature ally = Creature("ally", 1, Inert());
        BattleCreature target = Creature("target", 1, Inert());
        BattleCreature other = Creature("other", 1, Inert());
        var rng = new CountingRng();
        var battle = new BattleController([source, ally], [target, other], BattleTopology.Doubles,
            [0, 1], [0, 1], Chart(), rng);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(Actions(
            new BattleActionSubmission(Player0, new UseMove(0), new MoveReferenceSelection(Player0, 0)),
            Submission(Player1, new Pass()), Submission(Enemy0, new Pass()), Submission(Enemy1, new Pass())));

        Assert.Equal(8, events.OfType<MoveCalled>().Count());
        Assert.Contains(events, e => e is MoveCallFailed { Reason: MoveCallFailureReason.DepthExceeded });
        Assert.Equal(0, rng.Calls);
        Assert.Equal(9, loop.Pp);
    }

    [Fact]
    public void CalledMove_RevalidatesItsDifferentTargetShape()
    {
        BattleMove allyBuff = new(EntityId.Parse("move:ally_buff"), Normal, DamageClass.Status,
            null, null, 10, 0, 0, target: MoveTarget.Ally,
            secondaryEffects: [new StatChangeEffect(StatKind.Atk, 1, false)]);
        BattleMove caller = Call("caller", MoveReferenceSelector.AuthoredPool,
            CalledMovePpOwner.Caller, [allyBuff.Move]);
        BattleCreature source = Creature("source", 100, caller, allyBuff);
        BattleCreature ally = Creature("ally", 20, Inert());
        BattleCreature enemy0 = Creature("enemy0", 10, Inert());
        BattleCreature enemy1 = Creature("enemy1", 5, Inert());
        var battle = DoublesBattle(source, ally, enemy0, enemy1);

        battle.ResolveTurn(Actions(
            Submission(Player0, new UseMove(0), Enemy0),
            Submission(Player1, new Pass()), Submission(Enemy0, new Pass()), Submission(Enemy1, new Pass())));

        Assert.Equal(1, ally.Stage(StatKind.Atk));
        Assert.Equal(0, enemy0.Stage(StatKind.Atk));
    }

    [Fact]
    public void ActNext_MutatesOnlyThePendingCurrentTurnOrder()
    {
        BattleMove actNext = Order("act_next", TurnOrderIntentKind.ActNext, MoveTarget.Selected);
        BattleCreature source = Creature("source", 100, actNext);
        BattleCreature ally = Creature("ally", 1, Inert());
        BattleCreature enemy0 = Creature("enemy0", 80, Move("enemy_fast"));
        BattleCreature enemy1 = Creature("enemy1", 20, Move("enemy_slow"));
        var battle = DoublesBattle(source, ally, enemy0, enemy1);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(Actions(
            Submission(Player0, new UseMove(0), Enemy1), Submission(Player1, new Pass()),
            Submission(Enemy0, new UseMove(0), Player0), Submission(Enemy1, new UseMove(0), Player0)));

        Assert.Contains(events, e => e is TurnOrderIntentApplied { Kind: TurnOrderIntentKind.ActNext });
        Assert.Equal([actNext.Move, enemy1.Moves[0].Move, enemy0.Moves[0].Move],
            events.OfType<MoveUsed>().Select(e => e.Move));
    }

    [Fact]
    public void BoostAndRepeat_AffectPendingActionOnce()
    {
        BattleMove boost = Order("boost", TurnOrderIntentKind.BoostPower, MoveTarget.Ally);
        BattleMove repeat = Order("repeat", TurnOrderIntentKind.RepeatPending, MoveTarget.Selected);
        BattleMove attack = Move("attack");
        BattleCreature source = Creature("source", 100, boost);
        BattleCreature ally = Creature("ally", 50, attack);
        BattleCreature enemy0 = Creature("enemy0", 20, Inert());
        BattleCreature enemy1 = Creature("enemy1", 10, Inert());
        var boosted = DoublesBattle(source, ally, enemy0, enemy1);
        int before = enemy0.CurrentHp;

        boosted.ResolveTurn(Actions(Submission(Player0, new UseMove(0), Player1),
            Submission(Player1, new UseMove(0), Enemy0), Submission(Enemy0, new Pass()), Submission(Enemy1, new Pass())));
        int boostedDamage = before - enemy0.CurrentHp;

        BattleCreature repeater = Creature("repeater", 100, repeat);
        BattleCreature idle = Creature("idle", 1, Inert());
        BattleMove repeatedAttack = Move("repeated_attack");
        BattleCreature pending = Creature("pending", 50, repeatedAttack);
        BattleCreature other = Creature("other", 10, Inert());
        var repeated = DoublesBattle(repeater, idle, pending, other);
        IReadOnlyList<BattleEvent> repeatedEvents = repeated.ResolveTurn(Actions(
            Submission(Player0, new UseMove(0), Enemy0), Submission(Player1, new Pass()),
            Submission(Enemy0, new UseMove(0), Player0), Submission(Enemy1, new Pass())));

        Assert.True(boostedDamage > 0);
        Assert.Equal(2, repeatedEvents.OfType<MoveUsed>().Count(e => e.Move == repeatedAttack.Move));
        Assert.Equal(8, repeatedAttack.Pp);
    }

    [Fact]
    public void CombinedPair_DefersFirstPartnerAndUsesSharedBoostTypeAndSideCondition()
    {
        BattleMove first = Pair("first", "alpha", "beta");
        BattleMove second = Pair("second", "beta", "alpha", Alternate);
        BattleCreature source = Creature("source", 100, first);
        BattleCreature ally = Creature("ally", 10, second);
        BattleCreature enemy0 = Creature("enemy0", 50, Inert());
        BattleCreature enemy1 = Creature("enemy1", 40, Inert());
        var battle = DoublesBattle(source, ally, enemy0, enemy1);
        int before = enemy0.CurrentHp;

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(Actions(
            Submission(Player0, new UseMove(0), Enemy0), Submission(Player1, new UseMove(0), Enemy0),
            Submission(Enemy0, new Pass()), Submission(Enemy1, new Pass())));

        Assert.Equal([first.Move, second.Move], events.OfType<MoveUsed>().Select(e => e.Move));
        Assert.Single(events.OfType<PairedActionPrepared>());
        Assert.Equal(9, first.Pp);
        Assert.Equal(9, second.Pp);
        Assert.True(enemy0.CurrentHp < before);
        Assert.Contains(battle.ConditionSnapshot, condition => condition.Definition.Id
            == SideConditions.For(BattleSideCondition.SpeedReduction).Id
            && condition.Owner.Side == BattleSide.Enemy);
        BattleDamageRecord damage = Assert.Single(battle.ActionHistory.DamageSnapshot(),
            damage => damage.Move == second.Move);
        Assert.Equal(Normal, damage.DamageType);
    }

    [Fact]
    public void MoveReferenceSelection_MatchesExactRngAndEventGolden()
    {
        BattleMove first = Inert("golden_first");
        BattleMove second = Inert("golden_second");
        BattleMove caller = Call("golden_caller", MoveReferenceSelector.AuthoredPool,
            CalledMovePpOwner.Caller, [first.Move, second.Move]);
        BattleCreature source = Creature("source", 100, caller, first, second);
        var rng = new CountingRng(1);
        var battle = new BattleController(source, Creature("target", 1, Inert()), Chart(), rng);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());
        string actual = string.Join('\n',
        [
            .. events.Where(item => item is MoveUsed or MoveCalled).Select(item => item switch
            {
                MoveUsed used => $"event:MoveUsed:{used.Move}",
                MoveCalled called => $"event:MoveCalled:{called.Caller}>{called.Called}:{called.Depth}",
                _ => throw new InvalidOperationException(),
            }),
            .. battle.Trace.Where(entry => entry.Kind == EffectTraceKind.MoveSelection)
                .Select(entry => $"trace:{entry.Kind}:{entry.DrawResult}:{entry.DrawBound}:{entry.Value}"),
            $"pp:{caller.Pp},{first.Pp},{second.Pp}",
            $"rng:{rng.Calls}",
        ]);

        Assert.Equal(Golden("move-reference"), actual);
    }

    [Fact]
    public void EnvironmentAndPartySelectors_UseSharedBattleStateInStableOrder()
    {
        BattleMove environmentMove = Move("environment_move");
        BattleMove environmentCaller = new(EntityId.Parse("move:environment_caller"), Normal,
            DamageClass.Status, null, null, 10, 0, 0, secondaryEffects:
            [new CallMoveEffect(new CallMoveProfile(MoveReferenceSelector.EnvironmentPool,
                CalledMovePpOwner.Called, [],
                new Dictionary<BattleEnvironment, EntityId>
                    { [BattleEnvironment.Building] = environmentMove.Move },
                MoveReferenceResolver.DefaultExcludedTags))]);
        BattleCreature source = Creature("source", 100, environmentCaller);
        var environmentBattle = new BattleController(source, Creature("target", 1, Inert()),
            Chart(), new CountingRng(), moveData: [environmentMove]);

        IReadOnlyList<BattleEvent> environmentEvents = environmentBattle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Contains(environmentEvents, item => item is MoveCalled { Called: var called }
            && called == environmentMove.Move);
        Assert.Equal(10, environmentMove.Pp);

        BattleMove partyMove = Move("party_move");
        BattleMove partyCaller = Call("party_caller", MoveReferenceSelector.PartyKnown,
            CalledMovePpOwner.Caller, []);
        BattleCreature partySource = Creature("party_source", 100, partyCaller);
        BattleCreature partyReserve = Creature("party_reserve", 1, partyMove);
        var partyBattle = new BattleController([partySource, partyReserve], [Creature("other", 1, Inert())],
            Chart(), new CountingRng());

        IReadOnlyList<BattleEvent> partyEvents = partyBattle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Contains(partyEvents, item => item is MoveCalled { Called: var called }
            && called == partyMove.Move);
    }

    [Fact]
    public void SmartAi_PreviewsOnlyDeterministicCalledMoveAndKeepsOrderIntentPrivate()
    {
        BattleMove called = Move("called");
        BattleMove caller = Call("caller", MoveReferenceSelector.AuthoredPool,
            CalledMovePpOwner.Caller, [called.Move]);
        BattleMove order = Order("order", TurnOrderIntentKind.ActLast, MoveTarget.Selected);
        BattleCreature source = Creature("source", 100, caller, called, order);
        BattleCreature target = Creature("target", 1, Inert());

        SmartAiDecision decision = SmartAi.ChooseAction(new SmartAiContext(
            [source], 0, [target], 0, Chart(), new CountingRng()));

        Assert.Contains(decision.Scores.Single(score => score.Action == new UseMove(0)).Components,
            component => component.Name == "calledMovePreview");
        Assert.Contains(decision.Scores.Single(score => score.Action == new UseMove(2)).Components,
            component => component.Name == "turnOrderIntent" && component.Value == 0);
    }

    [Fact]
    public void CalledMove_FailsWhenItsRevalidatedTargetDisappears()
    {
        BattleMove allyMove = new(EntityId.Parse("move:ally_move"), Normal, DamageClass.Status,
            null, null, 10, 0, 0, target: MoveTarget.Ally,
            secondaryEffects: [new StatChangeEffect(StatKind.Atk, 1, false)]);
        BattleMove caller = Call("caller", MoveReferenceSelector.AuthoredPool,
            CalledMovePpOwner.Caller, [allyMove.Move]);
        BattleCreature source = Creature("source", 10, caller, allyMove);
        BattleCreature ally = Creature("ally", 1, Inert());
        ally.TakeDamage(ally.MaxHp - 1);
        BattleCreature striker = Creature("striker", 100,
            new BattleMove(EntityId.Parse("move:strike"), Normal, DamageClass.Physical,
                500, null, 10, 0, 0));
        var battle = DoublesBattle(source, ally, striker, Creature("other", 1, Inert()));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(Actions(
            Submission(Player0, new UseMove(0), Enemy0), Submission(Player1, new Pass()),
            Submission(Enemy0, new UseMove(0), Player1), Submission(Enemy1, new Pass())));

        Assert.Contains(events, item => item is MoveCallFailed
            { Reason: MoveCallFailureReason.TargetUnavailable });
        Assert.Equal(9, caller.Pp);
        Assert.Equal(0, source.Stage(StatKind.Atk));
    }

    [Fact]
    public void ActLast_ReordersPendingTargetAndExecutedTargetIsRejected()
    {
        BattleMove actLast = Order("act_last", TurnOrderIntentKind.ActLast, MoveTarget.Selected);
        BattleCreature source = Creature("source", 100, actLast);
        BattleCreature ally = Creature("ally", 1, Inert());
        BattleCreature enemy0 = Creature("enemy0", 80, Move("enemy_fast"));
        BattleCreature enemy1 = Creature("enemy1", 40, Move("enemy_slow"));
        var battle = DoublesBattle(source, ally, enemy0, enemy1);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(Actions(
            Submission(Player0, new UseMove(0), Enemy0), Submission(Player1, new Pass()),
            Submission(Enemy0, new UseMove(0), Player0), Submission(Enemy1, new UseMove(0), Player0)));
        Assert.Equal([actLast.Move, enemy1.Moves[0].Move, enemy0.Moves[0].Move],
            events.OfType<MoveUsed>().Select(item => item.Move));

        BattleMove late = Order("late", TurnOrderIntentKind.ActNext, MoveTarget.Selected);
        BattleCreature slow = Creature("slow", 1, late);
        BattleCreature fast = Creature("fast", 100, Move("fast_move"));
        var singles = new BattleController(slow, fast, Chart(), new CountingRng());
        IReadOnlyList<BattleEvent> rejected = singles.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Contains(rejected, item => item is TurnOrderIntentFailed
            { Kind: TurnOrderIntentKind.ActNext });
    }

    private static readonly BattleSlot Player0 = new(BattleSide.Player, 0);
    private static readonly BattleSlot Player1 = new(BattleSide.Player, 1);
    private static readonly BattleSlot Enemy0 = new(BattleSide.Enemy, 0);
    private static readonly BattleSlot Enemy1 = new(BattleSide.Enemy, 1);

    private static BattleActionSubmission Submission(BattleSlot source, BattleAction action,
        BattleSlot? target = null) => new(source, action,
            target is { } slot ? new ActiveSlotSelection(slot) : null);

    private static BattleTurnActions Actions(params BattleActionSubmission[] submissions) =>
        new(BattleTopology.Doubles, submissions);

    private static BattleController DoublesBattle(BattleCreature player0, BattleCreature player1,
        BattleCreature enemy0, BattleCreature enemy1) => new([player0, player1], [enemy0, enemy1],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new CountingRng());

    private static BattleMove Call(string slug, MoveReferenceSelector selector,
        CalledMovePpOwner ppOwner, IReadOnlyList<EntityId> pool) => new(EntityId.Parse($"move:{slug}"),
            Normal, DamageClass.Status, null, null, 10, 0, 0, target: selector == MoveReferenceSelector.ExplicitReference
                ? MoveTarget.SpecificMove : MoveTarget.Selected,
            secondaryEffects: [new CallMoveEffect(new CallMoveProfile(selector, ppOwner, pool,
                new Dictionary<BattleEnvironment, EntityId>(), MoveReferenceResolver.DefaultExcludedTags))]);

    private static BattleMove Order(string slug, TurnOrderIntentKind kind, MoveTarget target) =>
        new(EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 10, 0, 0,
            target: target, secondaryEffects: [new TurnOrderIntentEffect(new(kind))]);

    private static BattleMove Pair(string slug, string member, string partner, EntityId? authoredType = null) =>
        new(EntityId.Parse($"move:{slug}"), authoredType ?? Normal, DamageClass.Special, 60, 100, 10, 0, 0,
            secondaryEffects: [new PairedActionEffect(new PairedActionProfile("duet", member,
                PairedActionMode.Combine,
                [new PairedActionOption(partner, Normal, PairedActionSideEffect.SpeedReduction)],
                new Fraction(2, 1)))]);

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }, new TypeDef { Id = Alternate }]);

    private static BattleCreature Creature(string slug, int speed, params BattleMove[] moves) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [Normal],
            new Stats(1000, 100, 100, 100, 100, speed), moves);

    private static BattleMove Inert(string slug = "inert") => new(EntityId.Parse($"move:{slug}"), Normal,
        DamageClass.Status, null, null, 20, 0, 0);

    private static BattleMove Move(string slug, IReadOnlyList<string>? tags = null) =>
        new(EntityId.Parse($"move:{slug}"), Normal, DamageClass.Physical, 40, 100, 10, 0, 0,
            tags: tags);

    private static BattleMove Compile(string slug, MoveTarget target, params Effect[] effects) =>
        MoveCompiler.ToBattleMove(new Move
        {
            Id = EntityId.Parse($"move:{slug}"), Name = slug, Type = Normal,
            DamageClass = DamageClass.Status, Pp = 10, Target = target, Effects = effects,
        });

    private static BattleMove CompileDamage(string slug, params Effect[] effects) =>
        MoveCompiler.ToBattleMove(new Move
        {
            Id = EntityId.Parse($"move:{slug}"), Name = slug, Type = Normal,
            DamageClass = DamageClass.Special, Power = 60, Accuracy = 100, Pp = 10,
            Target = MoveTarget.Selected, Effects = [new Effect { Op = "damage" }, .. effects],
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

    private sealed class CountingRng(params int[] draws) : IRng
    {
        private int _index;
        public int Calls { get; private set; }
        public int Next(int maxExclusive) { Calls++; return draws.Length == 0 ? 0 : draws[_index++ % draws.Length] % maxExclusive; }
        public int Next(int minInclusive, int maxExclusive) => minInclusive + Next(maxExclusive - minInclusive);
        public double NextDouble() { Calls++; return 0; }
    }
}
