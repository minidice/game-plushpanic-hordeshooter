using Godot;

namespace HordeShooter;

/// <summary>
/// Audio buses, created in code so they can be tweaked without the editor:
///   SFX      — all effects. Carries a pitch shifter (helium) and a low-pass (flashbang) that start disabled.
///   Ambience — background loops, kept out of the helium/blind effects.
/// </summary>
public static class AudioSetup
{
    public const string SfxBus = "SFX";
    public const string AmbienceBus = "Ambience";
    public const int PitchEffect = 0;
    public const int MuffleEffect = 1;

    public static void Ensure()
    {
        if (AudioServer.GetBusIndex(SfxBus) < 0)
        {
            AudioServer.AddBus();
            int sfx = AudioServer.BusCount - 1;
            AudioServer.SetBusName(sfx, SfxBus);
            AudioServer.SetBusSend(sfx, "Master");
            AudioServer.AddBusEffect(sfx, new AudioEffectPitchShift { PitchScale = 1.55f }, PitchEffect);
            AudioServer.AddBusEffect(sfx, new AudioEffectLowPassFilter { CutoffHz = 550, Resonance = .6f }, MuffleEffect);
            AudioServer.SetBusEffectEnabled(sfx, PitchEffect, false);
            AudioServer.SetBusEffectEnabled(sfx, MuffleEffect, false);
        }
        if (AudioServer.GetBusIndex(AmbienceBus) < 0)
        {
            AudioServer.AddBus();
            int amb = AudioServer.BusCount - 1;
            AudioServer.SetBusName(amb, AmbienceBus);
            AudioServer.SetBusSend(amb, "Master");
        }
        SetHelium(false);
        SetMuffled(false);
    }

    /// <summary>Helium: every effect squeaks up in pitch.</summary>
    public static void SetHelium(bool on) => SetEffect(PitchEffect, on);

    /// <summary>Flashbang: everything sounds muffled while your eyes recover.</summary>
    public static void SetMuffled(bool on) => SetEffect(MuffleEffect, on);

    public static bool IsEffectOn(int effect)
    {
        int bus = AudioServer.GetBusIndex(SfxBus);
        return bus >= 0 && AudioServer.IsBusEffectEnabled(bus, effect);
    }

    private static void SetEffect(int effect, bool on)
    {
        int bus = AudioServer.GetBusIndex(SfxBus);
        if (bus >= 0 && AudioServer.IsBusEffectEnabled(bus, effect) != on) AudioServer.SetBusEffectEnabled(bus, effect, on);
    }
}
