using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>
/// The card pick window (decisions.md M-27, plan 7장), shaped like Hades' boon choice: the front offer's three cards
/// stacked in the middle, picked by click or 1 · 2 · 3. The world keeps running; the survivor can still walk and leap
/// but not shoot, aim, throw or switch weapons. Level-up and boss offers can be put off with ✕ (or F): they wait on the
/// "+" badge bottom right with their count and come back in the same order with the same cards. Arming offers cannot
/// be closed.
/// </summary>
public partial class CardWindow : CanvasLayer
{
    public Stage Stage { get; set; } = null!;
    public CardQueue Queue { get; set; } = null!;
    public bool IsOpen { get; private set; }

    private Control _root = null!, _dim = null!;
    private VBoxContainer _panel = null!;
    private Label _title = null!, _hint = null!, _plusCount = null!;
    private Button _close = null!;
    private PanelContainer _plus = null!;
    private readonly List<CardButton> _cards = new();
    private CardOffer? _shown;

    public override void _Ready()
    {
        Layer = 12;
        _root = new Control { Name = "CardRoot", Theme = UiTheme.Get(), MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        _dim = new ColorRect { Name = "CardDim", Color = new Color(0, 0, 0, .55f), MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        _dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_dim);

        _panel = new VBoxContainer { Name = "CardPanel", Visible = false, CustomMinimumSize = new Vector2(700, 0) };
        _panel.AddThemeConstantOverride("separation", 12);
        _panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _panel.GrowHorizontal = _panel.GrowVertical = Control.GrowDirection.Both;
        _root.AddChild(_panel);

        var head = new HBoxContainer();
        _panel.AddChild(head);
        _title = Hud.HudLabel("", 28, Hud.HudWarm);
        _title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(_title);
        _close = new Button { Name = "CardClose", Text = "✕", CustomMinimumSize = new Vector2(44, 44), FocusMode = Control.FocusModeEnum.None };
        _close.Pressed += Close;
        head.AddChild(_close);
        for (int i = 0; i < 3; i++)
        {
            var card = new CardButton(this, i) { Name = "Card" + (i + 1) };
            _cards.Add(card);
            _panel.AddChild(card);
        }
        _hint = Hud.HudLabel("", 13, Hud.HudMuted);
        _hint.HorizontalAlignment = HorizontalAlignment.Center;
        _panel.AddChild(_hint);

        // The waiting-cards badge, bottom right above the ammo panel.
        _plus = new PanelContainer { Name = "CardPlus", Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _plus.AnchorLeft = _plus.AnchorRight = 1;
        _plus.AnchorTop = _plus.AnchorBottom = 1;
        _plus.OffsetLeft = _plus.OffsetRight = -296;
        _plus.OffsetTop = _plus.OffsetBottom = -138;
        _plus.GrowHorizontal = Control.GrowDirection.Begin;
        _plus.GrowVertical = Control.GrowDirection.Begin;
        _plus.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Open(); };
        _root.AddChild(_plus);
        var plusRow = new HBoxContainer();
        plusRow.AddThemeConstantOverride("separation", 8);
        _plus.AddChild(plusRow);
        plusRow.AddChild(Hud.HudLabel("+", 30, Hud.HudWarm));
        _plusCount = Hud.HudLabel("", 20, Hud.HudWhite);
        plusRow.AddChild(_plusCount);
        plusRow.AddChild(Hud.HudLabel(Loc.Text("ui_card_plus_key"), 13, Hud.HudMuted));

        Queue.Changed += OnQueueChanged;
        OnQueueChanged();
    }

    public override void _ExitTree() => Queue.Changed -= OnQueueChanged;

    /// <summary>A new offer opens the window by itself when nothing was waiting; put-off ones only grow the badge.</summary>
    private int _lastCount;
    private void OnQueueChanged()
    {
        bool added = Queue.Count > _lastCount;
        bool firstOne = _lastCount == 0;
        _lastCount = Queue.Count;
        if (Queue.Front == null) { SetOpen(false); return; }
        if (IsOpen || (added && firstOne) || !Queue.Front.CanClose) SetOpen(true);
        else Refresh();
    }

    public void Open()
    {
        if (Queue.Front != null) SetOpen(true);
    }

    public void Close()
    {
        if (Queue.Front is { CanClose: false }) return;
        SetOpen(false);
    }

    public void Pick(int index)
    {
        if (!IsOpen) return;
        Queue.Choose(Stage.Player, index);
    }

    private void SetOpen(bool open)
    {
        IsOpen = open && Queue.Front != null;
        if (Stage.Hud != null) Stage.Hud.CardWindowOpen = IsOpen;
        Refresh();
    }

    private void Refresh()
    {
        var offer = Queue.Front;
        _dim.Visible = _panel.Visible = IsOpen;
        _plus.Visible = !IsOpen && Queue.Count > 0;
        _plusCount.Text = Queue.Count > 1 ? $"×{Queue.Count}" : "";
        _plus.AddThemeStyleboxOverride("panel", Hud.HudPlate(border: offer?.Kind == CardKind.Special ? new Color("ffcf5a") : Hud.HudWarm, pad: 10));
        if (offer == null || !IsOpen) return;
        _close.Visible = offer.CanClose;
        _title.Text = offer.Title;
        _hint.Text = Loc.Text(offer.CanClose ? "ui_card_hint_closable" : "ui_card_hint_arming");
        if (_shown == offer) return;
        _shown = offer;
        for (int i = 0; i < _cards.Count; i++)
            _cards[i].Show(i < offer.Choices.Count ? offer.Choices[i] : null, offer.Kind);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } key) return;
        switch (key.Keycode)
        {
            case Key.F:
                if (IsOpen) Close();
                else Open();
                break;
            case Key.Key1 or Key.Key2 or Key.Key3 when IsOpen:
                Pick((int)(key.Keycode - Key.Key1));
                break;
            default: return;
        }
        GetViewport().SetInputAsHandled();
    }

    /// <summary>Colour of a card's target, so "slot 2" or "me" reads at a glance.</summary>
    public static Color TargetColor(CardTarget target) => target switch
    {
        CardTarget.Slot1 => new Color("7fb8ff"),
        CardTarget.Slot2 => new Color("ffb347"),
        CardTarget.Slot3 => new Color("ff7a8a"),
        CardTarget.Self => new Color("7dff9a"),
        CardTarget.Summon => new Color("6be7ff"),
        _ => new Color("ffe066"),
    };
}

/// <summary>One wide card: number, big icon, name in the target's colour, a tag line and the description.</summary>
public partial class CardButton : PanelContainer
{
    private readonly CardWindow _window;
    private readonly int _index;
    private readonly Label _number, _name, _tag, _desc, _glyph;
    private readonly TextureRect _icon;
    private StyleBoxFlat _normal = null!, _hover = null!;

    public CardButton(CardWindow window, int index)
    {
        _window = window;
        _index = index;
        CustomMinimumSize = new Vector2(700, 118);
        MouseFilter = MouseFilterEnum.Stop;
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 16);
        AddChild(row);
        _number = Hud.HudLabel((index + 1).ToString(), 22, Hud.HudMuted);
        _number.CustomMinimumSize = new Vector2(22, 0);
        _number.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(_number);
        var iconBox = new PanelContainer { CustomMinimumSize = new Vector2(150, 92), MouseFilter = MouseFilterEnum.Ignore };
        iconBox.AddThemeStyleboxOverride("panel", Hud.HudPlate(new Color(.05f, .06f, .09f, .9f), new Color(.35f, .4f, .5f, .5f), 6, 8));
        row.AddChild(iconBox);
        _icon = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
        iconBox.AddChild(_icon);
        _glyph = Hud.HudLabel("", 40, Hud.HudWarm);
        _glyph.HorizontalAlignment = HorizontalAlignment.Center;
        _glyph.VerticalAlignment = VerticalAlignment.Center;
        iconBox.AddChild(_glyph);
        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        text.AddThemeConstantOverride("separation", 2);
        row.AddChild(text);
        _name = Hud.HudLabel("", 24, Hud.HudWhite);
        _tag = Hud.HudLabel("", 13, Hud.HudWarm);
        _desc = Hud.HudLabel("", 15, Hud.HudWhite);
        _desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _desc.CustomMinimumSize = new Vector2(440, 0);
        foreach (var l in new[] { _name, _tag, _desc }) { l.MouseFilter = MouseFilterEnum.Ignore; text.AddChild(l); }
        MouseEntered += () => AddThemeStyleboxOverride("panel", _hover);
        MouseExited += () => AddThemeStyleboxOverride("panel", _normal);
    }

    public void Show(CardChoice? choice, CardKind kind)
    {
        Visible = choice != null;
        if (choice == null) return;
        var color = CardWindow.TargetColor(choice.Target);
        var border = kind == CardKind.Special ? new Color("ffcf5a") : color;
        _normal = Hud.HudPlate(new Color(.07f, .08f, .12f, .96f), new Color(border, .7f), 14, 14);
        _hover = Hud.HudPlate(new Color(.13f, .14f, .2f, .98f), border, 14, 14);
        _hover.SetBorderWidthAll(3);
        AddThemeStyleboxOverride("panel", _normal);
        _name.Text = choice.Name;
        _name.AddThemeColorOverride("font_color", color);
        _tag.Text = choice.Tag;
        _desc.Text = choice.Description;
        _icon.Texture = choice.Icon;
        _glyph.Text = choice.Icon != null ? "" : choice.Target switch
        {
            CardTarget.Slot1 => "1",
            CardTarget.Slot2 => "2",
            CardTarget.Slot3 => "3",
            CardTarget.Self => "♥",
            CardTarget.Summon => "◆",
            _ => "✦",
        };
        _glyph.AddThemeColorOverride("font_color", color);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            AcceptEvent();
            _window.Pick(_index);
        }
    }
}
