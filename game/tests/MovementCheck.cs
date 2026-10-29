using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class MovementCheck : Node
{
    private Stage _stage = null!;
    private Player P => _stage.Player;
    private int _checks;
    private void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        _checks++; GD.Print("PASS: " + message);
    }
    private async Task Frames(int n)
    {
        for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }
    private void Place()
    {
        P.TeleportTo(new Vector3(30, 0, 0));
        P.SetControls(Vector2.Right, new Vector3(40, 1, 0), false);
    }
    private bool Speed(float expected) => Mathf.Abs(new Vector2(P.Velocity.X, P.Velocity.Z).Length() - expected) < .04f;
    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("MOVEMENT_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("MOVEMENT_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect(); GetTree().Quit(failed ? 1 : 0);
    }
    private async Task Run()
    {
        _stage = new Stage { CharacterOverride = "test_capsule", DirectorEnabled = false };
        AddChild(_stage);
        P.ManualControl = true;
        foreach (var enemy in _stage.AllEnemies.ToArray()) enemy.QueueFree();
        await Frames(4);
        Place();
        P.TakeDamage(1, Vector3.Zero);
        await Frames(10);
        Check(Speed(3.6f), "accepted damage immediately returns movement to 3.6m/s walking");
        await Frames(115);
        Check(Speed(4.2f), "two quiet seconds switch to 4.2m/s automatic running");
        P.HoldAim(true);
        await Frames(140);
        Check(Speed(3.6f * P.HandlingSpeedScale), "holding aim never becomes automatic running");
        P.HoldAim(false); Place();
        await Frames(90);
        Check(Speed(3.6f), "releasing aim waits two seconds before running");
        await Frames(35);
        Check(Speed(4.2f), "run resumes after aim release delay");
        int shots = P.ShotsFired;
        P.SetControls(Vector2.Right, new Vector3(40, 1, 0), true);
        await Frames(2);
        P.SetControls(Vector2.Right, new Vector3(40, 1, 0), false);
        Check(P.ShotsFired > shots, "fixture actually fired a round");
        await Frames(15);
        Check(Speed(3.6f), "a real shot cancels automatic running");
        await Frames(115);
        Check(Speed(4.2f), "run returns after last shot delay");
        Place(); P.PressRoll(); await Frames(3);
        int health = P.Health;
        P.TakeDamage(7, Vector3.Left);
        Check(P.IsRolling && P.Health == health - 7, "ordinary damage applies during a roll without cancelling it");
        Check(Mathf.Abs(P.RollCooldownLeft - 6) < .02f, "first recharge does not advance before the roll ends");
        await Frames(26);
        float recharge = P.RollCooldownLeft;
        Check(!P.IsRolling && recharge > 5.8f, "first recharge starts at the end of the roll");
        Place(); P.PressRoll(); await Frames(3);
        Check(P.IsRolling, "the second stored roll can be used immediately");
        Check(P.RollCooldownLeft < recharge && P.RollCooldownLeft > recharge - .15f, "second roll neither pauses nor resets existing recharge");
        await Frames(26);
        Place(); P.PressRoll(); await Frames(2);
        Check(!P.IsRolling, "third roll is refused before one charge recovers");
        await Frames(35);
        Check(Speed(3.6f), "roll completion returns to normal walking");
        await Frames(90);
        Check(Speed(4.2f), "roll recovery does not prevent automatic running after two seconds");
        P.SetControls(Vector2.Zero, P.AimPoint, false);
        await Frames(215);
        Check(P.RollCooldownLeft > 5.5f && P.RollCooldownLeft <= 6, "only one charge returns after six seconds and the next starts sequentially");
        Place(); P.PressRoll(); await Frames(3);
        Check(P.IsRolling, "the recovered charge is usable while the next recovers");
        await Frames(26);
        P.SetControls(Vector2.Zero, P.AimPoint, false);
        await Frames(730);
        Check(P.RollCooldownLeft == 0, "both missing charges eventually recover without overflowing");
        Check(P.RollCharges == 2, "charge capacity stays at two");
        P.Loadout.SetSlot(2, ArmoryCatalog.Weapons.Values.First(d => d.Category == WeaponCategory.Smg));
        P.Weapons.SelectSlot(2); Place(); await Frames(130);
        Check(Speed(4.2f), "positive weapon speed bonuses cannot make running faster than the agreed 4.2m/s ceiling");
        P.Weapons.SelectSlot(1);
        if (DisplayServer.GetName() != "headless")
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/movement-full.png"));
            P.PressRoll(); await Frames(30);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/movement-one.png"));
            P.PressRoll(); await Frames(30);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/movement-zero.png"));
        }
        // Keep the real roll active and the player within the melee arc: movement alone cannot hide immunity.
        P.SetPhysicsProcess(false);
        P.TeleportTo(new Vector3(4.05f, .05f, 0));
        P.SetControls(Vector2.Zero, new Vector3(3, 1, 0), false);
        P.SetPhysicsProcess(true);
        await Frames(730);
        P.PressRoll(); await Frames(2);
        P.SetPhysicsProcess(false);
        P.GlobalPosition = new Vector3(4.05f, .05f, 0);
        int meleeHealth = P.Health;
        var attacker = _stage.Spawn(MonsterDatabase.Get("humanoid"), new Vector3(3, 0, 0), true);
        for (int i = 0; i < 100 && P.Health == meleeHealth; i++) await Frames(1);
        Check(P.IsRolling && P.Health < meleeHealth, "a real enemy melee impact damages an in-range rolling survivor");
        attacker.QueueFree();
        P.SetPhysicsProcess(true);
        await Frames(30);
    }
}
