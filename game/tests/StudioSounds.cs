using Godot;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace HordeShooter.Tests;

public partial class StudioPreview
{
    private const string SoundPreviewNote = "원본 음원 미리듣기입니다. 게임 음량(dB)·음정 흔들림·거리 감쇠·버스 효과는 적용하지 않습니다. 브라우저 반복은 파일 전체를 반복합니다.";

    private Sfx SoundResolver()
    {
        // Use the game's exact override order and deterministic cache, but create no audio players and never Play.
        var resolver = new Sfx { Pool3DSize = 0, Pool2DSize = 0 };
        AddChild(resolver);
        return resolver;
    }

    private IEnumerable<object> SoundEntries()
    {
        var resolver = SoundResolver();
        try
        {
            var result = new List<object>();
            foreach (var def in SoundRecipes.All)
            {
                var stream = resolver.Stream(def.Id);
                string source = SoundSource(resolver, def, stream);
                var levels = SoundLevels(stream);
                var properties = new List<LibraryProperty>
                {
                    new("ID", def.Id), new("레시피", "res://scripts/Audio/SoundRecipes.cs"),
                    new("실제 음원", source), new("샘플러", stream.GetClass().ToString()),
                    new("길이", Number((float)stream.GetLength()) + " s"),
                    new("실제 스트림 반복", SoundLoops(stream) ? "예" : "아니요"),
                    new("게임 기본 음량", Number(def.VolumeDb) + " dB"),
                    new("음정 흔들림", "±" + Number(def.PitchJitter * 100) + "%"),
                    new("레시피 버스", def.Bus), new("동시 음성 한도", def.MaxVoices.ToString()),
                    new("우선순위", def.Priority.ToString()),
                    new("음성 한도 도달", def.RestartWhenFull ? "오래된 음성 재시작" : "새 음성 생략"),
                    new("가까운 거리 음량 상한", def.NearCap ? "기본 음량을 넘지 않음" : "3D 기본 상한 +3 dB"),
                    new("미리듣기", SoundPreviewNote),
                };
                if (def.Id == "ambience_loop") properties.Add(new("배경 재생 버스", AudioSetup.AmbienceBus + " (Sfx.Bed)"));
                if (stream is AudioStreamWav wav)
                {
                    properties.Add(new("샘플 형식", wav.Format + " · " + wav.MixRate + " Hz · " + (wav.Stereo ? "스테레오" : "모노")));
                    if (levels is { } level)
                    {
                        properties.Add(new("원본 피크", SoundDb(level.Peak)));
                        properties.Add(new("원본 RMS", SoundDb(level.Rms)));
                    }
                }
                result.Add(new { id = def.Id, name = def.Description, category = SoundCategory(def),
                    summary = (resolver.Overridden.Contains(def.Id) ? "게임이 선택한 교체 음원. " : "게임의 결정적 합성 음원. ") + SoundPreviewNote,
                    source, properties, duration = stream.GetLength(), loop = SoundLoops(stream), volumeDb = def.VolumeDb });
            }
            return result;
        }
        finally { resolver.Free(); }
    }

    private object PreviewSound(JsonElement data)
    {
        if (!data.TryGetProperty("id", out var value) || value.ValueKind != JsonValueKind.String
            || SoundRecipes.Find(value.GetString() ?? "") is not { } def)
            throw new ArgumentException("알 수 없는 게임 사운드 ID입니다.");
        var resolver = SoundResolver();
        try
        {
            var stream = resolver.Stream(def.Id);
            string source = SoundSource(resolver, def, stream);
            byte[] bytes;
            string mime;
            if (resolver.Overridden.Contains(def.Id))
            {
                // Source is derived from a catalog ID and the selected cached resource, never a caller-supplied path.
                bytes = Godot.FileAccess.GetFileAsBytes(source);
                if (bytes.Length == 0) throw new InvalidOperationException("선택된 게임 음원의 원본 파일을 읽을 수 없습니다: " + source);
                mime = source.GetExtension().ToLowerInvariant() switch
                { "ogg" => "audio/ogg", "wav" => "audio/wav", "mp3" => "audio/mpeg", _ => throw new InvalidOperationException("지원하지 않는 음원 형식입니다.") };
            }
            else
            {
                bytes = SoundWave(stream as AudioStreamWav ?? throw new InvalidOperationException("합성 음원이 PCM WAV가 아닙니다."));
                mime = "audio/wav";
            }
            var levels = SoundLevels(stream);
            return new { id = def.Id, mime, audio = Convert.ToBase64String(bytes), duration = stream.GetLength(),
                loop = SoundLoops(stream), volumeDb = def.VolumeDb, source, samplerType = stream.GetClass().ToString(),
                peak = levels?.Peak, rms = levels?.Rms, note = SoundPreviewNote };
        }
        finally { resolver.Free(); }
    }

    private static string SoundSource(Sfx resolver, SoundDef def, AudioStream stream)
    {
        if (!resolver.Overridden.Contains(def.Id)) return "res://scripts/Audio/SoundRecipes.cs#" + def.Id;
        string[] candidates = new[] { "ogg", "wav", "mp3" }.Select(ext => $"{Sfx.OverrideDir}/{def.Id}.{ext}").ToArray();
        if (candidates.Contains(stream.ResourcePath, StringComparer.Ordinal)) return stream.ResourcePath;
        // Imported resources normally retain their original res:// path. Resolve by the same resource instance if not.
        foreach (string candidate in candidates)
            if (ResourceLoader.Exists(candidate) && ResourceLoader.Load<AudioStream>(candidate) == stream) return candidate;
        throw new InvalidOperationException("선택된 교체 음원의 원본 경로를 확인할 수 없습니다: " + def.Id);
    }

    private static bool SoundLoops(AudioStream stream) => stream switch
    {
        AudioStreamWav wav => wav.LoopMode != AudioStreamWav.LoopModeEnum.Disabled,
        AudioStreamOggVorbis ogg => ogg.Loop,
        AudioStreamMP3 mp3 => mp3.Loop,
        _ => false,
    };

    private static string SoundCategory(SoundDef def) => def.Id switch
    {
        "ambience_loop" => "배경음",
        "game_over" or "stage_clear" => "결과 알림",
        "ui_click" => "UI",
        "door" or "alarm_loop" or "shutter" => "스테이지",
        "explosion" or "glass_break" or "fire_loop" or "flash_bang" or "ear_ring" or "lure_beep" => "폭발·투척물",
        "step" or "roll" or "honk" or "honk_long" or "heal" or "pickup" => "플레이어",
        "groan" or "sonar" => "몬스터",
        _ when def.Id.StartsWith("zombie_", StringComparison.Ordinal) || def.Id.StartsWith("balloon_", StringComparison.Ordinal)
            || def.Id.StartsWith("bull_", StringComparison.Ordinal) => "몬스터",
        _ when def.Id.StartsWith("shot_", StringComparison.Ordinal) || def.Id.StartsWith("reload_", StringComparison.Ordinal)
            || def.Id.StartsWith("mortar_", StringComparison.Ordinal) || def.Id is "rocket_launch" or "melee_swing" or "pan_bong" or "dry_fire" or "throw" => "무기",
        _ => def.Bus == "Master" ? "알림" : "기타 효과음",
    };

    private static (double Peak, double Rms)? SoundLevels(AudioStream stream)
    {
        if (stream is not AudioStreamWav { Format: AudioStreamWav.FormatEnum.Format16Bits } wav) return null;
        byte[] pcm = wav.Data;
        int count = pcm.Length / 2;
        if (count == 0) return (0, 0);
        double peak = 0, sum = 0;
        for (int i = 0; i < count; i++)
        {
            double sample = BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(i * 2, 2)) / 32768.0;
            peak = Math.Max(peak, Math.Abs(sample));
            sum += sample * sample;
        }
        return (peak, Math.Sqrt(sum / count));
    }

    private static string SoundDb(double linear) => linear <= 0 ? "−∞ dBFS" : Number((float)(20 * Math.Log10(linear))) + " dBFS";

    private static byte[] SoundWave(AudioStreamWav wav)
    {
        if (wav.Format != AudioStreamWav.FormatEnum.Format16Bits) throw new InvalidOperationException("합성 PCM16 형식이 필요합니다.");
        byte[] pcm = wav.Data;
        short channels = (short)(wav.Stereo ? 2 : 1);
        using var memory = new MemoryStream(44 + pcm.Length);
        using var writer = new BinaryWriter(memory, Encoding.ASCII, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + pcm.Length);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
        writer.Write((short)1); writer.Write(channels); writer.Write(wav.MixRate);
        writer.Write(wav.MixRate * channels * 2); writer.Write((short)(channels * 2)); writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(pcm.Length); writer.Write(pcm);
        writer.Flush();
        return memory.ToArray();
    }
}
