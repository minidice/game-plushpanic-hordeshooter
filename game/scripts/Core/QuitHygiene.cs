using System;

namespace HordeShooter;

/// <summary>
/// Godot .NET can abort on exit ("csharp_lang && !script_bindings.is_empty()") when thousands of
/// undisposed wrapper objects are finalized while the engine is already shutting down. Collecting
/// before quitting runs those finalizers while C# is still alive.
/// </summary>
public static class QuitHygiene
{
    public static void Collect()
    {
        // Release our strong hold on scripted content first, so it is finalized now, not during shutdown.
        ContentCache.Clear();
        CharacterEarMesh.ClearCache();
        CharacterRig.ClearSilhouetteCache();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}
