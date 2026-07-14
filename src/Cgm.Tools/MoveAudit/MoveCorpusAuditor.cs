using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cgm.Core.Serialization;

namespace Cgm.Tools.MoveAudit;

public enum MoveConformanceStatus
{
    InventoryOnly,
    Normalized,
    Compiled,
    Certified,
    BlockedReference,
    BlockedEngine,
    Invalid,
}

public sealed record MoveCorpusStatusCount(MoveConformanceStatus Status, int Count);

public sealed record MoveCorpusEntry(
    string ReferenceKey,
    int SourceId,
    string SourceFileHash,
    string PayloadContentHash,
    string SourceTarget,
    IReadOnlyList<string> ObservedMechanicFamilies,
    string RequiredTopology,
    string RequiredRuleset,
    MoveConformanceStatus Status,
    string? NormalizedDefinitionHash,
    IReadOnlyList<string> TestIds);

public sealed record MoveCorpusManifest(
    int FormatVersion,
    string CorpusDigest,
    int FileCount,
    IReadOnlyList<MoveCorpusStatusCount> StatusCounts,
    IReadOnlyList<MoveCorpusEntry> Entries);

public static class MoveCorpusAuditor
{
    public const int FormatVersion = 1;

    public static MoveCorpusManifest Build(string corpusFolder, MoveConformanceCatalog? conformance = null)
    {
        if (!Directory.Exists(corpusFolder))
            throw new DirectoryNotFoundException($"Move corpus folder not found: {corpusFolder}");

        var entries = Directory.EnumerateFiles(corpusFolder, "*.json", SearchOption.TopDirectoryOnly)
            .Select(ReadEntry)
            .OrderBy(e => e.SourceId)
            .ToList();
        if (entries.Count == 0)
            throw new InvalidDataException("Move corpus contains no JSON files.");

        int? duplicateId = entries.GroupBy(e => e.SourceId).FirstOrDefault(g => g.Count() > 1)?.Key;
        if (duplicateId is not null)
            throw new InvalidDataException($"Move corpus contains duplicate source id {duplicateId}.");

        string? duplicateKey = entries.GroupBy(e => e.ReferenceKey, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1)?.Key;
        if (duplicateKey is not null)
            throw new InvalidDataException($"Move corpus contains duplicate reference key '{duplicateKey}'.");

        string digestRows = string.Concat(entries.Select(e => $"{e.SourceId}:{e.SourceFileHash}\n"));
        string corpusDigest = Hash(Encoding.UTF8.GetBytes(digestRows));
        if (conformance is not null)
            ApplyConformance(entries, conformance);

        IReadOnlyList<MoveCorpusStatusCount> counts = Enum.GetValues<MoveConformanceStatus>()
            .Select(status => new MoveCorpusStatusCount(status, entries.Count(e => e.Status == status)))
            .ToList();

        return new MoveCorpusManifest(FormatVersion, corpusDigest, entries.Count, counts, entries);
    }

    private static void ApplyConformance(List<MoveCorpusEntry> entries, MoveConformanceCatalog conformance)
    {
        if (conformance.FormatVersion != MoveConformanceNormalizer.FormatVersion)
            throw new InvalidDataException($"Unsupported move conformance catalog format {conformance.FormatVersion}.");
        Dictionary<string, MoveCorpusEntry> byKey = entries.ToDictionary(e => e.ReferenceKey, StringComparer.Ordinal);
        foreach (MoveConformanceRecord record in conformance.Entries)
        {
            if (!byKey.TryGetValue(record.ReferenceKey, out MoveCorpusEntry? entry))
                throw new InvalidDataException($"Conformance key '{record.ReferenceKey}' is not in the corpus.");
            if (entry.SourceFileHash != record.SourceFileHash || entry.PayloadContentHash != record.PayloadContentHash)
                throw new InvalidDataException($"Conformance hashes for '{record.ReferenceKey}' do not match the corpus.");
            int index = entries.IndexOf(entry);
            entries[index] = entry with
            {
                ObservedMechanicFamilies = record.MechanicFamilies,
                RequiredTopology = record.RequiredTopology,
                RequiredRuleset = record.RequiredRuleset,
                Status = MoveConformanceStatus.Certified,
                NormalizedDefinitionHash = record.NormalizedDefinitionHash,
                TestIds = record.TestIds,
            };
            byKey[record.ReferenceKey] = entries[index];
        }
    }

    public static string Serialize(MoveCorpusManifest manifest) => CgmJson.Serialize(manifest);

    public static void Write(MoveCorpusManifest manifest, string outputPath)
    {
        string fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, Serialize(manifest).ReplaceLineEndings("\n"));
    }

    private static MoveCorpusEntry ReadEntry(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw Invalid(path, "wrapper root must be an object");
        if (RequiredString(root, "endpoint", path) != "move")
            throw Invalid(path, "endpoint must be 'move'");

        string payloadHash = RequiredString(root, "content_hash", path).ToLowerInvariant();
        if (payloadHash.Length != 64 || payloadHash.Any(c => !Uri.IsHexDigit(c)))
            throw Invalid(path, "content_hash must be a 64-character hexadecimal SHA-256");

        if (!root.TryGetProperty("payload", out JsonElement payload) || payload.ValueKind != JsonValueKind.Object)
            throw Invalid(path, "payload must be an object");
        int sourceId = RequiredInt(payload, "id", path);
        if (sourceId <= 0)
            throw Invalid(path, "payload.id must be positive");

        string sourceName = RequiredString(payload, "name", path);
        if (!string.Equals(Path.GetFileNameWithoutExtension(path), sourceName, StringComparison.Ordinal))
            throw Invalid(path, "filename must match payload.name");

        string target = "unknown";
        if (payload.TryGetProperty("target", out JsonElement targetElement)
            && targetElement.ValueKind == JsonValueKind.Object
            && targetElement.TryGetProperty("name", out JsonElement targetName)
            && targetName.ValueKind == JsonValueKind.String)
        {
            target = targetName.GetString() ?? "unknown";
        }

        return new MoveCorpusEntry(
            $"move-{sourceId:D4}",
            sourceId,
            Hash(bytes),
            payloadHash,
            target,
            ObserveFamilies(payload),
            "unclassified",
            "unclassified",
            MoveConformanceStatus.InventoryOnly,
            null,
            []);
    }

    private static IReadOnlyList<string> ObserveFamilies(JsonElement payload)
    {
        var families = new SortedSet<string>(StringComparer.Ordinal);
        if (HasNumber(payload, "power"))
            families.Add("standardDamage");
        if (payload.TryGetProperty("stat_changes", out JsonElement changes)
            && changes.ValueKind == JsonValueKind.Array && changes.GetArrayLength() > 0)
        {
            families.Add("statStage");
        }

        if (payload.TryGetProperty("meta", out JsonElement meta) && meta.ValueKind == JsonValueKind.Object)
        {
            if (meta.TryGetProperty("ailment", out JsonElement ailment)
                && ailment.ValueKind == JsonValueKind.Object
                && ailment.TryGetProperty("name", out JsonElement ailmentName)
                && ailmentName.ValueKind == JsonValueKind.String
                && !string.Equals(ailmentName.GetString(), "none", StringComparison.OrdinalIgnoreCase))
            {
                families.Add("ailment");
            }

            int drain = OptionalInt(meta, "drain");
            if (drain > 0) families.Add("drain");
            if (drain < 0) families.Add("recoil");
            if (OptionalInt(meta, "healing") > 0) families.Add("heal");
            if (HasNumber(meta, "min_hits") || HasNumber(meta, "max_hits")) families.Add("multiHit");
            if (HasNumber(meta, "min_turns") || HasNumber(meta, "max_turns")) families.Add("multiTurn");
            if (OptionalInt(meta, "crit_rate") > 0) families.Add("critical");
            if (OptionalInt(meta, "flinch_chance") > 0) families.Add("flinch");
        }

        if (families.Count == 0)
            families.Add("unclassified");
        return families.ToList();
    }

    private static bool HasNumber(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Number;

    private static int OptionalInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;

    private static string RequiredString(JsonElement element, string property, string path) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? throw Invalid(path, $"{property} must not be null")
            : throw Invalid(path, $"{property} must be a string");

    private static int RequiredInt(JsonElement element, string property, string path) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : throw Invalid(path, $"{property} must be an integer");

    private static InvalidDataException Invalid(string path, string message) =>
        new($"Invalid move corpus file '{Path.GetFileName(path)}': {message}.");

    private static string Hash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
