using System.Globalization;

namespace Cgm.Core.Battle;

public static class BattleRulesets
{
    public const string Gen4Like = "gen4_like";
    public const string ModernReference = "modern_reference";

    public static bool IsSupported(string ruleset) =>
        ruleset is Gen4Like or ModernReference;
}

public enum BattleQueryId
{
    BasePower,
    OffensiveStat,
    DefensiveStat,
    Accuracy,
    Speed,
    Healing,
    FinalDamage,
    CriticalChance,
    Priority,
    Effectiveness,
    SecondaryChance,
    Grounded,
}

public enum BattleQueryValueType { Integer, Fraction }
public enum BattleQueryStage { MoveIdentity, AuthoredBase, SourceTargetState, Hooks, RulesetOverride, FinalClamp }
public enum BattleQueryOperation { Replace, Add, Multiply, Min, Max }
public enum BattleQueryOwnerScope { Source, SourceSide, Target, TargetSide, Field }

public readonly record struct BattleQueryValue
{
    public BattleQueryValue(long numerator, long denominator = 1)
    {
        if (denominator <= 0)
            throw new ArgumentOutOfRangeException(nameof(denominator), "Query denominators must be positive.");

        if (numerator == 0)
        {
            Numerator = 0;
            Denominator = 1;
            return;
        }

        long divisor = GreatestCommonDivisor(Math.Abs(numerator), denominator);
        Numerator = numerator / divisor;
        Denominator = denominator / divisor;
    }

    public long Numerator { get; }
    public long Denominator { get; }
    public bool IsValid => Denominator > 0;
    public bool IsInteger => Denominator == 1;

    public int ToInt32()
    {
        if (!IsInteger)
            throw new InvalidOperationException("The query value is not an integer.");
        return checked((int)Numerator);
    }

    public override string ToString() => IsInteger
        ? Numerator.ToString(CultureInfo.InvariantCulture)
        : $"{Numerator.ToString(CultureInfo.InvariantCulture)}/{Denominator.ToString(CultureInfo.InvariantCulture)}";

    private static long GreatestCommonDivisor(long a, long b)
    {
        while (b != 0)
            (a, b) = (b, a % b);
        return a;
    }
}

public sealed record BattleQuerySpec(
    BattleQueryId Id,
    BattleQueryValueType ValueType,
    BattleQueryValue Minimum,
    BattleQueryValue Maximum);

public sealed record BattleQueryContext(
    BattleSlot? SourceSlot = null,
    BattleCreature? Source = null,
    BattleSlot? TargetSlot = null,
    BattleCreature? Target = null,
    Weather Weather = Weather.None,
    string Ruleset = BattleRulesets.Gen4Like,
    Terrain Terrain = Terrain.None);

public sealed record BattleQueryModifier(
    BattleQueryStage Stage,
    BattleQueryOperation Operation,
    BattleQueryValue Operand,
    int Priority = 0,
    BattleQueryOwnerScope OwnerScope = BattleQueryOwnerScope.Source,
    int InsertionOrder = 0);

public sealed record BattleQueryStep(
    BattleQueryStage Stage,
    BattleQueryOperation? Operation,
    bool Applied,
    BattleQueryValue Input,
    BattleQueryValue? Operand,
    BattleQueryValue Output,
    int? Priority = null,
    BattleQueryOwnerScope? OwnerScope = null,
    int? InsertionOrder = null);

public sealed record BattleQueryResult(
    BattleQueryId Query,
    BattleQueryValueType ValueType,
    BattleQueryValue AuthoredBase,
    BattleQueryValue FinalValue,
    BattleQueryInputs Inputs,
    IReadOnlyList<BattleQueryStep> Steps);

public sealed record BattleQueryInputs(
    BattleSlot? SourceSlot,
    BattleSlot? TargetSlot,
    Weather Weather,
    string Ruleset,
    Terrain Terrain = Terrain.None);

public sealed record BattleQueryTraceEntry(
    int Turn,
    int ActionSequence,
    BattleSlot SourceSlot,
    BattleSlot? TargetSlot,
    BattleQueryResult Result);

public static class BattleQuery
{
    public static BattleQueryValue StatStageMultiplier(int stage)
    {
        stage = StatStages.Clamp(stage);
        return stage >= 0 ? new BattleQueryValue(2 + stage, 2) : new BattleQueryValue(2, 2 - stage);
    }

    public static BattleQueryValue AccuracyStageMultiplier(int accuracyStage, int evasionStage)
    {
        int stage = StatStages.Clamp(accuracyStage - evasionStage);
        return stage >= 0 ? new BattleQueryValue(3 + stage, 3) : new BattleQueryValue(3, 3 - stage);
    }

    public static BattleQuerySpec Spec(BattleQueryId query) => query switch
    {
        BattleQueryId.BasePower => Integer(query, 1, int.MaxValue),
        BattleQueryId.OffensiveStat => Integer(query, 1, int.MaxValue),
        BattleQueryId.DefensiveStat => Integer(query, 1, int.MaxValue),
        BattleQueryId.Accuracy => Integer(query, 0, 100),
        BattleQueryId.Speed => Integer(query, 1, int.MaxValue),
        BattleQueryId.Healing => Integer(query, 0, int.MaxValue),
        BattleQueryId.FinalDamage => Integer(query, 0, int.MaxValue),
        BattleQueryId.CriticalChance => Fraction(query, 0, 1),
        BattleQueryId.Priority => Integer(query, -7, 7),
        BattleQueryId.Effectiveness => Fraction(query, 0, 4),
        BattleQueryId.SecondaryChance => Integer(query, 0, 100),
        BattleQueryId.Grounded => Integer(query, 0, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(query), query, "Unknown battle query."),
    };

    public static int ResolveInteger(BattleQueryId query, int authoredBase,
        IEnumerable<BattleQueryModifier>? modifiers = null, BattleQueryContext? context = null) =>
        Evaluate(query, new BattleQueryValue(authoredBase), modifiers, context).FinalValue.ToInt32();

    public static BattleQueryResult Evaluate(BattleQueryId query, BattleQueryValue authoredBase,
        IEnumerable<BattleQueryModifier>? modifiers = null, BattleQueryContext? context = null)
    {
        BattleQuerySpec spec = Spec(query);
        context ??= new BattleQueryContext();
        if (string.IsNullOrWhiteSpace(context.Ruleset))
            throw new ArgumentException("Query ruleset profile cannot be blank.", nameof(context));
        if (!authoredBase.IsValid)
            throw new ArgumentException("Query authored bases must have a positive denominator.", nameof(authoredBase));
        if (spec.ValueType == BattleQueryValueType.Integer && !authoredBase.IsInteger)
            throw new ArgumentException($"{query} requires an integer authored base.", nameof(authoredBase));

        List<BattleQueryModifier> ordered = ValidateAndOrder(modifiers ?? []);
        var steps = new List<BattleQueryStep>
        {
            new(BattleQueryStage.MoveIdentity, null, true, authoredBase, null, authoredBase),
            new(BattleQueryStage.AuthoredBase, null, true, authoredBase, null, authoredBase),
        };
        BattleQueryValue value = authoredBase;

        foreach (IGrouping<BattleQueryStage, BattleQueryModifier> stage in ordered.GroupBy(modifier => modifier.Stage))
        {
            foreach (IGrouping<BattleQueryOperation, BattleQueryModifier> operation in stage.GroupBy(modifier => modifier.Operation))
            {
                bool replaced = false;
                foreach (BattleQueryModifier modifier in operation)
                {
                    BattleQueryValue input = value;
                    bool applied = modifier.Operation != BattleQueryOperation.Replace || !replaced;
                    if (applied)
                    {
                        value = Apply(spec.ValueType, value, modifier.Operation, modifier.Operand);
                        replaced |= modifier.Operation == BattleQueryOperation.Replace;
                    }
                    steps.Add(new BattleQueryStep(stage.Key, modifier.Operation, applied, input, modifier.Operand, value,
                        modifier.Priority, modifier.OwnerScope, modifier.InsertionOrder));
                }
            }
        }

        BattleQueryValue beforeClamp = value;
        value = Clamp(value, spec.Minimum, spec.Maximum);
        steps.Add(new BattleQueryStep(BattleQueryStage.FinalClamp, null, true, beforeClamp, null, value));
        return new BattleQueryResult(query, spec.ValueType, authoredBase, value,
            new BattleQueryInputs(context.SourceSlot, context.TargetSlot, context.Weather, context.Ruleset,
                context.Terrain), steps.ToArray());
    }

    private static List<BattleQueryModifier> ValidateAndOrder(IEnumerable<BattleQueryModifier> modifiers)
    {
        var list = modifiers.ToList();
        foreach (BattleQueryModifier modifier in list)
        {
            if (!modifier.Operand.IsValid)
                throw new ArgumentException("Query modifier operands must have a positive denominator.", nameof(modifiers));
            if (modifier.Stage is not (BattleQueryStage.SourceTargetState or BattleQueryStage.Hooks or BattleQueryStage.RulesetOverride))
                throw new ArgumentOutOfRangeException(nameof(modifiers), modifier.Stage, "Modifiers can only use mutable query stages.");
            if (!Enum.IsDefined(modifier.Operation))
                throw new ArgumentOutOfRangeException(nameof(modifiers), modifier.Operation, "Unknown query operation.");
            if (!Enum.IsDefined(modifier.OwnerScope))
                throw new ArgumentOutOfRangeException(nameof(modifiers), modifier.OwnerScope, "Unknown query owner scope.");
            if (modifier.InsertionOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(modifiers), "Query insertion order cannot be negative.");
        }

        if (list.GroupBy(modifier => (modifier.Stage, modifier.InsertionOrder)).Any(group => group.Count() > 1))
            throw new ArgumentException("Query modifier insertion identities must be unique within a stage.", nameof(modifiers));

        return list
            .OrderBy(modifier => modifier.Stage)
            .ThenBy(modifier => modifier.Operation)
            .ThenByDescending(modifier => modifier.Priority)
            .ThenBy(modifier => modifier.OwnerScope)
            .ThenBy(modifier => modifier.InsertionOrder)
            .ToList();
    }

    private static BattleQueryValue Apply(BattleQueryValueType valueType, BattleQueryValue current,
        BattleQueryOperation operation, BattleQueryValue operand)
    {
        if (valueType == BattleQueryValueType.Integer)
        {
            long value = current.Numerator;
            long result = operation switch
            {
                BattleQueryOperation.Replace => RequiredInteger(operand),
                BattleQueryOperation.Add => checked(value + RequiredInteger(operand)),
                BattleQueryOperation.Multiply => FloorDivide(checked(value * operand.Numerator), operand.Denominator),
                BattleQueryOperation.Min => Math.Min(value, RequiredInteger(operand)),
                BattleQueryOperation.Max => Math.Max(value, RequiredInteger(operand)),
                _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown query operation."),
            };
            return new BattleQueryValue(result);
        }

        return operation switch
        {
            BattleQueryOperation.Replace => operand,
            BattleQueryOperation.Add => Add(current, operand),
            BattleQueryOperation.Multiply => Multiply(current, operand),
            BattleQueryOperation.Min => Compare(current, operand) <= 0 ? current : operand,
            BattleQueryOperation.Max => Compare(current, operand) >= 0 ? current : operand,
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown query operation."),
        };
    }

    private static BattleQueryValue Add(BattleQueryValue left, BattleQueryValue right) => new(
        checked(left.Numerator * right.Denominator + right.Numerator * left.Denominator),
        checked(left.Denominator * right.Denominator));

    private static BattleQueryValue Multiply(BattleQueryValue left, BattleQueryValue right) => new(
        checked(left.Numerator * right.Numerator), checked(left.Denominator * right.Denominator));

    private static int Compare(BattleQueryValue left, BattleQueryValue right) =>
        checked(left.Numerator * right.Denominator).CompareTo(checked(right.Numerator * left.Denominator));

    private static BattleQueryValue Clamp(BattleQueryValue value, BattleQueryValue minimum, BattleQueryValue maximum) =>
        Compare(value, minimum) < 0 ? minimum : Compare(value, maximum) > 0 ? maximum : value;

    private static long RequiredInteger(BattleQueryValue value) => value.IsInteger
        ? value.Numerator
        : throw new ArgumentException("This integer query operation requires an integer operand.");

    private static long FloorDivide(long numerator, long denominator)
    {
        long result = numerator / denominator;
        return numerator < 0 && numerator % denominator != 0 ? result - 1 : result;
    }

    private static BattleQuerySpec Integer(BattleQueryId id, int minimum, int maximum) =>
        new(id, BattleQueryValueType.Integer, new BattleQueryValue(minimum), new BattleQueryValue(maximum));

    private static BattleQuerySpec Fraction(BattleQueryId id, int minimum, int maximum) =>
        new(id, BattleQueryValueType.Fraction, new BattleQueryValue(minimum), new BattleQueryValue(maximum));
}
