using Godot;

namespace HordeShooter;

public partial class Hud
{
    // One material language for gun sights, magazine arcs and the existing action ring.
    private static readonly Color SightGlass = new(.008f, .016f, .025f, .48f);
    private static readonly Color SightBlue = new(.42f, .68f, .83f);
    private static readonly Vector2[] SightAxes = { Vector2.Up, Vector2.Right, Vector2.Down, Vector2.Left };

    private static bool HasMagazineSight(WeaponState gun) => gun.Def.MagSize > 0
        && gun.Def.Mode is FireMode.Hitscan or FireMode.Projectile or FireMode.Lob;

    private static void SightLine(Control c, Vector2 a, Vector2 b, Color core, float width, Color tint)
    {
        if (core.A <= 0) return;
        c.DrawLine(a + Vector2.Down, b + Vector2.Down, new Color(SightGlass, .18f * core.A), width + 7, true);
        c.DrawLine(a, b, new Color(SightGlass, .5f * core.A), width + 4, true);
        for (int i = 4; i >= 1; i--)
            c.DrawLine(a, b, new Color(tint, .025f * core.A), width + i * 1.6f, true);
        c.DrawLine(a, b, new Color(tint, .6f * core.A), width + 1.6f, true);
        c.DrawLine(a, b, core, width, true);
        c.DrawCircle(a, width * .5f, core, true, -1, true);
        c.DrawCircle(b, width * .5f, core, true, -1, true);
    }

    private static void SightArc(Control c, Vector2 center, float radius, float start, float end, Color core, float width, Color tint)
    {
        if (end <= start || core.A <= 0) return;
        int segments = Mathf.Max(8, Mathf.CeilToInt((end - start) * 18));
        c.DrawArc(center + Vector2.Down, radius, start, end, segments, new Color(SightGlass, .18f * core.A), width + 7, true);
        c.DrawArc(center, radius, start, end, segments, new Color(SightGlass, .5f * core.A), width + 4, true);
        for (int i = 4; i >= 1; i--)
            c.DrawArc(center, radius, start, end, segments, new Color(tint, .025f * core.A), width + i * 1.6f, true);
        c.DrawArc(center, radius, start, end, segments, new Color(tint, .6f * core.A), width + 1.6f, true);
        c.DrawArc(center, radius, start, end, segments, core, width, true);
        c.DrawCircle(center + Vector2.FromAngle(start) * radius, width * .5f, core, true, -1, true);
        c.DrawCircle(center + Vector2.FromAngle(end) * radius, width * .5f, core, true, -1, true);
    }

    private static void DrawGunSight(Control c, Vector2 center, WeaponDefinition def, float radius, float aimed, Color color, float heat)
    {
        var tint = SightBlue.Lerp(ReticleHot, heat * .65f);
        // The current cone is the primary silhouette; the aimed reference stays quiet and inside it.
        if (radius - aimed > 7)
        {
            var reference = new Color(color, color.A * .36f);
            foreach (var dir in SightAxes)
                SightLine(c, center + dir * (aimed + 1), center + dir * (aimed + 4), reference, 1.5f, SightBlue);
        }
        float r = radius + 2;
        void Line(Vector2 a, Vector2 b, float width = 2.7f) => SightLine(c, center + a, center + b, color, width, tint);
        void Arc(float a, float b) => SightArc(c, center, r, a, b, color, 2.7f, tint);
        switch (def.Category)
        {
            case WeaponCategory.Pistol:
                foreach (float side in new[] { -1f, 1f })
                {
                    Line(new Vector2(side * r, 4), new Vector2(side * (r + 3), -3));
                    Line(new Vector2(side * (r + 3), -3), new Vector2(side * (r + 12), -3));
                }
                break;
            case WeaponCategory.Smg:
                Arc(-.65f, .65f);
                Arc(Mathf.Pi - .65f, Mathf.Pi + .65f);
                break;
            case WeaponCategory.Shotgun:
                foreach (float a in new[] { 0f, Mathf.Pi * .5f, Mathf.Pi, Mathf.Pi * 1.5f }) Arc(a - .48f, a + .48f);
                break;
            case WeaponCategory.Lmg:
                foreach (var side in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
                {
                    var corner = side * (r * .7071f + 5);
                    Line(corner, corner - new Vector2(side.X * 8, 0), 3.2f);
                    Line(corner, corner - new Vector2(0, side.Y * 8), 3.2f);
                }
                break;
            case WeaponCategory.Sniper:
                float length = def.TypeId == "dmr" ? 10 : 19;
                foreach (var dir in SightAxes)
                    Line(dir * r, dir * (r + length), 1.9f);
                break;
            default:
                foreach (var dir in SightAxes) Line(dir * r, dir * (r + ReticleTickLength));
                break;
        }
        c.DrawCircle(center, ReticleDot + 3, new Color(SightGlass, color.A * .65f), true, -1, true);
        c.DrawCircle(center, ReticleDot + 1, new Color(tint, color.A * .45f), true, -1, true);
        c.DrawCircle(center, ReticleDot, color, true, -1, true);
    }

    private static void DrawFireGlyph(Control c, Vector2 center, WeaponDefinition def, float opacity)
    {
        if (opacity <= 0) return;
        var color = new Color(ActionLight, .85f * opacity);
        void Line(Vector2 a, Vector2 b) => SightLine(c, center + a, center + b, color, 2.2f, SightBlue);
        if (def.BurstCount > 1 || !def.Automatic)
        {
            int count = Mathf.Max(1, def.BurstCount);
            float spacing = Mathf.Min(6, 18f / Mathf.Max(1, count - 1));
            for (int i = 0; i < count; i++)
            {
                float x = (i - (count - 1) * .5f) * spacing;
                Line(new Vector2(x, -4), new Vector2(x, 4));
            }
        }
        else
        {
            foreach (float x in new[] { -6f, 2f })
            {
                Line(new Vector2(x, -4), new Vector2(x + 4, 0));
                Line(new Vector2(x + 4, 0), new Vector2(x, 4));
            }
        }
    }

    /// <summary>One track morphs from a lower-right magazine arc into the centered action ring. The clocks never blend.</summary>
    private void DrawMagazineAction(Control c, Vector2 center, WeaponState gun, float sightRadius)
    {
        float w = _weaponAction.Weight;
        bool magazine = HasMagazineSight(gun);
        if (!magazine) { DrawWeaponActionRing(c, center); return; }
        float restingRadius = Mathf.Max(66, sightRadius + 52);
        // Aim stays at the pointer. Only the auxiliary readout changes quadrant at screen edges.
        bool left = center.X + restingRadius + 14 > c.Size.X;
        bool up = center.Y + restingRadius + 14 > c.Size.Y;
        float restingStart = left ? (up ? Mathf.Pi + .05f : Mathf.Pi - 1.15f) : (up ? -1.15f : .05f);
        var glyphDirection = Vector2.FromAngle(1.36f) * new Vector2(left ? -1 : 1, up ? -1 : 1);
        float radius = Mathf.Lerp(restingRadius, 19, w);
        float start = Mathf.LerpAngle(restingStart, -Mathf.Pi / 2, w);
        float sweep = Mathf.Lerp(1.1f, Mathf.Tau, w);
        c.DrawArc(center + Vector2.Down, radius, start, start + sweep, 96, new Color(0, 0, 0, .16f), Mathf.Lerp(9, 14, w), true);
        c.DrawArc(center, radius, start, start + sweep, 96, SightGlass, Mathf.Lerp(7, 11, w), true);
        // A faint rim makes an empty magazine track readable without suggesting remaining rounds.
        if (w < 1)
            c.DrawArc(center, radius, start, start + sweep, 48, new Color(SightBlue, .23f * (1 - w)), 1, true);
        float fraction = Mathf.Clamp((float)gun.Mag / gun.Def.MagSize, 0, 1);
        float ammoAlpha = Mathf.Clamp(1 - w * 2, 0, 1);
        if (fraction > 0 && ammoAlpha > 0)
            SightArc(c, center, radius, start, start + sweep * fraction, new Color(ActionLight, .9f * ammoAlpha), 2.8f, SightBlue);
        if (w > 0 && _weaponAction.Progress > 0)
            DrawLightArc(c, center, radius, start + sweep * _weaponAction.Progress, ActionGlow, w, start);
        DrawFireGlyph(c, center + glyphDirection * (restingRadius + 2), gun.Def, 1 - w);
    }
}
