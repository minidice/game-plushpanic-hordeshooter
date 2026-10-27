using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Rendered regional spawn audit and repeatable combat samples on the real Stage 2 map.</summary>
public partial class CommonRosterFieldCheck : Node
{
    private Stage _stage = null!;
    private int _checks;
    private async Task Frames(int n) { for (int i=0;i<n;i++) await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame); }
    private void Check(bool value,string label) { if(!value)throw new Exception(label);_checks++;GD.Print("PASS "+label); }
    private async Task Shot(string name)
    {
        if(DisplayServer.GetName()=="headless")return;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage();
        Check(image.SavePng(ProjectSettings.GlobalizePath("res://output/roster-field-"+name+".png"))==Error.Ok,"capture "+name);
    }
    private async Task Clear()
    {
        foreach(var e in _stage.AllEnemies.ToArray())e.QueueFree();
        foreach(var p in _stage.GetChildren().OfType<EnemyProjectile>().ToArray())p.QueueFree();
        await Frames(3);
    }
    private Vector3 Ground(float x,float z) { var p=new Vector3(x,0,z);p.Y=_stage.Tiers.GroundY(p)+.05f;return p; }
    private void At(Vector3 p)
    {
        _stage.Player.TeleportTo(p);_stage.Cam.SnapTo(p+Vector3.Up);
        _stage.Player.SetControls(Vector2.Zero,p+Vector3.Right*10,false);
    }
    public override async void _Ready()
    {
        int exit=0;
        try
        {
            foreach(string theme in new[]{"animals","armed","zombie"})await Regions(theme);
            await ElevatedTell();await Pursuit();await Weapons();
            GD.Print("COMMON_ROSTER_FIELD_PASS "+_checks);
        }
        catch(Exception e){GD.PushError("COMMON_ROSTER_FIELD_FAIL "+e);exit=1;}
        _stage?.QueueFree();await Frames(3);QuitHygiene.Collect();GetTree().Quit(exit);
    }
    private async Task Regions(string theme)
    {
        if(_stage!=null){_stage.QueueFree();await Frames(3);}
        _stage=new Stage{MapOverride=new LayoutMap(),ThemeOverride=theme,CharacterOverride="goose",ModeOverride=GameMode.Scenario,SquadOverride=0,ArrivalOverride=false,NextStageOverride=false};
        AddChild(_stage);await Frames(4);
        _stage.Player.ManualControl=true;_stage.Player.Invulnerable=true;
        // Use real authored placement; pause the raid timer to isolate each birth group from cross-region pursuit.
        _stage.Director.SetPhysicsProcess(false);
        At(Ground(530,-100));_stage.Director.UpdateAreas();
        foreach(var area in _stage.Director.Encounters!.Areas)
        {
            var members=_stage.AllEnemies.Where(e=>!e.IsSpecial&&e.Position.X>=area.X0&&e.Position.X<area.X1).ToArray();
            if(members.Length==0)continue; // clearing/arrival have no ambient groups by design.
            string packId=_stage.CommonPackAt(members[0].Position,area.Id);
            var pack=CommonRoster.Current.Packs[packId];
            Check(members.All(e=>e.Monster.CommonIdentity is {} id&&id.Faction==pack.Faction&&id.Species==pack.Species&&id.Palette==pack.Palette),theme+"/"+area.Id+" ambient species and palette coherent");
            Check(members.All(e=>e.Monster.CommonIdentity!.Gear is CommonGear.None or CommonGear.Vest),theme+"/"+area.Id+" no eye accessories");
            Check(members.All(e=>PaletteMatches(e)),theme+"/"+area.Id+" actual material channels match pack palette");
            GD.Print($"FIELD_PACK theme={theme} area={area.Id} count={members.Length} pack={packId} weapons={string.Join(',',members.GroupBy(e=>e.Monster.CommonIdentity!.Weapon).Select(g=>$"{g.Key}:{g.Count()}"))}");
            foreach(var e in _stage.AllEnemies)e.SetPhysicsProcess(false);
            var nearest=members[0];
            var viewpoints=new[]{new Vector3(-2,0,0),new Vector3(2,0,0),new Vector3(0,0,2),new Vector3(0,0,-2)}
                .Select(offset=>Ground(nearest.Position.X+offset.X,nearest.Position.Z+offset.Z));
            var viewpoint=viewpoints.FirstOrDefault(v=>!_stage.Flow.IsBlocked(v)&&Mathf.Abs(v.Y-nearest.Position.Y)<.2f,Ground(nearest.Position.X-4,nearest.Position.Z+3));
            At(viewpoint);
            _stage.Player.SetControls(Vector2.Zero,nearest.Position+Vector3.Up,false);await Frames(30);
            await Shot(theme+"-"+area.Id);
        }
        await Clear();
        _stage.Director.FillAfterMission();
        Check(_stage.AllEnemies.Any()&&_stage.AllEnemies.Where(e=>!e.IsSpecial).All(e=>
        {
            var id=e.Monster.CommonIdentity!;var pack=CommonRoster.Current.Packs[_stage.CommonPackAt(e.Position)];
            return id.Species==pack.Species&&id.Palette==pack.Palette&&id.Faction==pack.Faction&&PaletteMatches(e);
        }),theme+" mission refill uses local species, palette and faction");
        await Clear();
        foreach(var entry in _stage.Director.Entries.Where(e=>e.Kind==EntryKind.Hidden).GroupBy(e=>_stage.CommonPackAt(e.Ground)).Select(g=>g.First()))
        {
            At(Ground(6,-2));var pack=CommonRoster.Current.Packs[_stage.CommonPackAt(entry.Ground)];
            int spawned=_stage.Director.SpawnWave(entry,4);
            Check(spawned>0&&_stage.AllEnemies.All(e=>e.Monster.CommonIdentity is {} id&&id.Species==pack.Species&&id.Palette==pack.Palette&&id.Faction==pack.Faction),theme+" wave uses entry-local pack "+pack.Id);
            await Clear();
        }
    }
    private bool PaletteMatches(Enemy enemy)
    {
        var id=enemy.Monster.CommonIdentity!;var palette=CommonRoster.Current.Palettes[id.Palette];
        string prefix=id.Faction=="zombie"?"common_cloth":"common_primary";
        var wanted=new Color(id.Faction=="zombie"?palette.Cloth:palette.Primary[id.Variant%palette.Primary.Length]);
        var materials=enemy.Rig.Model.FindChildren("*",nameof(MeshInstance3D),true,false).OfType<MeshInstance3D>()
            // Vision can temporarily override the whole mesh with a silhouette shader.
            // Inspect the themed surface beneath that visibility layer, not the silhouette color.
            .SelectMany(m=>Enumerable.Range(0,m.Mesh?.GetSurfaceCount()??0).Select(i=>m.GetSurfaceOverrideMaterial(i)??m.Mesh!.SurfaceGetMaterial(i)))
            .OfType<StandardMaterial3D>().Where(m=>m.ResourceName.StartsWith(prefix)).ToArray();
        return materials.Length>0&&materials.All(m=>m.AlbedoColor.IsEqualApprox(wanted));
    }
    private async Task Pursuit()
    {
        foreach(var species in CommonRoster.Current.Species.Values)
        {
            await Clear();At(Ground(11,-2));var p=_stage.Player;
            while(p.RollCooldownLeft>0)await Frames(1);
            var id=new CommonIdentity(species.Id=="human"?"zombie":"hunter",species.Id,species.Id=="human"?"ochre":"green",species.Id=="human"?"unarmed":"club");
            var e=_stage.SpawnCommonIdentity(id,Ground(5,-2),true);e.Reaction=Reaction.Hunter;
            float before=p.Position.X-e.Position.X;p.SetControls(Vector2.Right,p.Position+Vector3.Right*30,false);
            await Frames(180);float after=p.Position.X-e.Position.X;
            GD.Print($"FIELD_CHASE species={species.Id} speed={e.Speed:0.00} gap_start={before:0.00} gap_end={after:0.00}");
            Check(after<before-.15f&&after>1,"real terrain gradual pursuit "+species.Id);
            p.PressRoll();await Frames(30);
            Check(p.Position.X-e.Position.X>after+.8f,"roll escape opening "+species.Id);
            p.SetControls(Vector2.Zero,e.Position+Vector3.Up,false);
        }
        await Shot("pursuit");await Clear();
    }
    private async Task Weapons()
    {
        var p=_stage.Player;
        foreach(var weapon in CommonRoster.Current.Weapons.Values)
        {
            await Clear();At(Ground(12,-2));p.Heal(999);p.Invulnerable=false;
            while(p.RollCooldownLeft>0)await Frames(1);
            string faction=weapon.Factions[0];var id=new CommonIdentity(faction,faction=="zombie"?"human":"eagle",faction=="zombie"?"ochre":"green",weapon.Id);
            var e=_stage.SpawnCommonIdentity(id,Ground(18,-2),true);e.Reaction=Reaction.Hunter;
            p.SetControls(Vector2.Zero,e.Position+Vector3.Up,false);
            int before=p.Health,attacks=0;bool attacking=false,captured=false;
            for(int i=0;i<360;i++)
            {
                bool now=e.IsMeleeAttacking||e is RangedEnemy{WindingUp:true};
                if(now&&!attacking)attacks++;attacking=now;
                if(now&&!captured){await Shot("weapon-"+weapon.Id);captured=true;}
                await Frames(1);
            }
            GD.Print($"FIELD_COMBAT weapon={weapon.Id} windups={attacks} shots={(e as RangedEnemy)?.ProjectilesFired??0} damage={before-p.Health} window=6s");
            Check(attacks>=1&&p.Health<before,"real combat reaches stationary survivor "+weapon.Id);
            // A fresh locked aim gives all ranged weapons the same reaction opportunity.
            if(e is RangedEnemy ranged)ranged.StartWindup(p);
            else
            {
                for(int i=0;i<180&&e.IsMeleeAttacking;i++)await Frames(1);
                for(int i=0;i<180&&!e.IsMeleeAttacking;i++)await Frames(1);
                Check(e.IsMeleeAttacking,"fresh melee telegraph before reactive dodge "+weapon.Id);
            }
            p.Heal(999);before=p.Health;
            p.SetControls(Vector2.Down,e.Position+Vector3.Up,false);p.PressRoll();await Frames(75);
            Check(p.Health==before&&!e.IsMeleeAttacking,"reactive strafe and roll avoids completed attack "+weapon.Id);
            p.Invulnerable=true;p.SetControls(Vector2.Zero,e.Position+Vector3.Up,false);
        }
        await Clear();
    }
    private async Task ElevatedTell()
    {
        At(Ground(488,-92));var p=_stage.Player;
        var e=(RangedEnemy)_stage.SpawnCommonIdentity(new("armed","rhino","green","ak47"),Ground(484,-92),true);
        e.SetPhysicsProcess(false);e.StartWindup(p);await Frames(2);await Shot("plateau-tell");
        // The windup lane is a GroundMarker (A-19 world markers) since 2026-10-27; it used to be a stretched box (I-128).
        var marker=_stage.Effects.GetChildren().OfType<GroundMarker>().Last();
        float expected=(e.Position.Y+p.Position.Y)*.5f+.055f;
        Check(Mathf.Abs(marker.GlobalPosition.Y-expected)<.12f,"elevated gun windup marker follows actors floor instead of world zero");
    }
}
