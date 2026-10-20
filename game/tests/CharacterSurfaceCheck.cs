using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Actual GPU face-mask precision, plus close/quarter/game-size art review captures.</summary>
public partial class CharacterSurfaceCheck : Node
{
    private int _checks;
    private readonly bool _baseline = OS.GetCmdlineUserArgs().Contains("--baseline");
    private string Tag => _baseline ? "before" : "after";
    private void Check(bool ok, string message) { GD.Print((ok ? "PASS: " : "FAIL: ") + message); if (!ok && !_baseline) throw new Exception(message); _checks++; }
    public override async void _Ready()
    {
        try { await Run(); GD.Print((_baseline?"CHARACTER_SURFACE_BASELINE_CAPTURED ":"CHARACTER_SURFACE_PASS ") + _checks); QuitHygiene.Collect(); GetTree().Quit(); }
        catch (Exception e) { GD.PushError(e.ToString()); QuitHygiene.Collect(); GetTree().Quit(1); }
    }
    private async Task Run()
    {
        if (DisplayServer.GetName() == "headless") throw new Exception("This check requires a rendered viewport.");
        var world = new Node3D(); AddChild(world);
        var environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("172329"), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = Colors.White, AmbientLightEnergy = 1, TonemapMode = Godot.Environment.ToneMapper.Linear, ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled };
        world.AddChild(new WorldEnvironment { Environment = environment });
        var cam = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = .75f, Position = new Vector3(0, 1.34f, 6) }; world.AddChild(cam); cam.LookAt(new Vector3(0, 1.34f, 0));
        var mask = CharacterRig.Create(CharacterCatalog.Player("redpanda")); world.AddChild(mask);
        mask.ApplyAppearance(new AppearanceProfile { Body = "#000000", BodySecondary = "#000000", BodyMarking = "#ffffff", Hat = 0, Vest = false, Pack = false });
        foreach (var n in mask.Model.FindChildren("*", nameof(MeshInstance3D), true, false)) ((MeshInstance3D)n).Visible = n.Name == "redpanda_torso_mesh";
        await Frames(12);
        var shot = await Shot("mask");
        var tearPixel=cam.UnprojectPosition(new Vector3(.215f,1.265f,0))*new Vector2(shot.GetWidth(),shot.GetHeight())/GetViewport().GetVisibleRect().Size;
        Check(shot.GetPixel((int)tearPixel.X,(int)tearPixel.Y).SrgbToLinear().R<.15f,"red panda has a dark downward eye stripe distinct from its white cheek");
        // Compare actual pixels with continuous species regions, not their values at sparse vertices.
        // Ignore partially covered silhouette pixels and very soft transitions in the reference.
        int tested = 0, wrong = 0;
        for (int y = 0; y < shot.GetHeight(); y += 2) for (int x = 0; x < shot.GetWidth(); x += 2)
        {
            var p = cam.ProjectPosition(new Vector2(x + .5f, y + .5f) * GetViewport().GetVisibleRect().Size / new Vector2(shot.GetWidth(), shot.GetHeight()), 6);
            if (Mathf.Abs(p.X) > .36f || p.Y < 1.24f || p.Y > 1.50f) continue;
            var regions=CharacterFacePattern.Regions("redpanda",new Vector2(p.X,p.Y-1.34f));
            float reference=regions.X*(1-regions.Y);
            if (reference > .1f && reference < .9f) continue;
            float actual = shot.GetPixel(x,y).SrgbToLinear().R;
            tested++; if (Mathf.Abs(actual-reference) > .25f) wrong++;
        }
        Check(tested > 1000 && wrong < tested*.01f, $"continuous face mask: {wrong}/{tested} incorrect interior pixels (under 1%)");
        int[] transitions=new int[2];
        for(int blend=0;blend<2;blend++)
        {
            mask.ApplyAppearance(new AppearanceProfile{Body="#000000",BodySecondary="#000000",BodyMarking="#ffffff",MarkingSoftness=blend,Hat=0,Vest=false,Pack=false});
            await Frames(4);var pixels=await Shot("softness-"+blend);
            for(int y=0;y<pixels.GetHeight();y+=2)for(int x=0;x<pixels.GetWidth();x+=2)
            {
                var at=cam.ProjectPosition(new Vector2(x+.5f,y+.5f)*GetViewport().GetVisibleRect().Size/new Vector2(pixels.GetWidth(),pixels.GetHeight()),6);
                if(Mathf.Abs(at.X)>.35f||at.Y<1.24f||at.Y>1.50f)continue;
                float value=pixels.GetPixel(x,y).SrgbToLinear().R;if(value>.1f&&value<.9f)transitions[blend]++;
            }
        }
        Check(transitions[1]>transitions[0]*2,$"softness widens only the painted transition ({transitions[0]} -> {transitions[1]} pixels)");
        foreach(float spacing in new[]{.85f,1.12f})
        {
            mask.ApplyAppearance(new AppearanceProfile{Body="#000000",BodySecondary="#000000",BodyMarking="#ffffff",EyeSpacing=spacing,Hat=0,Vest=false,Pack=false});
            await Frames(4);var pixels=await Shot("spacing-"+spacing);
            var at=cam.UnprojectPosition(new Vector3(.215f*spacing,1.265f,0))*new Vector2(pixels.GetWidth(),pixels.GetHeight())/GetViewport().GetVisibleRect().Size;
            Check(pixels.GetPixel((int)at.X,(int)at.Y).SrgbToLinear().R<.15f,$"tear stripe follows eye spacing {spacing}");
        }
        mask.QueueFree(); await Frames(2);
        var band=CharacterRig.Create(CharacterCatalog.Player("raccoon"));world.AddChild(band);
        band.ApplyAppearance(new AppearanceProfile{Body="#000000",BodySecondary="#000000",BodyMarking="#ffffff",Hat=0,Vest=false,Pack=false});
        foreach(var n in band.Model.FindChildren("*",nameof(MeshInstance3D),true,false))((MeshInstance3D)n).Visible=n.Name=="raccoon_torso_mesh";
        await Frames(4);var bandImage=await Shot("raccoon-mask");
        foreach(var probe in new[]{new Vector3(-.20f,.03f,1),new Vector3(.20f,.03f,1),new Vector3(-.20f,.10f,1),new Vector3(.20f,.10f,1),new Vector3(0,.045f,1),new Vector3(.20f,-.10f,0)})
        {
            var at=cam.UnprojectPosition(new Vector3(probe.X,1.34f+probe.Y,0))*new Vector2(bandImage.GetWidth(),bandImage.GetHeight())/GetViewport().GetVisibleRect().Size;
            float value=bandImage.GetPixel((int)at.X,(int)at.Y).SrgbToLinear().R;
            Check(Mathf.Abs(value-probe.Z)<.15f,$"raccoon eye band GPU landmark x={probe.X} y={probe.Y} is {(probe.Z>0?"dark region":"light muzzle region")}");
        }
        band.QueueFree();await Frames(2);
        // Test the real GPU palette for all four species (CPU reference samples cannot catch a
        // missing/swapped shader uniform). Include default restoration and backside isolation.
        foreach(var species in new[]{"raccoon","redpanda","duck","goose"})
        {
            var rig=CharacterRig.Create(CharacterCatalog.Player(species));world.AddChild(rig);
            foreach(var n in rig.Model.FindChildren("*",nameof(MeshInstance3D),true,false))((MeshInstance3D)n).Visible=n.Name==species+"_torso_mesh";
            cam.Size=2.1f;cam.Position=new Vector3(0,1.1f,6);cam.LookAt(new Vector3(0,1.1f,0));
            foreach(bool custom in new[]{true,false})
            {
                rig.ApplyAppearance(custom?new AppearanceProfile{Body="#336699",BodySecondary="#cc8844",BodyMarking="#55bb77"}:new AppearanceProfile());
                var palette=AppearanceCatalog.Palette(species)!;
                var primary=(custom?new Color("336699"):palette.Primary).SrgbToLinear();
                var secondary=(custom?new Color("cc8844"):palette.Secondary).SrgbToLinear();
                float height=species=="goose"?1.74f:1.62f;
                foreach(bool back in new[]{false,true})
                {
                    rig.Rotation=back?new Vector3(0,Mathf.Pi,0):Vector3.Zero;
                    await Frames(4);var pixels=await Shot($"palette-{species}-{custom}-{back}");
                    foreach(float atY in new[]{.60f,1.70f})
                    {
                        float t=(atY-.22f)/height,blend=back||atY>1?0:.65f;
                        var expected=primary*(.88f+.12f*t)*(1-blend)+secondary*blend;
                        var screen=cam.UnprojectPosition(new Vector3(0,atY,0))*new Vector2(pixels.GetWidth(),pixels.GetHeight())/GetViewport().GetVisibleRect().Size;
                        var actual=pixels.GetPixel((int)screen.X,(int)screen.Y).SrgbToLinear();
                        float error=Mathf.Max(Mathf.Abs(actual.R-expected.R),Mathf.Max(Mathf.Abs(actual.G-expected.G),Mathf.Abs(actual.B-expected.B)));
                        Check(error<.035f,$"{species}: GPU {(custom?"custom":"default")} {(back?"back":"front")} y={atY} palette error={error:0.000}");
                    }
                }
            }
            rig.QueueFree();await Frames(2);
        }
        environment.AmbientLightEnergy = .55f; environment.TonemapMode = Godot.Environment.ToneMapper.Agx;
        world.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-40,-30,0), LightEnergy = 1.25f, LightColor = new Color("ffe4c8"), ShadowEnabled = true });
        world.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-25,145,0), LightEnergy = .6f, LightColor = new Color("b2d7ec") });
        foreach (var species in new[] { "raccoon", "redpanda", "duck", "goose" })
        {
            var rig = CharacterRig.Create(CharacterCatalog.Player(species)); world.AddChild(rig);
            var plain = new AppearanceProfile { Hat = 0, Vest = false, Pack = false, Goggles = false, HairStyle = 0 };
            rig.ApplyAppearance(plain); rig.HideBuiltInWeapon();
            cam.Size = 1.28f; cam.Position = new Vector3(0,1.45f,6); cam.LookAt(new Vector3(0,1.45f,0));
            await Frames(6); await Shot(species+"-front");
            rig.RotationDegrees = new Vector3(0,-40,0); await Frames(3); await Shot(species+"-quarter");
            plain.Body = "#225a72"; plain.BodySecondary = "#e8d4a2"; plain.BodyMarking = "#fae6c0";
            rig.ApplyAppearance(plain); await Frames(3); await Shot(species+"-custom");
            rig.ApplyAppearance(new AppearanceProfile()); cam.Size = 2.5f; cam.Position = new Vector3(0,3.8f,5); cam.LookAt(new Vector3(0,1,0));
            await Frames(3); await Shot(species+"-gear");
            cam.Size = 16f; await Frames(3); await Shot(species+"-game");
            rig.QueueFree(); await Frames(2);
        }
        // Side-by-side portraits for the art review: the two species must read differently.
        cam.Size=1.5f;cam.Position=new Vector3(0,1.45f,6);cam.LookAt(new Vector3(0,1.45f,0));
        foreach(var (species,x) in new[]{("raccoon",-.63f),("redpanda",.63f)})
        {
            var rig=CharacterRig.Create(CharacterCatalog.Player(species));world.AddChild(rig);rig.Position=new Vector3(x,0,0);
            rig.ApplyAppearance(new AppearanceProfile{Hat=0,Vest=false,Pack=false,Goggles=false,HairStyle=0});rig.HideBuiltInWeapon();
        }
        await Frames(6);await Shot("species-pair");
        world.QueueFree(); await Frames(2);
    }
    private async Task Frames(int n) { for(int i=0;i<n;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    private async Task<Image> Shot(string name)
    {
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        var image=GetViewport().GetTexture().GetImage();
        var folder=ProjectSettings.GlobalizePath("res://output");DirAccess.MakeDirRecursiveAbsolute(folder);
        var error=image.SavePng($"{folder}/surface-{Tag}-{name}.png");
        if(error!=Error.Ok)throw new Exception($"Capture {name} could not be saved: {error}");
        return image;
    }
}
