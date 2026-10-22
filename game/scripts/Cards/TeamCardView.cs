using Godot;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Team cards on screen (decisions.md M-29 plan 16.6.1): gather!'s gold ring at the anchor's feet with a megaphone
/// over their head, the next anchor's mark blinking for the last seconds, and a HUD line with the bonus and how many
/// are inside. Sprites by Codex (assets/effects/cards); drawn on every PC from the host's card sync.
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
        _time += (float)GetPhysicsProcessDeltaTime();
        var anchor = Cards.GatherAnchor;
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
        _line.Text = !has ? "" : members.Contains(me)
            ? Loc.Format("fmt_gather_hud", effect, Mathf.RoundToInt(members.Count * Cards.GatherPerPerson * 100), members.Count, total)
            : Loc.Format("fmt_gather_out", anchor!.DisplayLabel != "" ? anchor.DisplayLabel : Loc.Text(anchor.Definition.DisplayName), effect);
    }
}
