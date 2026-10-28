using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter.Tests;

/// <summary>
/// Card lab (doc/onslaught-card-ideas.md 5장, decisions.md M-27 `[임시]`): play on Onslaught's open field and switch
/// the six lab cards on and off to feel the goofy chains. Keyboard only, so the mouse keeps aiming:
/// F1–F6 cards, 0 all on/off, F7 a horde ring, F8 a named, F9 trickle, F10 slow motion, F11/F12 next gun in slot 1/2,
/// Backspace clears the monsters, Home is the cheat reset (frying pan only, then the slot 1 · 2 · 3 arming cards in the
/// card window), L offers a normal (level-up) card, K a special (boss reward) card, F reopens put-off cards, T puts a
/// training log at the cursor (Shift+T removes them). The survivor cannot be hurt.
/// Run with <c>Start-Game.ps1 -CardLab</c>.
/// </summary>
public partial class CardLab : Node
{
    private static readonly string[] Named = { "bull", "armed_veteran", "balloon" };

    public Stage Stage { get; private set; } = null!;
    public CardLabRules Rules { get; private set; } = null!;
    public CardWindow Window { get; private set; } = null!;
    private Label _cards = null!, _keys = null!, _stats = null!, _specials = null!;
    private int _gun1, _gun2, _named;
    private IReadOnlyList<string> _pool = new List<string>();
    private bool ReticleOnly => System.Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--reticle-lab");

    public override void _Ready()
    {
        Rules = new CardLabRules { Name = "CardLabRules", ReticlePreview = ReticleOnly };
        Stage = new Stage
        {
            CharacterOverride = "goose", ThemeOverride = "zombie", ModeOverride = GameMode.Survivor,
            RulesOverride = Rules, DirectorEnabled = false,
        };
        AddChild(Stage);
        Stage.Player.Invulnerable = true;
        if (ReticleOnly) { BuildReticleLab(); return; }
        _pool = ModeTuning.Current.Survivor.WeaponPool;
        _gun1 = Mathf.Max(0, _pool.ToList().IndexOf("m4"));
        _gun2 = Mathf.Max(0, _pool.ToList().IndexOf("r870"));
        Stage.Player.Loadout.SetSlot(1, ArmoryCatalog.Weapon(_pool[_gun1]));
        Stage.Player.Loadout.SetSlot(2, ArmoryCatalog.Weapon(_pool[_gun2]));
        Stage.Player.Weapons.RefreshVisual();
        Window = new CardWindow { Name = "CardWindow", Stage = Stage, Queue = Rules.Queue };
        AddChild(Window);
        BuildOverlay();
    }

    private void BuildOverlay()
    {
        var layer = new CanvasLayer { Layer = 11 };
        AddChild(layer);
        var panel = new PanelContainer { Name = "CardLabPanel", MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AnchorLeft = panel.AnchorRight = 0;
        panel.AnchorTop = panel.AnchorBottom = .5f;
        panel.OffsetLeft = panel.OffsetRight = 24;
        panel.GrowVertical = Control.GrowDirection.Both;
        panel.AddThemeStyleboxOverride("panel", Hud.HudPlate(pad: 12));
        layer.AddChild(panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);
        box.AddChild(Hud.HudLabel(Loc.Text("ui_cardlab_title"), 18, Hud.HudWarm));
        box.AddChild(_cards = Hud.HudLabel("", 14, Hud.HudWhite));
        box.AddChild(_specials = Hud.HudLabel("", 13, new Color("ffcf5a")));
        box.AddChild(_keys = Hud.HudLabel("", 12, Hud.HudMuted));
        box.AddChild(_stats = Hud.HudLabel("", 12, Hud.HudMuted));
    }

    public override void _Process(double delta)
    {
        if (ReticleOnly) { UpdateReticleLab(); return; }
        var cards = Rules.Cards;
        _cards.Text = string.Join("\n", LabCards.All.Select((c, i) =>
            Loc.Format("fmt_cardlab_card", i + 1, Loc.Text(cards.IsOn(c) ? "ui_cardlab_on" : "ui_cardlab_off"), Loc.Text(CardKey("name_card_", c))) +
            "\n      " + Loc.Text(CardKey("desc_card_", c))));
        _keys.Text = Loc.Format("ui_cardlab_keys", Loc.Text(Rules.Trickle ? "ui_cardlab_on" : "ui_cardlab_off"),
            Loc.Text(Engine.TimeScale < 1 ? "ui_cardlab_on" : "ui_cardlab_off"));
        var specials = LabCards.Specials.Where(cards.IsOn).Select(s => Loc.Text(LabCards.Key("name_card_", s))).ToList();
        _specials.Text = Loc.Format("fmt_cardlab_specials", specials.Count > 0 ? string.Join(" · ", specials) : Loc.Text("ui_cardlab_none"));
        _stats.Text = Loc.Format("fmt_cardlab_stats", cards.Floats, cards.Pops, cards.LargestClump, cards.ClumpBursts,
            cards.StuffingBursts, cards.BilliardHits, cards.Honks, cards.Ducks);
    }

    public static string CardKey(string prefix, LabCard card) => LabCards.Key(prefix, card);

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (ReticleOnly) { ReticleLabKey(key.Keycode); return; }
        var cards = Rules.Cards;
        switch (key.Keycode)
        {
            case >= Key.F1 and <= Key.F6:
                cards.Toggle(LabCards.All[key.Keycode - Key.F1]);
                break;
            case Key.Key0:
                bool on = !LabCards.All.All(cards.IsOn);
                foreach (var c in LabCards.All) cards.Set(c, on);
                break;
            case Key.F7: Rules.SpawnRing(14, 7, 11); break;
            case Key.F8: Rules.SpawnMonster(Named[_named++ % Named.Length]); break;
            case Key.F9: Rules.Trickle = !Rules.Trickle; break;
            case Key.F10: Engine.TimeScale = Engine.TimeScale < 1 ? 1 : .35; break;
            case Key.F11:
                _gun1 = (_gun1 + 1) % _pool.Count;
                Stage.Player.Loadout.SetSlot(1, ArmoryCatalog.Weapon(_pool[_gun1]));
                Stage.Player.Weapons.RefreshVisual();
                break;
            case Key.F12:
                _gun2 = (_gun2 + 1) % _pool.Count;
                Stage.Player.Loadout.SetSlot(2, ArmoryCatalog.Weapon(_pool[_gun2]));
                Stage.Player.Weapons.RefreshVisual();
                break;
            case Key.Backspace: Rules.ClearEnemies(); break;
            case Key.Home: Rules.CheatReset(); break;
            case Key.L: Rules.TestLevelUp(); break;
            case Key.K: Rules.TestSpecial(); break;
            case Key.C when key.ShiftPressed: Rules.Companions.Clear(); break;
            case Key.C: Rules.AddNextCompanion(); break;
            case Key.T when key.ShiftPressed: Rules.RemoveDummies(); break;
            case Key.T: Rules.SpawnDummy(Stage.Player.AimPoint); break;
            default: return;
        }
        GetViewport().SetInputAsHandled();
    }

    public override void _ExitTree() => Engine.TimeScale = 1;
}
