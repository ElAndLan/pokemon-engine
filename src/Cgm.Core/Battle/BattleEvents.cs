using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum BattleSide { Player, Enemy }

/// <summary>An action a side submits for a turn (UI/AI produce these; the controller applies them).
/// Switch/item/run join in later battle layers.</summary>
public abstract record BattleAction;
public sealed record UseMove(int MoveIndex) : BattleAction;

/// <summary>What happened during resolution — the stream the UI renders (BATTLE_SYSTEM_SPEC).
/// UI consumes these; it never reads or mutates state to infer what happened.</summary>
public abstract record BattleEvent;
public sealed record MoveUsed(BattleSide Side, EntityId Move) : BattleEvent;
public sealed record MoveMissed(BattleSide Side, EntityId Move) : BattleEvent;
public sealed record DamageDealt(BattleSide Target, int Amount, double Effectiveness, bool Crit) : BattleEvent;
public sealed record Fainted(BattleSide Side) : BattleEvent;
public sealed record BattleEnded(BattleSide Winner) : BattleEvent;

public sealed record BattleOutcome(BattleSide Winner);
