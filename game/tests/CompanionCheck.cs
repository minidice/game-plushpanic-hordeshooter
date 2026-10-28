using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Companions in play (decisions.md M-27 소환수, Companion.cs) on the card lab's open field: every kind joins and
/// follows, pets and drones have no body and cannot be hurt, each kind fights a monster nearby, a survivor running
/// off makes them drop the fight and come back, a started laser windup still goes off first, a swarmed sidekick
/// falls and gets up beside its survivor, the model quality switches live, and the keys and squad list round-trip.
/// With a real window it saves output/companion-*.png.
/// </summary>
public partial class CompanionCheck : Node
{
    private int _checks;
    private Stage _stage = null!;
    private CardLabRules _rules = null!;
    private Player _player = null!;
    private bool Windowed => DisplayServer.GetName() != "headless";
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    private async Task Until(Func<bool> done, string what, float seconds = 5)
    {
        for (int i = 0; i < seconds * Engine.PhysicsTicksPerSecond; i++)
        {
            if (done()) { Check(true, what); return; }
            await Frames(1);
        }
        Check(done(), what + " (timed out)");
    }

    public override async void _Ready()
    {
        bool failed = false;
        var quality = GameSettings.PetQuality;
        try { await Run(); GD.Print("COMPANION_CHECK_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("COMPANION_CHECK_FAIL " + e.Message); failed = true; }
        GameSettings.PetQuality = quality;
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task Shot(string name)
    {
        if (!Windowed) return;
        for (int i = 0; i < 3; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://output/companion-{name}.png"));
        GD.Print($"SHOT: output/companion-{name}.png");
    }

    private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    /// <summary>Open ground about <paramref name="distance"/> from the survivor.</summary>
    private Vector3 OpenSpot(float distance) => _stage.Flow.OpenCellsNear(_player.GlobalPosition, distance, distance + 4).First();

    private async Task<Enemy> Common(Vector3 at)
    {
        var e = _stage.SpawnCommon(at, awake: false);
        await Frames(2);
        return e;
    }

    private async Task Clear()
    {
        _rules.ClearEnemies();
        _rules.Companions.Clear();
        await Frames(3);
    }

    private async Task Run()
    {
        var lab = new CardLab { Name = "CardLab" };
        AddChild(lab);
        await Frames(3);
        _stage = lab.Stage;
        _rules = lab.Rules;
        _player = _stage.Player;
        _player.ManualControl = true;

        // ---------- keys and cards ----------
        foreach (var key in CardLabRules.CompanionCycle)
            Check(CompanionSpec.Parse(key)?.Key == key && !CompanionSpec.Parse(key)!.DisplayName.Contains("companion_"), $"{key} reads back and has a name");
        Check(CompanionSpec.Parse("pet:cat:twin_blaster") == null && CompanionSpec.Parse("drone:bee:cannon") == null && CompanionSpec.Parse("sidekick:nobody") == null,
            "unknown companions are refused");
        var offer = _rules.Companions.Offer(new RandomNumberGenerator { Seed = 5 });
        Check(offer.Kind == CardKind.Arming && offer.Choices.Count == 3 && offer.Choices.Select(c => CompanionSpec.Parse(c.Id)!.Kind).Distinct().Count() == 3 &&
              offer.Choices.All(c => c.Target == CardTarget.Summon && c.Key == "f|" + c.Id && CardCatalog.FromKey(c.Key, null)?.Name == c.Name),
            "the companion card offers a pet, a drone and a sidekick, and a client rebuilds the same cards");

        // ---------- each kind joins and follows; pets and drones have no body ----------
        var pet = _rules.AddNextCompanion()!;
        var drone = _rules.AddNextCompanion()!;
        var sidekick = _rules.AddNextCompanion()!;
        await Frames(5);
        Check(pet.Pet != null && drone.Drone != null && sidekick.Sidekick != null && _rules.Companions.Of(_player).Count() == 3, "C adds a pet, a drone and a sidekick");
        Check(new Node[] { pet, drone, sidekick }.All(c => !c.FindChildren("*", nameof(CollisionObject3D), true, false).Any()),
            "no companion has a physics body: nothing walks or shoots into them");
        Check(drone.GlobalPosition.Y > 1.5f && pet.GlobalPosition.Y < .1f && sidekick.GlobalPosition.Y < .1f, "the drone flies, the pet and the sidekick walk");
        var start = _player.GlobalPosition;
        var away = OpenSpot(9);
        _player.TeleportTo(away);
        await Until(() => new[] { pet, drone, sidekick }.All(c => Flat(c.GlobalPosition, _player.GlobalPosition) < 3.5f), "all three follow the survivor to a new spot", 4);
        await Frames(30);
        Check(new[] { pet, drone, sidekick }.All(c => c.Speed < .4f), "and stand still once they are there (no sliding on the spot)");
        if (Windowed)
        {
            _stage.Cam.Close = true;
            _stage.Cam.SnapTo(_player.GlobalPosition);
            await Frames(10);
            await Shot("follow");
            _stage.Cam.Close = false;
        }

        // ---------- pets and drones cannot be hurt; the sidekick can ----------
        pet.Damage(999);
        drone.Damage(999);
        Check(!pet.Down && !drone.Down && pet.Visible && drone.Visible, "pets and drones cannot be hurt");

        // ---------- each kind fights a monster near the survivor ----------
        foreach (var c in new[] { pet, drone, sidekick })
        {
            await Clear();
            var buddy = _rules.Companions.Add(_player, c.Spec.Key)!;
            await Frames(10);
            var target = await Common(_player.GlobalPosition + new Vector3(5, 0, 2));
            float health = target.Health;
            await Until(() => buddy.Attacks > 0 && (target.IsDead || target.Health < health), $"the {buddy.Spec.Kind} ({buddy.Spec.Key}) attacks a monster nearby", 4);
        }
        await Clear();

        // ---------- the leash: running off drops the fight ----------
        var gunner = _rules.Companions.Add(_player, "pet:dog:twin_blaster")!;
        await Frames(10);
        _rules.Companions.Add(_player, "drone:bee:blaster");
        var near = await Common(_player.GlobalPosition + new Vector3(4, 0, 0));
        near.ScaleHealth(100);
        await Until(() => gunner.Attacks > 0, "the pet opens fire", 3);
        var far = OpenSpot(16);
        _player.TeleportTo(far);
        await Frames(3);
        int attacks = gunner.Attacks;
        await Until(() => gunner.Returning, "with its survivor more than 12 m away it stops fighting and comes back", 1);
        await Frames(20);
        Check(gunner.Attacks == attacks, "no more shots while it comes back");
        await Until(() => Flat(gunner.GlobalPosition, _player.GlobalPosition) < Companion.Return && !gunner.Returning, "back at the survivor's side it may fight again", 5);
        await Clear();

        // ---------- a started windup still goes off ----------
        var laser = _rules.Companions.Add(_player, "drone:bee:laser")!;
        await Frames(10);
        var line = await Common(_player.GlobalPosition + new Vector3(6, 0, 0));
        line.ScaleHealth(100);
        await Until(() => laser.Casting, "the laser drone starts its windup", 4);
        _player.TeleportTo(OpenSpot(18));
        await Frames(2);
        Check(laser.Casting && laser.Casts == 0, "the survivor runs off mid-windup: the drone keeps aiming");
        await Until(() => laser.Casts == 1, "the laser goes off first", 2);
        await Until(() => laser.Returning, "then it heads back", 1);
        await Clear();

        // ---------- a swarmed sidekick falls and gets up ----------
        var buddyKey = "sidekick:guard";
        var guard = _rules.Companions.Add(_player, buddyKey)!;
        await Frames(10);
        for (int i = 0; i < 10; i++)
        {
            var e = await Common(guard.GlobalPosition + new Vector3(Mathf.Cos(i * .63f) * .7f, 0, Mathf.Sin(i * .63f) * .7f));
            e.ScaleHealth(100);
        }
        await Until(() => guard.Down, "monsters crowding the sidekick wear it down until it falls", 25);
        Check(!guard.Visible, "it is gone in a burst of stuffing");
        _rules.ClearEnemies();
        await Until(() => !guard.Down && guard.Revives == 1, $"it gets up again after {Companion.SidekickRevive} s", Companion.SidekickRevive + 2);
        Check(guard.Visible && Mathf.IsEqualApprox(guard.Health, Companion.SidekickHealth) && Flat(guard.GlobalPosition, _player.GlobalPosition) < 4,
            "beside its survivor, at full health");
        await Clear();

        // ---------- the model quality switches live ----------
        GameSettings.PetQuality = PetModelQuality.High;
        var p2 = _rules.Companions.Add(_player, "pet:turtle:rocket_pod")!;
        var d2 = _rules.Companions.Add(_player, "drone:ufo:support")!;
        var s2 = _rules.Companions.Add(_player, "sidekick:scout")!;
        await Frames(5);
        Check(p2.Pet!.Quality == PetModelQuality.High && d2.Drone!.Quality == PetModelQuality.High, "companions start at the setting's quality");
        GameSettings.PetQuality = PetModelQuality.Low;
        await Frames(2);
        Check(p2.Pet!.Quality == PetModelQuality.Low && d2.Drone!.Quality == PetModelQuality.Low && s2.Sidekick!.Quality == PetModelQuality.Low &&
              p2.Pet.Weapon != null && d2.Drone.Equipment != null, "switching it mid-match swaps their models at once, gear kept");
        _stage.Hud.SetPaused(true);
        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _stage.Hud.OpenGraphicsOptions();
        var pause = _stage.Hud.FindChild("Graphics_Pet_Low", true, false) as Button;
        Check(pause != null && pause.IsVisibleInTree() && pause.Text.Contains(Loc.Text("settings_quality_low")), "the pause menu shows the setting: " + pause?.Text);
        var high = (Button)_stage.Hud.FindChild("Graphics_Pet_High",true,false);
        high.EmitSignal(BaseButton.SignalName.Pressed);
        Check(GameSettings.PetQuality == PetModelQuality.High && p2.Pet!.Quality == PetModelQuality.High && high.ButtonPressed && !pause!.ButtonPressed,
            "and its button switches it back mid-match");
        _stage.Hud.SetPaused(false);
        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        // ---------- the squad list round-trips (what a client is sent) ----------
        var w = new NetWriter();
        _rules.Companions.Write(w);
        var copy = new CompanionSquad { Name = "Copy", Stage = _stage };
        AddChild(copy);
        copy.Read(new NetReader(w.ToArray()));
        await Frames(2);
        Check(copy.All.Select(c => c.Spec.Key).SequenceEqual(_rules.Companions.All.Select(c => c.Spec.Key)) && copy.All.All(c => c.Survivor == _player),
            "a client builds the same companions for the same survivors");
        copy.QueueFree();

        if (Windowed)
        {
            // A fight with one of each for the render review.
            await Clear();
            foreach (var key in new[] { "pet:dog:twin_blaster", "drone:bee:laser", "sidekick:cadet", "drone:quadcopter:blaster" }) _rules.Companions.Add(_player, key);
            _player.TeleportTo(start);
            await Frames(20);
            _rules.SpawnRing(8, 6, 9);
            _stage.Cam.Close = true;
            await Frames(60);
            await Shot("fight");
            _stage.Cam.Close = false;
        }
    }
}
