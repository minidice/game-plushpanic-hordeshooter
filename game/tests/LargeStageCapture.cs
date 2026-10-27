using Godot;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Renders the large campaign drafts (L-30) from the default outdoor camera: one spot per zone of the new Stage 1 and 2,
/// and the moved button and ladder spots (L-32) of all three, to output/large-stage{n}-*.png. Needs a window.
/// The safe room and mission doors are opened first so the views show the way on.
/// </summary>
public partial class LargeStageCapture : Node
{
    private static readonly (int stage, string name, Vector2 at)[] Spots =
    {
        (1, "a-saferoom", new(6, 0)), (1, "b-street", new(70, -14)), (1, "b-terrace", new(114, -32)), (1, "c-alley", new(172, -64)),
        (1, "d-depot", new(250, -82)), (1, "d-panel", new(300, -114)), (1, "d-stack-ladder", new(269, -90)), (1, "e-post", new(352, -89)),
        (1, "f-yard", new(440, -104)), (1, "g-overpass", new(556, -124)), (1, "h-safehouse", new(612, -133)),
        (1, "c-alarm-car", new(170, -58)), (1, "d-panel-pocket", new(300, -108)), (1, "f-alarm-truck", new(496, -104)), (1, "b-warehouse-sign", new(200, -76)), (1, "c-alley-east", new(186, -70)),
        (2, "a-drain", new(10, 0)), (2, "b-slope", new(90, -14)), (2, "b-cabin-ladder", new(150, -58)), (2, "c-ford", new(190, -46)),
        (2, "d-bunker", new(250, -62)), (2, "d-panel", new(276, -106)), (2, "e-post", new(379, -71)), (2, "f-hill", new(460, -86)),
        (2, "f-ladder-east", new(444, -108)), (2, "g-safehouse", new(594, -109)),
        (3, "c-return-ladder", new(190, -13)), (3, "g-safehouse", new(630, -143)),
    };

    public override async void _Ready()
    {
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
        foreach (int n in Spots.Select(s => s.stage).Distinct())
        {
            var map = LayoutMap.ForStage(n);
            var stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", ModeOverride = GameMode.Scenario, MapOverride = map, DirectorEnabled = false, SquadOverride = 0 };
            AddChild(stage);
            await Frames(4);
            stage.Player.ManualControl = true;
            stage.Player.Invulnerable = true;
            map.StartDoor?.SetOpen(true, 0);
            foreach (var (_, name, at) in Spots.Where(s => s.stage == n))
            {
                foreach (var e in stage.AllEnemies.ToList()) if (IsInstanceValid(e)) e.QueueFree();
                var ground = new Vector3(at.X, stage.Tiers.GroundY(new Vector3(at.X, 0, at.Y)) + .05f, at.Y);
                stage.Player.TeleportTo(ground);
                stage.Player.SetControls(Vector2.Zero, ground + new Vector3(8, 1, -4), false);
                stage.Cam.SnapTo(ground);
                await Frames(20);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var path = ProjectSettings.GlobalizePath($"res://output/large-stage{n}-{name}.png");
                GetViewport().GetTexture().GetImage().SavePng(path);
                GD.Print("SHOT: " + path);
            }
            stage.Free();
            await Frames(2);
        }
        QuitHygiene.Collect();
        GetTree().Quit(0);
    }

    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
}
