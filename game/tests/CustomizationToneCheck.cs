using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Two/three-tone body colors, arms following the body, presets, ear style/angle and tail style.
/// Windowed runs also save output/custom-tones-*.png.
/// </summary>
public partial class CustomizationToneCheck : Node
{
    private int _checks;
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("CUSTOM_TONE_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("CUSTOM_TONE_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task Run()
    {
        Check(AppearancePresets.Errors.Count == 0, "appearance presets load without errors" + (AppearancePresets.Errors.Count > 0 ? ": " + string.Join("; ", AppearancePresets.Errors) : ""));
        foreach (var id in new[] { "goose", "duck", "raccoon", "redpanda" })
        {
            var def = CharacterCatalog.Player(id);
            var rig = CharacterRig.Create(def);
            AddChild(rig);
            rig.ApplyAppearance(new AppearanceProfile());
            var imported = rig.SkinColors();
            var weights = rig.SkinToneWeights();
            Check(imported.Length > 0 && weights.Length == imported.Length, $"{id}: skin has a tone weight per vertex");
            Check(weights.All(w => w.X >= 0 && w.Y >= 0 && w.Z >= 0 && w.X + w.Y + w.Z <= 1.001f), $"{id}: tone weights are a valid blend");
            Check(weights.Count(w => w.Y > .3f) > 30 && weights.Count(w => w.Y < .001f) > 300, $"{id}: the belly region exists and most of the back has none of it");
            if (AppearanceCatalog.HasMarking(id)) Check(weights.Count(w => w.Z > .5f) > 10, $"{id}: face markings have their own region");

            // Secondary tone: belly moves, the back stays exactly as imported.
            rig.ApplyAppearance(new AppearanceProfile { BodySecondary = "#1e7a3c" });
            var toned = rig.SkinColors();
            int back = Enumerable.Range(0, weights.Length).First(i => weights[i].Y < .001f && weights[i].Z < .001f);
            int belly = Enumerable.Range(0, weights.Length).OrderByDescending(i => weights[i].Y).First();
            Check(toned[back].IsEqualApprox(imported[back]), $"{id}: changing the second color leaves the back untouched");
            var want = new Color("1e7a3c").SrgbToLinear();
            float Dist(Color c) => Mathf.Abs(c.R - want.R) + Mathf.Abs(c.G - want.G) + Mathf.Abs(c.B - want.B);
            Check(Dist(toned[belly]) < Dist(imported[belly]) * .6f,
                $"{id}: the belly moves toward the chosen second color");

            // Exact: a pure-fur vertex becomes the chosen main color times its height shading.
            rig.ApplyAppearance(new AppearanceProfile { Body = "#3c6e8f" });
            var main = new Color("3c6e8f").SrgbToLinear();
            var mainToned = rig.SkinColors()[back];
            float shade = weights[back].X;
            Check(Mathf.Abs(mainToned.R - main.R * shade) < .02f && Mathf.Abs(mainToned.G - main.G * shade) < .02f && Mathf.Abs(mainToned.B - main.B * shade) < .02f,
                $"{id}: a fur vertex takes exactly the chosen main color (linear, shaded)");

            // Arms follow the body tone they were painted in; feet keep their own color.
            rig.ApplyAppearance(new AppearanceProfile());
            var arm0 = rig.ChannelColor("wing");
            var feet0 = rig.ChannelColor("feet");
            var allTones = new AppearanceProfile { Body = "#3c6e8f", BodySecondary = "#d8c9a0", BodyMarking = "#5a2a2a" };
            rig.ApplyAppearance(allTones);
            var arm = rig.ChannelColor("wing");
            Check(!arm.IsEqualApprox(arm0) && new[] { allTones.Body, allTones.BodySecondary, allTones.BodyMarking }.Any(c => arm.IsEqualApprox(new Color(c))),
                $"{id}: arms recolor with the body");
            var handShift = rig.HandColor - def.HandColor;
            var armShift = arm - arm0;
            Check(Mathf.Abs(handShift.R - armShift.R) < .01f && Mathf.Abs(handShift.G - armShift.G) < .01f && rig.ChannelColor("feet").IsEqualApprox(feet0),
                $"{id}: hands move with the arms, feet keep their color");
            rig.ApplyAppearance(new AppearanceProfile { BodySecondary = AppearanceCatalog.IsBird(id) ? "#202020" : "" , BodyMarking = "" });
            if (AppearanceCatalog.IsBird(id)) Check(rig.HandColor.IsEqualApprox(def.HandColor), $"{id}: changing only the belly leaves the hands alone");
            rig.ApplyAppearance(new AppearanceProfile());
            Check(rig.SkinColors().SequenceEqual(imported) && rig.HandColor.IsEqualApprox(def.HandColor), $"{id}: default tones restore the imported look");

            // Presets: shared shapes plus this species' own color presets, each visibly different.
            var presets = AppearancePresets.For(id);
            Check(presets.Count >= 5 && presets[0].Id == "default", $"{id}: default, shared and species presets are listed");
            var own = AppearanceCatalog.CreatePreset(id, presets.Count - 1);
            rig.ApplyAppearance(own);
            Check(own.HasCustomBodyTones && !rig.SkinColors().SequenceEqual(imported), $"{id}: a species color preset recolors the body");
            var round = AppearanceCatalog.CreatePreset(id, 1);
            Check(round.BodyWidth > 1.05f && round.TailStyle == 1 && round.Hat == 0, $"{id}: the round preset changes shape, tail and gear");

            // Tail styles rebuild the surface; length follows the style.
            rig.ApplyAppearance(new AppearanceProfile());
            float defaultLength = TailLength(rig, id);
            rig.ApplyAppearance(new AppearanceProfile { TailStyle = 2 });
            float longLength = TailLength(rig, id);
            rig.ApplyAppearance(new AppearanceProfile { TailStyle = 3 });
            float shortLength = TailLength(rig, id);
            Check(rig.TailStyleBuilt == 3 && longLength > defaultLength * 1.15f && shortLength < defaultLength * .75f,
                $"{id}: tail styles change the tail ({shortLength:0.00} < {defaultLength:0.00} < {longLength:0.00} m)");
            Check(Enumerable.Range(0, AppearanceCatalog.TailStyleCount).All(style => CharacterTailMesh.FrameIsContinuous(id, style)),
                $"{id}: no tail style twists its rings (curled included)");
            rig.ApplyAppearance(new AppearanceProfile { TailStyle = 4 });
            var tailBox = ((MeshInstance3D)rig.Model.FindChild(id + "_tail_mesh", true, false)!).Mesh.GetAabb();
            rig.ApplyAppearance(new AppearanceProfile());
            var straightBox = ((MeshInstance3D)rig.Model.FindChild(id + "_tail_mesh", true, false)!).Mesh.GetAabb();
            Check(tailBox.End.Y > straightBox.End.Y + .1f, $"{id}: the curled tail rises above the default one");

            if (!AppearanceCatalog.IsBird(id))
            {
                var ears = rig.EarPivots.ToList();
                Check(ears.Count == 2, $"{id}: both ears are rebuilt shells");
                rig.ApplyAppearance(new AppearanceProfile { Hat = 0 });
                float straight = ears[0].Rotation.Z;
                float h0 = EarHeight(ears[0]);
                rig.ApplyAppearance(new AppearanceProfile { Hat = 0, EarAngle = 30, EarStyle = 2 });
                Check(Mathf.IsEqualApprox(ears[0].Rotation.Z - straight, Mathf.DegToRad(30), .01f) && Mathf.IsEqualApprox(ears[1].Rotation.Z - (-straight), -Mathf.DegToRad(30), .01f),
                    $"{id}: ear angle tilts both ears outward");
                Check(rig.EarStyleBuilt == 2 && EarHeight(ears[0]) > h0 * 1.15f, $"{id}: pointed ears are taller");
                rig.ApplyAppearance(new AppearanceProfile { Hat = 0, EarStyle = 1 });
                var roundBox = ((MeshInstance3D)ears[0].GetChild(0)).Mesh.GetAabb();
                Check(roundBox.Size.X > roundBox.Size.Y, $"{id}: round ears are wider than tall");
                rig.ApplyAppearance(new AppearanceProfile { Hat = 0, EarStyle = 3 });
                var foldBox = ((MeshInstance3D)ears[0].GetChild(0)).Mesh.GetAabb();
                Check(foldBox.End.Z > .08f && foldBox.Size.Y < .33f, $"{id}: folded ears bend their tip forward");
                rig.ApplyAppearance(new AppearanceProfile { Hat = 1, EarAngle = 30 });
                Check(Mathf.IsEqualApprox(ears[0].Rotation.Z, .55f, .01f), $"{id}: a hat still folds the ears to the sides");
            }
            rig.QueueFree();
        }

        // Save file keeps the new fields; old color-only files still load.
        const string path = "user://custom-tone-check.cfg";
        var saved = new AppearanceProfile { Body = "#112233", BodySecondary = "#445566", BodyMarking = "#778899", EarStyle = 3, EarAngle = -12, TailStyle = 4 };
        AppearanceStore.SaveProfile("raccoon", saved, path);
        var loaded = AppearanceStore.ReadProfile("raccoon", path);
        Check(loaded.BodySecondary == saved.BodySecondary && loaded.BodyMarking == saved.BodyMarking && loaded.EarStyle == 3 && Mathf.IsEqualApprox(loaded.EarAngle, -12) && loaded.TailStyle == 4,
            "two-tone colors, ear style/angle and tail style survive save and load");
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        // A file saved before two-tone bodies stored "body" as a tint over the whole skin.
        const string oldPath = "user://custom-tone-old.cfg";
        using (var old = new ConfigFile())
        {
            old.SetValue("raccoon", "body", "#9ecfd7");
            old.SetValue("raccoon", "hat", 0);
            old.Save(oldPath);
        }
        var migrated = AppearanceStore.ReadProfile("raccoon", oldPath);
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(oldPath));
        Check(migrated.BodySecondary.Length > 0 && migrated.BodyMarking.Length > 0 && migrated.EarAngle == 0 && migrated.EarStyle == 0 && migrated.TailStyle == 0,
            "an old tint-only save becomes three tones; new fields take their defaults");
        var oldRig = CharacterRig.Create(CharacterCatalog.Player("raccoon"));
        AddChild(oldRig);
        oldRig.ApplyAppearance(new AppearanceProfile());
        var plain = oldRig.SkinColors();
        oldRig.ApplyAppearance(migrated);
        var now = oldRig.SkinColors();
        var tint = new Color("9ecfd7").SrgbToLinear();
        float worst = Enumerable.Range(0, plain.Length).Max(i => Mathf.Max(Mathf.Abs(now[i].R - plain[i].R * tint.R), Mathf.Max(Mathf.Abs(now[i].G - plain[i].G * tint.G), Mathf.Abs(now[i].B - plain[i].B * tint.B))));
        Check(Enumerable.Range(0, plain.Length).Count(i => Mathf.Abs(now[i].G - plain[i].G * tint.G) < .02f) > plain.Length * .95f,
            $"the migrated tones reproduce the old tinted look (worst vertex {worst:0.000})");
        oldRig.QueueFree();

        var reselect = new AppearanceEditor { ProfilePath = "user://custom-tone-editor.cfg" };
        AddChild(reselect);
        var shown = CharacterRig.Create(CharacterCatalog.Player("goose"));
        AddChild(shown);
        reselect.ShowRig(shown);
        Check(reselect.FindChild("PresetChoice", true, false) is OptionButton { AllowReselect: true }, "the shown preset can be picked again to undo edits");
        reselect.QueueFree();
        shown.QueueFree();

        var bad = new AppearanceProfile();
        Check(bad.TrySet("body", "not-a-color") != null && bad.TrySet("ear_style", "x") != null && bad.TrySet("wings", 1) != null && bad.TrySet("ear_angle", 25) == null && bad.TrySet("wing", "#123456") == null,
            "preset fields are validated by type and name");

        await Shots();
    }

    private static float TailLength(CharacterRig rig, string id)
    {
        var mesh = (MeshInstance3D)rig.Model.FindChild(id + "_tail_mesh", true, false)!;
        var box = mesh.Mesh.GetAabb();
        return Mathf.Max(box.Size.Z, box.Size.Y);
    }

    private static float EarHeight(Node3D pivot) => ((MeshInstance3D)pivot.GetChild(0)).Mesh.GetAabb().Size.Y;

    private async Task Shots()
    {
        if (DisplayServer.GetName() == "headless") return;
        var world = new Node3D();
        AddChild(world);
        world.AddChild(new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("1a2224"), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color("c6dcdf"), AmbientLightEnergy = .6f, TonemapMode = Godot.Environment.ToneMapper.Agx } });
        world.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-45, -30, 0), LightEnergy = 1.4f, LightColor = new Color("ffe2be"), ShadowEnabled = true });
        world.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-20, 150, 0), LightEnergy = .45f, LightColor = new Color("a8c8ff") });
        Mat.Box(world, new Vector3(0, -.1f, 0), new Vector3(20, .2f, 10), Mat.Solid("3a4440"));
        var cam = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 7.2f, Position = new Vector3(0, 3.2f, 9) };
        world.AddChild(cam);
        cam.LookAt(new Vector3(0, 1, 0));

        // Regression (I-52): a recolored skin must render as one clean surface, without black patches.
        foreach (var id in new[] { "goose", "raccoon" })
        {
            var close = CharacterRig.Create(CharacterCatalog.Player(id));
            world.AddChild(close);
            close.ApplyAppearance(new AppearanceProfile { Hat = 0, Vest = false, Pack = false, Goggles = false, Body = "#f0f0f0", BodySecondary = "#f0f0f0", BodyMarking = "#f0f0f0" });
            close.HideBuiltInWeapon();
            cam.Size = 1.0f;
            cam.Position = new Vector3(0, .75f, 6);
            cam.LookAt(new Vector3(0, .75f, 0));
            await Frames(8);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var image = GetViewport().GetTexture().GetImage();
            image.SavePng(ProjectSettings.GlobalizePath($"res://output/custom-tones-close-{id}.png"));
            // A clean skin is a smooth gradient; depth fighting shows up as pixel-to-pixel speckle.
            int w = image.GetWidth(), h = image.GetHeight(), speckle = 0, total = 0;
            for (int y = (int)(h * .15f); y < h * .85f; y += 2)
                for (int x = (int)(w * .40f); x < w * .60f; x += 2)
                { total++; if (Mathf.Abs(image.GetPixel(x, y).Luminance - image.GetPixel(x + 1, y).Luminance) > .04f) speckle++; }
            Check(speckle < total * .005f, $"{id}: a recolored body renders as a clean surface ({speckle}/{total} speckled samples)");
            close.QueueFree();
            await Frames(2);
        }
        cam.Size = 7.2f;
        cam.Position = new Vector3(0, 3.2f, 9);
        cam.LookAt(new Vector3(0, 1, 0));

        // Row 1: species defaults. Row 2: the species' last color preset with no gear.
        string[] species = { "goose", "duck", "raccoon", "redpanda" };
        for (int row = 0; row < 2; row++)
            for (int i = 0; i < species.Length; i++)
            {
                var rig = CharacterRig.Create(CharacterCatalog.Player(species[i]));
                world.AddChild(rig);
                rig.Position = new Vector3(-4.2f + i * 2.8f, 0, row == 0 ? -1.6f : 1.6f);
                rig.RotationDegrees = new Vector3(0, 20, 0);
                var p = row == 0 ? new AppearanceProfile { Hat = 0, Vest = false, Pack = false, Goggles = false }
                    : AppearanceCatalog.CreatePreset(species[i], AppearancePresets.For(species[i]).Count - 1);
                if (row == 1) { p.Hat = 0; p.Vest = false; p.Pack = false; p.Goggles = false; }
                rig.ApplyAppearance(p);
                rig.HideBuiltInWeapon();
            }
        await Frames(10);
        await Shot("custom-tones-lineup");
        foreach (var child in world.GetChildren()) if (child is CharacterRig r) r.QueueFree();
        await Frames(2);

        // Red panda ear styles (with a +20° tilt on the last) and tail styles, seen from behind-left.
        cam.Size = 5.4f;
        for (int i = 0; i < 4; i++)
        {
            var rig = CharacterRig.Create(CharacterCatalog.Player("redpanda"));
            world.AddChild(rig);
            rig.Position = new Vector3(-3.3f + i * 2.2f, 0, 0);
            rig.RotationDegrees = new Vector3(0, -35, 0);
            rig.ApplyAppearance(new AppearanceProfile { Hat = 0, Vest = false, Pack = false, Goggles = false, EarStyle = i, EarAngle = i == 3 ? 20 : 0, TailStyle = i + 1, BodySecondary = i == 2 ? "#4a3a6a" : "" });
            rig.HideBuiltInWeapon();
        }
        await Frames(10);
        await Shot("custom-tones-ears-tails");
        foreach (var child in world.GetChildren()) if (child is CharacterRig r) r.QueueFree();
        await Frames(2);

        // Bird tail styles side by side (default, fluffy, long, short, curled), seen from the side.
        for (int i = 0; i < AppearanceCatalog.TailStyleCount; i++)
        {
            var rig = CharacterRig.Create(CharacterCatalog.Player(i % 2 == 0 ? "goose" : "duck"));
            world.AddChild(rig);
            rig.Position = new Vector3(-4f + i * 2f, 0, 0);
            rig.RotationDegrees = new Vector3(0, -80, 0);
            rig.ApplyAppearance(new AppearanceProfile { Hat = 0, Vest = false, Pack = false, Goggles = false, TailStyle = i });
            rig.HideBuiltInWeapon();
        }
        cam.Size = 6.5f;
        await Frames(10);
        await Shot("custom-tones-bird-tails");
        world.QueueFree();
        await Frames(2);
    }

    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Shot(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/" + name + ".png"));
    }
}
