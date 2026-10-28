using Godot;

namespace HordeShooter;

public static class UiTheme
{
    public static readonly Color Ink = new("0d1114");
    public static readonly Color Paper = new("f3f4f1");
    public static readonly Color Accent = new("ffcf5c");
    public static readonly Color Muted = new("a8b6c9");
    public static readonly Color Danger = new("ff5b4f");
    public static readonly Color Heal = new("7dff9a");
    public static readonly Color Helium = new("ff9ccc");

    // Shared presentation tokens. Accent stays gold for focus/rewards; Selection means an actionable UI choice.
    public static readonly Color Selection = new("b8e5ff");
    public static readonly Color Surface = new(.025f, .045f, .09f, .83f);
    public static readonly Color SelectedSurface = new(.08f, .15f, .22f, .94f);
    public static readonly Color Edge = new(.45f, .57f, .72f, .10f);
    public static readonly Color Glass = new(.008f, .016f, .025f, .48f);
    public static readonly Color SightTint = new(.42f, .68f, .83f);
    public static readonly Color ActionLight = new(.94f, .98f, 1f, .98f);
    public static readonly Color ActionGlow = new(.22f, .70f, 1f);
    public const float EnterSeconds = .16f, ExitSeconds = .12f, ActionReturnSeconds = .20f;
    public static readonly StyleBoxFlat ProgressTrack = Plate(new Color(.008f, .016f, .025f, .62f), Colors.Transparent, 0, 11);
    public static readonly StyleBoxFlat ProgressFill = LightFill();

    private static StyleBoxFlat LightFill()
    {
        var fill = Plate(ActionLight, Colors.Transparent, 0, 7);
        fill.ShadowColor = new Color(.18f, .68f, 1, .65f);
        fill.ShadowSize = 10;
        return fill;
    }

    public static StyleBoxFlat Plate(Color? fill = null, Color? border = null, int pad = 12, int radius = 12) => new()
    {
        BgColor = fill ?? Surface, BorderColor = border ?? Edge,
        BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, BorderWidthLeft = 1,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        ContentMarginLeft = pad, ContentMarginRight = pad, ContentMarginTop = pad, ContentMarginBottom = pad,
        ShadowColor = new Color(0, .01f, .04f, .3f), ShadowSize = 5,
        AntiAliasing = true, AntiAliasingSize = 1,
    };

    public static void DrawProgress(Control control, Rect2 rect, float progress, float inset = 3)
    {
        control.DrawStyleBox(ProgressTrack, rect);
        var fill = rect.Grow(-inset);
        fill.Size = new Vector2(fill.Size.X * Mathf.Clamp(progress, 0, 1), fill.Size.Y);
        if (fill.Size.X > 0) control.DrawStyleBox(ProgressFill, fill);
    }

    private static Theme? _theme;

    /// <summary>Korean-capable theme: Godot's bundled font has no Hangul, so prefer an OS font.</summary>
    public static Theme Get()
    {
        if (_theme != null) return _theme;
        var font = new SystemFont
        {
            FontNames = new[] { "Malgun Gothic", "Noto Sans CJK KR", "Noto Sans KR", "Apple SD Gothic Neo", "NanumGothic", "sans-serif" },
            FontWeight = 600,
        };
        _theme = new Theme { DefaultFont = font, DefaultFontSize = 18 };
        _theme.SetColor("font_color", "Label", Paper);
        _theme.SetColor("font_outline_color", "Label", Ink);
        _theme.SetConstant("outline_size", "Label", 2);
        _theme.SetStylebox("panel", "PanelContainer", Plate());
        _theme.SetStylebox("normal", "Button", Panel(Surface, 10));
        _theme.SetStylebox("hover", "Button", Panel(SelectedSurface, 10, new Color(Selection, .4f)));
        _theme.SetStylebox("pressed", "Button", Panel(SelectedSurface, 10, Selection));
        _theme.SetStylebox("focus", "Button", Panel(new Color(0, 0, 0, 0), 10, Selection));
        _theme.SetStylebox("hover_pressed", "Button", Panel(SelectedSurface, 10, Selection));
        _theme.SetStylebox("disabled", "Button", Panel(new Color(Surface, .5f), 10));
        _theme.SetColor("font_color", "Button", Paper);
        _theme.SetColor("font_hover_color", "Button", Selection);
        _theme.SetColor("font_pressed_color", "Button", Selection);
        _theme.SetColor("font_disabled_color", "Button", new Color(Muted, .45f));
        _theme.SetStylebox("panel", "PopupMenu", Plate());
        _theme.SetStylebox("panel", "TooltipPanel", Plate());
        _theme.SetStylebox("normal", "LineEdit", Panel(Surface, 10));
        _theme.SetStylebox("focus", "LineEdit", Panel(SelectedSurface, 10, Selection));
        _theme.SetColor("font_color", "LineEdit", Paper);
        return _theme;
    }

    public static StyleBoxFlat Panel(Color bg, int pad = 14, Color? border = null)
    {
        var box = Plate(bg, border, pad, 10);
        box.ContentMarginTop = box.ContentMarginBottom = pad * .7f;
        return box;
    }

    public static Label Label(string text = "", int size = 18, Color? color = null)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        if (color is { } c) label.AddThemeColorOverride("font_color", c);
        return label;
    }
}
