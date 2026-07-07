namespace Cgm.Runtime.Engine;

/// <summary>The runtime scene stack (ENGINE_RUNTIME_SPEC): overworld at the base, with battle/menu
/// scenes pushed on top. Only the top scene is active. Generic + pure so it's testable on its own.</summary>
public sealed class SceneStack<T> where T : class
{
    private readonly List<T> _scenes = [];

    public int Count => _scenes.Count;
    public T? Active => _scenes.Count > 0 ? _scenes[^1] : null;

    public void Push(T scene) => _scenes.Add(scene);

    public T? Pop()
    {
        if (_scenes.Count == 0)
            return null;
        T top = _scenes[^1];
        _scenes.RemoveAt(_scenes.Count - 1);
        return top;
    }

    /// <summary>Replaces the top scene (or pushes if empty).</summary>
    public void Replace(T scene)
    {
        if (_scenes.Count > 0)
            _scenes[^1] = scene;
        else
            _scenes.Add(scene);
    }
}
