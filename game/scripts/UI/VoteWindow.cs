using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>
/// The special card vote on screen (decisions.md M-29, plan 16.5): the three cards with the same stamp on every vote
/// (never whose), "votes 3/6" and a time gauge, then a highlight that spins between tied cards and stops on the
/// winner, its name, and 3 · 2 · 1 before play goes on. Keys 1 · 2 · 3 or a click stamp; a stamp cannot be moved.
/// Shown on every PC; runs while the tree is paused.
/// </summary>
public partial class VoteWindow : CanvasLayer
{
    /// <summary>From content/data/asset_paths.json (T-26).</summary>
    public static string StampIcon => AssetPaths.File("ui.vote_stamp");
    public static string WinnerFrame => AssetPaths.File("ui.vote_winner_frame");

    public Stage Stage { get; set; } = null!;
    public SpecialVote Vote { get; set; } = null!;
    /// <summary>The normal card window, hidden while everyone is stopped for the vote.</summary>
    public CanvasLayer? NormalCards { get; set; }

    private Control _root = null!;
    private Label _title = null!, _count = null!, _status = null!, _countdown = null!;
    private ProgressBar _gauge = null!;
    private readonly List<CardButton> _cards = new();
    private readonly List<HBoxContainer> _stampRows = new();
    private readonly List<Control> _frames = new();
    private Texture2D? _stamp;
    private int _shownSerial = -1, _revealedSerial = -1;
    private MysteryReveal _reveal = null!;

    public override void _Ready()
    {
        Layer = 14;
        ProcessMode = ProcessModeEnum.Always;
        _stamp = ResourceLoader.Exists(StampIcon) ? GD.Load<Texture2D>(StampIcon) : null;
        var frame = ResourceLoader.Exists(WinnerFrame) ? GD.Load<Texture2D>(WinnerFrame) : null;

        _root = new Control { Name = "VoteRoot", Theme = UiTheme.Get(), Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);
        var dim = new ColorRect { Color = new Color(0, 0, 0, .62f), MouseFilter = Control.MouseFilterEnum.Ignore };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(dim);

        var panel = new VBoxContainer { Name = "VotePanel", CustomMinimumSize = new Vector2(760, 0) };
        panel.AddThemeConstantOverride("separation", 12);
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.GrowHorizontal = panel.GrowVertical = Control.GrowDirection.Both;
        _root.AddChild(panel);

        var head = new HBoxContainer();
        panel.AddChild(head);
        _title = Hud.HudLabel(Loc.Text("ui_vote_title"), 28, new Color("ffcf5a"));
        _title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(_title);
        _count = Hud.HudLabel("", 22, Hud.HudWhite);
        head.AddChild(_count);

        for (int i = 0; i < 3; i++)
        {
            // A plain holder so the stamps and the winner's glow can sit over the card.
            var holder = new Control { Name = "VoteCard" + (i + 1), CustomMinimumSize = new Vector2(760, 118), MouseFilter = Control.MouseFilterEnum.Ignore };
            panel.AddChild(holder);
            Control glow;
            if (frame != null)
                glow = new NinePatchRect { Texture = frame, PatchMarginLeft = 40, PatchMarginRight = 40, PatchMarginTop = 40, PatchMarginBottom = 40 };
            else
            {
                var fallback = new Panel();
                var style = Hud.HudPlate(new Color(0, 0, 0, 0), new Color("ffcf5a"), 0, 18);
                style.SetBorderWidthAll(5);
                fallback.AddThemeStyleboxOverride("panel", style);
                glow = fallback;
            }
            glow.MouseFilter = Control.MouseFilterEnum.Ignore;
            glow.Visible = false;
            glow.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            glow.OffsetLeft = glow.OffsetTop = -14;
            glow.OffsetRight = glow.OffsetBottom = 14;
            var card = new CardButton(Vote.StampLocal, i);
            card.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            holder.AddChild(card);
            holder.AddChild(glow);
            var stamps = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End };
            stamps.AddThemeConstantOverride("separation", -18);
            stamps.SetAnchorsPreset(Control.LayoutPreset.RightWide);
            stamps.OffsetLeft = -330;
            stamps.OffsetRight = -18;
            holder.AddChild(stamps);
            _cards.Add(card);
            _frames.Add(glow);
            _stampRows.Add(stamps);
        }

        _gauge = new ProgressBar { CustomMinimumSize = new Vector2(760, 14), ShowPercentage = false, MaxValue = 1, Step = 0 };
        _gauge.AddThemeStyleboxOverride("background", Hud.HudPlate(new Color(.1f, .1f, .14f, .9f), new Color(.3f, .3f, .4f, .6f), 0, 7));
        _gauge.AddThemeStyleboxOverride("fill", Hud.HudPlate(new Color("ffcf5a"), new Color("ffe9a8"), 0, 7));
        panel.AddChild(_gauge);
        _status = Hud.HudLabel("", 16, Hud.HudMuted);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        panel.AddChild(_status);

        _countdown = Hud.HudLabel("", 120, new Color("ffcf5a"));
        _countdown.SetAnchorsPreset(Control.LayoutPreset.Center);
        _countdown.GrowHorizontal = _countdown.GrowVertical = Control.GrowDirection.Both;
        _countdown.HorizontalAlignment = HorizontalAlignment.Center;
        _countdown.Visible = false;
        _root.AddChild(_countdown);
        _reveal = new MysteryReveal { Name = "MysteryReveal" };
        AddChild(_reveal);
    }

    public override void _Process(double delta)
    {
        bool open = Vote.Active && Vote.Offer != null;
        _root.Visible = open;
        if (NormalCards != null) NormalCards.Visible = !Stage.AllStopped;
        if (!open) { _shownSerial = -1; return; }
        var offer = Vote.Offer!;
        if (_shownSerial != Vote.Serial)
        {
            _shownSerial = Vote.Serial;
            for (int i = 0; i < _cards.Count; i++) _cards[i].Show(i < offer.Choices.Count ? offer.Choices[i] : null, CardKind.Special);
        }
        // A special "?" that won turns into its card once the vote is settled (plan 16.3).
        if (Vote.Phase == VotePhase.Result && _revealedSerial != Vote.Serial && Vote.Winner >= 0 && Vote.Winner < offer.Choices.Count &&
            offer.Choices[Vote.Winner].Inner is { } inner)
        {
            _revealedSerial = Vote.Serial;
            _reveal.Play(inner);
        }
        bool counting = Vote.Phase == VotePhase.Countdown;
        _countdown.Visible = counting;
        _countdown.Text = counting ? Mathf.CeilToInt(Vote.Left).ToString() : "";
        for (int i = 0; i < _cards.Count; i++)
        {
            _cards[i].Modulate = counting ? new Color(1, 1, 1, .35f) : Vote.Mine >= 0 && Vote.Mine != i && Vote.Phase == VotePhase.Voting ? new Color(1, 1, 1, .7f) : Colors.White;
            _frames[i].Visible = Highlighted() == i;
            SetStamps(i, i < Vote.Stamps.Length ? Vote.Stamps[i] : 0);
        }
        _count.Text = Loc.Format("fmt_vote_count", Vote.Voted, Vote.Voters);
        // Faded, not hidden, so the panel does not jump when the voting ends.
        _gauge.Modulate = new Color(1, 1, 1, Vote.Phase == VotePhase.Voting ? 1 : 0);
        _gauge.Value = Vote.Left / Mathf.Max(1, Vote.Limit);
        _status.Text = Vote.Phase switch
        {
            VotePhase.Voting when !Vote.IsVoter => Loc.Text("ui_vote_spectate"),
            VotePhase.Voting when Vote.Mine >= 0 => Loc.Format("fmt_vote_waiting", Mathf.CeilToInt(Vote.Left)),
            VotePhase.Voting => Loc.Format("fmt_vote_hint", Mathf.CeilToInt(Vote.Left)),
            VotePhase.Spinning when Vote.Tied.Count > 1 => Loc.Text("ui_vote_tie"),
            _ when Vote.Winner >= 0 && Vote.Winner < offer.Choices.Count && Vote.Phase != VotePhase.Spinning =>
                Loc.Format("fmt_vote_result", offer.Choices[Vote.Winner].Inner?.Name ?? offer.Choices[Vote.Winner].Name),
            _ => "",
        };
    }

    /// <summary>The card the glow is on: a tie spins it between the tied cards and slows down onto the winner.</summary>
    private int Highlighted()
    {
        if (Vote.Phase == VotePhase.Voting || Vote.Winner < 0) return -1;
        if (Vote.Phase != VotePhase.Spinning || Vote.Tied.Count < 2 || Vote.Left < .35f) return Vote.Winner;
        // Faster at first, slower near the end: steps of the elapsed time squared.
        float done = SpecialVote.SpinSeconds - Vote.Left;
        int step = Mathf.FloorToInt(done * 9f - done * done * 1.6f);
        return Vote.Tied[((step % Vote.Tied.Count) + Vote.Tied.Count) % Vote.Tied.Count];
    }

    private void SetStamps(int card, int count)
    {
        var row = _stampRows[card];
        while (row.GetChildCount() > count) { var last = row.GetChild(row.GetChildCount() - 1); row.RemoveChild(last); last.QueueFree(); }
        while (row.GetChildCount() < count)
        {
            Control mark = _stamp != null
                ? new TextureRect { Texture = _stamp, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(92, 92) }
                : Hud.HudLabel("◉", 40, new Color("d9473b"));
            mark.MouseFilter = Control.MouseFilterEnum.Ignore;
            mark.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(mark);
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Vote.Active || e is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode is Key.Key1 or Key.Key2 or Key.Key3)
        {
            Vote.StampLocal((int)(key.Keycode - Key.Key1));
            GetViewport().SetInputAsHandled();
        }
    }
}
