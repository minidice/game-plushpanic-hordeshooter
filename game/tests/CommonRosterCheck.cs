using Godot;
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class CommonRosterCheck : Node
{
    private int _checks;
    private Stage? _stage;
    private void Check(bool value, string message) { if (!value) throw new Exception(message); _checks++; }
    private void Reject(Action action, string message) { try { action(); } catch (ArgumentException) { _checks++; return; } throw new Exception(message); }
    private async Task Frames(int count) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private sealed class TestMap : StageMap
    {
        public override Rect2 Area => new(-15, -15, 30, 30);
        protected override void BuildMap() { PlayerStart = Vector3.Zero; Floor(-14, 14, -14, 14, "546852"); }
    }
    public override async void _Ready()
    {
        int exit = 0;
        try
        {
            DataChecks(); AppearanceChecks(); await CombatChecks(); await CoreChecks();
            GD.Print("COMMON_ROSTER_CHECK_PASS " + _checks);
        }
        catch (Exception e) { GD.PushError("COMMON_ROSTER_CHECK_FAIL " + e); exit = 1; }
        _stage?.QueueFree(); await Frames(3); QuitHygiene.Collect(); GetTree().Quit(exit);
    }

    private void DataChecks()
    {
        var catalog = CommonRoster.Current;
        Check(catalog.Errors.Count == 0, string.Join(";", catalog.Errors));
        Check(StageTheme.Commons("zombie").All(x => x == "humanoid"), "ordinary infected pool human-only");
        Check(catalog.Species.Count == 7 && catalog.Presets.Count == 10, "seven species and ten presets");
        Check(catalog.Packs.Keys.SelectMany(p => catalog.CreatePack(p, 20)).All(i => !i.Gear.HasFlag(CommonGear.Glasses)), "no pack equips eyewear");
        Check(catalog.Presets.All(p => !p.Modules.Contains("Gear_Eyepatch")), "no zombie preset equips an eyepatch");
        Reject(() => catalog.Compose(new("armed", "eagle", "red", "mp5", 0, CommonGear.Glasses)), "disabled eyewear cannot be explicitly equipped");
        string text = FileAccess.GetFileAsString(CommonRoster.DataPath);
        void Bad(Action<JsonObject> alter)
        { var json = JsonNode.Parse(text)!.AsObject(); alter(json); Check(CommonRoster.FromText(json.ToJsonString()).Errors.Count > 0, "invalid catalog rejected"); }
        Bad(j => j["version"] = 2); Bad(j => j["surprise"] = true);
        Bad(j => j["species"]![0]!["speed"] = 4.4); Bad(j => j["species"]![0]!["hp"] = -2);
        Bad(j => j["species"]![0]!["typo_speed"] = 4.6); Bad(j => j["species"]![1]!["id"] = "human");
        Bad(j => j["species"]![1]!["look"] = "common_eagel");
        Bad(j => j["palettes"]![0]!["primary"]![0] = "oops");
        Bad(j => j["weapons"]![3]!["projectile_speed"] = 0);
        Bad(j => j["packs"]![0]!["species"] = "dragon");
        Bad(j => j["packs"]![0]!["weapons"]![0] = "mp5");
        Bad(j => j["presets"]![0]!["modules"]![0] = "Helmet");
        Bad(j => j["presets"]!.AsArray().RemoveAt(9));
        Check(CommonRoster.FromText("{\"version\":1,\"version\":1}").Errors.Count > 0, "duplicate JSON keys rejected");
        Reject(() => catalog.Compose(new("zombie", "eagle", "red", "unarmed")), "animal zombie rejected");
        Reject(() => catalog.Compose(new("hunter", "eagle", "red", "m4")), "hunter firearm rejected");
        Reject(() => catalog.Compose(new("hunter", "eagle", "red", "club", 0, CommonGear.Vest)), "hunter gear rejected");
        Reject(() => catalog.Compose(new("zombie", "human", "ochre", "unarmed", 10)), "variant out of range rejected");
        Reject(() => catalog.Compose(new("zombie", "human", "red", "unarmed")), "zombie clothing rejects red palette");
        Reject(() => catalog.Compose(new("zombie", "human", "slate", "unarmed")), "zombie clothing rejects blue/slate palette");
        foreach (var species in catalog.Species.Values)
        {
            var id = new CommonIdentity(species.Id == "human" ? "zombie" : "hunter", species.Id, species.Id == "human" ? "ochre" : "red", species.Id == "human" ? "unarmed" : "club");
            var monster = catalog.Compose(id);
            Check(monster.MoveSpeed is >= 4.55f and <= 4.75f && monster.MoveSpeed > 4.4f, "fixed speed above baseline player");
            Check(monster.BalloonPop == null && monster.Capture == null && monster.BullCharge == null, "ordinary species no named skills");
            Check(monster.CommonIdentity == id, "composition retains immutable birth value");
        }
        var baseline = catalog.Compose(new("zombie", "human", "ochre", "unarmed"));
        for (int v = 0; v < 10; v++)
        {
            var monster = catalog.Compose(new("zombie", "human", "green", "unarmed", v));
            Check(monster.Hp == baseline.Hp && monster.AttackDamage == baseline.AttackDamage && monster.MoveSpeed == baseline.MoveSpeed && monster.MeleeWindup == baseline.MeleeWindup, "appearance never changes stats");
        }
        foreach (var pack in catalog.Packs.Values)
        {
            var members = catalog.CreatePack(pack.Id, pack.Weapons.Length * 3);
            Check(members.All(i => i.Species == pack.Species && i.Palette == pack.Palette && i.Faction == pack.Faction), "pack faction/species/palette isolation");
            Check(members.Select(i => i.Weapon).SequenceEqual(Enumerable.Range(0, 3).SelectMany(_ => pack.Weapons)), "party scaling repeats exact weapon cycle");
        }
        Check(catalog.CreatePack("zombie_street", 10).Select(i => i.Variant).Distinct().Count() == 10, "ten appearances distributed within zombie pack");
        var sample = catalog.CreatePack(new CommonPack("preview", "armed", "eagle", "red", Enumerable.Repeat("mp5",4).Concat(Enumerable.Repeat("ak47",6)).ToArray()),10);
        Check(sample.Count(x => x.Weapon == "mp5") == 4 && sample.Count(x => x.Weapon == "ak47") == 6, "explicit 4 MP5 plus 6 AK47 pack exact");
        foreach (var identity in sample.Concat(catalog.CreatePack("zombie_street", 10)))
        {
            var writer = new NetWriter(); CommonRoster.WriteIdentity(writer, identity);
            Check(CommonRoster.ReadIdentity(new NetReader(writer.ToArray())) == identity, "spawn codec exact identity roundtrip");
        }
        var legacy = new NetWriter(); CommonRoster.WriteIdentity(legacy, null);
        Check(CommonRoster.ReadIdentity(new NetReader(legacy.ToArray())) == null, "legacy named/boss spawn has no common identity");
        string interactions = FileAccess.GetFileAsString(ObjectInteractions.DataPath), placements = FileAccess.GetFileAsString(DestructionPlacements.Path);
        Check(NetIds.DataHash(interactions, placements, text) != NetIds.DataHash(interactions, placements, text.Replace("4.55", "4.56")), "catalog stats change compatibility hash");
        Check(NetIds.DataHash(interactions, placements, text) == NetIds.DataHash(interactions, placements, text.Replace("\r\n", "\n").Replace("\n", "\r\n")), "hash normalizes CRLF");
        Check(SpawnDatabase.Current.Errors.Count == 0 && SpawnDatabase.Current.Stage1.CommonProfiles.Count == 3 && SpawnDatabase.Current.Stage2.CommonProfiles.Count == 3, "stage profiles validate");
    }

    private void AppearanceChecks()
    {
        var catalog = CommonRoster.Current;
        var red = new CommonIdentity("hunter", "eagle", "red", "club"); var green = red with { Palette = "green", Variant = 3 };
        var source = catalog.LookFor(red).Model!.Instantiate<Node3D>();
        var originals = source.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().Where(m => m.Mesh != null)
            .SelectMany(m => Enumerable.Range(0, m.Mesh!.GetSurfaceCount()).Select(i => m.GetActiveMaterial(i))).OfType<StandardMaterial3D>().Distinct().ToArray();
        var colors = originals.Select(m => m.AlbedoColor).ToArray();
        var r = CharacterRig.Create(catalog.LookFor(red)); catalog.Apply(r, red);
        var g = CharacterRig.Create(catalog.LookFor(green)); catalog.Apply(g, green);
        Check(originals.Select(m => m.AlbedoColor).SequenceEqual(colors), "themed materials never mutate shared GLB sources");
        Color Primary(CharacterRig rig) => rig.Model.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
            .SelectMany(m => Enumerable.Range(0,m.Mesh?.GetSurfaceCount() ?? 0).Select(i=>m.GetActiveMaterial(i))).OfType<StandardMaterial3D>().First(m => m.ResourceName.StartsWith("common_primary")).AlbedoColor;
        Check(Primary(r).R > Primary(r).G && Primary(g).G > Primary(g).R, "red and green eagle shades stay in theme");
        Check(!((Node3D)r.Model.FindChild("Gear_Vest", true, false)).Visible && !((Node3D)r.Model.FindChild("Gear_Glasses", true, false)).Visible, "hunter bare body has no modern gear");
        r.Free(); g.Free(); source.Free();
        for (int v=0;v<10;v++)
        {
            var identity = new CommonIdentity("zombie","human","ochre","unarmed",v);
            var rig=CharacterRig.Create(catalog.LookFor(identity)); catalog.Apply(rig,identity);
            Check(rig.Model.FindChild("Gear_Eyepatch",true,false) is Node3D { Visible: false }, "zombie eyepatch stays hidden " + v);
            foreach (var module in catalog.Presets.SelectMany(p=>p.Modules).Distinct())
            {
                var node=rig.Model.FindChild(module,true,false) as Node3D;
                Check(node != null && node.Visible == catalog.Presets[v].Modules.Contains(module), "preset module visibility exact " + v + "/" + module);
                if (node!.Visible) Check(node.GetChildren().OfType<Node3D>().All(n=>n.Visible), "visible module retains mesh children");
            }
            rig.Free();
        }
    }

    private async Task CombatChecks()
    {
        _stage = new Stage { CharacterOverride="test_capsule", MapOverride=new TestMap(), DirectorEnabled=false, SquadOverride=0 };
        AddChild(_stage); _stage.Player.ManualControl=true; _stage.Player.Invulnerable=true; await Frames(3);
        var randomPack=_stage.CommonPack(Vector3.Zero,10);
        Check(randomPack.Select(i=>i.Variant).Distinct().Count()==10,"authoritative random appearance bag distributes ten presets without replacement");
        Check(randomPack.All(i=>i.Faction=="zombie"&&i.Species=="human"&&i.Palette=="ochre"&&i.Weapon=="unarmed"),"random cosmetics preserve gameplay pack identity");
        foreach (var weapon in CommonRoster.Current.Weapons.Values)
        {
            string faction=weapon.Factions[0];
            var identity=new CommonIdentity(faction,faction=="zombie"?"human":"eagle",faction=="zombie"?"ochre":"red",weapon.Id);
            var enemy=_stage.SpawnCommonIdentity(identity,new Vector3(0,.05f,8),true);
            Check(enemy.Monster.CommonIdentity == identity && enemy.Monster.Ranged?.ProjectileSpeed == (weapon.Range>0?weapon.ProjectileSpeed:null), "actual factory retains weapon combat " + weapon.Id);
            Check((enemy is RangedEnemy)==(weapon.Range>0), "weapon selects real melee/ranged AI");
            Check(weapon.Id=="unarmed" || enemy.Rig.Weapon.FindChild("Weapon_"+weapon.Id,true,false)!=null, "selected weapon model present");
            if(enemy is RangedEnemy ranged)
            {
                ranged.StartWindup(_stage.Player); Check(ranged.WindingUp && ranged.ProjectilesFired==0,"shot has visible windup");
                await Frames((int)Math.Ceiling(enemy.Monster.Ranged!.Windup*60)+5);
                Check(ranged.ProjectilesFired==1,"selected weapon actually fires one shot: "+weapon.Id);
            }
            enemy.QueueFree(); await Frames(3);
        }
    }

    private async Task CoreChecks()
    {
        _stage!.QueueFree(); await Frames(3);
        _stage = new Stage { CharacterOverride="test_capsule", MapOverride=new TestMap(), DirectorEnabled=false, SquadOverride=0, ModeOverride=GameMode.Survival };
        AddChild(_stage); _stage.Player.ManualControl=true; _stage.Player.Invulnerable=true;
        _stage.ModeRules!.SetPhysicsProcess(false); _stage.Player.GlobalPosition=new Vector3(-12,0,-12);
        var core=_stage.ModeRules.Core!;
        foreach (var weapon in new[] { "woodbow", "mp5", "ak47", "m4" })
        {
            var identity=new CommonIdentity(weapon=="woodbow"?"hunter":"armed","eagle","red",weapon);
            var enemy=_stage.SpawnCommonIdentity(identity,core.GlobalPosition+Vector3.Back*2,true);
            float before=core.Health;
            for(int i=0;i<240&&Mathf.IsEqualApprox(core.Health,before);i++)await Frames(1);
            Check(core.Health<before,"common ranged loadout attacks nearer Outpost core: "+weapon);
            Check(enemy is RangedEnemy {ProjectilesFired: >0},"Outpost core damage comes from selected ranged projectile: "+weapon);
            enemy.QueueFree();await Frames(3);
        }
    }
}
