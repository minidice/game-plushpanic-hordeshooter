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

    /// <summary>Body text is regular; actions and titles have explicit, locale-aware font roles.</summary>
    public static Theme Get()
    {
        if (_theme != null) return _theme;
        _theme = new Theme { DefaultFont = UiFonts.Get(), DefaultFontSize = 18 };
        foreach (string type in new[] { "Button", "OptionButton", "CheckButton", "CheckBox", "TabContainer", "TabBar", "PopupMenu" })
            _theme.SetFont("font", type, UiFonts.Get(UiFontRole.Menu));
        _theme.SetTypeVariation("TitleLabel", "Label");
        _theme.SetFont("font", "TitleLabel", UiFonts.Get(UiFontRole.Title));
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
        _theme.SetStylebox("panel", "PopupMenu", Plate(new Color(Surface, .98f)));
        _theme.SetStylebox("panel", "TooltipPanel", Plate(new Color(Surface, .98f)));
        _theme.SetStylebox("normal", "LineEdit", Panel(Surface, 10));
        _theme.SetStylebox("focus", "LineEdit", Panel(SelectedSurface, 10, Selection));
        _theme.SetColor("font_color", "LineEdit", Paper);
        StyleMenuControls(_theme);
        return _theme;
    }

    // Native controls share the same state palette instead of falling back to the engine theme.
    private static void StyleMenuControls(Theme theme)
    {
        foreach (string type in new[] { "Button", "OptionButton", "CheckButton", "CheckBox" })
        {
            if (type != "Button")
                foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" })
                    theme.SetStylebox(state, type, theme.GetStylebox(state, "Button"));
            theme.SetColor("font_color", type, Paper);
            foreach (string state in new[] { "hover", "pressed", "hover_pressed", "focus" })
                theme.SetColor("font_" + state + "_color", type, Selection);
            theme.SetColor("font_disabled_color", type, new Color(Muted, .45f));
            theme.SetColor("icon_normal_color", type, Muted);
            theme.SetColor("icon_hover_color", type, Selection);
            theme.SetColor("icon_pressed_color", type, Selection);
        }
        theme.SetIcon("arrow", "OptionButton", MenuIcon(16, 16, "<path d='M4 6 L8 10 L12 6' fill='none' stroke='#b8e5ff' stroke-width='1.6' stroke-linecap='round' stroke-linejoin='round'/>"));
        foreach (string type in new[] { "TabContainer", "TabBar" })
        {
            theme.SetStylebox("tab_selected", type, Panel(SelectedSurface, 12, new Color(Selection, .5f)));
            theme.SetStylebox("tab_unselected", type, Panel(Surface, 12));
            theme.SetStylebox("tab_hovered", type, Panel(SelectedSurface, 12));
            theme.SetStylebox("tab_disabled", type, Panel(Surface, 12));
            theme.SetStylebox("tab_focus", type, Panel(Colors.Transparent, 12, Selection));
            theme.SetColor("font_selected_color", type, Selection);
            theme.SetColor("font_unselected_color", type, Muted);
            theme.SetColor("font_hovered_color", type, Paper);
            theme.SetColor("font_disabled_color", type, new Color(Muted, .45f));
        }
        theme.SetStylebox("panel", "TabContainer", Plate(Surface, Edge, 8));
        theme.SetStylebox("hover", "PopupMenu", Panel(SelectedSurface, 8, new Color(Selection, .25f)));
        theme.SetColor("font_color", "PopupMenu", Paper);
        theme.SetColor("font_hover_color", "PopupMenu", Selection);
        theme.SetColor("font_disabled_color", "PopupMenu", new Color(Muted, .45f));
        theme.SetColor("selection_color", "LineEdit", SelectedSurface);
        theme.SetColor("caret_color", "LineEdit", Selection);
        theme.SetColor("font_selected_color", "LineEdit", Paper);
        var handle = MenuIcon(18, 18, "<circle cx='9' cy='9' r='7' fill='#203b52' stroke='#b8e5ff' stroke-width='1.4'/><circle cx='9' cy='9' r='3' fill='#f3f4f1'/>" );
        var activeHandle = MenuIcon(18, 18, "<circle cx='9' cy='9' r='8' fill='#b8e5ff'/><circle cx='9' cy='9' r='3' fill='#f3f4f1'/>" );
        foreach (string type in new[] { "HSlider", "VSlider" })
        {
            theme.SetStylebox("slider", type, Plate(Glass, Edge, 2, 4));
            theme.SetStylebox("grabber_area", type, Plate(SightTint, Colors.Transparent, 2, 4));
            theme.SetStylebox("grabber_area_highlight", type, Plate(Selection, Colors.Transparent, 2, 4));
            theme.SetIcon("grabber", type, handle);
            theme.SetIcon("grabber_highlight", type, activeHandle);
            theme.SetIcon("grabber_disabled", type, handle);
        }
        foreach (string type in new[] { "HScrollBar", "VScrollBar" })
        {
            theme.SetStylebox("scroll", type, Plate(Glass, Colors.Transparent, 3, 4));
            theme.SetStylebox("grabber", type, Plate(SightTint, Colors.Transparent, 3, 4));
            theme.SetStylebox("grabber_highlight", type, Plate(Selection, Colors.Transparent, 3, 4));
            theme.SetStylebox("grabber_pressed", type, Plate(Selection, Colors.Transparent, 3, 4));
        }
        foreach (bool on in new[] { false, true })
        {
            string state = on ? "checked" : "unchecked";
            var icon = MenuIcon(36, 20, $"<rect x='1' y='1' width='34' height='18' rx='9' fill='{(on ? "#31536c" : "#101b29")}' stroke='{(on ? "#b8e5ff" : "#61738a")}'/><circle cx='{(on ? 26 : 10)}' cy='10' r='6' fill='{(on ? "#e8f6ff" : "#a8b6c9")}'/>");
            theme.SetIcon(state, "CheckButton", icon);
            theme.SetIcon(state + "_disabled", "CheckButton", icon);
            theme.SetIcon(state + "_mirrored", "CheckButton", icon);
        }
    }

    private static Texture2D MenuIcon(int width, int height, string shapes)
    {
        using var image = new Image();
        image.LoadSvgFromString($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' viewBox='0 0 {width} {height}'>{shapes}</svg>", 2);
        var texture = ImageTexture.CreateFromImage(image);
        // Render at logical size while retaining a sharper source on a scaled window.
        texture.SetSizeOverride(new Vector2I(width, height));
        return texture;
    }

    public static StyleBoxFlat Panel(Color bg, int pad = 14, Color? border = null)
    {
        var box = Plate(bg, border, pad, 10);
        box.ContentMarginTop = box.ContentMarginBottom = pad * .7f;
        return box;
    }

    public static Label Label(string text = "", int size = 18, Color? color = null, bool title = false)
    {
        var label = new Label { Text = text, Theme = Get(), MouseFilter = Control.MouseFilterEnum.Ignore };
        if (title) label.ThemeTypeVariation = "TitleLabel";
        label.AddThemeFontSizeOverride("font_size", size);
        if (color is { } c) label.AddThemeColorOverride("font_color", c);
        return label;
    }
}
