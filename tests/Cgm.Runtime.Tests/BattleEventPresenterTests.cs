using System.Reflection;
using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>ENGINE_RUNTIME_SPEC 16F event-catalog completeness: every event Core can emit must have
/// a presentation. An unpresented event fails here rather than silently vanishing from the log.</summary>
public sealed class BattleEventPresenterTests
{
    /// <summary>Every concrete event type the engine can emit, discovered by reflection so adding one
    /// in Core automatically enters this suite.</summary>
    public static TheoryData<Type> EventTypes()
    {
        var data = new TheoryData<Type>();
        foreach (Type type in typeof(BattleEvent).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(BattleEvent).IsAssignableFrom(t))
            .OrderBy(t => t.Name, StringComparer.Ordinal))
            data.Add(type);
        return data;
    }

    /// <summary>Builds an instance with neutral arguments, so the test needs no per-event fixture.</summary>
    private static BattleEvent Instantiate(Type type)
    {
        ConstructorInfo ctor = type.GetConstructors()
            .OrderBy(c => c.GetParameters().Length)
            .First();
        object?[] args = ctor.GetParameters().Select(p => Default(p.ParameterType)).ToArray();
        return (BattleEvent)ctor.Invoke(args);
    }

    private static object? Default(Type type)
    {
        Type target = Nullable.GetUnderlyingType(type) ?? type;

        if (target == typeof(string))
            return "";
        if (target.IsEnum)
            return Enum.GetValues(target).GetValue(0);
        if (target == typeof(EntityId))
            return EntityId.Parse("move:test");
        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(target) && target != typeof(string))
            return target.IsArray
                ? Array.CreateInstance(target.GetElementType()!, 0)
                : Activator.CreateInstance(typeof(List<>).MakeGenericType(
                    target.IsGenericType ? target.GetGenericArguments()[0] : typeof(object)));
        if (target.IsValueType)
            return Activator.CreateInstance(target);
        if (typeof(BattleEvent).IsAssignableFrom(target))
            return null;
        return null;
    }

    [Theory]
    [MemberData(nameof(EventTypes))]
    public void EveryEventTypeHasAPresentation(Type type)
    {
        BattleEvent instance = Instantiate(type);
        string? line = BattleEventPresenter.TryLine(instance, id => id.Slug);

        Assert.True(line is not null,
            $"{type.Name} has no presentation. Add it to BattleEventPresenter so it cannot vanish "
            + "from the battle log.");
        Assert.False(string.IsNullOrWhiteSpace(line), $"{type.Name} presents as blank text.");
    }

    /// <summary>The discovery itself must be finding events, or the theory above would pass vacuously
    /// with an empty data set.</summary>
    [Fact]
    public void TheCatalogCoversTheWholeEventSurface()
    {
        int discovered = typeof(BattleEvent).Assembly.GetTypes()
            .Count(t => !t.IsAbstract && typeof(BattleEvent).IsAssignableFrom(t));
        Assert.True(discovered >= 70, $"only discovered {discovered} event types");
    }

    [Fact]
    public void KnownEventsPresentTheirDetail()
    {
        Assert.Equal("Player used tackle", BattleEventPresenter.Line(
            new MoveUsed(BattleSide.Player, EntityId.Parse("move:tackle")), id => id.Slug));
        Assert.Contains("fainted", BattleEventPresenter.Line(new Fainted(BattleSide.Enemy)));
        Assert.Contains("won", BattleEventPresenter.Line(new BattleEnded(BattleSide.Player)));
        Assert.Equal("The battle ended", BattleEventPresenter.Line(new BattleEnded(null)));
        Assert.Equal("Got away safely", BattleEventPresenter.Line(
            new Escaped(BattleSide.Player, 1, 256, null)));
        Assert.Equal("You cannot run from a trainer battle!", BattleEventPresenter.Line(
            new EscapePrevented(BattleSide.Player, EscapePreventionReason.TrainerBattle)));
    }

    /// <summary>The fallback is visible rather than silent, so a gap shows up in play too.</summary>
    [Fact]
    public void AnUnpresentedEventRendersAVisibleMarker()
    {
        string marker = BattleEventPresenter.Unpresented(new Fainted(BattleSide.Player));
        Assert.Contains("unpresented", marker);
        Assert.Contains(nameof(Fainted), marker);
    }

    [Fact]
    public void NullArguments_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => BattleEventPresenter.TryLine(null!, id => id.Slug));
        Assert.Throws<ArgumentNullException>(() =>
            BattleEventPresenter.TryLine(new Fainted(BattleSide.Player), null!));
    }
}
