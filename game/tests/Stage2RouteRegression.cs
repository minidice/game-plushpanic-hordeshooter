using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Focused reproductions discovered during the continuous rendered route.</summary>
public partial class Stage2RouteRegression : Node
{
    private Stage _stage = null!;
    private int _checks;
    private void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        _checks++;
        GD.Print("PASS: " + message);
    }
    private async Task Frames(int n)
    {
        for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }
    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("STAGE2_ROUTE_REGRESSION_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("STAGE2_ROUTE_REGRESSION_FAIL " + e.Message); failed = true; }
        if (_stage != null && IsInstanceValid(_stage)) _stage.Free();
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }
    private async Task Run()
    {
        _stage = new Stage { MapOverride = new LayoutMap(encounters: null, supplies: null), ModeOverride = GameMode.Scenario,
            CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false, SquadOverride = 0 };
        AddChild(_stage);
        await Frames(4);
        _stage.Player.ManualControl = true;
        _stage.Player.Invulnerable = true;
        _stage.Player.TeleportTo(new Vector3(205, .05f, -7));
        var start = new Vector3(202.219f, .05f, -12.79308f);
        var bot = _stage.AddSurvivor(CharacterCatalog.Player("goose"), start, true, "PumpExitBot");
        bot.Invulnerable = true;
        var brain = SquadBrain.Attach(_stage, bot, SquadDatabase.Current.Roles[0], 0);
        _stage.Squad.Add(brain);
        Check(_stage.Flow.IsBlocked(start), "physically valid pump alcove quantizes into padded wall cell");
        float furthestStep = 0;
        var previous = bot.GlobalPosition;
        for (int i = 0; i < 480 && bot.GlobalPosition.Z < -11.3f; i++)
        {
            await Frames(1);
            furthestStep = Mathf.Max(furthestStep, bot.GlobalPosition.DistanceTo(previous));
            previous = bot.GlobalPosition;
        }
        GD.Print($"PUMP_EXIT: {bot.GlobalPosition}, max step {furthestStep}, activity {brain.Doing}");
        Check(bot.GlobalPosition.Z > -11.3f && furthestStep < .5f && brain.Teleports == 0, "actual squad bot walks out of pump alcove without catch-up teleport");
        _stage.Squad.Remove(brain);
        bot.QueueFree();
        await Frames(2);
        var enemy = _stage.SpawnCommon(start, false);
        for (int i = 0; i < 480 && enemy.GlobalPosition.Z < -11.3f; i++) await Frames(1);
        Check(enemy.GlobalPosition.Z > -11.3f, "actual enemy follows out of the same alcove: " + enemy.GlobalPosition);
        enemy.QueueFree();
        await Frames(2);
        // The full rendered fight can push a survivor deeper into the same alcove: this cell is open but disconnected
        // in the coarse grid. Physical recovery must cover this case as well as a wall-padded source cell.
        var deep = new Vector3(201.671f, .05f, -13.645491f);
        var deepField = _stage.Flow.CopyLayout();
        deepField.Update(_stage.Player.GlobalPosition);
        Check(!deepField.IsBlocked(deep) && !deepField.Reachable(deep), "deeper pump alcove is open but disconnected in the grid");
        var deepBot = _stage.AddSurvivor(CharacterCatalog.Player("goose"), deep, true, "DeepPumpExitBot");
        deepBot.Invulnerable = true;
        var deepBrain = SquadBrain.Attach(_stage, deepBot, SquadDatabase.Current.Roles[0], 0);
        _stage.Squad.Add(deepBrain);
        for (int i = 0; i < 480 && deepBot.GlobalPosition.Z < -11.3f; i++) await Frames(1);
        Check(deepBot.GlobalPosition.Z > -11.3f && deepBrain.Teleports == 0, "actual squad bot exits deeper disconnected alcove: " + deepBot.GlobalPosition);
        _stage.StartCrescendo();
        await Frames(3);
        Check(_stage.Objective == Objective.HoldOut, "pump enters defense objective");
        string originalLanguage = Loc.Language;
        foreach (string language in new[] { "kor", "eng" })
        {
            Loc.Language = language;
            Check(Loc.Has(_stage.Map.CrescendoHoldOutKey, language) && _stage.ObjectiveText.Contains(language == "kor" ? "다리" : "bridge"), language + " bridge defense text is translated");
            Check(Loc.Format(new StageOneLayout().CrescendoHoldOutKey, 25).Contains(language == "kor" ? "셔터" : "shutter"), language + " Stage 1 keeps shutter defense text");
        }
        Loc.Language = originalLanguage;
    }
}
