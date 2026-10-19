using System;

namespace HordeShooter;

public enum Wave
{
    Sine,
    Square,
    Saw,
    Triangle,
}

/// <summary>
/// Tiny offline synthesizer: every sound in the game is built from noise, oscillators, envelopes
/// and one-pole filters into a mono float buffer. Pure C#, so recipes are testable without Godot.
/// </summary>
public sealed class Synth
{
    public const int Rate = 22050;

    private readonly Random _rng;

    public Synth(int seed) => _rng = new Random(seed);

    public static float[] Buffer(float seconds) => new float[Math.Max(1, (int)(seconds * Rate))];

    public static float Time(int i) => i / (float)Rate;

    // ---------- sources ----------

    /// <summary>White noise into [start, start + duration).</summary>
    public void Noise(float[] buf, float amp, float start = 0, float duration = float.MaxValue)
    {
        Range(buf, start, duration, out int a, out int b);
        for (int i = a; i < b; i++) buf[i] += amp * (float)(_rng.NextDouble() * 2 - 1);
    }

    /// <summary>Sparse random clicks (crackle).</summary>
    public void Crackle(float[] buf, float amp, float perSecond)
    {
        float chance = perSecond / Rate;
        for (int i = 0; i < buf.Length; i++)
        {
            if (_rng.NextDouble() > chance) continue;
            float level = amp * (.4f + .6f * (float)_rng.NextDouble());
            for (int k = 0; k < 40 && i + k < buf.Length; k++) buf[i + k] += level * MathF.Exp(-k / 6f) * (k % 2 == 0 ? 1 : -1);
        }
    }

    /// <summary>Oscillator with a frequency curve over its own local time (seconds).</summary>
    public static void Tone(float[] buf, Func<float, float> freq, Wave wave, float amp, float start = 0, float duration = float.MaxValue)
    {
        Range(buf, start, duration, out int a, out int b);
        double phase = 0;
        for (int i = a; i < b; i++)
        {
            float t = (i - a) / (float)Rate;
            phase += freq(t) / Rate;
            float p = (float)(phase - Math.Floor(phase));
            float v = wave switch
            {
                Wave.Square => p < .5f ? 1 : -1,
                Wave.Saw => 2 * p - 1,
                Wave.Triangle => 4 * MathF.Abs(p - .5f) - 1,
                _ => MathF.Sin(p * MathF.Tau),
            };
            buf[i] += amp * v;
        }
    }

    public static void Tone(float[] buf, float freq, Wave wave, float amp, float start = 0, float duration = float.MaxValue)
        => Tone(buf, _ => freq, wave, amp, start, duration);

    /// <summary>Linear glide from f0 to f1 across the duration.</summary>
    public static Func<float, float> Glide(float f0, float f1, float duration) => t => f0 + (f1 - f0) * MathF.Min(1, t / duration);

    /// <summary>Wobble around a base frequency curve.</summary>
    public static Func<float, float> Vibrato(Func<float, float> baseFreq, float rate, float depth) =>
        t => baseFreq(t) * (1 + depth * MathF.Sin(MathF.Tau * rate * t));

    // ---------- shaping ----------

    /// <summary>Linear attack then exponential decay (per second), applied to a region.</summary>
    public static void Envelope(float[] buf, float attack, float decay, float start = 0, float duration = float.MaxValue)
    {
        Range(buf, start, duration, out int a, out int b);
        for (int i = a; i < b; i++)
        {
            float t = (i - a) / (float)Rate;
            float gain = (attack > 0 ? MathF.Min(1, t / attack) : 1) * MathF.Exp(-decay * MathF.Max(0, t - attack));
            buf[i] *= gain;
        }
    }

    /// <summary>Envelope from an arbitrary gain curve over the whole buffer.</summary>
    public static void Shape(float[] buf, Func<float, float> gain)
    {
        for (int i = 0; i < buf.Length; i++) buf[i] *= gain(Time(i));
    }

    public static void LowPass(float[] buf, Func<float, float> cutoff)
    {
        float y = 0;
        for (int i = 0; i < buf.Length; i++)
        {
            float a = 1 - MathF.Exp(-MathF.Tau * cutoff(Time(i)) / Rate);
            y += a * (buf[i] - y);
            buf[i] = y;
        }
    }

    public static void LowPass(float[] buf, float cutoff) => LowPass(buf, _ => cutoff);

    public static void HighPass(float[] buf, float cutoff)
    {
        float y = 0, a = 1 - MathF.Exp(-MathF.Tau * cutoff / Rate);
        for (int i = 0; i < buf.Length; i++)
        {
            y += a * (buf[i] - y);
            buf[i] -= y;
        }
    }

    /// <summary>Soft saturation for punch.</summary>
    public static void Drive(float[] buf, float amount)
    {
        for (int i = 0; i < buf.Length; i++) buf[i] = MathF.Tanh(buf[i] * amount) / MathF.Tanh(amount);
    }

    public static void Mix(float[] dst, float[] src, float gain = 1, float start = 0)
    {
        int offset = (int)(start * Rate);
        for (int i = 0; i < src.Length && i + offset < dst.Length; i++) dst[i + offset] += src[i] * gain;
    }

    /// <summary>Scale so the loudest sample sits at <paramref name="peak"/>.</summary>
    public static float[] Normalize(float[] buf, float peak = .9f)
    {
        float max = 0;
        foreach (var v in buf) max = MathF.Max(max, MathF.Abs(v));
        if (max < 1e-6f) return buf;
        float k = peak / max;
        for (int i = 0; i < buf.Length; i++) buf[i] *= k;
        return buf;
    }

    /// <summary>Short fades at both ends so loops and cut sounds never click (also removes any DC offset).</summary>
    public static float[] Declick(float[] buf, float seconds = .004f)
    {
        float mean = 0;
        foreach (var v in buf) mean += v;
        mean /= buf.Length;
        for (int i = 0; i < buf.Length; i++) buf[i] -= mean;
        int n = Math.Min(buf.Length / 2, (int)(seconds * Rate));
        for (int i = 0; i < n; i++)
        {
            float g = i / (float)n;
            buf[i] *= g;
            buf[^(i + 1)] *= g;
        }
        return buf;
    }

    /// <summary>Crossfade the tail into the head so a loop is seamless.</summary>
    public static float[] MakeLoop(float[] buf, float fadeSeconds)
    {
        int n = Math.Min(buf.Length / 3, (int)(fadeSeconds * Rate));
        var outBuf = new float[buf.Length - n];
        Array.Copy(buf, outBuf, outBuf.Length);
        for (int i = 0; i < n; i++)
        {
            float g = i / (float)n;
            outBuf[i] = buf[i] * g + buf[outBuf.Length + i] * (1 - g);
        }
        return outBuf;
    }

    private static void Range(float[] buf, float start, float duration, out int a, out int b)
    {
        a = Math.Clamp((int)(start * Rate), 0, buf.Length);
        double end = duration >= float.MaxValue / 2 ? buf.Length : (start + duration) * Rate;
        b = (int)Math.Clamp(end, a, buf.Length);
    }

    public static byte[] ToPcm16(float[] buf)
    {
        var bytes = new byte[buf.Length * 2];
        for (int i = 0; i < buf.Length; i++)
        {
            short s = (short)Math.Clamp((int)(buf[i] * 32767f), short.MinValue, short.MaxValue);
            bytes[2 * i] = (byte)(s & 0xff);
            bytes[2 * i + 1] = (byte)((s >> 8) & 0xff);
        }
        return bytes;
    }
}
