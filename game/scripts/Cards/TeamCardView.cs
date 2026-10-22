using Godot;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Team cards on screen (decisions.md M-29 plan 16.6.1–16.6.3): gather!'s gold ring at the anchor's feet with a
/// megaphone over their head (the next anchor blinking), the honk beam's red cone on the ground following its
/// shooter's aim and its yellow flash, stars over dazed teammates and steam off weak ones, static fluff's + / − over
/// every head (blinking before they change) with sparks between opposite poles and bolts where rounds jump, and a HUD
/// line per card. Sprites by Codex (assets/effects/cards); drawn on every PC from the host's card sync and snapshots.
/// </summary>
public partial class TeamCardView : Node3D
{
    public const string FxDir = "res://assets/effects/cards/";
    public LabCards Cards { get; set; } = null!;
    public Stage Stage { get; set; } = null!;

    private MeshInstance3D _ring = null!;
    private Sprite3D _mark = null!, _nextMark = null!;
    private CanvasLayer _hud = null!;
    private Label _line = null!;
    private TextureRect _icon = null!;
    private float _time;
    private MeshInstance3D _cone = null!, _flash = null!;
    private StandardMaterial3D _coneMat = null!, _flashMat = null!;
    private float _flashLeft;
    private Label _beamLine = null!, _staticLine = null!;
    private readonly System.Collections.Generic.Dictionary<Player, (Sprite3D Pole, Sprite3D Stars)> _marks = new();
    private float _puffTick;

    public static Texture2D? Fx(string name) => ResourceLoader.Exists(FxDir + name + ".png") ? GD.Load<Texture2D>(FxDir + name + ".png") : null;

    public override void _Ready()
    {
        var ringMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color("ffcf5a"), AlbedoTexture = Fx("fx_ring"), CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        if (ringMat.AlbedoTexture == null) ringMat.AlbedoColor = new Color(1, .81f, .35f, .25f);
        _ring = new MeshInstance3D { Name = "GatherRing", Mesh = new PlaneMesh { Size = Vector2.One }, MaterialOverride = ringMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_ring);
        _mark = Mark("GatherMark");
        _nextMark = Mark("GatherNextMark");
        (_cone, _coneMat) = Wedge("BeamCone", new Color(1, .25f, .2f, .75f));
        (_flash, _flashMat) = Wedge("BeamFlash", new Color(1, .86f, .3f, 1));
        _hud = new CanvasLayer { Name = "TeamCardHud", Layer = 6 };
        AddChild(_hud);
        var plate = new PanelContainer { Name = "GatherPlate" };
        plate.AddThemeStyleboxOverride("panel", Hud.HudPlate(new Color(.06f, .07f, .1f, .85f), new Color("ffcf5a"), 8, 10));
        plate.AnchorLeft = plate.AnchorRight = .5f;
        plate.OffsetTop = 92;
        plate.GrowHorizontal = Control.GrowDirection.Both;
        _hud.AddChild(plate);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        plate.AddChild(row);
        _icon = new TextureRect { CustomMinimumSize = new Vector2(32, 32), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
        row.AddChild(_icon);
        _line = Hud.HudLabel("", 16, Hud.HudWhite);
        _line.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(_line);
        _beamLine = Hud.HudLabel("", 16, new Color("ffd23f"));
        _staticLine = Hud.HudLabel("", 16, new Color("6bb8ff"));
        foreach (var (label, y) in new[] { (_beamLine, 132f), (_staticLine, 156f) })
        {
            label.AnchorLeft = label.AnchorRight = .5f;
            label.OffsetTop = y;
            label.GrowHorizontal = Control.GrowDirection.Both;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            _hud.AddChild(label);
        }
    }

    /// <summary>A flat cone the size of the flashlight's on the ground (Codex's fx_cone, its tip at the bottom middle).</summary>
    private (MeshInstance3D, StandardMaterial3D) Wedge(string name, Color color)
    {
        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = color,
            AlbedoTexture = Fx("fx_cone"), CullMode = BaseMaterial3D.CullModeEnum.Disabled, NoDepthTest = false,
        };
        float size = VisionSystem.Range * 2 * 512f / 490f;
        var mesh = new MeshInstance3D { Name = name, Mesh = new PlaneMesh { Size = new Vector2(size, size) }, MaterialOverride = mat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
        AddChild(mesh);
        return (mesh, mat);
    }

    private void PlaceWedge(MeshInstance3D wedge, Vector3 at, Vector3 aim)
    {
        float size = VisionSystem.Range * 2 * 512f / 490f;
        var dir = new Vector3(aim.X, 0, aim.Z).Normalized();
        // The texture's tip sits near its bottom edge: push the plane forward so the tip is at the shooter.
        wedge.GlobalPosition = new Vector3(at.X, .08f, at.Z) + dir * (size * .5f - size * 12f / 512f);
        // PlaneMesh's +V (texture top) is −Z: turn so it points along the aim.
        wedge.GlobalRotation = new Vector3(0, Mathf.Atan2(-dir.X, -dir.Z), 0);
    }

    /// <summary>The beam goes off: a yellow cone that fades in under half a second.</summary>
    public void Flash(Vector3 at, Vector3 aim)
    {
        _flashLeft = .45f;
        PlaceWedge(_flash, at, aim);
    }

    /// <summary>A round jumping between two monsters (static fluff).</summary>
    public void Bolt(Vector3 from, Vector3 to)
    {
        var tex = Fx("fx_bolt");
        if (tex == null) return;
        var mid = (from + to) * .5f;
        float length = from.DistanceTo(to);
        var sprite = new Sprite3D { Texture = tex, Billboard = BaseMaterial3D.BillboardModeEnum.Disabled, PixelSize = length / 256f, Modulate = new Color("9fd8ff"),
            Shaded = false, Transparent = true };
        Stage.Effects.AddChild(sprite);
        sprite.GlobalPosition = mid + Vector3.Up * .2f;
        var flat = new Vector3(to.X - from.X, 0, to.Z - from.Z);
        sprite.GlobalRotation = new Vector3(-Mathf.Pi / 2, Mathf.Atan2(-flat.Z, flat.X), 0);
        var tween = sprite.CreateTween();
        tween.TweenProperty(sprite, "modulate:a", 0f, .25f);
        tween.TweenCallback(Callable.From(sprite.QueueFree));
    }

    private Sprite3D Mark(string name)
    {
        var sprite = new Sprite3D { Name = name, Texture = Fx("fx_gather_mark"), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, PixelSize = .006f,
            NoDepthTest = true, Visible = false };
        AddChild(sprite);
        return sprite;
    }

    public new void Hide()
    {
        Visible = false;
        if (_hud != null) _hud.Visible = false;
    }

    public void Refresh()
    {
        Visible = true;
        _hud.Visible = true;
        float dt = (float)GetPhysicsProcessDeltaTime();
        _time += dt;
        RefreshBeam(dt);
        RefreshPeople(dt);
        var anchor = Cards.IsOn(LabSpecial.Gather) ? Cards.GatherAnchor : null;
        bool has = anchor != null && IsInstanceValid(anchor);
        _ring.Visible = _mark.Visible = has;
        if (has)
        {
            var at = anchor!.GlobalPosition;
            float d = Cards.GatherRadius * 2;
            _ring.GlobalPosition = new Vector3(at.X, .06f, at.Z);
            _ring.Scale = new Vector3(d, 1, d);
            _mark.GlobalPosition = at + Vector3.Up * (2.6f * anchor.MadBodyScale);
        }
        var next = Cards.GatherNext;
        _nextMark.Visible = next != null && IsInstanceValid(next) && next != anchor && Mathf.PosMod(_time, .5f) < .3f;
        if (_nextMark.Visible) _nextMark.GlobalPosition = next!.GlobalPosition + Vector3.Up * 2.6f;

        var me = Stage.Player;
        var members = Cards.GatherMembers.ToList();
        int total = Stage.Players.Count(p => IsInstanceValid(p) && !p.IsDead);
        string effect = Loc.Text(Cards.GatherEffect switch { 1 => "ui_gather_crit", 2 => "ui_gather_fire", _ => "ui_gather_damage" });
        _icon.Texture = Fx(Cards.GatherEffect switch { 1 => "fx_gather_crit", 2 => "fx_gather_fire", _ => "fx_gather_damage" });
        _line.GetParent<HBoxContainer>().GetParent<PanelContainer>().Visible = has;
        _line.Text = !has ? "" : members.Contains(me)
            ? Loc.Format("fmt_gather_hud", effect, Mathf.RoundToInt(members.Count * Cards.GatherPerPerson * 100), members.Count, total)
            : Loc.Format("fmt_gather_out", anchor!.DisplayLabel != "" ? anchor.DisplayLabel : Loc.Text(anchor.Definition.DisplayName), effect);
    }

    private void RefreshBeam(float dt)
    {
        var shooter = Cards.IsOn(LabSpecial.HonkBeam) ? Cards.BeamShooter : null;
        bool tele = shooter != null && IsInstanceValid(shooter) && Cards.BeamTelegraphing;
        _cone.Visible = tele;
        if (tele)
        {
            PlaceWedge(_cone, shooter!.GlobalPosition, Cards.BeamAim);
            // Pulses faster as it comes; steady once locked.
            float left = Cards.BeamPeriod - Cards.BeamClock;
            _coneMat.AlbedoColor = new Color(1, .25f, .2f, left <= LabCards.BeamLock ? .9f : .45f + .3f * Mathf.Abs(Mathf.Sin(_time * (10 - left * 2))));
        }
        _flashLeft = Mathf.Max(0, _flashLeft - dt);
        _flash.Visible = _flashLeft > 0;
        _flashMat.AlbedoColor = new Color(1, .86f, .3f, _flashLeft / .45f);
        _beamLine.Text = !Cards.IsOn(LabSpecial.HonkBeam) ? "" : tele
            ? shooter == Stage.Player ? Loc.Format("fmt_beam_you", Mathf.CeilToInt(Cards.BeamPeriod - Cards.BeamClock))
              : Loc.Format("fmt_beam_other", PersonName(shooter!), Mathf.CeilToInt(Cards.BeamPeriod - Cards.BeamClock))
            : Loc.Format("fmt_beam_wait", Mathf.CeilToInt(Cards.BeamPeriod - Cards.BeamClock));
    }

    private void RefreshPeople(float dt)
    {
        bool poles = Cards.IsOn(LabSpecial.StaticFluff);
        bool blink = poles && Cards.StaticClock >= LabCards.StaticPeriod - LabCards.StaticWarn && Mathf.PosMod(_time, .4f) < .2f;
        _puffTick -= dt;
        bool puff = _puffTick <= 0;
        if (puff) _puffTick = .3f;
        foreach (var gone in _marks.Keys.Where(p => !IsInstanceValid(p)).ToList()) { _marks[gone].Pole.QueueFree(); _marks[gone].Stars.QueueFree(); _marks.Remove(gone); }
        var people = Stage.Players.Where(IsInstanceValid).ToList();
        foreach (var p in people)
        {
            if (!_marks.TryGetValue(p, out var m))
            {
                m = (new Sprite3D { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, PixelSize = .004f, NoDepthTest = true },
                     new Sprite3D { Texture = Fx("fx_stars"), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, PixelSize = .005f, NoDepthTest = true });
                AddChild(m.Pole);
                AddChild(m.Stars);
                _marks[p] = m;
            }
            float head = 2.3f * p.MadBodyScale;
            m.Pole.Visible = poles && p.Pole != 0 && !p.IsDead && !blink;
            m.Pole.Texture = Fx(p.Pole > 0 ? "fx_pole_plus" : "fx_pole_minus");
            m.Pole.GlobalPosition = p.GlobalPosition + Vector3.Up * (head + .9f);
            m.Stars.Visible = p.DazeTime > 0;
            m.Stars.GlobalPosition = p.GlobalPosition + Vector3.Up * (head + .3f);
            m.Stars.RotationDegrees = new Vector3(0, _time * 200, 0);
            if (p.WeakTime > 0 && p.DazeTime <= 0 && puff) Stage.Effects.Puff(p.GlobalPosition + Vector3.Up * (head - .4f), .35f, new Color("9fd8ff"));
        }
        // Sparks between opposite poles close together.
        if (!poles || !puff) { _staticLine.Text = poles ? _staticLine.Text : ""; return; }
        for (int i = 0; i < people.Count; i++)
            for (int j = i + 1; j < people.Count; j++)
            {
                var a = people[i];
                var b = people[j];
                if (a.IsDead || b.IsDead || a.Pole == 0 || a.Pole == b.Pole) continue;
                if (new Vector3(a.GlobalPosition.X - b.GlobalPosition.X, 0, a.GlobalPosition.Z - b.GlobalPosition.Z).Length() > LabCards.StaticReach) continue;
                Bolt(a.GlobalPosition + Vector3.Up * 1.2f, b.GlobalPosition + Vector3.Up * 1.2f);
            }
        var me = Stage.Player;
        _staticLine.Text = me.Pole == 0 ? "" : Loc.Format("fmt_static_me", me.Pole > 0 ? "+" : "−",
            Mathf.CeilToInt(LabCards.StaticPeriod - Cards.StaticClock));
    }

    private static string PersonName(Player p) => p.DisplayLabel != "" ? p.DisplayLabel : Loc.Text(p.Definition.DisplayName);
}
