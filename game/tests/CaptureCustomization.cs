using Godot;
using System;
using System.Threading.Tasks;

namespace HordeShooter.Tests;
public partial class CaptureCustomization : Node
{
    private MainMenu _menu=null!;
    private int _checks;
    private const string TestPath="user://workshop-capture-test.cfg";
    private T Ui<T>(string name) where T:Node=>(T)(_menu.FindChild(name,true,false)??throw new Exception("Missing control "+name));
    private void Check(bool value,string message){if(!value)throw new Exception(message);_checks++;GD.Print("PASS: "+message);}
    private void Click(string name)=>Ui<Button>(name).EmitSignal(BaseButton.SignalName.Pressed);
    private void Choose(string name,int value){var c=Ui<OptionButton>(name);c.Select(value);c.EmitSignal(OptionButton.SignalName.ItemSelected,value);}
    private void Tint(string channel,string color){var c=Ui<ColorPickerButton>("Color_"+channel);c.Color=new Color(color);c.EmitSignal(ColorPickerButton.SignalName.ColorChanged,c.Color);}
    public override async void _Ready()
    {
        try
        {
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(TestPath));
            _menu=GD.Load<PackedScene>("res://scenes/Menu.tscn").Instantiate<MainMenu>();_menu.AppearancePath=TestPath;AddChild(_menu);
            await Frames(5);
            Ui<CheckButton>("PreviewAutoRotate").ButtonPressed=false;Ui<HSlider>("PreviewRotation").Value=-18;Ui<HSlider>("PreviewZoom").Value=1.25;
            Check(_menu.Characters.Count==4,"four playable species exposed before customization");
            Check(Ui<TabContainer>("PartTabs").GetTabCount()==6,"six part categories available");
            await Shot("custom-workshop-default");
            Choose("PresetChoice",1);Click("ApplyPreset");await Frames(2);
            Ui<HSlider>("Scale_body_width").Value=1.08;
            Choose("Choice_HairStyle",2);Choose("Choice_FaceStyle",1);Tint("body","#9ecfd7");Tint("hair","#638f9d");
            Ui<CheckButton>("PreviewGear").ButtonPressed=false;Ui<CheckButton>("PreviewWeapon").ButtonPressed=false;
            Ui<TabContainer>("PartTabs").CurrentTab=1;
            await Frames(3);await Shot("custom-workshop-goose");
            var saved=AppearanceStore.ReadProfile("goose",TestPath);
            Check(Mathf.IsEqualApprox(saved.BodyWidth,1.08f)&&saved.HairStyle==2&&saved.Body=="#9ecfd7","preset plus manual edits save through UI");
            Click("Species_raccoon");await Frames(2);
            Check(_menu.Selected.Id=="raccoon"&&Ui<HSlider>("Scale_ear_size")!=null,"raccoon exposes species-specific ears");
            Check(Math.Abs(Ui<HSlider>("Scale_body_width").Value-1)<.001,"switching species does not inherit goose proportions (value="+Ui<HSlider>("Scale_body_width").Value+")");
            Choose("PresetChoice",1);Click("ApplyPreset");await Frames(2);
            Ui<HSlider>("Scale_ear_size").Value=1.20;Choose("Choice_HairStyle",1);Ui<TabContainer>("PartTabs").CurrentTab=2;
            Ui<HSlider>("PreviewRotation").Value=25;await Frames(2);await Shot("custom-workshop-raccoon");
            Click("Species_redpanda");await Frames(2);
            Choose("PresetChoice",2);Click("ApplyPreset");await Frames(2);
            Choose("Choice_HairStyle",2);Ui<HSlider>("Scale_tail_size").Value=1.25;
            Ui<TabContainer>("PartTabs").CurrentTab=4;Ui<HSlider>("PreviewRotation").Value=130;
            await Frames(2);await Shot("custom-workshop-redpanda");
            Click("Species_duck");await Frames(2);
            Choose("PresetChoice",1);Click("ApplyPreset");await Frames(2);
            Choose("Choice_BeakStyle",1);Tint("body","#c1d7aa");Ui<TabContainer>("PartTabs").CurrentTab=3;Ui<HSlider>("PreviewRotation").Value=-25;
            await Frames(2);await Shot("custom-workshop-duck");
            Click("Species_goose");await Frames(2);
            Check(Math.Abs(Ui<HSlider>("Scale_body_width").Value-1.08)<.001&&Ui<OptionButton>("Choice_HairStyle").Selected==2,"species switch restores saved custom shape and hair");
            Check(_menu.FindChild("Scale_ear_size",true,false)==null,"birds hide mammal-only ear controls");
            Ui<CheckButton>("PreviewGear").ButtonPressed=true;Ui<CheckButton>("PreviewWeapon").ButtonPressed=true;
            Choose("HatChoice",1);Ui<CheckButton>("WearVest").ButtonPressed=true;Ui<CheckButton>("WearHeadset").ButtonPressed=true;
            Ui<TabContainer>("PartTabs").CurrentTab=5;
            var before=AppearanceStore.ReadProfile("goose",TestPath);
            Ui<CheckButton>("PreviewGear").ButtonPressed=false;Ui<CheckButton>("PreviewGear").ButtonPressed=true;
            Check(AppearanceStore.ReadProfile("goose",TestPath).Hat==before.Hat,"temporary preview toggles do not save equipment changes");
            Click("PreviewPose_1");await Frames(20);await Shot("custom-workshop-equipped");
            _menu.QueueFree();await Frames(2);
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(TestPath));
            GD.Print("CUSTOM_WORKSHOP_PASS "+_checks);QuitHygiene.Collect();GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private async Task Frames(int n){for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Shot(string name)
    {
        if(DisplayServer.GetName()=="headless")return;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/"+name+".png"));
    }
}
