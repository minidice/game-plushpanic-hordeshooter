using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// A dense pile of 100 tumbling corpses must come to rest (I-71). Without assisted sleep about 85 of them kept nudging
/// each other awake for good (about 1.2 ms of physics every frame); now the whole pile sleeps, typically within 10–20 s (the check allows 30).
/// Run: Godot --headless --path . res://tests/CorpsePileCheck.tscn
/// </summary>
public partial class CorpsePileCheck : Node
{
    public override async void _Ready()
    {
        var stage = new Stage { CharacterOverride = "goose", DirectorEnabled = false };
        AddChild(stage);
        await Frames(3);
        foreach (var e in stage.AllEnemies.ToList()) e.QueueFree();
        stage.Player.ManualControl = true;
        stage.Player.SetPhysicsProcess(false);
        var bodies = new List<Corpse>();
        for (int i = 0; i < 100; i++)
        {
            var def = CharacterCatalog.Enemy(i % 4 == 0 ? "crawler" : "humanoid");
            var rig = CharacterRig.Create(def);
            AddChild(rig);
            rig.Position = new Vector3(65 + (i % 12 - 5.5f) * .9f, 1, (i / 12 - 5) * .8f);
            bodies.Add((Corpse)stage.Effects.SpawnCorpse(rig, def.ColliderRadius, def.ColliderHeight, new Vector3((i % 3 - 1) * 2, 2, 1), new Vector3(.7f, 1, .4f)));
        }
        int asleepAt = -1;
        for (int s = 1; s <= 30 && asleepAt < 0; s++)
        {
            await Frames(60);
            int awake = bodies.Count(b => !b.Sleeping);
            float fastest = bodies.Max(b => b.LinearVelocity.Length());
            float meanSpeed = bodies.Average(b => b.LinearVelocity.Length());
            int calmish = bodies.Count(b => !b.Sleeping && b.LinearVelocity.Length() < Corpse.CalmSpeed && b.AngularVelocity.Length() < Corpse.CalmSpin);
            float spin = bodies.Where(b => !b.Sleeping).Select(b => b.AngularVelocity.Length()).DefaultIfEmpty(0).Max();
            GD.Print($"t={s}s awake={awake} calm_but_awake={calmish} max_spin={spin:0.00} mean_speed={meanSpeed:0.000} max_speed={fastest:0.000}");
            if (awake == 0) asleepAt = s;
        }
        bool ok = asleepAt > 0;
        GD.Print((ok ? "PASS: " : "FAIL: ") + $"a dense pile of 100 corpses comes to rest ({(ok ? asleepAt + " s" : "still awake after 30 s")})");
        var one = bodies[50];
        one.Kick(Vector3.Right * 3, Vector3.Zero);
        await Frames(2);
        bool woke = !one.Sleeping && one.LinearVelocity.Length() > .5f;
        GD.Print((woke ? "PASS: " : "FAIL: ") + "a resting corpse still wakes when kicked");
        GD.Print(ok && woke ? "CORPSE_PILE_PASS 2" : "CORPSE_PILE_FAIL");
        QuitHygiene.Collect();
        GetTree().Quit(ok && woke ? 0 : 1);
    }

    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
}
