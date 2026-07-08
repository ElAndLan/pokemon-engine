using Cgm.Core.Model;

namespace Cgm.Core.Validation.Rules;

/// <summary>Encounter tables need at least one slot; each slot needs positive weight and a sane
/// level range (1–100, min ≤ max).</summary>
public sealed class EncounterTableRule : IValidationRule
{
    public string Id => "encounter-table";

    public IEnumerable<ValidationIssue> Check(Project project)
    {
        foreach (EncounterTable table in project.All<EncounterTable>())
        {
            if (table.Slots.Count == 0)
                yield return new ValidationIssue(Id, ValidationSeverity.Error, table.Id,
                    "Encounter table has no slots.");

            foreach (EncounterSlot slot in table.Slots)
            {
                if (slot.Weight <= 0)
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, table.Id,
                        $"Slot '{slot.Species}' has weight {slot.Weight}; must be > 0.");
                if (slot.MinLevel < 1 || slot.MaxLevel > 100 || slot.MinLevel > slot.MaxLevel)
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, table.Id,
                        $"Slot '{slot.Species}' level range {slot.MinLevel}–{slot.MaxLevel} is invalid (1–100, min ≤ max).");
            }
        }
    }
}

/// <summary>Trainer party legality (Phase 11): 1–6 members, levels 1–100, non-negative sight range,
/// sight dialogue on sighted trainers, and explicit moves the species can actually learn at that level
/// (a warning — the editor allows move overrides). Reference existence is covered by broken-reference.</summary>
public sealed class TrainerPartyRule : IValidationRule
{
    public string Id => "trainer-party";

    public IEnumerable<ValidationIssue> Check(Project project)
    {
        foreach (Trainer t in project.All<Trainer>())
        {
            if (t.SightRange < 0)
                yield return new ValidationIssue(Id, ValidationSeverity.Error, t.Id,
                    $"Sight range {t.SightRange} is negative (0 = interact-only).");

            if (t.Party.Count is < 1 or > 6)
                yield return new ValidationIssue(Id, ValidationSeverity.Error, t.Id,
                    $"Party has {t.Party.Count} members; must be 1–6.");

            if (t.SightRange > 0 && string.IsNullOrWhiteSpace(t.Dialogue.Sight))
                yield return new ValidationIssue(Id, ValidationSeverity.Warning, t.Id,
                    "Sighted trainer has no sight dialogue.", "Add dialogue.sight so the approach shows text.");

            foreach (PartyMember m in t.Party)
            {
                if (m.Level is < 1 or > 100)
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, t.Id,
                        $"Party member '{m.Species}' level {m.Level} is out of 1–100.");

                foreach (ValidationIssue issue in CheckMoves(t, m, project))
                    yield return issue;
            }
        }
    }

    private IEnumerable<ValidationIssue> CheckMoves(Trainer t, PartyMember m, Project project)
    {
        if (m.Moves is null)
            yield break; // unspecified → auto-generated from the learnset, nothing to check

        if (m.Moves.Count is < 1 or > 4)
            yield return new ValidationIssue(Id, ValidationSeverity.Error, t.Id,
                $"Party member '{m.Species}' has {m.Moves.Count} moves; must be 1–4.");

        Species? species = project.Find<Species>(m.Species);
        if (species is null)
            yield break; // missing species is reported by broken-reference

        foreach (EntityId move in m.Moves)
            if (!species.Learnset.Any(e => e.Move == move && e.Level <= m.Level))
                yield return new ValidationIssue(Id, ValidationSeverity.Warning, t.Id,
                    $"'{species.Id}' can't learn '{move}' by level {m.Level}.",
                    "Allowed as an override; confirm it's intentional.");
    }
}

/// <summary>Warps must target an existing map and land within that map's bounds.</summary>
public sealed class WarpTargetRule : IValidationRule
{
    public string Id => "warp-target";

    public IEnumerable<ValidationIssue> Check(Project project)
    {
        foreach (Map map in project.All<Map>())
            foreach (WarpEntity warp in map.Entities.OfType<WarpEntity>())
            {
                Map? target = project.Find<Map>(warp.Target);
                if (target is null)
                    continue; // existence is reported by broken-reference; only bounds-check real maps

                GridPos p = warp.TargetPos;
                if (p.X < 0 || p.Y < 0 || p.X >= target.Width || p.Y >= target.Height)
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, map.Id,
                        $"Warp targets ({p.X},{p.Y}) outside '{warp.Target}' ({target.Width}×{target.Height}).");
            }
    }
}
