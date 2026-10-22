using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Inspection only: unchanged live monsters and their actual death path, with fixed camera and lighting.</summary>
public partial class MonsterReviewCapture : Node
{
    private string _output = "monster-review";
    private sealed class ReviewFloor : StageMap
    {
        public override Rect2 Area => new(-25, -25, 50, 50);
        protected override void BuildMap() { PlayerStart = new Vector3(0, 0, 20); Floor(-24, 24, -24, 24, "4a5560"); }
    }
    private async Task Frames(int count) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private async Task Shot(string id, string phase)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        if (image.SavePng(ProjectSettings.GlobalizePath($"res://output/{_output}/{id}-{phase}.png")) != Error.Ok)
            throw new Exception("Screenshot failed");
    }
    private static (float min, float box) Bottom(Node node)
    {
        float min = float.PositiveInfinity, box = float.PositiveInfinity;
        if (node is MeshInstance3D mesh && mesh.IsVisibleInTree() && mesh.Mesh != null)
        {
            for (int c = 0; c < 8; c++) box = Mathf.Min(box, (mesh.GlobalTransform * mesh.GetAabb().GetEndpoint(c)).Y);
            for (int s = 0; s < mesh.Mesh.GetSurfaceCount(); s++)
                foreach (var v in mesh.Mesh.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                    min = Mathf.Min(min, (mesh.GlobalTransform * v).Y);
        }
        foreach (Node child in node.GetChildren()) { var b = Bottom(child); min = Mathf.Min(min, b.min); box = Mathf.Min(box, b.box); }
        return (min, box);
    }
    public override async void _Ready()
    {
        try
        {
            if (OS.GetCmdlineUserArgs().Contains("--after")) _output = "monster-review-after";
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath($"res://output/{_output}"));
            var stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", ModeOverride = GameMode.Survivor,
                MapOverride = new ReviewFloor(), RulesOverride = new CardLabRules { Name = "CardLabRules" }, DirectorEnabled = false };
            AddChild(stage);
            await Frames(4);
            stage.Player.ManualControl = true; stage.Player.Invulnerable = true; stage.Player.Visible = false;
            stage.Hud.Visible = false; stage.Vision.SetPhysicsProcess(false);
            stage.Cam.SetPhysicsProcess(false); stage.Cam.SetProcess(false);
            stage.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, -30, 0), LightEnergy = 1.4f, ShadowEnabled = true });
            var camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 7.5f };
            AddChild(camera); camera.MakeCurrent();
            var focus = new Vector3(0, .9f, -.4f);
            camera.Position = focus + new Vector3(7, 13, 8); camera.LookAt(focus);
            string[] ids = { "bull", "balloon", "tether", "leaper", "spitter", "armed_veteran", "boss", "boss_bull_giant", "boss_balloon_king", "boss_spitter_mother" };
            foreach (var id in ids)
            {
                if (stage.Effects.GetChildren().OfType<NamedCorpseRecord>().Any())
                    throw new Exception("Previous named corpse was not cleared between inspection subjects");
                var enemy = stage.Spawn(MonsterDatabase.Get(id), Vector3.Zero, awake: false);
                await Frames(3);
                enemy.SetPhysicsProcess(false); enemy.Awake = true;
                enemy.Rig.ShowAs(VisionSystem.Visibility.Visible); enemy.Rig.Face(Vector3.Back);
                await Frames(60);
                await Shot(id, "alive");
                enemy.Rig.Face(Vector3.Right); await Frames(2); await Shot(id, "side");
                enemy.Rig.Face(Vector3.Back);
                var rig = enemy.Rig;
                GD.Print($"MODEL {id} scale={rig.Model.Scale} collider={enemy.Definition.ColliderRadius},{enemy.Definition.ColliderHeight}");
                enemy.Die(new Vector3(0, 0, -1), 6);
                double elapsed = 0;
                foreach (var (count, phase) in new[] { (1, "hit"), (11, "fall"), (15, "land"), (24, "rest") })
                {
                    await Frames(count); elapsed += count / 60.0;
                    await Shot(id, phase);
                    var bottom = Bottom(rig);
                    GD.Print($"DEATH {id} {phase} nominalPhysicsSeconds={elapsed:0.000} vertexMinY={bottom.min:0.000} aabbMinY={bottom.box:0.000} headVisible={rig.Head?.Visible}");
                }
                camera.Position = focus + new Vector3(9, 3, 11); camera.LookAt(focus);
                await Shot(id, "rest-low");
                camera.Position = focus + new Vector3(7, 13, 8); camera.LookAt(focus);
                // Named corpses are children of their persistent records, not direct Corpse children of Effects.
                foreach (var record in stage.Effects.GetChildren().OfType<NamedCorpseRecord>()) record.QueueFree();
                foreach (var corpse in stage.Effects.GetChildren().OfType<Corpse>()) corpse.QueueFree();
                // Retain paint queue ownership but hide the previous subject's ground marks.
                foreach (var mesh in stage.Effects.GetChildren().OfType<MeshInstance3D>()) mesh.Visible = false;
                await Frames(65);
            }
            stage.QueueFree(); await Frames(3); QuitHygiene.Collect();
            GD.Print("MONSTER_REVIEW_CAPTURE_COMPLETE 10 models, 70 images; inspection, not animation assertions");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
