using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Windowed check of the real input path (input actions, mouse aim, interact, pause) that the headless
/// suite bypasses with ManualControl. Needs window focus; reports SKIPPED if the OS did not give it.
/// Run: Godot --path . res://tests/InputCheck.tscn
/// </summary>
public partial class InputCheck : Node
{
    private readonly List<string> _passed = new();

    public override async void _Ready()
    {
        try
        {
            var stage = new Stage { CharacterOverride = "test_capsule", DirectorEnabled = false, ProcessMode = ProcessModeEnum.Pausable };
            AddChild(stage);
            await Frames(10);
            if (!GetWindow().HasFocus())
            {
                GetWindow().GrabFocus();
                await Frames(10);
            }
            if (!GetWindow().HasFocus())
            {
                GD.Print("INPUT_CHECK_SKIPPED window has no focus");
                QuitHygiene.Collect();
                GetTree().Quit(2);
                return;
            }
            var player = stage.Player;

            var start = player.GlobalPosition;
            Send(GameInput.MoveRight, true);
            await Frames(30);
            Send(GameInput.MoveRight, false);
            Check(player.GlobalPosition.X > start.X + .5f, "move action moves the player");

            var target = new Vector3(player.GlobalPosition.X, Player.AimHeight, player.GlobalPosition.Z + 4);
            // Re-warp every frame: a real mouse moving over the window must not win the race.
            for (int i = 0; i < 5; i++)
            {
                Input.WarpMouse(stage.Cam.UnprojectPosition(target));
                await Frames(1);
            }
            Check(player.AimDirection.Z > .9f, "mouse position drives the aim direction");

            player.GlobalPosition = new Vector3(8.6f, 0, 0);
            Input.WarpMouse(stage.Cam.UnprojectPosition(new Vector3(12, Player.AimHeight, 0)));
            await Frames(4);
            Send(GameInput.Interact, true);
            await Frames(2);
            Send(GameInput.Interact, false);
            await Frames(2);
            Check(stage.Objective == Objective.ReachShutter && stage.Layout.StartDoor.IsOpen, "interact key opens the safe-room door");

            int shots = player.ShotsFired;
            Send(GameInput.Fire, true);
            await Frames(20);
            Send(GameInput.Fire, false);
            Check(player.ShotsFired > shots, "fire action swings the starting melee weapon");

            player.Weapons.Pickup(ArmoryCatalog.Weapon("m4"), null);
            Send(GameInput.Slot1, true);
            await Frames(2);
            Send(GameInput.Slot1, false);
            Check(player.Loadout.ActiveSlot == 1, "key 1 switches to the secondary weapon");
            Send(GameInput.Slot2, true);
            await Frames(2);
            Send(GameInput.Slot2, false);
            Check(player.Loadout.ActiveSlot == 2, "key 2 switches to the primary weapon");

            int mag = player.Gun.Mag;
            Send(GameInput.Fire, true);
            await Frames(20);
            Send(GameInput.Fire, false);
            Check(player.Gun.Mag < mag, "fire action shoots the rifle");

            Send(GameInput.Reload, true);
            await Frames(2);
            Send(GameInput.Reload, false);
            Check(player.Gun.Reloading, "reload action starts a reload");

            player.Loadout.AddThrowable(ArmoryCatalog.Throwable("frag"));
            Send(GameInput.Throw, true);
            await Frames(2);
            Send(GameInput.Throw, false);
            Check(player.Weapons.AimingThrow && player.Loadout.CurrentThrowable != null, "G starts aiming the grenade");
            Send(GameInput.AltFire, true);
            await Frames(2);
            Send(GameInput.AltFire, false);
            Check(!player.Weapons.AimingThrow, "right click cancels the aim");
            Send(GameInput.Throw, true);
            await Frames(2);
            Send(GameInput.Throw, false);
            Send(GameInput.Fire, true);
            await Frames(2);
            Send(GameInput.Fire, false);
            Check(player.Loadout.CurrentThrowable == null && !player.Weapons.AimingThrow, "left click throws and returns to the gun");

            Send(GameInput.Dodge, true);
            await Frames(2);
            Send(GameInput.Dodge, false);
            Check(player.IsRolling || player.RollCooldownLeft > 0, "Space starts a dodge roll");
            await Frames(40);

            Send(GameInput.Pause, true);
            Send(GameInput.Pause, false);
            await Frames(3);
            var frozen = player.GlobalPosition;
            Send(GameInput.MoveRight, true);
            await Frames(20);
            Send(GameInput.MoveRight, false);
            Check(GetTree().Paused && stage.Hud.Paused && player.GlobalPosition == frozen, "Esc pauses the game");
            Send(GameInput.Pause, true);
            Send(GameInput.Pause, false);
            await Frames(3);
            Check(!GetTree().Paused, "Esc again resumes");

            GD.Print("INPUT_CHECK_PASS " + _passed.Count);
            QuitHygiene.Collect();
            GetTree().Quit(0);
        }
        catch (Exception e)
        {
            GD.Print("INPUT_CHECK_FAIL " + e.Message);
            GetTree().Paused = false;
            QuitHygiene.Collect();
            GetTree().Quit(1);
        }
    }

    /// <summary>Feed an action through the normal event pipeline, like a real key press.</summary>
    private static void Send(string action, bool pressed)
    {
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = pressed, Strength = pressed ? 1 : 0 });
        Input.FlushBufferedEvents();
    }

    private void Check(bool ok, string name)
    {
        if (!ok) throw new Exception(name);
        _passed.Add(name);
        GD.Print("PASS: " + name);
    }

    private async Task Frames(int n)
    {
        // The tree keeps emitting frame signals while paused (this node is set to Always).
        for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    public override void _EnterTree() => ProcessMode = ProcessModeEnum.Always;
}
