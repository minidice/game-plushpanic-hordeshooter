using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter.Tests;

/// <summary>Pure sample checks: catches missing cues, truncated draw, harsh clipping and identical weapon swipes.</summary>
public static class HunterSoundChecks
{
    public static void Run(Action<bool, string> check)
    {
        var clips = new Dictionary<string, float[]>();
        string[] ids = { "hunter_club_raise", "hunter_club_swing", "hunter_club_hit",
            "hunter_knife_ready", "hunter_knife_swing", "hunter_knife_hit",
            "hunter_bow_draw", "hunter_bow_release", "hunter_bow_hit" };
        foreach (var id in ids)
        {
            var def = SoundRecipes.Find(id);
            check(def != null, id + " is registered");
            if (def == null) continue;
            var clip = def.Build(new Synth(813));
            clips.Add(id, clip);
            float duration = clip.Length / (float)Synth.Rate;
            check(duration >= .05f && duration <= 1.6f && !def.Loop, id + " has a finite one-shot duration");
            check(clip.All(float.IsFinite), id + " has finite samples");
            float peak = clip.Max(v => MathF.Abs(v));
            check(peak > .1f && peak < .9f && Rms(clip, 0, clip.Length) > .005f,
                id + " is audible without clipping");
            check(MathF.Abs(clip[0]) < .001f && MathF.Abs(clip[^1]) < .001f,
                id + " fades at both ends");
            check(clip.SequenceEqual(def.Build(new Synth(813))), id + " is deterministic");
            check(def.NearCap && def.MaxVoices is >= 1 and <= 3 && def.Priority <= 1,
                id + " limits close crowd playback without stealing priority gunfire");
            float stackedPeak = peak * MathF.Pow(10, def.VolumeDb / 20) * def.MaxVoices;
            check(stackedPeak < .8f, id + " leaves headroom even with coherent voices at its cap");
        }
        if (clips.Count != ids.Length) return;
        var club = clips["hunter_club_swing"];
        var knife = clips["hunter_knife_swing"];
        check(club.Length > knife.Length * 1.5f, "club sweep carries longer weight than the quick knife swipe");
        check(Brightness(knife) > Brightness(club) * 1.5f, "knife swipe has more high-frequency motion than club sweep");
        var draw = clips["hunter_bow_draw"];
        check(MathF.Abs(draw.Length / (float)Synth.Rate - 1.2f) < .002f,
            "bow tension spans the base 1.2 second windup");
        check(Rms(draw, draw.Length / 2, draw.Length / 3) > .01f,
            "bow tension remains audible late in the draw");
        check(SoundRecipes.Find("hunter_bow_draw")!.PitchJitter == 0,
            "bow draw timing is not randomized away from its windup");
        check(clips["hunter_knife_ready"].Length < .25f * Synth.Rate,
            "knife ready finishes within the base anticipation");
    }

    private static double Rms(float[] samples, int start, int count)
    {
        double sum = 0;
        for (int i = start; i < start + count; i++) sum += samples[i] * samples[i];
        return Math.Sqrt(sum / count);
    }

    // First-difference energy relative to signal energy measures spectral brightness without FFT fixtures.
    private static double Brightness(float[] samples)
    {
        double difference = 0, energy = 0;
        for (int i = 1; i < samples.Length; i++)
        {
            double delta = samples[i] - samples[i - 1];
            difference += delta * delta;
            energy += samples[i] * samples[i];
        }
        return difference / energy;
    }
}
