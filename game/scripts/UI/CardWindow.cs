using Godot;
using System.Collections.Generic;
using System.Linq;

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
    /// <summary>Set on a client: a pick is sent to the host instead of applied here.</summary>
    public System.Action<int>? PickHandler { get; set; }
    /// <summary>Rerolls this survivor has left (M-29 plan 16.4); null = no rerolls here (the lab).</summary>
    public System.Func<int>? RerollsLeft { get; set; }
    /// <summary>Draws card n of the front offer again (the host's count; a client asks the host).</summary>
    public System.Action<int>? RerollHandler { get; set; }

    private Control _root = null!, _dim = null!;
    private VBoxContainer _panel = null!;
    private Label _title = null!, _hint = null!, _plusCount = null!;
    private Button _close = null!;
    private PanelContainer _plus = null!;
    private readonly List<CardButton> _cards = new();
    private CardOffer? _shown;
    private string _shownKeys = "";
    private MysteryReveal _reveal = null!;

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
            var card = new CardButton(Pick, i, Reroll) { Name = "Card" + (i + 1) };
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

        _reveal = new MysteryReveal { Name = "MysteryReveal" };
        _root.AddChild(_reveal);

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
        if (!IsOpen || Queue.Front is not { } offer || index < 0 || index >= offer.Choices.Count) return;
        // A "?" card shows what it was (plan 16.3); the card itself is already decided and applies at once.
        if (offer.Choices[index].Inner is { } inner) _reveal.Play(inner);
        if (PickHandler != null) PickHandler(index);
        else Queue.Choose(Stage.Player, index);
    }

    /// <summary>Draws card <paramref name="index"/> again, when the offer allows it and a reroll is left.</summary>
    public void Reroll(int index)
    {
        if (!IsOpen || Queue.Front is not { CanReroll: true } offer || index < 0 || index >= offer.Choices.Count) return;
        if (RerollsLeft == null || RerollsLeft() <= 0 || RerollHandler == null) return;
        RerollHandler(index);
    }

    public override void _Process(double delta)
    {
        // The count left changes on the host's word, not only with the queue.
        if (IsOpen && Queue.Front is { } offer) RefreshRerolls(offer);
    }

    private void RefreshRerolls(CardOffer offer)
    {
        int left = RerollsLeft?.Invoke() ?? 0;
        bool can = RerollsLeft != null && offer.CanReroll;
        foreach (var card in _cards) card.SetReroll(can, left > 0);
        string baseHint = Loc.Text(offer.CanClose ? "ui_card_hint_closable" : "ui_card_hint_arming");
        _hint.Text = can ? baseHint + "  ·  " + Loc.Format("fmt_card_rerolls", left) : baseHint;
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
        _title.AddThemeColorOverride("font_color", offer.Grade > 0 ? CardCatalog.GradeColor(offer.Grade) : Hud.HudWarm);
        RefreshRerolls(offer);
        // A reroll changes the cards of the same offer.
        string keys = string.Join(",", offer.Choices.Select(c => c.Key));
        if (_shown == offer && _shownKeys == keys) return;
        _shown = offer;
        _shownKeys = keys;
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
            case Key.Key1 or Key.Key2 or Key.Key3 when IsOpen && key.ShiftPressed:
                Reroll((int)(key.Keycode - Key.Key1));
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
    private readonly System.Action<int> _pick;
    private readonly int _index;
    private readonly Button _reroll;
    private readonly Label _number, _name, _tag, _desc, _glyph;
    private readonly TextureRect _icon;
    private StyleBoxFlat _normal = null!, _hover = null!;

    /// <param name="pick">What clicking it does (the card window's pick, or the vote's stamp).</param>
    /// <param name="reroll">What its ↻ button does (null = none: the vote).</param>
    public CardButton(System.Action<int> pick, int index, System.Action<int>? reroll = null)
    {
        _pick = pick;
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
        _reroll = new Button { Name = "Reroll", Text = "↻", TooltipText = Loc.Text("ui_card_reroll"), CustomMinimumSize = new Vector2(44, 44),
            FocusMode = FocusModeEnum.None, Visible = false, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        _reroll.Pressed += () => reroll?.Invoke(_index);
        row.AddChild(_reroll);
        MouseEntered += () => AddThemeStyleboxOverride("panel", _hover);
        MouseExited += () => AddThemeStyleboxOverride("panel", _normal);
    }

    public void Show(CardChoice? choice, CardKind kind)
    {
        Visible = choice != null;
        if (choice == null) return;
        var color = CardWindow.TargetColor(choice.Target);
        // The grade shows in the border (M-29 plan 16.2); arming and companion cards keep their target's colour.
        var border = choice.Grade > 0 ? CardCatalog.GradeColor(choice.Grade) : kind == CardKind.Special ? new Color("ffcf5a") : color;
        _normal = Hud.HudPlate(new Color(.07f, .08f, .12f, .96f), new Color(border, .7f), 14, 14);
        _hover = Hud.HudPlate(new Color(.13f, .14f, .2f, .98f), border, 14, 14);
        _hover.SetBorderWidthAll(3);
        AddThemeStyleboxOverride("panel", _normal);
        _name.Text = choice.Name;
        _name.AddThemeColorOverride("font_color", color);
        _tag.Text = choice.Grade > 0 ? Loc.Format("fmt_card_grade", choice.Grade) + " · " + choice.Tag : choice.Tag;
        _tag.AddThemeColorOverride("font_color", choice.Grade > 0 ? CardCatalog.GradeColor(choice.Grade) : Hud.HudWarm);
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
        if (choice.Inner != null) _glyph.Text = "?";
        _glyph.AddThemeColorOverride("font_color", color);
    }

    /// <summary>Shows the ↻ button when this offer can be rerolled, greyed out when none are left.</summary>
    public void SetReroll(bool shown, bool enabled)
    {
        _reroll.Visible = shown;
        _reroll.Disabled = !enabled;
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            AcceptEvent();
            _pick(_index);
        }
    }
}

/// <summary>
/// A "?" card turning into its card (M-29 plan 16.3): names of its grade band flick past like a slot machine for
/// about 1.3 seconds and stop on the card that was decided when it was drawn, which stays big for a moment. Only a
/// show: the card applies at once. Used by the card window and the vote.
/// </summary>
public partial class MysteryReveal : PanelContainer
{
    public const float SpinSeconds = 1.3f, HoldSeconds = 1.1f;
    private Label _name = null!, _desc = null!;
    private CardChoice? _inner;
    private List<string> _names = new();
    private float _time = -1;

    /// <summary>Whether it is on screen (checks).</summary>
    public bool Playing => _time >= 0;
    public string Shown => _name.Text;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(560, 160);
        // Centred on the screen whatever the parent is (a preset alone left it in the corner).
        AnchorLeft = AnchorRight = AnchorTop = AnchorBottom = .5f;
        OffsetLeft = -280;
        OffsetRight = 280;
        OffsetTop = -80;
        OffsetBottom = 80;
        GrowHorizontal = GrowVertical = GrowDirection.Both;
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        AddChild(box);
        _name = Hud.HudLabel("", 40, Hud.HudWhite);
        _name.HorizontalAlignment = HorizontalAlignment.Center;
        _desc = Hud.HudLabel("", 16, Hud.HudMuted);
        _desc.HorizontalAlignment = HorizontalAlignment.Center;
        _desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(_name);
        box.AddChild(_desc);
    }

    public void Play(CardChoice inner)
    {
        _inner = inner;
        _names = CardCatalog.RevealNames(inner.Grade >= CardCatalog.SpecialLow).Where(n => n != inner.Name).ToList();
        if (_names.Count == 0) _names.Add("?");
        _time = 0;
        Visible = true;
        AddThemeStyleboxOverride("panel", Hud.HudPlate(new Color(.06f, .07f, .1f, .97f), CardCatalog.GradeColor(inner.Grade), 18, 16));
    }

    public override void _Process(double delta)
    {
        if (_time < 0 || _inner == null) return;
        _time += (float)delta;
        if (_time < SpinSeconds)
        {
            // Fast at first, slowing down before it stops.
            int step = Mathf.FloorToInt(_time * 16f - _time * _time * 5f);
            _name.Text = _names[((step % _names.Count) + _names.Count) % _names.Count];
            _name.AddThemeColorOverride("font_color", Hud.HudMuted);
            _desc.Text = "";
            return;
        }
        _name.Text = _inner.Name;
        _name.AddThemeColorOverride("font_color", CardCatalog.GradeColor(_inner.Grade));
        _desc.Text = _inner.Description;
        if (_time < SpinSeconds + HoldSeconds) return;
        _time = -1;
        Visible = false;
    }
}
