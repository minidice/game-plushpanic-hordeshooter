using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>
/// Recoil that moves the aim point (decisions.md C-30, doc/weapon-aim-reference.md). Each shot kicks it up the screen by
/// recoil_up and sideways by a random amount within ±recoil_side, in pixels of a 1080-high screen. The local player adds
/// the offset to the mouse, so the reticle climbs and the hand pulls it back down. Once the gun has been quiet for one
/// shot's interval it slides back at recoil_return pixels a second. Studio draws the same path from <see cref="Trace"/>.
/// </summary>
public sealed class WeaponRecoil
{
    public const float ReferenceHeight = 1080f;
    /// <summary>The kick never takes the reticle further than this from the hand (pixels at 1080).</summary>
    public const float MaxOffset = 320f;
    /// <summary>Extra quiet time after a shot's interval before the aim point starts back.</summary>
    public const float QuietAfter = .05f;

    public Vector2 Offset { get; private set; }
    private float _quiet, _return = 200;

    /// <param name="scale">Worn gear's recoil factor (C-34): both the climb and the sideways kick, not the slide back.</param>
    public void Kick(WeaponDefinition def, RandomNumberGenerator rng, float scale = 1f)
    {
        if (def.RecoilUp <= 0 && def.RecoilSide <= 0) return;
        var kicked = Offset + new Vector2(rng.RandfRange(-def.RecoilSide, def.RecoilSide), -def.RecoilUp) * scale;
        Offset = kicked.LimitLength(MaxOffset);
        _quiet = def.FireInterval + QuietAfter;
        _return = def.RecoilReturn;
    }

    public void Tick(float dt)
    {
        if (_quiet > 0)
        {
            _quiet -= dt;
            if (_quiet > 0) return;
            dt = -_quiet;
        }
        Offset = Offset.MoveToward(Vector2.Zero, _return * dt);
    }

    public void Clear()
    {
        Offset = Vector2.Zero;
        _quiet = 0;
    }

    /// <summary>The aim point after each of <paramref name="shots"/> shots held at the gun's own rate (Studio, checks).</summary>
    public static List<Vector2> Trace(WeaponDefinition def, int shots, ulong seed = 1)
    {
        var rng = new RandomNumberGenerator { Seed = seed };
        var recoil = new WeaponRecoil();
        var path = new List<Vector2>(shots);
        for (int i = 0; i < shots; i++)
        {
            recoil.Kick(def, rng);
            path.Add(recoil.Offset);
            recoil.Tick(def.FireInterval);
        }
        return path;
    }
}
