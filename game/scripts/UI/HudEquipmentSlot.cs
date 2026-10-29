using Godot;

namespace HordeShooter;

/// <summary>
/// One small tile of the bottom row (decisions.md M-42): the icon in a square, a count in its corner for items, and the
/// key in a little cap under it. The name lives in the tooltip; the active weapon's name and ammo float above its tile.
/// Icons follow content data; line art is the fallback for unassigned assets.
/// </summary>
public partial class HudEquipmentSlot : VBoxContainer
{
    public const float TileSize = 60;

    private readonly PanelContainer _tile;
    private readonly Label _count;
    private readonly StyleBoxFlat _normal = Hud.HudPlate(new Color(.02f, .035f, .06f, .62f), new Color(.6f, .7f, .82f, .16f), 0, 9);
    private readonly StyleBoxFlat _selected = Hud.HudPlate(new Color(.12f, .09f, .08f, .78f), new Color(Hud.HudWarm, .95f), 0, 9);
    private readonly StyleBoxFlat _medkitPlate = Hud.HudPlate(new Color(.12f, .18f, .2f, .9f), Hud.HudMuted, 0, 6);
    private Texture2D? _texture;
    private string _iconPath = "", _kind = "";
    private bool _available, _active;

    /// <summary>The item's name (shown as the tooltip, read by checks).</summary>
    public string Title { get; private set; } = "";
    public bool Active => _active;
    /// <summary>The square itself, without the key cap: the active weapon's name and ammo sit above it.</summary>
    public Rect2 TileRect => _tile.GetGlobalRect();

    public HudEquipmentSlot() : this("EquipmentSlot", "") { }

    public HudEquipmentSlot(string nodeName, string key)
    {
        Name = nodeName;
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsVertical = SizeFlags.ShrinkEnd;
        AddThemeConstantOverride("separation", 4);
        _tile = new PanelContainer { Name = "Tile", CustomMinimumSize = new Vector2(TileSize, TileSize), MouseFilter = MouseFilterEnum.Ignore };
        _tile.AddThemeStyleboxOverride("panel", _normal);
        var face = new Control { MouseFilter = MouseFilterEnum.Ignore };
        face.AddChild(new Paint { Painter = DrawIcon, AnchorRight = 1, AnchorBottom = 1 });
        _count = Hud.HudLabel("", 10, Hud.HudWhite);
        _count.HorizontalAlignment = HorizontalAlignment.Right;
        _count.AnchorLeft = _count.AnchorRight = 1;
        _count.OffsetLeft = -40;
        _count.OffsetRight = -4;
        _count.OffsetTop = 1;
        face.AddChild(_count);
        _tile.AddChild(face);
        AddChild(_tile);
        AddChild(Hud.KeyCap(key));
    }

    public void UpdateSlot(string title, string count, string iconPath, string kind, bool active, bool available)
    {
        Title = title;
        TooltipText = title;
        _count.Text = count;
        _kind = kind;
        _available = available;
        if (_active != active)
        {
            _active = active;
            _tile.AddThemeStyleboxOverride("panel", active ? _selected : _normal);
        }
        _tile.Modulate = new Color(1, 1, 1, available || active ? 1 : .55f);
        if (_iconPath == iconPath) return;
        _iconPath = iconPath;
        _texture = iconPath != "" && ResourceLoader.Exists(iconPath) ? ResourceLoader.Load<Texture2D>(iconPath) : null;
    }

    private void DrawIcon(Control c)
    {
        var center = c.Size / 2;
        var color = _available ? (_active ? Hud.HudWarm : new Color("c0c8d2")) : new Color(.4f, .47f, .58f, .35f);
        if (_texture != null)
        {
            var size = _texture.GetSize();
            var scale = Mathf.Min((c.Size.X - 8) / size.X, (c.Size.Y - 8) / size.Y);
            c.DrawTextureRect(_texture, new Rect2(center - size * scale / 2, size * scale), false, Colors.White);
            return;
        }
        // Line art sized for the 60 px tile.
        switch (_kind)
        {
            case "medkit":
                _medkitPlate.BorderColor = color;
                c.DrawStyleBox(_medkitPlate, new Rect2(center - new Vector2(15, 11), new Vector2(30, 22)));
                c.DrawRect(new Rect2(center - new Vector2(2.5f, 7), new Vector2(5, 14)), color);
                c.DrawRect(new Rect2(center - new Vector2(7, 2.5f), new Vector2(14, 5)), color);
                break;
            case "throwable":
                c.DrawCircle(center + new Vector2(0, 3), 9, color);
                c.DrawRect(new Rect2(center + new Vector2(-4, -10), new Vector2(8, 6)), color);
                c.DrawArc(center + new Vector2(5, -9), 4, 0, Mathf.Tau, 18, color, 2, true);
                break;
            case "melee":
                c.DrawCircle(center + new Vector2(-6, -3), 10, color);
                c.DrawCircle(center + new Vector2(-6, -3), 7, new Color(.08f, .1f, .15f));
                c.DrawLine(center + new Vector2(2, 4), center + new Vector2(17, 13), color, 5, true);
                break;
            case "head":
                c.DrawArc(center + new Vector2(0, 4), 11, Mathf.Pi, Mathf.Tau, 16, color, 3, true);
                c.DrawLine(center + new Vector2(-13, 4), center + new Vector2(15, 4), color, 3, true);
                break;
            case "armor":
                c.DrawPolyline(new[] { center + new Vector2(-6, -13), center + new Vector2(-12, -9), center + new Vector2(-12, 13),
                    center + new Vector2(12, 13), center + new Vector2(12, -9), center + new Vector2(6, -13), center + new Vector2(0, -5),
                    center + new Vector2(-6, -13) }, color, 2.5f, true);
                break;
            case "pistol":
                c.DrawColoredPolygon(new[] { center + new Vector2(-16, -8), center + new Vector2(15, -8), center + new Vector2(15, -2), center + new Vector2(6, -2), center + new Vector2(2, 11), center + new Vector2(-5, 11), center + new Vector2(-2, -1), center + new Vector2(-16, -1) }, color);
                break;
            default:
                c.DrawLine(center + new Vector2(-22, -4), center + new Vector2(22, -4), color, 4, true);
                c.DrawRect(new Rect2(center + new Vector2(-14, -8), new Vector2(22, 9)), color);
                c.DrawLine(center + new Vector2(-7, 0), center + new Vector2(-10, 9), color, 5, true);
                c.DrawLine(center + new Vector2(2, 0), center + new Vector2(5, 10), color, 5, true);
                break;
        }
    }
}
