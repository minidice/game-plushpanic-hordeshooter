using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>
/// Strongly holds C#-scripted content resources (.tres character/throwable definitions) for the session.
///
/// Why: Godot's ResourceLoader cache only holds resources weakly. If nothing else references a scripted
/// resource, the .NET finalizer may free it on its own thread while the main thread is loading the same
/// path again, which aborts the engine ("gchandle.is_released") and returns a broken resource.
/// Holding them here prevents that; <see cref="QuitHygiene.Collect"/> clears the cache before quitting so
/// nothing scripted is still alive when the C# runtime shuts down.
/// </summary>
public static class ContentCache
{
    private static readonly Dictionary<string, Resource> Loaded = new();

    public static Resource? Load(string path)
    {
        if (Loaded.TryGetValue(path, out var cached)) return cached;
        var resource = ResourceLoader.Load(path);
        if (resource != null) Loaded[path] = resource;
        return resource;
    }

    public static void Clear() => Loaded.Clear();
}
