using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Promo reel (doc/features/tech-tools/log/): four AI survivors fight their way through the better-looking parts of the
/// large campaign, shot by shot, from the game camera without the HUD. Meant to run under Godot's Movie Maker
/// (tools/promo/Make-PromoReel.ps1), which renders every frame at a fixed 60 fps with the game's own sound, so heavy
/// fights still come out smooth. The leader walks a scripted line and shoots the nearest monster; the three squadmates
/// are the game's own squad AI. Everyone is invulnerable except a squadmate knocked down on purpose for the revive shot.
/// With <c>--promo-scout</c> it instead saves a still every 30 m along each stage's main route to output/promo/scout/,
/// for picking where the shots go.
/// </summary>
public partial class PromoReel : Node
{
    /// <summary>One shot: where on which stage, which enemies, the caption, and what happens in it.</summary>
    private sealed record Shot(string Caption, int StageNumber, string Theme, Vector2 From, Vector2 To, float Seconds, Func<Stage, Vector3, Vector3, Task>? Beat = null);

    private const float Fade = .25f;
    private static readonly string OutDir = "res://output/promo";

    private Stage? _stage;
    private FlowField? _flow;
    private Vector3 _goal, _facing = Vector3.Right;
    private bool _piloting;
    private float _pace = .55f;
    private ColorRect _black = null!;
    private Label _caption = null!;
    private readonly RandomNumberGenerator _rng = new() { Seed = 20261029 };

    public override async void _Ready()
    {
        int exit = 0;
        try
        {
            if (DisplayServer.GetName() == "headless") throw new InvalidOperationException("the promo reel needs a window (GPU)");
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(OutDir));
            GD.Print($"PROMO window {GetWindow().Size}, screen {DisplayServer.ScreenGetSize()}, usable {DisplayServer.ScreenGetUsableRect().Size}");
            BuildOverlay();
            if (OS.GetCmdlineUserArgs().Concat(OS.GetCmdlineArgs()).Contains("--promo-scout")) await Scout();
            else await Reel();
            GD.Print("PROMO_REEL_PASS");
        }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("PROMO_REEL_FAIL " + e.Message); exit = 1; }
        QuitHygiene.Collect();
        GetTree().Quit(exit);
    }

    // ---------- the reel ----------

    private IEnumerable<Shot> Shots() => new Shot[]
    {
        // Picked from the scout stills (2026-10-29): Stage 1's asphalt streets and Stage 3's grass and trees before x 250.
        // Stage 2 and Stage 3 past x 250 are still flat ground without dressing, so no shot goes there.
        new("넷이서 함께", 1, "zombie", new(20, -2), new(62, -10), 6f, Trickle),
        new("몰려오는 무리", 1, "zombie", new(258, -82), new(285, -86), 7f, Rush),
        new("수렵단", 3, "animals", new(36, -3), new(78, -8), 5.5f, Trickle),
        new("무장단", 3, "armed", new(118, -31), new(158, -39), 5.5f, Trickle),
        new("쓰러진 동료를 일으켜라", 3, "zombie", new(60, -7), new(80, -12), 6f, Revive),
        new("보스", 3, "zombie", new(140, -36), new(156, -39), 6.5f, Boss),
    };

    /// <summary>Where the closing title is shown (Stage 3, grass and trees around the path).</summary>
    private static readonly Vector2 EndingSpot = new(92, -21);

    private async Task Reel()
    {
        Stage? stage = null;
        int built = 0;
        string builtTheme = "";
        foreach (var shot in Shots())
        {
            if (stage == null || built != shot.StageNumber || builtTheme != shot.Theme)
            {
                await Close(stage);
                stage = await Open(shot.StageNumber, shot.Theme);
                built = shot.StageNumber;
                builtTheme = shot.Theme;
            }
            await Play(stage, shot);
        }
        await Ending(stage!);
        await Close(stage);
    }

    private async Task Play(Stage stage, Shot shot)
    {
        _black.Color = Colors.Black;
        var from = Ground(stage, shot.From);
        var to = Ground(stage, shot.To);
        await Clear(stage);
        Team(stage, from, to);
        stage.Cam.SnapTo(from);
        _caption.Text = shot.Caption;
        _flow = Toward(stage, to);
        _goal = to;
        _facing = (to - from) with { Y = 0 };
        _facing = _facing.LengthSquared() > .01f ? _facing.Normalized() : Vector3.Right;
        _piloting = true;
        // Let the camera settle and the squad find formation under the black.
        await Frames(8);
        var beat = shot.Beat?.Invoke(stage, from, to);
        await Run(shot.Seconds);
        if (beat != null) await beat;
        _piloting = false;
    }

    /// <summary>Fades in, holds for <paramref name="seconds"/> refilling everyone's ammo, fades out.</summary>
    private async Task Run(float seconds)
    {
        int frames = Mathf.RoundToInt(seconds * 60);
        for (int i = 0; i < frames; i++)
        {
            float t = i / 60f;
            float alpha = Mathf.Max(1 - t / Fade, 1 - (seconds - t) / Fade);
            _black.Color = new Color(0, 0, 0, Mathf.Clamp(alpha, 0, 1));
            _caption.Modulate = new Color(1, 1, 1, Mathf.Clamp(Mathf.Min(t / .5f, (seconds - .4f - t) / .4f), 0, 1));
            // Quietly: the stage's own refill floats "+100 AMMO" over everyone.
            if (i % 90 == 0 && _stage != null)
                foreach (var p in _stage.Players.Where(IsInstanceValid))
                    foreach (var gun in new[] { p.Loadout.Primary, p.Loadout.Special })
                        if (gun is { Def.Refillable: true }) gun.Refill();
            await Frames(1);
        }
        _black.Color = Colors.Black;
        _caption.Modulate = new Color(1, 1, 1, 0);
    }

    /// <summary>Small groups keep coming from ahead and the sides.</summary>
    private async Task Trickle(Stage stage, Vector3 from, Vector3 to)
    {
        var ahead = (to - from) with { Y = 0 };
        for (int wave = 0; wave < 4; wave++)
        {
            float along = 14 + wave * 7;
            var center = from + ahead.Normalized() * Mathf.Min(along, ahead.Length() + 8);
            var side = new Vector3(-ahead.Z, 0, ahead.X).Normalized() * (wave % 2 == 0 ? 5 : -5);
            Horde(stage, center + side, 5, 4);
            await Frames(75);
        }
    }

    /// <summary>A large horde charges from ahead while the squad holds the line.</summary>
    private async Task Rush(Stage stage, Vector3 from, Vector3 to)
    {
        _pace = .2f;
        var ahead = ((to - from) with { Y = 0 }).Normalized();
        Horde(stage, from + ahead * 20, 18, 7);
        await Frames(90);
        Horde(stage, from + ahead * 24, 18, 8);
        await Frames(90);
        Horde(stage, from + ahead * 18 + new Vector3(-ahead.Z, 0, ahead.X) * 8, 10, 5);
        await Frames(200);
        _pace = .55f;
    }

    /// <summary>A squadmate goes down early; another walks over and picks it up while the rest keep shooting.</summary>
    private async Task Revive(Stage stage, Vector3 from, Vector3 to)
    {
        _pace = .15f;
        var ahead = ((to - from) with { Y = 0 }).Normalized();
        Horde(stage, from + ahead * 16, 10, 5);
        await Frames(40);
        var victim = stage.Players.Where(p => IsInstanceValid(p) && p.IsBot).Skip(1).FirstOrDefault();
        if (victim != null)
        {
            victim.Invulnerable = false;
            victim.TakeDamage(9999, -ahead, 0);
            victim.Invulnerable = true;
        }
        await Frames(80);
        Horde(stage, from + ahead * 18 + new Vector3(ahead.Z, 0, -ahead.X) * 6, 8, 4);
        await Frames(200);
        _pace = .55f;
    }

    /// <summary>The boss walks in with a crowd at its feet.</summary>
    private async Task Boss(Stage stage, Vector3 from, Vector3 to)
    {
        _pace = .15f;
        var ahead = ((to - from) with { Y = 0 }).Normalized();
        var at = Ground(stage, new Vector2(from.X + ahead.X * 17, from.Z + ahead.Z * 17));
        stage.Spawn(MonsterDatabase.Get(BossEnemy.MonsterId), at, true);
        Horde(stage, from + ahead * 14, 8, 5);
        await Frames(150);
        Horde(stage, from + ahead * 15, 8, 6);
        await Frames(150);
        _pace = .55f;
    }

    /// <summary>The squad stands together with the title over them.</summary>
    private async Task Ending(Stage stage)
    {
        _black.Color = Colors.Black;
        await Clear(stage);
        var at = Ground(stage, EndingSpot);
        Team(stage, at, at + new Vector3(0, 0, 8));
        _piloting = false;
        foreach (var p in stage.Players.Where(IsInstanceValid)) p.SetControls(Vector2.Zero, p.GlobalPosition + new Vector3(0, 1, 8), false);
        foreach (var brain in stage.Squad) brain.ProcessMode = ProcessModeEnum.Disabled;
        stage.Cam.Close = true;
        stage.Cam.Target = null;
        stage.Cam.SnapTo(at + new Vector3(-1, 0, .5f));
        _caption.Text = "";
        var title = UiTheme.Label(Loc.Text("ui_menu_title"), 64, UiTheme.Selection);
        var subtitle = UiTheme.Label(Loc.Text("ui_menu_subtitle"), 26, Colors.White);
        var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        box.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (var label in new[] { title, subtitle })
        {
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AddThemeConstantOverride("outline_size", 10);
            label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, .8f));
            box.AddChild(label);
        }
        box.Modulate = new Color(1, 1, 1, 0);
        _black.GetParent().AddChild(box);
        await Frames(20);
        const float seconds = 4.5f;
        for (int i = 0; i < seconds * 60; i++)
        {
            float t = i / 60f;
            _black.Color = new Color(0, 0, 0, Mathf.Clamp(Mathf.Max(1 - t / Fade, .35f * Mathf.Clamp((t - .8f) / .6f, 0, 1)), 0, 1));
            if (t > seconds - .6f) _black.Color = new Color(0, 0, 0, Mathf.Clamp((t - (seconds - .6f)) / .6f, .35f, 1));
            box.Modulate = new Color(1, 1, 1, Mathf.Clamp((t - .8f) / .6f, 0, 1));
            await Frames(1);
        }
        box.QueueFree();
    }

    // ---------- scouting ----------

    private async Task Scout()
    {
        string dir = OutDir + "/scout";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(dir));
        _black.Color = new Color(0, 0, 0, 0);
        foreach (int n in new[] { 1, 2, 3 })
        {
            var stage = await Open(n, "zombie");
            var route = ((LayoutMap)stage.Map).Layout.Routes["main"];
            int index = 0;
            foreach (var point in Resample(route, 30))
            {
                await Clear(stage);
                var at = Ground(stage, point);
                var ahead = Ground(stage, point + Forward(route, point) * 10);
                Team(stage, at, ahead);
                Horde(stage, ahead, 6, 3, awake: false);
                stage.Cam.SnapTo(at);
                await Frames(30);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                string path = ProjectSettings.GlobalizePath($"{dir}/s{n}-{index++:00}-{point.X:0}_{point.Y:0}.png");
                GetViewport().GetTexture().GetImage().SavePng(path);
                GD.Print("SHOT: " + path);
            }
            await Close(stage);
        }
    }

    private static IEnumerable<Vector2> Resample(Vector2[] route, float step)
    {
        float carried = 0;
        yield return route[0];
        for (int i = 1; i < route.Length; i++)
        {
            var a = route[i - 1];
            var b = route[i];
            float length = a.DistanceTo(b);
            for (float d = step - carried; d < length; d += step) yield return a.Lerp(b, d / length);
            carried = (carried + length) % step;
        }
    }

    private static Vector2 Forward(Vector2[] route, Vector2 point)
    {
        int best = 0;
        for (int i = 0; i < route.Length - 1; i++)
            if (DistanceToSegment(point, route[i], route[i + 1]) < DistanceToSegment(point, route[best], route[best + 1])) best = i;
        return (route[best + 1] - route[best]).Normalized();
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b) => p.DistanceTo(Geometry2D.GetClosestPointToSegment(p, a, b));

    // ---------- stage handling ----------

    private async Task<Stage> Open(int n, string theme)
    {
        var map = n == 3 ? new LayoutMap() : LayoutMap.ForStage(n);
        var stage = new Stage
        {
            CharacterOverride = "goose", ThemeOverride = theme, ModeOverride = GameMode.Scenario, MapOverride = map,
            DirectorEnabled = false, SquadOverride = 3, ReviveOverride = true,
        };
        AddChild(stage);
        await Frames(5);
        _stage = stage;
        stage.Hud.Visible = false;
        stage.Player.ManualControl = true;
        stage.LeaveSafeRoom();
        map.StartDoor?.SetOpen(true, 0);
        stage.Cam.View = CameraView.Default with { WideSize = 14.5f };
        stage.Player.Weapons.Pickup(ArmoryCatalog.Weapon("m4"), null);
        foreach (var p in stage.Players.Where(IsInstanceValid)) p.Invulnerable = true;
        return stage;
    }

    private async Task Close(Stage? stage)
    {
        _piloting = false;
        _stage = null;
        if (stage == null) return;
        stage.QueueFree();
        await Frames(3);
    }

    /// <summary>Removes every monster between shots, out of the stage's list too: squadmates still look through it this frame.</summary>
    private async Task Clear(Stage stage)
    {
        foreach (var e in stage.AllEnemies.ToList())
        {
            stage.Enemies.Remove(e);
            if (IsInstanceValid(e)) e.QueueFree();
        }
        await Frames(2);
    }

    private static Vector3 Ground(Stage stage, Vector2 at) => new(at.X, stage.Tiers.GroundY(new Vector3(at.X, 0, at.Y)) + .05f, at.Y);

    private static void Team(Stage stage, Vector3 at, Vector3 look)
    {
        var people = stage.Players.Where(IsInstanceValid).ToList();
        var forward = ((look - at) with { Y = 0 }).Normalized();
        var right = new Vector3(-forward.Z, 0, forward.X);
        var offsets = new[] { Vector3.Zero, -forward * 1.6f + right * 1.4f, -forward * 1.6f - right * 1.4f, -forward * 3f };
        for (int i = 0; i < people.Count; i++)
        {
            var spot = at + offsets[i % offsets.Length];
            people[i].TeleportTo(Ground(stage, new Vector2(spot.X, spot.Z)));
            people[i].Invulnerable = true;
            people[i].SetControls(Vector2.Zero, look + Vector3.Up, false);
        }
    }

    private static FlowField Toward(Stage stage, Vector3 at)
    {
        var flow = stage.Flow.CopyLayout();
        flow.Update(at);
        return flow;
    }

    /// <summary>Awake common monsters of the stage's theme on open ground around <paramref name="center"/>.</summary>
    private void Horde(Stage stage, Vector3 center, int count, float radius, bool awake = true)
    {
        var spots = stage.Flow.OpenCellsNear(center, 0, radius).OrderBy(_ => _rng.Randi()).Take(count).ToList();
        foreach (var spot in spots) stage.SpawnCommon(Ground(stage, new Vector2(spot.X, spot.Z)), awake);
    }

    // ---------- the leader ----------

    /// <summary>The leader walks toward the shot's goal at <see cref="_pace"/> and shoots the nearest monster, slowing to hold ground when one is close.</summary>
    public override void _PhysicsProcess(double delta)
    {
        if (!_piloting || _stage is not { } stage || !IsInstanceValid(stage)) return;
        var leader = stage.Player;
        if (!IsInstanceValid(leader) || leader.IsDead || leader.IsDowned) return;
        var here = leader.GlobalPosition;
        Enemy? target = null;
        float best = 15f;
        foreach (var e in stage.Enemies)
        {
            if (!IsInstanceValid(e) || e.IsDead) continue;
            float d = e.GlobalPosition.DistanceTo(here);
            if (d < best) { best = d; target = e; }
        }
        var move = Vector3.Zero;
        if (_flow != null && here.DistanceTo(_goal) > 1.5f)
        {
            var dir = _flow.NextCell(here) < 0 ? (_goal - here) with { Y = 0 } : _flow.Direction(here);
            if (dir.LengthSquared() > .0001f) move = dir.Normalized() * _pace * (target != null && best < 9 ? .4f : 1);
        }
        if (move.LengthSquared() > .0001f) _facing = move.Normalized();
        var aim = target != null ? target.GlobalPosition + Vector3.Up * .8f : here + _facing * 6 + Vector3.Up;
        leader.SetControls(new Vector2(move.X, move.Z), aim, target != null);
    }

    // ---------- overlay ----------

    private void BuildOverlay()
    {
        var layer = new CanvasLayer { Layer = 100 };
        AddChild(layer);
        _black = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
        _black.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(_black);
        _caption = UiTheme.Label("", 40, Colors.White);
        _caption.AddThemeConstantOverride("outline_size", 10);
        _caption.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, .75f));
        _caption.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _caption.VerticalAlignment = VerticalAlignment.Bottom;
        _caption.OffsetLeft = 72;
        _caption.OffsetBottom = -64;
        _caption.Modulate = new Color(1, 1, 1, 0);
        layer.AddChild(_caption);
    }

    private async Task Frames(int count) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
}
