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

/// <summary>Trainers need a 1–6 party with each member's level in 1–100.</summary>
public sealed class TrainerPartyRule : IValidationRule
{
    public string Id => "trainer-party";

    public IEnumerable<ValidationIssue> Check(Project project)
    {
        foreach (Trainer t in project.All<Trainer>())
        {
            if (t.Party.Count is < 1 or > 6)
                yield return new ValidationIssue(Id, ValidationSeverity.Error, t.Id,
                    $"Party has {t.Party.Count} members; must be 1–6.");

            foreach (PartyMember m in t.Party)
                if (m.Level is < 1 or > 100)
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, t.Id,
                        $"Party member '{m.Species}' level {m.Level} is out of 1–100.");
        }
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
