using Godot;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Strongest blasts beside Stage 1's walls and fence (I-70): some corpses fly over a wall. None may be left falling
/// below the level after the next cleanup pass; the ones inside stay where they landed.
/// Run: Godot --headless --path . res://tests/CorpseBoundsCheck.tscn
/// </summary>
public partial class CorpseBoundsCheck : Node
{
    public override async void _Ready()
    {
        var stage = new Stage { CharacterOverride = "goose", DirectorEnabled = false };
        AddChild(stage);
        await Frames(5);
        foreach (var e in stage.AllEnemies.ToList()) e.QueueFree();
        stage.Player.ManualControl = true;
        stage.Player.Invulnerable = true;
        stage.LeaveSafeRoom();
        stage.Player.TeleportTo(new Vector3(4, 0, 0));
        await Frames(2);
        // Street (tall wall z -6, low fence z 6), warehouse (tall wall z -10), yard (wall z -7).
        var spots = new[] { new Vector3(30, 0, -4.8f), new Vector3(30, 0, 4.8f), new Vector3(66, 0, -8.6f), new Vector3(100, 0, -5.8f) };
        foreach (var at in spots)
        {
            var group = Enumerable.Range(0, 6).Select(i => stage.SpawnEnemy<Enemy>(stage.CommonDefinition, at + new Vector3(-1.2f + i % 3 * 1.2f, 0, i / 3 * .8f - .4f), awake: false)).ToList();
            await Frames(3);
            Blast.Explode(stage, at + new Vector3(0, 0, at.Z > 0 ? -1.2f : 1.2f), 5.2f, 10000, Colors.Orange, knockbackPower: 150f);
        }
        // Follow every corpse from the blast on: it either stays in the level or is removed after falling out.
        await Frames(2);
        var tracked = stage.Effects.GetChildren().OfType<Corpse>().Select(c => (body: c, lowest: c.GlobalPosition.Y)).ToList();
        for (int f = 0; f < 60 * 8; f++)
        {
            await Frames(1);
            for (int i = 0; i < tracked.Count; i++)
                if (IsInstanceValid(tracked[i].body) && !tracked[i].body.IsQueuedForDeletion())
                    tracked[i] = (tracked[i].body, Mathf.Min(tracked[i].lowest, tracked[i].body.GlobalPosition.Y));
        }
        var gone = tracked.Where(t => !IsInstanceValid(t.body) || t.body.IsQueuedForDeletion()).ToList();
        var kept = tracked.Except(gone).ToList();
        foreach (var (body, _) in kept)
        {
            var p = body.GlobalPosition;
            GD.Print($"corpse at ({p.X:0.0}, {p.Y:0.0}, {p.Z:0.0}){(p.Y > 2.5f ? " on top of a prop" : "")}");
        }
        GD.Print($"blast result: corpses={tracked.Count} removed-after-falling-out={gone.Count} kept={kept.Count}");
        bool ok = tracked.Count >= 20 && kept.All(t => t.body.GlobalPosition.Y > -1)
                  && gone.All(t => t.lowest < Effects.FellOutY) && stage.Effects.CommonCorpseCount == kept.Count;
        GD.Print((ok ? "PASS: " : "FAIL: ") + $"corpses thrown out of the level are removed, the rest stay ({kept.Count} kept, {gone.Count} removed below {Effects.FellOutY} m)");
        GD.Print(ok ? "CORPSE_BOUNDS_PASS 1" : "CORPSE_BOUNDS_FAIL");
        QuitHygiene.Collect();
        GetTree().Quit(ok ? 0 : 1);
    }

    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
}
