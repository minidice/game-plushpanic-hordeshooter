using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Player options and the last menu choices, saved to <see cref="Path"/>.
/// Values are read live (every shot, every bus update), so the settings page can change them at any time.
/// Nothing is read or written until <see cref="Load"/> runs: the real game loads on boot from the main menu,
/// test scenes never do, so they cannot overwrite a player's settings file.
/// </summary>
public static class GameSettings
{
    public const string DefaultPath = "user://settings.cfg";
    public static string Path { get; set; } = DefaultPath;
    public static bool Loaded { get; private set; }

    /// <summary>1 = as authored, 0 = no self-knockback when shooting.</summary>
    public static float RecoilScale { get; set; } = 1f;
    public static float MasterVolume { get; set; } = 1f;
    public static float SfxVolume { get; set; } = 1f;
    public static float AmbienceVolume { get; set; } = 1f;
    /// <summary>Game option "revive" for single player (multiplayer: a room option). Default on (decisions.md M-18).</summary>
    public static bool ReviveEnabled { get; set; } = true;
    private static PetModelQuality _petQuality=PetModelQuality.High;
    public static event Action<PetModelQuality>? PetQualityChanged;
    public static PetModelQuality PetQuality
    {
        get=>_petQuality;
        set
        {
            if(!Enum.IsDefined(value)) throw new ArgumentException("Unknown pet quality",nameof(value));
            if(_petQuality==value) return;
            _petQuality=value;PetQualityChanged?.Invoke(value);
        }
    }

    /// <summary>Korean first (the fallback language), then the rest in folder order.</summary>
    public static IReadOnlyList<string> Languages =>
        Loc.Languages.OrderBy(l => l == Loc.Fallback ? 0 : 1).ThenBy(l => l).ToList();

    /// <summary>Each language is listed in its own name, so it can be found whatever the current language is.</summary>
    public static string LanguageName(string id) => id switch { "kor" => "한국어", "eng" => "English", _ => id };

    public static void Load()
    {
        using var cfg = new ConfigFile();
        if (cfg.Load(Path) == Error.Ok)
        {
            string language = (string)cfg.GetValue("options", "language", Loc.Fallback);
            Loc.Language = Languages.Contains(language) ? language : Loc.Fallback;
            RecoilScale = Unit(cfg.GetValue("options", "recoil_scale", 1f));
            MasterVolume = Unit(cfg.GetValue("options", "volume_master", 1f));
            SfxVolume = Unit(cfg.GetValue("options", "volume_sfx", 1f));
            AmbienceVolume = Unit(cfg.GetValue("options", "volume_ambience", 1f));
            ReviveEnabled = (bool)cfg.GetValue("options", "revive", true);
            int petQuality=(int)cfg.GetValue("graphics","pet_quality",0);
            PetQuality=petQuality == 1 ? PetModelQuality.Low : PetModelQuality.High;
            GameSession.SelectedCharacterId = (string)cfg.GetValue("menu", "character", GameSession.DefaultCharacterId);
            GameSession.SquadSize = GameSession.ValidSquad((int)cfg.GetValue("menu", "squad_size", GameSession.DefaultSquadSize));
            GameSession.SelectedTheme = (string)cfg.GetValue("menu", "theme", GameSession.SelectedTheme);
            NetLaunch.LastAddress = (string)cfg.GetValue("menu", "join_address", "");
        }
        Loaded = true;
        ApplyAudio();
    }

    /// <summary>Writes only after <see cref="Load"/>, so test scenes leave the player's file alone.</summary>
    public static void Save()
    {
        if (!Loaded) return;
        using var cfg = new ConfigFile();
        cfg.SetValue("options", "language", Loc.Language);
        cfg.SetValue("options", "recoil_scale", RecoilScale);
        cfg.SetValue("options", "volume_master", MasterVolume);
        cfg.SetValue("options", "volume_sfx", SfxVolume);
        cfg.SetValue("options", "volume_ambience", AmbienceVolume);
        cfg.SetValue("options", "revive", ReviveEnabled);
        cfg.SetValue("graphics","pet_quality",(int)PetQuality);
        cfg.SetValue("menu", "character", GameSession.SelectedCharacterId);
        cfg.SetValue("menu", "squad_size", GameSession.SquadSize);
        cfg.SetValue("menu", "theme", GameSession.SelectedTheme);
        cfg.SetValue("menu", "join_address", NetLaunch.LastAddress);
        var error = cfg.Save(Path);
        if (error != Error.Ok) GD.PushWarning($"Settings not saved to {Path}: {error}");
    }

    /// <summary>Restores defaults and forgets the file (tests use this to leave no state behind).</summary>
    public static void Reset()
    {
        Loaded = false;
        Path = DefaultPath;
        RecoilScale = MasterVolume = SfxVolume = AmbienceVolume = 1f;
        ReviveEnabled = true;
        PetQuality=PetModelQuality.High;
        NetLaunch.LastAddress = "";
        Loc.Language = Loc.Fallback;
        ApplyAudio();
    }

    public static void ApplyAudio()
    {
        SetBus("Master", MasterVolume);
        SetBus(AudioSetup.SfxBus, SfxVolume);
        SetBus(AudioSetup.AmbienceBus, AmbienceVolume);
    }

    private static void SetBus(string name, float volume)
    {
        int bus = AudioServer.GetBusIndex(name);
        if (bus < 0) return;
        AudioServer.SetBusMute(bus, volume <= .001f);
        AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb(Mathf.Max(volume, .001f)));
    }

    private static float Unit(Variant value)
    {
        float f = (float)value;
        return float.IsFinite(f) ? Mathf.Clamp(f, 0f, 1f) : 1f;
    }
}
