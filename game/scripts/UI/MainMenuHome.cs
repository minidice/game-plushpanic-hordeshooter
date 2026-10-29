using Godot;

namespace HordeShooter;

public partial class MainMenu
{
    private Control _detailLayout = null!, _homeShell = null!, _previewContainer = null!;
    private VBoxContainer _previewColumn = null!;
    private Node3D _homeRoom = null!;
    private MeshInstance3D _previewPlatform = null!;

    private void BuildHomeShell()
    {
        _homeShell = new Control { Name = "HomeShell" };
        _homeShell.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_homeShell);

        // A soft dark scrim keeps the menu readable without enclosing it in a card.
        var shade = new TextureRect
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Texture = new GradientTexture2D
            {
                Width = 512, Height = 2,
                FillFrom = Vector2.Zero, FillTo = Vector2.Right,
                Gradient = new Gradient
                {
                    Offsets = new[] { 0f, .38f, .7f, 1f },
                    Colors = new[] { new Color(.025f, .035f, .04f, .98f), new Color(.025f, .035f, .04f, .87f), new Color(.025f, .035f, .04f, .12f), Colors.Transparent }
                }
            }
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _homeShell.AddChild(shade);

        var navigation = new VBoxContainer
        {
            Name = "HomeNavigation", AnchorLeft = .085f, AnchorRight = .38f,
            AnchorTop = .17f, AnchorBottom = .87f
        };
        navigation.AddThemeConstantOverride("separation", 0);
        _homeShell.AddChild(navigation);
        var title = UiTheme.Label(Loc.Text("ui_menu_title").Replace(" FUR", "\nFUR"), 52, UiTheme.Paper);
        title.AddThemeConstantOverride("outline_size", 0);
        title.AddThemeConstantOverride("line_spacing", -4);
        navigation.AddChild(title);
        navigation.AddChild(new Control { CustomMinimumSize = new Vector2(0, 76) });
        AddPage(navigation, MenuPage.Home, BuildHome());

        var play = new VBoxContainer();
        foreach (var (name, key, target) in new[]
        {
            ("Menu_Single", "menu_home_single", MenuPage.Single),
            ("Menu_Multi", "menu_home_multi", MenuPage.Multi),
            ("Back_Play", "menu_back", MenuPage.Home)
        })
        {
            var button = HomeButton(name, Loc.Text(key));
            button.Pressed += () => Open(target);
            play.AddChild(button);
        }
        AddPage(navigation, MenuPage.Play, play);
    }

    private static Button HomeButton(string name, string text)
    {
        var button = new Button
        {
            Name = name, Text = text, Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new Vector2(0, 54), MouseDefaultCursorShape = CursorShape.PointingHand
        };
        button.AddThemeFontSizeOverride("font_size", 26);
        button.AddThemeConstantOverride("outline_size", 0);
        button.AddThemeColorOverride("font_color", UiTheme.Muted);
        button.AddThemeColorOverride("font_hover_color", UiTheme.Paper);
        button.AddThemeColorOverride("font_focus_color", UiTheme.Paper);
        button.AddThemeColorOverride("font_pressed_color", UiTheme.Selection);
        var empty = new StyleBoxEmpty { ContentMarginLeft = 18, ContentMarginRight = 8, ContentMarginTop = 8, ContentMarginBottom = 8 };
        button.AddThemeStyleboxOverride("normal", empty);
        button.AddThemeStyleboxOverride("hover", empty);
        button.AddThemeStyleboxOverride("pressed", empty);
        button.AddThemeStyleboxOverride("focus", new StyleBoxFlat
        {
            BgColor = Colors.Transparent, BorderColor = UiTheme.Paper, BorderWidthLeft = 2,
            ContentMarginLeft = 18, ContentMarginRight = 8, ContentMarginTop = 8, ContentMarginBottom = 8
        });
        button.MouseEntered += button.GrabFocus;
        return button;
    }

    private void ShowHomeShell(MenuPage page)
    {
        bool home = page is MenuPage.Home or MenuPage.Play;
        _homeShell.Visible = home;
        _detailLayout.Visible = !home;
        _homeRoom.Visible = home;
        _previewPlatform.Visible = !home;
        Node parent = home ? _homeShell : _previewColumn;
        if (_previewContainer.GetParent() != parent)
        {
            _previewContainer.Reparent(parent, false);
            parent.MoveChild(_previewContainer, 0);
        }
        _previewContainer.MouseFilter = MouseFilterEnum.Ignore;
        _previewContainer.CustomMinimumSize = home ? Vector2.Zero : new Vector2(520, 320);
        _previewContainer.SetAnchorsAndOffsetsPreset(home ? LayoutPreset.FullRect : LayoutPreset.TopLeft);
        _turntable.Rotation = home ? new Vector3(0, -.28f, 0) : new Vector3(0, Mathf.DegToRad((float)_rotation.Value), 0);
        UpdatePreviewCamera();
    }

    private static Node3D BuildHomeRoom()
    {
        var room = new Node3D { Name = "HomeRoom", Visible = false };
        // A quiet safe-house corner, built with the game's existing geometry/material helpers.
        var concrete = Mat.Solid("343d3e");
        var wall = Mat.Solid("414a49");
        var metal = Mat.Solid("202b2d");
        Mat.Box(room, new Vector3(0, -.12f, 0), new Vector3(40, .24f, 30), concrete);
        Mat.Box(room, new Vector3(0, 2.6f, -3.4f), new Vector3(32, 5.2f, .25f), wall);
        Mat.Box(room, new Vector3(0, .3f, -3.23f), new Vector3(32, .6f, .08f), metal);
        // Recessed storage door and a bench provide scale without competing with the character.
        Mat.Box(room, new Vector3(2.8f, 1.6f, -3.15f), new Vector3(1.75f, 3.2f, .18f), metal);
        Mat.Box(room, new Vector3(2.8f, 1.6f, -3.04f), new Vector3(1.5f, 2.95f, .08f), Mat.Solid("374342"));
        Mat.Box(room, new Vector3(2.25f, 1.5f, -2.96f), new Vector3(.07f, .32f, .08f), Mat.Solid("8a9997"));
        Mat.Box(room, new Vector3(-1.7f, .63f, -2), new Vector3(2.2f, .16f, .65f), Mat.Solid("515d59"));
        foreach (float x in new[] { -2.55f, -.85f })
            Mat.Box(room, new Vector3(x, .28f, -2), new Vector3(.09f, .56f, .5f), metal);
        Mat.Box(room, new Vector3(2.8f, 3.4f, -2.95f), new Vector3(1.25f, .07f, .12f), Mat.Glow(new Color("d1ddd8"), 1));
        room.AddChild(new OmniLight3D { Position = new Vector3(1.7f, 3, -.8f), LightColor = new Color("d4e0dc"), LightEnergy = .8f, OmniRange = 6 });
        return room;
    }
}
