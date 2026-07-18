using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum BattleAbilityOperation { Copy, Swap, Replace, Suppress }
public enum BattleAbilitySubject { User, Target, UserAndAllies }
public enum BattleAbilityMutationFailure
{
    None,
    Fainted,
    MissingAbility,
    UnknownAbility,
    SameAbility,
    Protected,
}

public sealed record BattleAbilityMutationResult(
    BattleAbilityOperation Operation,
    BattleAbilityMutationFailure Failure,
    IReadOnlyList<(BattleOverlayOwner Owner, EntityId? Before, EntityId? After)> Changes,
    IReadOnlyList<long> SuppressionSequences)
{
    public bool Succeeded => Failure == BattleAbilityMutationFailure.None;
}

/// <summary>Atomic runtime-only effective-ability mutation over immutable catalog definitions.</summary>
public sealed class BattleAbilityState(
    BattleOverlayStore overlays,
    IReadOnlyDictionary<EntityId, Ability> catalog)
{
    public EntityId? Effective(BattleOverlayOwner owner, BattleEffectiveValues baseValues,
        IEnumerable<long>? ignoredSuppressions = null) =>
        overlays.Resolve(owner, baseValues, ignoredSuppressions).Values.Ability;

    public IReadOnlyList<AbilityHook> Hooks(BattleOverlayOwner owner, BattleEffectiveValues baseValues,
        IReadOnlyList<AbilityHook> legacyHooks, IEnumerable<long>? ignoredSuppressions = null)
    {
        EntityId? effective = Effective(owner, baseValues, ignoredSuppressions);
        if (effective is { } id && catalog.TryGetValue(id, out Ability? ability))
            return ability.Hooks;
        return baseValues.Ability == effective ? legacyHooks : [];
    }

    public BattleAbilityMutationResult Mutate(
        BattleAbilityOperation operation,
        BattleAbilitySubject subject,
        BattleAbilitySubject source,
        EntityId? replacement,
        BattleOverlayOwner user,
        BattleEffectiveValues userBase,
        bool userFainted,
        BattleOverlayOwner target,
        BattleEffectiveValues targetBase,
        bool targetFainted,
        IReadOnlyList<(BattleOverlayOwner Owner, BattleEffectiveValues BaseValues, bool Fainted)> userAllies,
        int turn,
        int actionSequence)
    {
        ValidateRequest(operation, subject, source, replacement, user, target, turn, actionSequence);
        var participants = new List<(BattleOverlayOwner Owner, BattleEffectiveValues BaseValues, bool Fainted)>
        {
            (user, userBase, userFainted),
            (target, targetBase, targetFainted),
        };
        participants.AddRange(userAllies);

        var effective = participants.ToDictionary(p => (p.Owner.Side, p.Owner.PartyIndex),
            p => Effective(p.Owner, p.BaseValues));
        var required = operation switch
        {
            BattleAbilityOperation.Swap => participants.Take(2).ToArray(),
            BattleAbilityOperation.Copy => Recipients(subject, participants).Append(Source(source, participants))
                .DistinctBy(p => p.Owner).ToArray(),
            _ => Recipients(subject, participants),
        };
        if (required.Any(p => p.Fainted))
            return Fail(operation, BattleAbilityMutationFailure.Fainted);
        if (required.Any(p => effective[(p.Owner.Side, p.Owner.PartyIndex)] is null))
            return Fail(operation, BattleAbilityMutationFailure.MissingAbility);
        if (required.Any(p => !catalog.ContainsKey(effective[(p.Owner.Side, p.Owner.PartyIndex)]!.Value))
            || replacement is { } replacementId && !catalog.ContainsKey(replacementId))
            return Fail(operation, BattleAbilityMutationFailure.UnknownAbility);

        (BattleOverlayOwner Owner, BattleEffectiveValues BaseValues, bool Fainted)[] recipients =
            Recipients(subject, participants);
        EntityId? sourceAbility = operation switch
        {
            BattleAbilityOperation.Copy => effective[Key(Source(source, participants).Owner)],
            BattleAbilityOperation.Replace => replacement,
            _ => null,
        };
        var protectedParticipants = operation switch
        {
            BattleAbilityOperation.Swap => required,
            BattleAbilityOperation.Copy => recipients
                .Where(p => effective[Key(p.Owner)] != sourceAbility)
                .Append(Source(source, participants)).DistinctBy(p => p.Owner).ToArray(),
            _ => recipients,
        };
        if (protectedParticipants.Any(p => Protected(effective[Key(p.Owner)]!.Value, operation)))
            return Fail(operation, BattleAbilityMutationFailure.Protected);
        if (operation == BattleAbilityOperation.Swap
            && effective[Key(user)] == effective[Key(target)]
            || operation is BattleAbilityOperation.Copy or BattleAbilityOperation.Replace
            && recipients.All(p => effective[Key(p.Owner)] == sourceAbility))
            return Fail(operation, BattleAbilityMutationFailure.SameAbility);

        var changes = new List<(BattleOverlayOwner Owner, EntityId? Before, EntityId? After)>();
        var suppressions = new List<long>();
        if (operation == BattleAbilityOperation.Swap)
        {
            EntityId userAbility = effective[Key(user)]!.Value;
            EntityId targetAbility = effective[Key(target)]!.Value;
            Apply(user, new AbilityOverlay(targetAbility), BattleOverlayLayer.PermanentInstance, turn, actionSequence);
            Apply(target, new AbilityOverlay(userAbility), BattleOverlayLayer.PermanentInstance, turn, actionSequence);
            changes.Add((user, userAbility, targetAbility));
            changes.Add((target, targetAbility, userAbility));
        }
        else
        {
            foreach (var recipient in recipients)
            {
                EntityId before = effective[Key(recipient.Owner)]!.Value;
                if (before == sourceAbility)
                    continue;
                if (operation == BattleAbilityOperation.Suppress)
                {
                    BattleOverlayChangeSet change = Apply(recipient.Owner,
                        new SuppressionOverlay(BattleEffectiveValueKind.Ability), BattleOverlayLayer.Suppression,
                        turn, actionSequence);
                    suppressions.Add(change.Affected.Single().Sequence);
                    changes.Add((recipient.Owner, before, null));
                }
                else
                {
                    Apply(recipient.Owner, new AbilityOverlay(sourceAbility), BattleOverlayLayer.PermanentInstance,
                        turn, actionSequence);
                    changes.Add((recipient.Owner, before, sourceAbility));
                }
            }
        }

        return new(operation, BattleAbilityMutationFailure.None,
            changes.OrderBy(change => change.Owner.Side).ThenBy(change => change.Owner.PartyIndex).ToArray(),
            suppressions.ToArray());
    }

    internal static IReadOnlySet<BattleAbilityOperation> Operations(Effect effect)
    {
        if (effect.Params is null || !effect.Params.TryGetValue("operations", out var value)
            || value.ValueKind != System.Text.Json.JsonValueKind.String)
            return new HashSet<BattleAbilityOperation>();
        return value.GetString()!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Enum.TryParse(value, true, out BattleAbilityOperation operation)
                ? operation : (BattleAbilityOperation?)null)
            .Where(operation => operation.HasValue).Select(operation => operation!.Value).ToHashSet();
    }

    private bool Protected(EntityId ability, BattleAbilityOperation operation) =>
        catalog[ability].Hooks.SelectMany(hook => hook.Effects).Any(effect =>
            effect.Op == "abilityMutationGuard" && Operations(effect).Contains(operation));

    private BattleOverlayChangeSet Apply(BattleOverlayOwner owner, BattleOverlayPayload payload,
        BattleOverlayLayer layer, int turn, int actionSequence) => overlays.Apply(new BattleOverlayApplication(
            owner, new BattleOverlaySource(), layer, payload, turn, actionSequence, Cleanup:
            BattleOverlayCleanup.Switch | BattleOverlayCleanup.Faint | BattleOverlayCleanup.BattleEnd));

    private static (BattleSide Side, int PartyIndex) Key(BattleOverlayOwner owner) =>
        (owner.Side, owner.PartyIndex);

    private static (BattleOverlayOwner Owner, BattleEffectiveValues BaseValues, bool Fainted) Source(
        BattleAbilitySubject source,
        IReadOnlyList<(BattleOverlayOwner Owner, BattleEffectiveValues BaseValues, bool Fainted)> participants) =>
        source switch
        {
            BattleAbilitySubject.User => participants[0],
            BattleAbilitySubject.Target => participants[1],
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Ability source must be user or target."),
        };

    private static (BattleOverlayOwner Owner, BattleEffectiveValues BaseValues, bool Fainted)[] Recipients(
        BattleAbilitySubject subject,
        IReadOnlyList<(BattleOverlayOwner Owner, BattleEffectiveValues BaseValues, bool Fainted)> participants) =>
        subject switch
        {
            BattleAbilitySubject.User => [participants[0]],
            BattleAbilitySubject.Target => [participants[1]],
            BattleAbilitySubject.UserAndAllies => [participants[0], .. participants.Skip(2)],
            _ => throw new ArgumentOutOfRangeException(nameof(subject)),
        };

    private static BattleAbilityMutationResult Fail(BattleAbilityOperation operation,
        BattleAbilityMutationFailure failure) => new(operation, failure, [], []);

    private static void ValidateRequest(BattleAbilityOperation operation, BattleAbilitySubject subject,
        BattleAbilitySubject source, EntityId? replacement, BattleOverlayOwner user, BattleOverlayOwner target,
        int turn, int actionSequence)
    {
        if (!Enum.IsDefined(operation) || !Enum.IsDefined(subject) || !Enum.IsDefined(source))
            throw new ArgumentOutOfRangeException(nameof(operation), "Ability mutation vocabulary is invalid.");
        if (turn < 0 || actionSequence < 0)
            throw new ArgumentOutOfRangeException(nameof(turn), "Ability mutation time cannot be negative.");
        if (user.Side == target.Side && user.PartyIndex == target.PartyIndex)
            throw new ArgumentException("Ability mutation user and target must be distinct creatures.");
        if (source == BattleAbilitySubject.UserAndAllies
            || operation == BattleAbilityOperation.Copy && source == subject
            || operation == BattleAbilityOperation.Swap && (subject != BattleAbilitySubject.User
                || source != BattleAbilitySubject.Target)
            || operation == BattleAbilityOperation.Replace != replacement.HasValue
            || operation is BattleAbilityOperation.Replace or BattleAbilityOperation.Suppress
                && source != BattleAbilitySubject.Target)
            throw new ArgumentException("Ability mutation parameters do not match the operation.");
    }
}
