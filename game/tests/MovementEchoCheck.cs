using Godot;
using System;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class MovementEchoCheck : Node
{
    private async Task Frames(int n)
    { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    public override async void _Ready()
    {
        bool failed = false;
        try
        {
            var lab = new CardLab(); AddChild(lab); await Frames(3);
            var player = lab.Stage.Player; player.ManualControl = true;
            lab.Rules.Cards.Set(LabSpecial.DoubleSpecial, true);
            player.Loadout.Equip(3, new WeaponState(ArmoryCatalog.Weapon("alabong")));
            player.Weapons.SelectSlot(3);
            await Frames(3);
            player.SetControls(Vector2.Zero, player.GlobalPosition + new Vector3(12, 1, 0), true);
            await Frames(2);
            player.SetControls(Vector2.Zero, player.AimPoint, false);
            for (int i = 0; i < 100 && player.Weapons.Echoes == 0; i++) await Frames(1);
            if (player.Weapons.Echoes != 1 || player.AutoRunDelayLeft < 1.9f)
                throw new Exception($"actual follow-up shot must restart the quiet delay (echoes {player.Weapons.Echoes}, delay {player.AutoRunDelayLeft:0.00})");
            GD.Print("MOVEMENT_ECHO_PASS 1");
        }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("MOVEMENT_ECHO_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect(); GetTree().Quit(failed ? 1 : 0);
    }
}
