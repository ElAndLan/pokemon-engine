using Cgm.Core.Model;

namespace Cgm.Creator.ViewModels;

/// <summary>Animation clip editor (ASSET_PIPELINE_SPEC 17B): frames in order with per-frame
/// duration, reorder/remove/add, loop flag. The fixed-tick preview lives in the view — it is
/// presentation, and the tick durations it plays are exactly these authored values.</summary>
public sealed class AnimDocument : EntityEditorDocument<Animation>
{
    public AnimDocument(ProjectSession session, Animation model) : base(session, model) { }

    public string Name
    {
        get => Model.Name;
        set { if (value != Model.Name) Edit(Model with { Name = value }); }
    }

    public bool Loop
    {
        get => Model.Loop;
        set { if (value != Model.Loop) Edit(Model with { Loop = value }); }
    }

    public IReadOnlyList<AnimFrame> Frames => Model.Frames;

    public void SetFrameMs(int index, int ms)
    {
        if (index < 0 || index >= Model.Frames.Count || ms <= 0 || Model.Frames[index].Ms == ms)
            return;
        var frames = Model.Frames.ToList();
        frames[index] = frames[index] with { Ms = ms };
        Edit(Model with { Frames = frames });
    }

    public void RemoveFrame(int index)
    {
        if (index < 0 || index >= Model.Frames.Count)
            return;
        var frames = Model.Frames.ToList();
        frames.RemoveAt(index);
        Edit(Model with { Frames = frames });
    }

    /// <summary>Moves a frame up (-1) or down (+1) one slot.</summary>
    public void MoveFrame(int index, int delta)
    {
        int target = index + delta;
        if (index < 0 || index >= Model.Frames.Count || target < 0 || target >= Model.Frames.Count)
            return;
        var frames = Model.Frames.ToList();
        (frames[index], frames[target]) = (frames[target], frames[index]);
        Edit(Model with { Frames = frames });
    }

    public void AddFrame(EntityId sprite, int ms = 150)
    {
        if (ms <= 0)
            return;
        Edit(Model with { Frames = Model.Frames.Append(new AnimFrame(sprite, ms)).ToList() });
    }
}
