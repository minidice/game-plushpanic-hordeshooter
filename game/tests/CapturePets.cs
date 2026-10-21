using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter.Tests;

/// <summary>Actual engine model sheet, four views, and optional walk frames. No game rules.</summary>
public partial class CapturePets : Node3D
{
    public override async void _Ready()
    {
        try
        {
            GetWindow().Size = new Vector2I(1500,1080);
            AddChild(new WorldEnvironment { Environment = new Godot.Environment {
                BackgroundMode=Godot.Environment.BGMode.Color, BackgroundColor=new Color("1c2938"),
                AmbientLightSource=Godot.Environment.AmbientSource.Color, AmbientLightColor=new Color("d6e2ed"), AmbientLightEnergy=.5f,
                TonemapMode=Godot.Environment.ToneMapper.Agx } });
            AddChild(new DirectionalLight3D { RotationDegrees=new Vector3(-55,-30,0), LightEnergy=1.25f, ShadowEnabled=true });
            AddChild(new DirectionalLight3D { RotationDegrees=new Vector3(-25,140,0), LightEnergy=.35f });
            Mat.Box(this,new Vector3(0,-.15f,0),new Vector3(18,.2f,17),Mat.Solid("344456"));
            var camera=new Camera3D { Projection=Camera3D.ProjectionType.Orthogonal,Size=10.8f,Position=new Vector3(0,11,13) };
            AddChild(camera); camera.LookAt(new Vector3(0,.5f,0));
            var pets=new List<PetModel>();
            string[] species={ "turtle","dog","rabbit" };
            string?[] weapons={ null,"twin_blaster","rocket_pod" };
            for(int row=0;row<3;row++)
            for(int col=0;col<3;col++)
            {
                var pet=PetModel.Create(species[col]);AddChild(pet);pet.Position=new Vector3((col-1)*3.5f,0,(row-1)*3.3f);
                pet.RotationDegrees=new Vector3(0,-25,0);pet.SetWeapon(weapons[row]);pets.Add(pet);
                AddChild(new Label3D { Text=$"{species[col].ToUpperInvariant()} / {weapons[row]?.Replace('_',' ').ToUpperInvariant() ?? "BASE"}",FontSize=36,PixelSize=.004f,
                    Position=pet.Position+new Vector3(0,.07f,1.25f),Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,NoDepthTest=true });
            }
            var title=new Label { Text="QUADRUPED PETS  /  SHARED BACK MODULES",Position=new Vector2(40,24) };
            title.AddThemeFontSizeOverride("font_size",30);AddChild(title);
            string output=ProjectSettings.GlobalizePath("res://output/pets");DirAccess.MakeDirRecursiveAbsolute(output);
            async System.Threading.Tasks.Task Save(string file)
            {
                for(int f=0;f<3;f++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using var image=GetViewport().GetTexture().GetImage();
                if(image.SavePng($"{output}/{file}.png")!=Error.Ok) throw new Exception("Image save failed");
            }
            if(OS.GetCmdlineUserArgs().Contains("--dog-quality"))
            {
                // The old GLB is a local snapshot saved before regenerating the model.
                string beforePath=$"{output}/quality-before/dog.glb";
                if(!System.IO.File.Exists(beforePath)) throw new Exception("Save the previous dog.glb to output/pets/quality-before before comparing.");
                foreach(var pet in pets) pet.Hide();
                foreach(var label in GetChildren().OfType<Label3D>()) label.Hide();
                using var document=new GltfDocument();
                using var state=new GltfState();
                if(document.AppendFromFile(beforePath,state)!=Error.Ok) throw new Exception("Cannot load before model");
                var before=(Node3D)document.GenerateScene(state);AddChild(before);
                before.Position=new Vector3(-1.15f,0,0);before.RotationDegrees=new Vector3(0,-25,0);
                var after=PetModel.Create("dog");AddChild(after);after.Position=new Vector3(1.15f,0,0);after.RotationDegrees=before.RotationDegrees;
                GetWindow().Size=new Vector2I(1400,850);
                camera.Size=4.9f;camera.Position=new Vector3(0,6,8);camera.LookAt(new Vector3(0,.65f,0));
                title.Text="DOG MODEL  /  SAME CAMERA & LIGHTING";
                foreach(var (model,text) in new[] { (before,"BEFORE"),((Node3D)after,"REFINED") })
                    AddChild(new Label3D { Text=text,FontSize=36,PixelSize=.005f,Position=model.Position+new Vector3(0,.05f,1.15f),Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,NoDepthTest=true });
                await Save("dog-quality-comparison");
                before.RotationDegrees=after.RotationDegrees=new Vector3(0,90,0);
                await Save("dog-quality-side");
                GD.Print("PET_QUALITY_CAPTURE_PASS");GetTree().Quit();return;
            }
            await Save("pet-lineup");
            foreach(var (view,angle) in new[] { ("front",0f),("side",90f),("rear",180f) })
            {
                foreach(var pet in pets) pet.RotationDegrees=new Vector3(0,angle,0);
                await Save($"pet-{view}");
            }
            if(OS.GetCmdlineUserArgs().Contains("--walk"))
            {
                foreach(var pet in pets) pet.RotationDegrees=new Vector3(0,-25,0);
                for(int frame=0;frame<72;frame++)
                {
                    foreach(var pet in pets) pet.Pose(frame/24f,1);
                    await Save($"walk-{frame:000}");
                }
            }
            GD.Print("PET_CAPTURE_PASS");GetTree().Quit();
        }
        catch(Exception e) { GD.PushError(e.ToString());GetTree().Quit(1); }
    }
}
