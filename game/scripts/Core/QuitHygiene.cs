using System;
using Godot;

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
        // A sound still playing at quit (the stage's ambience and alarm loops) keeps its stream and playback held by the
        // audio server through shutdown: "4 ObjectDB instances were leaked" (I-17). Stop them, then give the audio thread
        // a moment: it lets go of a stopped playback on its next mix, not at once. The wait also covers players freed just
        // before (a stage freed right before quitting), which are already out of the tree.
        if (Engine.GetMainLoop() is SceneTree { Root: { } root }) StopSounds(root);
        OS.DelayMsec(StopWaitMs);
        // Release our strong hold on scripted content first, so it is finalized now, not during shutdown.
        ContentCache.Clear();
        CharacterEarMesh.ClearCache();
        CharacterRig.ClearSilhouetteCache();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    /// <summary>Long enough for a few audio mixes.</summary>
    public const int StopWaitMs = 80;

    /// <summary>Stops every playing sound under <paramref name="node"/>.</summary>
    private static void StopSounds(Node node)
    {
        switch (node)
        {
            case AudioStreamPlayer { Playing: true } p: p.Stop(); break;
            case AudioStreamPlayer2D { Playing: true } p: p.Stop(); break;
            case AudioStreamPlayer3D { Playing: true } p: p.Stop(); break;
        }
        foreach (var child in node.GetChildren()) StopSounds(child);
    }
}
