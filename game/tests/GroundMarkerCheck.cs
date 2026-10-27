using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class GroundMarkerCheck : Node
{
    private int _checks;
    private bool Windowed => DisplayServer.GetName() != "headless";
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Shot(string name)
    {
        if (!Windowed) return;
        await Frames(3);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        image.SavePng(ProjectSettings.GlobalizePath($"res://output/ground-marker-{name}.png"));
    }
    private static GroundMarker[] Markers(Stage stage) => stage.Effects.GetChildren().OfType<GroundMarker>().Where(m => !m.IsQueuedForDeletion()).ToArray();
    private static void Freeze(Node node)
    {
        node.SetProcess(false);
        node.SetPhysicsProcess(false);
        foreach (var child in node.GetChildren()) Freeze(child);
    }
    public override async void _Ready()
    {
        bool failed = false;
        try
        {
            var stage = new Stage { DirectorEnabled = false, CharacterOverride = "goose" };
            AddChild(stage);
            await Frames(5);
            stage.Player.ManualControl = true;
            stage.Player.Invulnerable = true;
            stage.LeaveSafeRoom();
            stage.Player.GlobalPosition = new Vector3(21, 0, .5f);
            stage.Player.SetControls(Vector2.Zero, stage.Player.GlobalPosition + new Vector3(4, 1, -2), false);
            stage.Cam.SnapTo(stage.Player.GlobalPosition);
            await Frames(20);
            Freeze(stage);
            var preview = ThrowPreview.Create(stage);
            var feet = stage.Player.GlobalPosition;
            var from = feet + Vector3.Up;
            var aim = feet + new Vector3(4, 0, -2);
            var frag = ArmoryCatalog.Throwable("frag");
            preview.Show(frag, from, aim, feet, 0, stage.Tiers);
            var ground = preview.GetChildren().OfType<MeshInstance3D>().Where(m => m.Mesh is TorusMesh or PlaneMesh).ToArray();
            var bounds = ground.Select(m => m.GetAabb().Size * m.Scale).ToArray();
            preview.Show(frag, from, aim, feet, .17f, stage.Tiers);
            Check(ground.Length >= 2 && ground.Select((m, i) => (m.GetAabb().Size * m.Scale).IsEqualApprox(bounds[i])).All(same => same),
                "throw landing and reach boundaries do not pulse in size as time passes");
            var landing = preview.GetNode<GroundMarker>("Landing");
            var reach = preview.GetNode<GroundMarker>("Reach");
            Check(landing.Footprint.IsEqualApprox(Vector2.One * frag.AimRadius * 2) && reach.Footprint.IsEqualApprox(Vector2.One * frag.MaxRange * 2), "grenade marker boundary matches effect radius and maximum reach");
            Check(preview.Target.IsEqualApprox(ThrownItem.ClampTarget(from, aim, frag.MaxRange)) && Mathf.IsEqualApprox(landing.GlobalPosition.Y, .06f), "grenade keeps target and floor offset");
            await Shot("grenade");
            var mortar = ArmoryCatalog.Weapon("mortar");
            preview.ShowLob(mortar, from, aim, feet, .25f, stage.Tiers);
            Check(landing.Footprint.IsEqualApprox(Vector2.One * mortar.ExplosionRadius * 2) && reach.Footprint.IsEqualApprox(Vector2.One * mortar.MaxRange * 2), "lob preview uses weapon blast radius and reach");
            await Shot("mortar-aim");
            preview.QueueFree();
            await Frames(3);
            var pickup = WeaponPickup.ForWeapon(stage, feet + new Vector3(3, 0, 1), ArmoryCatalog.Weapon("m4"));
            var loot = LootPickup.Spawn(stage, feet + new Vector3(5, 0, 1), InvItem.Medkit());
            Check(pickup.GetChildren().OfType<GroundMarker>().Single().Footprint == Vector2.One && loot.GetChildren().OfType<GroundMarker>().Single().Footprint == Vector2.One * .8f, "weapon and loot markers keep their small identification footprints");
            await Shot("pickups");
            pickup.QueueFree(); loot.QueueFree();
            var shell = MortarShell.Fire(stage, from, aim, mortar, stage.Player);
            shell.SetPhysicsProcess(false);
            var impact = Markers(stage).Single();
            shell._PhysicsProcess(mortar.FlightTime * .5f);
            Check(impact.Footprint.IsEqualApprox(Vector2.One * mortar.ExplosionRadius * 2) && Mathf.IsEqualApprox(((ShaderMaterial)impact.MaterialOverride).GetShaderParameter("progress").AsSingle(), .5f), "in-flight mortar shows actual blast boundary and half-flight progress");
            await Shot("mortar-incoming");
            shell.QueueFree(); await Frames(3);
            Check(Markers(stage).Length == 0, "removing the shell removes its independent floor marker");
            foreach (string id in new[] { "bull", "spitter", "armed_animal", "tether", "leaper" })
            {
                var enemy = stage.Spawn(MonsterDatabase.Get(id), feet + new Vector3(5, 0, -3), false);
                enemy.SetPhysicsProcess(false);
                enemy.Visible = true;
                if (enemy is BullEnemy bull) bull.StartWindup((feet - enemy.GlobalPosition) with { Y = 0 });
                else if (enemy is RangedEnemy ranged) ranged.StartWindup(stage.Player);
                else ((CaptureEnemy)enemy).StartWindup(stage.Player);
                var marker = Markers(stage).Single();
                Check(marker.CastShadow == GeometryInstance3D.ShadowCastingSetting.Off && marker.Footprint.X > 0 && marker.Footprint.Y > 0, id + " creates a visible floor warning without shadows");
                if (id == "spitter") Check(marker.Footprint.IsEqualApprox(Vector2.One * enemy.Monster.Ranged!.HazardRadius * 2), "poison windup matches hazard radius");
                await Shot(id);
                enemy.Stun(2); await Frames(3);
                Check(Markers(stage).Length == 0, id + " interruption removes the warning");
                enemy.QueueFree(); await Frames(3);
            }
            foreach (var kind in new[] { BossSkillKind.Slam, BossSkillKind.FanShot })
            {
                var boss = (BossEnemy)stage.Spawn(MonsterDatabase.Get("boss"), feet + new Vector3(5, 0, -3), true);
                boss.SetPhysicsProcess(false);
                boss.Visible = true;
                var skill = boss.Data.Skills.Values.First(s => s.Kind == kind);
                Check(boss.StartSkill(kind), kind + " starts");
                var markers = Markers(stage);
                var footprints = markers.Select(m => m.Footprint).ToArray();
                if (kind == BossSkillKind.Slam) Check(markers.Single().Footprint.IsEqualApprox(Vector2.One * skill.Radius * 2), "slam boundary matches damage radius immediately");
                else Check(markers.Length == skill.Count && markers.All(m => Mathf.IsEqualApprox(m.Footprint.Y, skill.Range)), "all fan lanes show their full range immediately");
                boss._PhysicsProcess(skill.Windup * .5f);
                Check(markers.Select((m, i) => m.Footprint.IsEqualApprox(footprints[i]) && Mathf.IsEqualApprox(((ShaderMaterial)m.MaterialOverride).GetShaderParameter("progress").AsSingle(), .5f)).All(ok => ok), kind + " fills to half without moving the boundary");
                await Shot(kind.ToString().ToLowerInvariant());
                boss.Stun(2); await Frames(3);
                Check(Markers(stage).Length == 0 && !boss.Casting, kind + " interruption removes every warning");
                boss.QueueFree(); await Frames(3);
            }
            // A bright raised test platform in the real stage: same markers, different contrast and floor height.
            var raisedAt = aim with { Y = TierMap.Height };
            Mat.Box(stage, raisedAt - Vector3.Up * .1f, new Vector3(12, .2f, 10), Mat.Solid("e8dfc9"));
            stage.Tiers.PaintTier(aim.X - 6, aim.X + 6, aim.Z - 5, aim.Z + 5, 1);
            var highPreview = ThrowPreview.Create(stage);
            highPreview.Show(frag, from, raisedAt, feet, 0, stage.Tiers);
            Check(Mathf.IsEqualApprox(highPreview.Target.Y, TierMap.Height + .2f) && Mathf.IsEqualApprox(highPreview.GetNode<GroundMarker>("Landing").GlobalPosition.Y, TierMap.Height + .06f), "throw marker stays above the target tier");
            await Shot("bright-tier-grenade");
            highPreview.QueueFree(); await Frames(3);
            var highSpitter = (RangedEnemy)stage.Spawn(MonsterDatabase.Get("spitter"), raisedAt + new Vector3(2, 0, -1), false);
            highSpitter.SetPhysicsProcess(false);
            highSpitter.Visible = true;
            highSpitter.StartWindup(aim: raisedAt + Vector3.Up * .8f);
            Check(Mathf.IsEqualApprox(Markers(stage).Single().GlobalPosition.Y, TierMap.Height + .055f), "enemy warning stays above the locked target tier");
            await Shot("bright-tier-danger");
            highSpitter.QueueFree(); await Frames(3);
            Check(Markers(stage).Length == 0, "removing the enemy also removes the warning");
            // Retain the scene through the renderer's in-flight frames before freeing its resources.
            await Frames(15);
            stage.QueueFree();
            await Frames(3);
            GD.Print("GROUND_MARKER_PASS " + _checks);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("GROUND_MARKER_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }
}
