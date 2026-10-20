using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Main menu pages, single-player options, the multiplayer shell and saved settings.
/// Uses its own settings/appearance files so the player's real ones are never touched.
/// Windowed runs also save output/menu-*.png.
/// </summary>
public partial class MenuCheck : Node
{
    private const string SettingsPath = "user://menu-check-settings.cfg";
    private const string AppearancePath = "user://menu-check-appearance.cfg";
    private MainMenu _menu = null!;
    private int _checks;

    private T Ui<T>(string name) where T : Node => (T)(_menu.FindChild(name, true, false) ?? throw new Exception("Missing control " + name));
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private void Click(string name) => Ui<Button>(name).EmitSignal(BaseButton.SignalName.Pressed);
    private bool PageVisible(MenuPage page) => Ui<Control>("Page_" + page).Visible;

    public override async void _Ready()
    {
        bool failed = false;
        try
        {
            Remove(SettingsPath);
            Remove(AppearancePath);
            GameSettings.Reset();
            RoomDirectory.Url = ""; // no network: rooms are fed in below (RoomDirectoryCheck covers the server)
            GameSession.SquadSize = GameSession.DefaultSquadSize;
            GameSession.Mode = GameMode.Scenario;
            await Run();
            GD.Print("MENU_CHECK_PASS " + _checks);
        }
        catch (Exception e)
        {
            GD.PushError(e.ToString());
            GD.Print("MENU_CHECK_FAIL " + e.Message);
            failed = true;
        }
        GameSettings.Reset();
        Remove(SettingsPath);
        Remove(AppearancePath);
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task Run()
    {
        await OpenMenu();
        Check(_menu.Page == MenuPage.Home && PageVisible(MenuPage.Home) && !PageVisible(MenuPage.Single) && !PageVisible(MenuPage.Customize),
            "menu opens on the home page");
        Check(new[] { "Menu_Customize", "Menu_Single", "Menu_Multi", "Menu_Settings", "Menu_Quit" }.All(n => _menu.FindChild(n, true, false) is Button { Disabled: false }),
            "home lists customize, single, multi, settings and quit");
        Check(!GameSettings.Loaded, "a menu inside a test scene does not load the player's settings");
        await Shot("menu-home");
        Check(GetViewport().GuiGetFocusOwner()?.Name == "Menu_Single", "home focuses single play, so Enter starts the usual path");
        GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = true });
        GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = false });
        await Frames(2);
        Check(_menu.Page == MenuPage.Single && GetViewport().GuiGetFocusOwner()?.Name == "StartGame", "Enter on home opens single play with the start button focused");
        _menu.ShowPage(MenuPage.Home);
        await Frames(2);

        // Customize: the workshop with species buttons and preview tools.
        Click("Menu_Customize");
        await Frames(2);
        Check(_menu.Page == MenuPage.Customize && Ui<Control>("PreviewTools").Visible && Ui<TabContainer>("PartTabs").GetTabCount() == 6,
            "customize page shows the workshop and preview tools");
        Click("Species_raccoon");
        await Frames(2);
        Check(_menu.Selected.Id == "raccoon" && GameSession.SelectedCharacterId == "raccoon", "picking a species makes it the character to play");
        await Shot("menu-customize");
        Click("Back_Customize");
        await Frames(2);
        Check(_menu.Page == MenuPage.Home && !Ui<Control>("PreviewTools").Visible, "back returns home and hides preview tools");

        // Single: modes, squad option, start.
        Click("Menu_Single");
        await Frames(2);
        Check(_menu.Page == MenuPage.Single && PageVisible(MenuPage.Single) && !PageVisible(MenuPage.Home), "single page opens");
        Check(Ui<Button>("Mode_scenario") is { Disabled: false, ButtonPressed: true } && Ui<Button>("Mode_survival").Disabled && Ui<Button>("Mode_survivor").Disabled,
            "scenario is playable; survival and survivor are listed but locked");
        var squad = Ui<OptionButton>("SquadChoice");
        Check(squad.ItemCount == 3 && squad.GetItemId(squad.Selected) == 1, "AI squadmates: none / 1 / 3, defaulting to 1");
        squad.Select(2);
        squad.EmitSignal(OptionButton.SignalName.ItemSelected, 2);
        Check(GameSession.SquadSize == 3, "choosing 3 squadmates is stored for the stage");
        Check(Ui<Button>("StartGame") is { Disabled: false } && _menu.FindChild("StageThemeChoice", true, false) != null && _menu.FindChild("DebugEscape", true, false) != null,
            "start button and test options stay on the single page");
        await Shot("menu-single");
        _menu._UnhandledInput(new InputEventAction { Action = "ui_cancel", Pressed = true });
        await Frames(2);
        Check(_menu.Page == MenuPage.Home, "Esc goes back to the home page");

        // Multi: two-player test by address (room codes and public rooms come later).
        Click("Menu_Multi");
        await Frames(2);
        Check(!Ui<Button>("Multi_Host").Disabled && !Ui<Button>("Multi_Join").Disabled && Ui<LineEdit>("Multi_Address").Editable &&
              Ui<LineEdit>("Multi_Address").Text == "",
            "multiplayer page: host and join by address work (address empty, I-76)");
        Check(Ui<Label>("Rooms_Status").Text == Loc.Text("menu_rooms_off") && Ui<Button>("Rooms_Refresh").Disabled,
            "with the room list off the page says so");
        RoomDirectory.Url = "http://127.0.0.1:9/hordeshooter"; // only for the rows below: nothing is fetched
        _menu.ShowRooms(new[]
        {
            new RoomDirectory.Room("old", "거위", "p0-00000000", 1, 2, "lobby", "203.0.113.5", 24680, false, Array.Empty<string>()),
            new RoomDirectory.Room("open", "라쿤", NetLobby.Version, 1, 2, "playing", "203.0.113.6", 24680, true, new[] { "192.168.0.14" }),
            new RoomDirectory.Room("full", "오리", NetLobby.Version, 2, 2, "lobby", "203.0.113.7", 24680, false, Array.Empty<string>()),
        });
        RoomDirectory.Url = "";
        var rows = Ui<Container>("Rooms_List").GetChildren().Select(n => n.Name.ToString()).ToArray();
        Check(rows.SequenceEqual(new[] { "Room_open", "Room_full", "Room_old" }) && Ui<Label>("Rooms_Status").Text == Loc.Format("fmt_menu_rooms_count", 3),
            "rooms list same-version rooms first");
        Check(Ui<Button>("Room_Join_open") is { Disabled: false } && Ui<Button>("Room_Join_full") is { Disabled: true } &&
              Ui<Button>("Room_Join_old") is { Disabled: true } old && old.Text == Loc.Text("menu_room_other_version") && old.TooltipText.Contains(NetLobby.Version),
            "a room of another version is locked and says why before any connection; a full room is locked");
        Check(new RoomDirectory.Room("x", "", "", 1, 2, "lobby", "203.0.113.6", 24680, true, new[] { "192.168.0.14" }).JoinAddress == "192.168.0.14" &&
              new RoomDirectory.Room("x", "", "", 1, 2, "lobby", "203.0.113.6", 24680, false, Array.Empty<string>()).JoinAddress == "203.0.113.6",
            "behind the same router a room is joined on its LAN address, otherwise on its public one");
        Click("Multi_Join");
        Check(Ui<Label>("Multi_Status").Text == Loc.Text("menu_multi_need_address") && !Ui<Button>("Multi_Join").Disabled,
            "joining with no address asks for one instead of connecting");
        await Shot("menu-multi");

        // Create Room opens the room page (menu-multiplayer-plan.md 2장); LobbyCheck covers two sides.
        Click("Multi_Host");
        await Frames(2);
        if (NetLaunch.Lobby != null)
        {
            Check(_menu.Page == MenuPage.Lobby && PageVisible(MenuPage.Lobby), "Create Room opens the room page");
            Check(Ui<Button>("Lobby_Start") is { Visible: true, Disabled: false } && !Ui<Button>("Lobby_Ready").Visible,
                "the host alone sees [Start] enabled and no [Ready]");
            Check(Ui<Button>("Lobby_Mode_scenario") is { Disabled: false, ButtonPressed: true } && Ui<Button>("Lobby_Mode_survival").Disabled,
                "the host picks the mode in the room; locked modes stay locked");
            Check(Ui<Container>("Lobby_Players").GetChildCount() == 1 && Ui<Label>("Lobby_Count").Text == Loc.Format("fmt_lobby_players", 1, NetLaunch.MaxPlayers),
                "the player list shows the host");
            await Shot("menu-lobby");
            _menu._UnhandledInput(new InputEventAction { Action = "ui_cancel", Pressed = true });
            await Frames(2);
            Check(_menu.Page == MenuPage.Multi && NetLaunch.Lobby == null && GetTree().GetMultiplayer().MultiplayerPeer == null,
                "Esc in the room leaves it and closes the link");
        }
        else GD.Print($"SKIP: room page (UDP {NetLaunch.Port} is in use on this PC)");
        Click("Back_Multi");
        await Frames(2);

        // Settings: live values, saved and restored.
        GameSettings.Path = SettingsPath;
        GameSettings.Load();
        Click("Menu_Settings");
        await Frames(2);
        float fullRecoil = Knockback.Recoil(100);
        Ui<HSlider>("Settings_Recoil").Value = 40;
        Ui<HSlider>("Settings_SfxVolume").Value = 50;
        Check(Mathf.IsEqualApprox(GameSettings.RecoilScale, .4f) && Mathf.IsEqualApprox(Knockback.Recoil(100), fullRecoil * .4f),
            "recoil slider scales shooting push live");
        int sfxBus = AudioServer.GetBusIndex(AudioSetup.SfxBus);
        Check(sfxBus >= 0 && Mathf.Abs(AudioServer.GetBusVolumeDb(sfxBus) - Mathf.LinearToDb(.5f)) < .1f, "effects volume slider changes the SFX bus");
        Ui<HSlider>("Settings_MasterVolume").Value = 0;
        Check(AudioServer.IsBusMute(AudioServer.GetBusIndex("Master")), "master volume 0% mutes");
        Ui<HSlider>("Settings_MasterVolume").Value = 100;
        var revive = Ui<CheckButton>("Settings_Revive");
        Check(revive.ButtonPressed && GameSettings.ReviveEnabled, "revive option is on by default");
        revive.ButtonPressed = false;
        Check(!GameSettings.ReviveEnabled, "revive option can be turned off");
        revive.ButtonPressed = true;
        string koreanTitle = Loc.Text("menu_settings_title");
        var languageChoice = Ui<OptionButton>("Settings_Language");
        int english = GameSettings.Languages.ToList().IndexOf("eng");
        languageChoice.Select(english);
        languageChoice.EmitSignal(OptionButton.SignalName.ItemSelected, english);
        await Frames(3);
        Check(Loc.Language == "eng" && _menu.Page == MenuPage.Settings && _menu.FindChild("Page_Settings", true, false) is Control { Visible: true },
            "switching language rebuilds the menu on the settings page");
        Check(Ui<Button>("Back_Settings").Text == Loc.Table("eng")["menu_back"] && Loc.Text("menu_settings_title") != koreanTitle, "menu text is now English");
        await Shot("menu-settings-eng");
        Click("Back_Settings");
        await Frames(2);

        // Reload from disk: everything chosen above comes back.
        GameSettings.Reset();
        GameSession.SquadSize = GameSession.DefaultSquadSize;
        GameSession.SelectedCharacterId = GameSession.DefaultCharacterId;
        Check(Loc.Language == "kor" && GameSettings.RecoilScale == 1f, "reset returns to defaults");
        GameSettings.Path = SettingsPath;
        GameSettings.Load();
        Check(Loc.Language == "eng" && Mathf.IsEqualApprox(GameSettings.RecoilScale, .4f) && Mathf.IsEqualApprox(GameSettings.SfxVolume, .5f),
            "language, recoil and volume are restored from the settings file");
        Check(GameSession.SquadSize == 3 && GameSession.SelectedCharacterId == "raccoon", "squad size and character are restored from the settings file");

        // A broken file or unknown values fall back to safe defaults.
        GameSettings.Reset();
        using (var bad = new ConfigFile())
        {
            bad.SetValue("options", "volume_sfx", float.NaN);
            bad.SetValue("options", "language", "xx");
            bad.SetValue("options", "recoil_scale", 7f);
            bad.SetValue("menu", "squad_size", 2);
            bad.Save(SettingsPath);
        }
        GameSettings.Path = SettingsPath;
        GameSettings.Load();
        Check(Loc.Language == "kor" && GameSettings.RecoilScale == 1f && GameSettings.SfxVolume == 1f && GameSession.SquadSize == GameSession.DefaultSquadSize,
            "unknown language, out-of-range recoil, NaN volume and invalid squad size fall back to defaults");
    }

    private async Task OpenMenu()
    {
        _menu = GD.Load<PackedScene>("res://scenes/Menu.tscn").Instantiate<MainMenu>();
        _menu.AppearancePath = AppearancePath;
        AddChild(_menu);
        await Frames(10);
    }

    private static void Remove(string path)
    {
        if (FileAccess.FileExists(path)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
    }

    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }

    private async Task Shot(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await Frames(15);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/" + name + ".png"));
    }
}
