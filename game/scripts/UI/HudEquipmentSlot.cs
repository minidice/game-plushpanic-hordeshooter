using Godot;

namespace HordeShooter;

/// <summary>Display-only hotbar tile. Icons follow content data; line art is the fallback for unassigned assets.</summary>
public partial class HudEquipmentSlot : PanelContainer
{
    private readonly Label _name;
    private readonly Label _count;
    private readonly StyleBoxFlat _normal = Hud.HudPlate(pad: 7, radius: 12);
    private readonly StyleBoxFlat _selected = Hud.HudPlate(UiTheme.SelectedSurface, new Color(UiTheme.Selection, .5f), 7, 12);
    private readonly StyleBoxFlat _medkitPlate = Hud.HudPlate(new Color(.12f, .18f, .2f, .9f), Hud.HudMuted, 0, 6);
    private Texture2D? _texture;
    private string _iconPath = "", _kind = "";
    private bool _available, _active;

    public HudEquipmentSlot() : this("EquipmentSlot", "") { }

    public HudEquipmentSlot(string nodeName, string key)
    {
        Name = nodeName;
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(94, 94);
        AddThemeStyleboxOverride("panel", _normal);
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 0);
        AddChild(box);
        var top = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        top.AddChild(Hud.HudLabel(key, 12, UiTheme.Selection));
        _count = Hud.HudLabel("", 11, Hud.HudMuted);
        _count.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _count.HorizontalAlignment = HorizontalAlignment.Right;
        top.AddChild(_count);
        box.AddChild(top);
        box.AddChild(new Paint { CustomMinimumSize = new Vector2(80, 39), Painter = DrawIcon });
        _name = Hud.HudLabel("", 11, Hud.HudMuted);
        _name.HorizontalAlignment = HorizontalAlignment.Center;
        _name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _name.CustomMinimumSize = new Vector2(80, 0);
        box.AddChild(_name);
    }

    public void UpdateSlot(string title, string count, string iconPath, string kind, bool active, bool available)
    {
        _name.Text = title;
        _count.Text = count;
        _kind = kind;
        _available = available;
        if (_active != active)
        {
            _active = active;
            AddThemeStyleboxOverride("panel", active ? _selected : _normal);
            _name.AddThemeColorOverride("font_color", active ? Hud.HudWhite : Hud.HudMuted);
        }
        if (_iconPath == iconPath) return;
        _iconPath = iconPath;
        _texture = iconPath != "" && ResourceLoader.Exists(iconPath) ? ResourceLoader.Load<Texture2D>(iconPath) : null;
    }

    private void DrawIcon(Control c)
    {
        var center = c.Size / 2;
        var color = _available ? (_active ? UiTheme.Selection : new Color("c0c8d2")) : new Color(.4f, .47f, .58f, .35f);
        if (_texture != null)
        {
            var size = _texture.GetSize();
            var scale = Mathf.Min((c.Size.X - 8) / size.X, (c.Size.Y - 3) / size.Y);
            c.DrawTextureRect(_texture, new Rect2(center - size * scale / 2, size * scale), false, Colors.White);
            return;
        }
        switch (_kind)
        {
            case "medkit":
                _medkitPlate.BorderColor = color;
                c.DrawStyleBox(_medkitPlate, new Rect2(center - new Vector2(20, 14), new Vector2(40, 28)));
                c.DrawRect(new Rect2(center - new Vector2(3, 9), new Vector2(6, 18)), color);
                c.DrawRect(new Rect2(center - new Vector2(9, 3), new Vector2(18, 6)), color);
                break;
            case "throwable":
                c.DrawCircle(center + new Vector2(0, 3), 11, color);
                c.DrawRect(new Rect2(center + new Vector2(-5, -12), new Vector2(10, 7)), color);
                c.DrawArc(center + new Vector2(6, -11), 5, 0, Mathf.Tau, 18, color, 2, true);
                break;
            case "melee":
                c.DrawCircle(center + new Vector2(-8, -3), 13, color);
                c.DrawCircle(center + new Vector2(-8, -3), 9, new Color(.08f, .1f, .15f));
                c.DrawLine(center + new Vector2(3, 5), center + new Vector2(23, 17), color, 6, true);
                break;
            case "pistol":
                c.DrawColoredPolygon(new[] { center + new Vector2(-22, -10), center + new Vector2(20, -10), center + new Vector2(20, -2), center + new Vector2(8, -2), center + new Vector2(3, 15), center + new Vector2(-7, 15), center + new Vector2(-3, -1), center + new Vector2(-22, -1) }, color);
                c.DrawLine(center + new Vector2(-18, -6), center + new Vector2(16, -6), new Color(.1f, .14f, .2f), 2, true);
                break;
            default:
                c.DrawLine(center + new Vector2(-29, -5), center + new Vector2(30, -5), color, 5, true);
                c.DrawRect(new Rect2(center + new Vector2(-19, -10), new Vector2(30, 12)), color);
                c.DrawLine(center + new Vector2(-10, 0), center + new Vector2(-14, 12), color, 6, true);
                c.DrawLine(center + new Vector2(3, 0), center + new Vector2(7, 13), color, 7, true);
                c.DrawLine(center + new Vector2(-29, -5), center + new Vector2(-29, 5), color, 8, true);
                break;
        }
    }
}
