using Cgm.Core.Model;

namespace Cgm.Creator.ViewModels;

/// <summary>Sound metadata editor (DATA_SCHEMA §4.6b): kind, loop, volume, display name. Audition
/// is deliberately absent — the Creator plays no audio (the Runtime is the one player, per the
/// 17B no-second-decoder rule).</summary>
public sealed class SoundDocument : EntityEditorDocument<Sound>
{
    public SoundDocument(ProjectSession session, Sound model) : base(session, model) { }

    public string Name
    {
        get => Model.Name;
        set { if (value != Model.Name) Edit(Model with { Name = value }); }
    }

    public SoundKind Kind
    {
        get => Model.Kind;
        set { if (value != Model.Kind) Edit(Model with { Kind = value }); }
    }

    public bool Loop
    {
        get => Model.Loop;
        set { if (value != Model.Loop) Edit(Model with { Loop = value }); }
    }

    public int Volume
    {
        get => Model.Volume;
        set { if (value != Model.Volume) Edit(Model with { Volume = value }); }
    }

    public string AssetPath => Model.Asset;

    public IReadOnlyList<SoundKind> Kinds { get; } = [SoundKind.Music, SoundKind.Sfx];
}
