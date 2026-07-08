using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum BattleSide { Player, Enemy }

/// <summary>An action a side submits for a turn (UI/AI produce these; the controller applies them).
/// Switch/item/run join in later battle layers.</summary>
public abstract record BattleAction;
public sealed record UseMove(int MoveIndex) : BattleAction;
public sealed record Switch(int PartyIndex) : BattleAction;
public sealed record ThrowBall(double BallBonus, double StatusBonus) : BattleAction;

/// <summary>What happened during resolution — the stream the UI renders (BATTLE_SYSTEM_SPEC).
/// UI consumes these; it never reads or mutates state to infer what happened.</summary>
public abstract record BattleEvent;
public sealed record MoveUsed(BattleSide Side, EntityId Move) : BattleEvent;
public sealed record MoveMissed(BattleSide Side, EntityId Move) : BattleEvent;
public sealed record DamageDealt(BattleSide Target, int Amount, double Effectiveness, bool Crit) : BattleEvent;
public sealed record Fainted(BattleSide Side) : BattleEvent;
public sealed record SwitchedIn(BattleSide Side, int PartyIndex) : BattleEvent;
public sealed record StatusApplied(BattleSide Side, PersistentStatus Status) : BattleEvent;
public sealed record StatStageChanged(BattleSide Side, StatKind Stat, int Delta) : BattleEvent;
public sealed record StatusDamage(BattleSide Side, int Amount) : BattleEvent;
public sealed record Healed(BattleSide Side, int Amount) : BattleEvent;
public sealed record Recoiled(BattleSide Side, int Amount) : BattleEvent;
public sealed record CritBoosted(BattleSide Side) : BattleEvent;
public sealed record LeechSeeded(BattleSide Side) : BattleEvent;
public sealed record LeechSapped(BattleSide Side, int Amount) : BattleEvent;
public sealed record HazardSet(BattleSide Side, int Layers) : BattleEvent;
public sealed record StealthRockSet(BattleSide Side) : BattleEvent;
public sealed record HurtByHazard(BattleSide Side, int Amount) : BattleEvent;
public sealed record WeatherChanged(Weather Weather) : BattleEvent;
public sealed record WeatherEnded(Weather Weather) : BattleEvent;
public sealed record WeatherDamage(BattleSide Side, int Amount) : BattleEvent;
public sealed record Bound(BattleSide Side) : BattleEvent;
public sealed record BoundHurt(BattleSide Side, int Amount) : BattleEvent;
public sealed record BindReleased(BattleSide Side) : BattleEvent;
public sealed record Protected(BattleSide Side) : BattleEvent;
public sealed record ProtectFailed(BattleSide Side) : BattleEvent;
public sealed record MoveBlocked(BattleSide Side) : BattleEvent;
public sealed record ForcedOut(BattleSide Side) : BattleEvent;
public sealed record Charging(BattleSide Side, EntityId Move) : BattleEvent;
public sealed record FullyParalyzed(BattleSide Side) : BattleEvent;
public sealed record Confused(BattleSide Side) : BattleEvent;
public sealed record ConfusionEnded(BattleSide Side) : BattleEvent;
public sealed record HurtInConfusion(BattleSide Side, int Amount) : BattleEvent;
public sealed record Flinched(BattleSide Side) : BattleEvent;
public sealed record StillAsleep(BattleSide Side) : BattleEvent;
public sealed record WokeUp(BattleSide Side) : BattleEvent;
public sealed record Thawed(BattleSide Side) : BattleEvent;
public sealed record StillFrozen(BattleSide Side) : BattleEvent;
public sealed record BallThrown : BattleEvent;
public sealed record CaptureShakes(int Count) : BattleEvent;
public sealed record Captured(BattleSide Side) : BattleEvent;
public sealed record BrokeFree : BattleEvent;
public sealed record BattleEnded(BattleSide Winner) : BattleEvent;

public sealed record BattleOutcome(BattleSide Winner);
