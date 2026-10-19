using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Sound effects for one scene: builds every <see cref="SoundRecipes"/> entry once (or loads
/// content/sounds/&lt;id&gt;.ogg|.wav if present), then plays them from pooled 3D/2D players with
/// per-sound voice limits and pitch jitter. Positional sounds are heard from the listener on the player.
/// </summary>
public partial class Sfx : Node
{
    public const string OverrideDir = "res://content/sounds";

    /// <summary>Recipes are deterministic, so each is synthesized once per run and shared by every scene.</summary>
    private static readonly Dictionary<string, AudioStreamWav> Synthesized = new();

    public int Pool3DSize { get; init; } = 40;
    public int Pool2DSize { get; init; } = 10;

    private readonly Dictionary<string, AudioStream> _streams = new();
    private readonly Dictionary<string, SoundDef> _defs = new();
    private readonly List<AudioStreamPlayer3D> _players3D = new();
    private readonly List<AudioStreamPlayer> _players2D = new();
    private readonly RandomNumberGenerator _rng = new();

    /// <summary>How often each sound was started (tests and debugging).</summary>
    public Dictionary<string, int> Played { get; } = new();
    /// <summary>Ids that came from files in content/sounds instead of the synthesizer.</summary>
    public HashSet<string> Overridden { get; } = new();

    public override void _Ready()
    {
        Name = "Sfx";
        AudioSetup.Ensure();
        foreach (var def in SoundRecipes.All)
        {
            _defs[def.Id] = def;
            _streams[def.Id] = LoadOverride(def) ?? Cached(def);
        }
        for (int i = 0; i < Pool3DSize; i++)
        {
            var p = Configure(new AudioStreamPlayer3D());
            AddChild(p);
            _players3D.Add(p);
        }
        for (int i = 0; i < Pool2DSize; i++)
        {
            var p = new AudioStreamPlayer { Bus = AudioSetup.SfxBus };
            AddChild(p);
            _players2D.Add(p);
        }
    }

    public AudioStream Stream(string id) => _streams[id];

    private static AudioStreamPlayer3D Configure(AudioStreamPlayer3D p)
    {
        p.Bus = AudioSetup.SfxBus;
        p.UnitSize = 7;
        p.MaxDistance = 48;
        p.AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Logarithmic;
        p.PanningStrength = .7f;
        p.DopplerTracking = AudioStreamPlayer3D.DopplerTrackingEnum.Disabled;
        return p;
    }

    private static AudioStreamWav Cached(SoundDef def)
    {
        if (!Synthesized.TryGetValue(def.Id, out var wav))
        {
            wav = Synthesize(def);
            Synthesized[def.Id] = wav;
        }
        return wav;
    }

    private AudioStream? LoadOverride(SoundDef def)
    {
        foreach (var ext in new[] { "ogg", "wav", "mp3" })
        {
            var path = $"{OverrideDir}/{def.Id}.{ext}";
            if (!ResourceLoader.Exists(path)) continue;
            if (ResourceLoader.Load<AudioStream>(path) is not { } stream) continue;
            Overridden.Add(def.Id);
            if (def.Loop) MakeLooping(stream);
            return stream;
        }
        return null;
    }

    /// <summary>Imported files do not loop by default; loop recipes (fire, alarm, ambience) must.</summary>
    private static void MakeLooping(AudioStream stream)
    {
        switch (stream)
        {
            case AudioStreamOggVorbis ogg:
                ogg.Loop = true;
                break;
            case AudioStreamMP3 mp3:
                mp3.Loop = true;
                break;
            case AudioStreamWav wav when wav.LoopMode == AudioStreamWav.LoopModeEnum.Disabled:
                wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
                wav.LoopBegin = 0;
                wav.LoopEnd = (int)(wav.GetLength() * wav.MixRate);
                break;
        }
    }

    public static AudioStreamWav Synthesize(SoundDef def)
    {
        var samples = def.Build(new Synth(StableSeed(def.Id)));
        var wav = new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = Synth.Rate,
            Stereo = false,
            Data = Synth.ToPcm16(samples),
        };
        if (def.Loop)
        {
            wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            wav.LoopBegin = 0;
            wav.LoopEnd = samples.Length;
        }
        return wav;
    }

    private static int StableSeed(string id)
    {
        unchecked
        {
            int hash = (int)2166136261;
            foreach (char c in id) hash = (hash ^ c) * 16777619;
            return hash & 0x7fffffff;
        }
    }

    /// <summary>Positional one-shot. Returns null if this sound already has all its voices busy.</summary>
    public AudioStreamPlayer3D? PlayAt(string id, Vector3 at, float volumeDb = 0, float pitch = 1)
    {
        if (!TryGetDef(id, out var def)) return null;
        var player = Pick(_players3D, id, def);
        if (player == null) return null;
        player.GlobalPosition = at;
        Start(player, id, def, volumeDb, pitch);
        return player;
    }

    /// <summary>Non-positional one-shot (UI, stingers, the player's own ears).</summary>
    public AudioStreamPlayer? Play(string id, float volumeDb = 0, float pitch = 1)
    {
        if (!TryGetDef(id, out var def)) return null;
        var player = Pick(_players2D, id, def);
        if (player == null) return null;
        player.Stream = _streams[id];
        player.Bus = def.Bus;
        player.VolumeDb = def.VolumeDb + volumeDb;
        player.PitchScale = pitch * (1 + _rng.RandfRange(-def.PitchJitter, def.PitchJitter));
        player.SetMeta("sound", id);
        player.Play();
        Count(id);
        return player;
    }

    private readonly List<AudioStreamPlayer3D> _attached = new();

    /// <summary>
    /// A dedicated emitter that follows <paramref name="parent"/> and dies with it (fire loop, mortar whistle).
    /// Respects the sound's voice limit; returns null when that many are already attached.
    /// </summary>
    public AudioStreamPlayer3D? Attach(string id, Node3D parent, float volumeDb = 0)
    {
        if (!TryGetDef(id, out var def)) return null;
        _attached.RemoveAll(p => !IsInstanceValid(p) || p.IsQueuedForDeletion());
        if (_attached.Count(p => (string)p.GetMeta("sound", "") == id) >= def.MaxVoices) return null;
        var player = Configure(new AudioStreamPlayer3D());
        player.Stream = _streams[id];
        player.Bus = def.Bus;
        player.VolumeDb = def.VolumeDb + volumeDb;
        player.Autoplay = true;
        player.SetMeta("sound", id);
        parent.AddChild(player);
        _attached.Add(player);
        Count(id);
        return player;
    }

    /// <summary>A looping non-positional bed (alarm, ambience).</summary>
    public AudioStreamPlayer Bed(string id, string bus, float volumeDb = 0)
    {
        var def = _defs[id];
        var player = new AudioStreamPlayer { Stream = _streams[id], Bus = bus, VolumeDb = def.VolumeDb + volumeDb };
        AddChild(player);
        player.Play();
        Count(id);
        return player;
    }

    private bool TryGetDef(string id, out SoundDef def)
    {
        if (_defs.TryGetValue(id, out def!)) return true;
        GD.PushWarning($"Unknown sound '{id}'");
        return false;
    }

    private void Start(AudioStreamPlayer3D player, string id, SoundDef def, float volumeDb, float pitch)
    {
        player.Stream = _streams[id];
        player.Bus = def.Bus;
        player.VolumeDb = def.VolumeDb + volumeDb;
        player.PitchScale = pitch * (1 + _rng.RandfRange(-def.PitchJitter, def.PitchJitter));
        player.SetMeta("sound", id);
        player.Play();
        Count(id);
    }

    private void Count(string id) => Played[id] = Played.GetValueOrDefault(id) + 1;

    /// <summary>
    /// Choose a player for <paramref name="def"/>:
    ///   at its own voice limit → restart its oldest voice, or skip (crowd pings);
    ///   otherwise a free player, or else take over the oldest voice of an equal-or-lower priority sound.
    /// </summary>
    private T? Pick<T>(List<T> pool, string id, SoundDef def) where T : Node
    {
        int busy = 0;
        T? free = null, oldestSame = null, victim = null;
        float oldestSameTime = -1, victimTime = -1;
        int victimPriority = int.MaxValue;
        foreach (var p in pool)
        {
            bool playing = p is AudioStreamPlayer3D p3 ? p3.Playing : ((AudioStreamPlayer)(object)p).Playing;
            if (!playing)
            {
                free ??= p;
                continue;
            }
            var other = (string)p.GetMeta("sound", "");
            float pos = p is AudioStreamPlayer3D q3 ? q3.GetPlaybackPosition() : ((AudioStreamPlayer)(object)p).GetPlaybackPosition();
            if (other == id)
            {
                busy++;
                if (pos > oldestSameTime)
                {
                    oldestSameTime = pos;
                    oldestSame = p;
                }
                continue;
            }
            int priority = _defs.TryGetValue(other, out var otherDef) ? otherDef.Priority : 0;
            if (priority > def.Priority) continue;
            if (priority < victimPriority || (priority == victimPriority && pos > victimTime))
            {
                victimPriority = priority;
                victimTime = pos;
                victim = p;
            }
        }
        if (busy >= def.MaxVoices) return def.RestartWhenFull ? oldestSame : null;
        return free ?? victim;
    }
}
