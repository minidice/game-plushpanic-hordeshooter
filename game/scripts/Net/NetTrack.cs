using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>
/// Recent host samples of one moving thing. A client draws the world a little in the past (<see cref="NetTrack.Delay"/>)
/// and blends the two samples around that moment, so 10–20 updates a second still look smooth.
/// </summary>
public sealed class NetTrack
{
    /// <summary>How far behind the host's clock a client draws (decided 2026-10-20, multiplayer-sync-design.md 9장 ③).</summary>
    public const double Delay = .1;
    private readonly List<(double Time, Vector3 Pos, Vector3 Facing, int Flags)> _samples = new(8);

    public int Flags { get; private set; }
    public int Count => _samples.Count;
    public double LatestTime => _samples.Count == 0 ? 0 : _samples[^1].Time;
    public double SampledTime { get; private set; }

    public void Push(double time, Vector3 pos, Vector3 facing, int flags)
    {
        if (_samples.Count > 0 && time <= _samples[^1].Time) return; // out of order or duplicate
        _samples.Add((time, pos, facing, flags));
        if (_samples.Count > 8) _samples.RemoveAt(0);
    }

    /// <summary>Position and facing at <paramref name="time"/>; holds the newest sample when updates stop.</summary>
    public bool Sample(double time, out Vector3 pos, out Vector3 facing)
    {
        pos = facing = Vector3.Zero;
        if (_samples.Count == 0) return false;
        SampledTime = System.Math.Clamp(time, _samples[0].Time, _samples[^1].Time);
        if (time <= _samples[0].Time || _samples.Count == 1)
        {
            (pos, facing, Flags) = (_samples[0].Pos, _samples[0].Facing, _samples[0].Flags);
            return true;
        }
        for (int i = 1; i < _samples.Count; i++)
        {
            var (t1, p1, f1, fl1) = _samples[i];
            if (time > t1) continue;
            var (t0, p0, f0, _) = _samples[i - 1];
            float k = (float)((time - t0) / System.Math.Max(1e-6, t1 - t0));
            pos = p0.Lerp(p1, k);
            facing = f0.Slerp(f1, k);
            if (facing.LengthSquared() < .01f) facing = f1;
            Flags = fl1;
            return true;
        }
        (pos, facing, Flags) = (_samples[^1].Pos, _samples[^1].Facing, _samples[^1].Flags);
        return true;
    }
}
