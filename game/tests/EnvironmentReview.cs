using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Fixed before/after cameras and gameplay contracts for the environment art pass.
/// Run windowed for PNGs, headless for contracts. -- --baseline writes the pre-change contract;
/// ordinary runs compare it. -- --tag=before/after separates local captures; --services captures the three indoor exterior bays.</summary>
public partial class EnvironmentReview : Node
{
    private Stage _stage = null!;
    private int _checks;
    private readonly Dictionary<string, string> _contracts = new();
    private readonly List<object> _metrics = new();
    private bool Windowed => DisplayServer.GetName() != "headless";
    private string _tag = "after";
    private void Check(bool ok, string why) { if (!ok) throw new Exception(why); _checks++; GD.Print("PASS: " + why); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static IEnumerable<Node> Walk(Node n) { foreach (Node c in n.GetChildren()) { yield return c; foreach (var d in Walk(c)) yield return d; } }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public override async void _Ready()
    {
        bool failed = false;
        try
        {
            if(Windowed) { DisplayServer.WindowMoveToForeground(); OS.LowProcessorUsageMode=false; Engine.MaxFps=OS.GetCmdlineUserArgs().Contains("--uncapped")?0:120; }
            var args = OS.GetCmdlineUserArgs();
            _tag = args.FirstOrDefault(a => a.StartsWith("--tag="))?.Substring(6) ?? "after";
            if (_tag.Any(c => !char.IsLetterOrDigit(c) && c != '-')) throw new Exception("tag must be a file name fragment");
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
            await Review(1, GameMode.Scenario);
            await Review(2, GameMode.Scenario);
            await Review(1, GameMode.Survival);
            string contractPath = ProjectSettings.GlobalizePath("res://tests/environment-contracts.json");
            if (args.Contains("--baseline"))
            {
                if (System.IO.File.Exists(contractPath)) throw new Exception("baseline already exists; do not silently replace it");
                System.IO.File.WriteAllText(contractPath, JsonSerializer.Serialize(_contracts, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                var expected = JsonSerializer.Deserialize<Dictionary<string, string>>(System.IO.File.ReadAllText(contractPath))!;
                foreach (var (key, value) in expected) Check(_contracts.GetValueOrDefault(key) == value, key + " remains identical to the pre-art gameplay contract");
            }
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath($"res://output/environment-{_tag}-metrics.json"),
                JsonSerializer.Serialize(_metrics, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print("ENVIRONMENT_REVIEW_PASS " + _checks);
        }
        catch (Exception e) { GD.PushError(e.ToString()); failed = true; }
        if (IsInstanceValid(_stage)) _stage.Free();
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task Review(int number, GameMode mode)
    {
        if (IsInstanceValid(_stage)) { _stage.Free(); await Frames(2); }
        _stage = new Stage { MapOverride = number == 1 ? new StageOneLayout() : new StageTwoMap(), ModeOverride = mode,
            CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false };
        AddChild(_stage);
        _stage.Player.ManualControl = true;
        _stage.Player.Invulnerable = true;
        foreach (var door in _stage.Map.Doors) door.SetOpen(true, 0);
        _stage.Map.ApplyFlow();
        // Capture contracts before simulation can move physics props or a character.
        string prefix = $"stage{number}-{mode}";
        var cells = new StringBuilder();
        var area = _stage.Map.Area;
        for (float z = area.Position.Y + .5f; z < area.End.Y; z++)
            for (float x = area.Position.X + .5f; x < area.End.X; x++) cells.Append(_stage.Flow.IsBlocked(new Vector3(x, 0, z)) ? '1' : '0');
        _contracts[prefix + "-flow"] = Hash(cells.ToString());
        var bodies = Walk(_stage).OfType<StaticBody3D>().Where(b => (b.CollisionLayer & Layers.World) != 0);
        var shapes = bodies.SelectMany(b => b.GetChildren().OfType<CollisionShape3D>()
            .Select(c => $"{b.GlobalTransform}|{c.Transform}|{c.Shape.GetClass()}|" + (c.Shape is BoxShape3D box ? box.Size.ToString() : c.Shape is CylinderShape3D cy ? $"{cy.Radius},{cy.Height}" : "other")))
            .OrderBy(s => s, StringComparer.Ordinal).ToArray();
        _contracts[prefix + "-world"] = Hash(string.Join("\n", shapes));
        Check(shapes.Length > 0, prefix + " has world collisions");
        if (number == 2)
        {
            var river = new Rect2(60.3f,-28,7.4f,56);
            foreach(var ground in _stage.GetChildren().OfType<MeshInstance3D>().Where(m=>m.MaterialOverride is ShaderMaterial material && material.Shader.ResourcePath=="res://shaders/woodland_ground.gdshader"))
            {
                var plane=(PlaneMesh)ground.Mesh;
                var footprint=new Rect2(ground.Position.X-plane.Size.X/2,ground.Position.Z-plane.Size.Y/2,plane.Size.X,plane.Size.Y);
                Check(ground.Position.Y<-.38f || !river.Intersects(footprint.Grow(-.001f)),"woodland ground never covers stream water");
                Check(ground.Position.Y>-.03f && ground.Position.Y<0,"woodland continuation lies above soil fill and below the painted ground");
            }
            Check(!_stage.FindChildren("edge_*", nameof(MultiMeshInstance3D), true, false).Any(),
                "rectangular foliage-band meshes are absent from Stage 2");
            var cover=_stage.GetChildren().OfType<MultiMeshInstance3D>().Where(m=>m.Name.ToString().StartsWith("WoodlandGrass")).ToArray();
            Check(cover.Length>0&&cover.All(m=>m.CastShadow==GeometryInstance3D.ShadowCastingSetting.Off),
                "inaccessible ground has instanced grass without grass shadows");
            var floors = new[]{new Rect2(-2,-9,22,18),new Rect2(20,-7,40.3f,14),new Rect2(67.7f,-10,32.3f,20),new Rect2(100.4f,-3.8f,11.4f,7.6f)};
            foreach(var grove in (Windowed ? _stage.GetChildren() : new Godot.Collections.Array<Node>()).OfType<MultiMeshInstance3D>().Where(m=>m.Name.ToString().StartsWith("Woodland")))
                for(int i=0;i<grove.Multimesh.InstanceCount;i++)
                {
                    var box=grove.Multimesh.GetInstanceTransform(i)*grove.Multimesh.Mesh.GetAabb();
                    var footprint=new Rect2(box.Position.X,box.Position.Z,box.Size.X,box.Size.Z);
                    if(grove.Name.ToString().StartsWith("WoodlandGrass")&&river.Intersects(footprint))
                        throw new Exception("Grass blades cross stream water: "+box);
                    if(floors.Any(r=>r.Intersects(footprint)))
                        throw new Exception("Woodland crown overlaps playable floor: "+box);
                }
            if(Windowed)Check(true,"woodland crowns stay outside playable floors, including region corners");
        }
        if(number == 2 && _stage.FindChild("FarGround",true,false) is MeshInstance3D farGround)
            Check(farGround.GlobalPosition.Y + farGround.GetAabb().End.Y < -.38f,
                "the background ground lies below the stream water (-0.38m), not across its surface");
        foreach (var at in number == 1 ? new[] { new Vector3(10.2f, 0, 0), new Vector3(52.2f, 0, 0), new Vector3(84.2f, 0, 0), new Vector3(116.2f, 0, 0) }
            : new[] { _stage.Map.PlayerStart, new Vector3(64, 0, 0), new Vector3(100.2f, 0, 0) })
            Check(!_stage.Flow.IsBlocked(at), prefix + " open passage " + at);

        if (!Windowed || mode != GameMode.Scenario || OS.GetCmdlineUserArgs().Contains("--contracts")) return;
        bool foliage = OS.GetCmdlineUserArgs().Contains("--foliage");
        if(foliage && number!=2)return;
        bool services = OS.GetCmdlineUserArgs().Contains("--services");
        if (services && number != 1) return;
        if(number == 2)
        {
            var playable=new[]{new Rect2(-2,-9,22,18),new Rect2(20,-7,40.3f,14),new Rect2(67.7f,-10,32.3f,20),new Rect2(100.4f,-3.8f,11.4f,7.6f)};
            foreach(var part in _stage.GetChildren().OfType<MultiMeshInstance3D>().Where(m=>m.Name.ToString().StartsWith("CloseRockBank")))
                for(int i=0;i<part.Multimesh.InstanceCount;i++)
                {
                    var box=part.Multimesh.GetInstanceTransform(i)*part.Multimesh.Mesh.GetAabb();
                    var footprint=new Rect2(box.Position.X,box.Position.Z,box.Size.X,box.Size.Z);
                    Check(!playable.Any(r=>r.Intersects(footprint)),"close decorative rocks remain outside walkable floors: "+box.Position);
                }
        }
        // Freeze simulation, keep render/camera live; both runs use identical empty stages and poses.
        _stage.SetPhysicsProcess(false);
        _stage.Player.SetPhysicsProcess(false);
        foreach (var e in _stage.AllEnemies) e.SetPhysicsProcess(false);
        _stage.Cam.Target = null; _stage.Cam.Player = null;
        var shots = number == 1 ? new[] {
            ("room", new Vector3(5,0,0)), ("entry", new Vector3(51,0,0)), ("hall", new Vector3(66,0,0)),
            ("shutter", new Vector3(82,0,0)), ("yard", new Vector3(98,0,0)), ("hatch", new Vector3(122,0,0)),
            ("near", new Vector3(62,0,8.5f)) }
            : new[] { ("arrival", new Vector3(6,0,1.5f)), ("forest", new Vector3(38,0,0)), ("bridge", new Vector3(63,0,0)),
                ("yard", new Vector3(82,0,0)), ("near", new Vector3(38,0,5.7f)), ("far", new Vector3(38,0,-5.7f)), ("end", new Vector3(108,0,0)) };
        if (services) shots = new[] { ("street-service", new Vector3(29,0,-4.8f)),
            ("hall-service", new Vector3(68,0,-8.5f)), ("yard-service", new Vector3(101,0,-6.5f)) };
        if(foliage)shots=new[]{("forest",new Vector3(38,0,-3)),("far",new Vector3(38,0,-5.7f)),
            ("end",new Vector3(108,0,0)),("back",new Vector3(108,0,-3)),("near",new Vector3(38,0,5.7f))};
        foreach (var (name, at) in shots)
        {
            _stage.Player.TeleportTo(at);
            _stage.Player.SetControls(Vector2.Zero, at + Vector3.Right * 3, false);
            foreach (bool close in new[] { false, true })
            {
                _stage.Cam.Close = close;
                _stage.Cam.SnapTo(at + Vector3.Up * .8f);
                for (int i = 0; i < 90; i++) _stage.Cam._Process(1.0 / 60);
                await Frames(12);
                var samples = new List<double>();
                for (int i = 0; i < 20; i++) { ulong start = Time.GetTicksUsec(); await Frames(1); samples.Add((Time.GetTicksUsec() - start) / 1000.0); }
                samples.Sort();
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                string shot = $"stage{number}-{name}-{(close ? "close" : "wide")}";
                _metrics.Add(new { shot, medianFrameMs = samples[10], p95FrameMs = samples[18],
                    drawCalls = Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),
                    primitives = Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame) });
                _stage.GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://output/environment-{_tag}-{shot}.png"));
                GD.Print("CAPTURE " + shot);
            }
        }
    }
}
