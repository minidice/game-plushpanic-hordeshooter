using Godot;

namespace HordeShooter;

public static class UiTheme
{
    public static readonly Color Ink = new("0d1114");
    public static readonly Color Paper = new("efe6cc");
    public static readonly Color Accent = new("ffcf5c");
    public static readonly Color Muted = new("9aa39a");
    public static readonly Color Danger = new("ff5b4f");
    public static readonly Color Heal = new("7dff9a");
    public static readonly Color Helium = new("ff9ccc");

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
        _theme.SetConstant("outline_size", "Label", 6);
        _theme.SetStylebox("panel", "PanelContainer", Panel(new Color(.05f, .07f, .08f, .82f)));
        _theme.SetStylebox("normal", "Button", Panel(new Color("2c3530"), 10));
        _theme.SetStylebox("hover", "Button", Panel(new Color("435047"), 10));
        _theme.SetStylebox("pressed", "Button", Panel(new Color("5c6b55"), 10));
        _theme.SetStylebox("focus", "Button", Panel(new Color(0, 0, 0, 0), 10, Accent));
        _theme.SetColor("font_color", "Button", Paper);
        return _theme;
    }

    public static StyleBoxFlat Panel(Color bg, int pad = 14, Color? border = null)
    {
        var box = new StyleBoxFlat
        {
            BgColor = bg,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = pad,
            ContentMarginRight = pad,
            ContentMarginTop = pad * .7f,
            ContentMarginBottom = pad * .7f,
        };
        if (border is { } b)
        {
            box.BorderColor = b;
            box.BorderWidthBottom = box.BorderWidthTop = box.BorderWidthLeft = box.BorderWidthRight = 2;
        }
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
