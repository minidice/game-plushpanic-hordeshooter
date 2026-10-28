using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// How a run is set up from the menu (decisions.md M-39): the mode, the card under it — a scenario's campaign, or the
/// enemy family of the survival and onslaught maps — and the difficulty (M-40). The single-player page and the room
/// share it; in a room the host's is sent to everyone (<see cref="NetLobby"/>).
/// </summary>
public readonly record struct PlaySetup(GameMode Mode, string Choice, Difficulty Difficulty)
{
    public static string[] ChoicesOf(GameMode mode) => mode == GameMode.Scenario ? GameSession.Campaigns : GameSession.Themes;
    public PlaySetup Valid() => this with { Choice = ChoicesOf(Mode).Contains(Choice) ? Choice : ChoicesOf(Mode)[0] };
    public static PlaySetup FromSession() =>
        new PlaySetup(GameSession.Mode, GameSession.Mode == GameMode.Scenario ? GameSession.Campaign : GameSession.SelectedTheme, GameSession.Difficulty).Valid();

    public void Apply()
    {
        GameSession.Mode = Mode;
        if (Mode == GameMode.Scenario) GameSession.Campaign = Choice;
        else GameSession.SelectedTheme = Choice;
        GameSession.Difficulty = Difficulty;
    }

    /// <summary>Language key of a card's name (add "_desc" for its line under it).</summary>
    public static string CardKey(GameMode mode, string choice) => (mode == GameMode.Scenario ? "play_campaign_" : "play_theme_") + choice;
    /// <summary>A card's picture (Codex, doc/model-briefs/play-setup-cards.md).</summary>
    public static string ArtPath(GameMode mode, string choice) => $"res://assets/ui/play-setup/play_{mode.ToString().ToLowerInvariant()}_{choice}.png";
    public static string DifficultyKey(Difficulty difficulty) => "difficulty_" + difficulty.ToString().ToLowerInvariant();
}

/// <summary>
/// The menu's "game setup" (M-39): mode tabs, the cards of that mode (the picked one large with its line, the others in a
/// row), the difficulty with what it allows, all in the menu's own rounded style (M-38).
/// </summary>
public partial class PlaySetupPanel : VBoxContainer
{
    public event Action<PlaySetup>? Changed;
    public PlaySetup Setup { get; private set; }
    public bool Editable { get; private set; } = true;

    private readonly Dictionary<GameMode, Button> _modes = new();
    private readonly Dictionary<Difficulty, Button> _levels = new();
    private HBoxContainer _cards = null!;
    private TextureRect _art = null!;
    private Label _cardName = null!, _cardDesc = null!, _rule = null!;
    private string _prefix = "";
    private GameMode? _cardsFor;
    // The card last picked under each kind, so going back to a mode finds it again.
    private string _campaign = GameSession.Campaigns[0], _theme = GameSession.Themes[0];

    /// <summary>Builds the panel; <paramref name="prefix"/> keeps node names apart when the menu has two ("Play_", "Lobby_").</summary>
    public void Build(PlaySetup setup, string prefix)
    {
        _prefix = prefix;
        AddThemeConstantOverride("separation", 6);
        AddChild(Section("menu_mode_label"));
        var modes = new HBoxContainer();
        var modeGroup = new ButtonGroup();
        foreach (var mode in Enum.GetValues<GameMode>())
        {
            string id = mode.ToString().ToLowerInvariant();
            var button = new Button
            {
                Name = prefix + "Mode_" + id, ToggleMode = true, ButtonGroup = modeGroup, SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 42), Text = Loc.Text("mode_" + id), TooltipText = Loc.Text("mode_" + id + "_desc"),
            };
            button.Pressed += () => Pick(Setup with { Mode = mode, Choice = mode == GameMode.Scenario ? _campaign : _theme });
            modes.AddChild(button);
            _modes[mode] = button;
        }
        AddChild(modes);

        // The picked card large with its name and line, the mode's cards in a row under it.
        var detail = new HBoxContainer();
        detail.AddThemeConstantOverride("separation", 12);
        var frame = new PanelContainer { Name = prefix + "Art", ClipChildren = ClipChildrenMode.AndDraw, CustomMinimumSize = new Vector2(232, 145) };
        frame.AddThemeStyleboxOverride("panel", UiTheme.Panel(UiTheme.Ink, 12, new Color(UiTheme.Selection, .35f)));
        _art = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered };
        frame.AddChild(_art);
        detail.AddChild(frame);
        var words = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _cardName = UiTheme.Label("", 21, UiTheme.Selection);
        _cardName.Name = prefix + "CardName";
        words.AddChild(_cardName);
        _cardDesc = UiTheme.Label("", 14, UiTheme.Muted);
        _cardDesc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        words.AddChild(_cardDesc);
        detail.AddChild(words);
        AddChild(detail);
        _cards = new HBoxContainer { Name = prefix + "Cards" };
        _cards.AddThemeConstantOverride("separation", 8);
        AddChild(_cards);

        AddChild(Section("difficulty_label"));
        var levels = new HBoxContainer();
        var levelGroup = new ButtonGroup();
        foreach (var level in Enum.GetValues<Difficulty>())
        {
            var button = new Button
            {
                Name = prefix + "Difficulty_" + level.ToString().ToLowerInvariant(), ToggleMode = true, ButtonGroup = levelGroup,
                SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 38), Text = Loc.Text(PlaySetup.DifficultyKey(level)),
            };
            button.Pressed += () => Pick(Setup with { Difficulty = level });
            levels.AddChild(button);
            _levels[level] = button;
        }
        AddChild(levels);
        _rule = UiTheme.Label("", 14, UiTheme.Muted);
        _rule.Name = prefix + "DifficultyRule";
        _rule.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        AddChild(_rule);
        Show(setup, true);
    }

    /// <summary>Shows <paramref name="setup"/>; a room's guests see the host's and cannot change it.</summary>
    public void Show(PlaySetup setup, bool editable)
    {
        setup = setup.Valid();
        Setup = setup;
        Editable = editable;
        if (setup.Mode == GameMode.Scenario) _campaign = setup.Choice;
        else _theme = setup.Choice;
        foreach (var (mode, button) in _modes)
        {
            button.SetPressedNoSignal(mode == setup.Mode);
            button.Disabled = !editable || !GameSession.PlayableModes.Contains(mode);
        }
        if (_cardsFor != setup.Mode) BuildCards(setup.Mode);
        foreach (var child in _cards.GetChildren().OfType<Button>())
        {
            child.SetPressedNoSignal(child.Name == CardName(setup.Choice));
            child.Disabled = !editable;
        }
        _art.Texture = Art(setup.Mode, setup.Choice);
        _cardName.Text = Loc.Text(PlaySetup.CardKey(setup.Mode, setup.Choice));
        _cardDesc.Text = Loc.Text(PlaySetup.CardKey(setup.Mode, setup.Choice) + "_desc");
        foreach (var (level, button) in _levels)
        {
            button.SetPressedNoSignal(level == setup.Difficulty);
            button.Disabled = !editable;
        }
        var rules = SquadDatabase.Current.Rules;
        _rule.Text = Loc.Format("fmt_difficulty_rule", rules.Downs(setup.Difficulty), rules.Respawns(setup.Difficulty));
    }

    private string CardName(string choice) => _prefix + "Card_" + choice;

    private void BuildCards(GameMode mode)
    {
        _cardsFor = mode;
        foreach (var child in _cards.GetChildren()) { _cards.RemoveChild(child); child.QueueFree(); }
        var group = new ButtonGroup();
        foreach (var choice in PlaySetup.ChoicesOf(mode))
        {
            var card = new Button
            {
                Name = CardName(choice), ToggleMode = true, ButtonGroup = group, SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 128), Text = Loc.Text(PlaySetup.CardKey(mode, choice)), Icon = Art(mode, choice),
                ExpandIcon = true, IconAlignment = HorizontalAlignment.Center, VerticalIconAlignment = VerticalAlignment.Top,
                ClipText = true,
            };
            card.AddThemeFontSizeOverride("font_size", 14);
            card.AddThemeConstantOverride("icon_max_width", 168);
            card.Pressed += () => Pick(Setup with { Choice = choice });
            _cards.AddChild(card);
        }
    }

    private static Texture2D? Art(GameMode mode, string choice)
    {
        string path = PlaySetup.ArtPath(mode, choice);
        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    private void Pick(PlaySetup setup)
    {
        if (!Editable) return;
        Show(setup, true);
        Changed?.Invoke(Setup);
    }

    private static Label Section(string key) => UiTheme.Label(Loc.Text(key), 17, UiTheme.Selection);
}
