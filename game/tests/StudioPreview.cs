using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

// Development-only stdin/stdout bridge. Never opens a game menu or writes an appearance profile.
public partial class StudioPreview : Node
{
    private readonly ConcurrentQueue<string> _requests = new();
    private SubViewport _viewport = null!;
    private Camera3D _camera = null!;
    private CharacterRig? _rig;
    private Node3D? _librarySubject;
    private MeshInstance3D _platform = null!;
    private List<CharacterDefinition> _characters = new();
    private bool _busy;
    private volatile bool _closed;
    private static void Send(object value) => Console.WriteLine("STUDIO:" + JsonSerializer.Serialize(value));

    public override void _Ready()
    {
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);
        DisplayServer.WindowSetPosition(new Vector2I(-10000, -10000));
        _characters = CharacterCatalog.LoadAll(CharacterCatalog.PlayerDir)
            .Where(d => !d.Id.StartsWith("test_")).ToList();
        _viewport = new SubViewport { Size = new Vector2I(900, 900), OwnWorld3D = true,
            Msaa3D = Viewport.Msaa.Msaa4X, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled };
        AddChild(_viewport);
        // Match MainMenu.BuildPreview lighting and tone mapping, using the actual game renderer.
        _viewport.AddChild(new WorldEnvironment { Environment = new Godot.Environment {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("1a2224"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color("c6dddf"),
            AmbientLightEnergy = .45f, TonemapMode = Godot.Environment.ToneMapper.Agx } });
        _viewport.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50,-35,0), LightEnergy = 1.2f,
            LightColor = new Color("ffe2b8"), ShadowEnabled = true });
        _viewport.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-20,150,0), LightEnergy = .35f,
            LightColor = new Color("a8c8ff") });
        _platform = Mat.Mesh(_viewport, new CylinderMesh { TopRadius = 1.6f, BottomRadius = 1.6f, Height = .1f },
            new Vector3(0,-.05f,0), Mat.Solid("3a4440"));
        _camera = new Camera3D { Fov = 35, Current = true };
        _viewport.AddChild(_camera);
        Task.Run(() => { string? line; while ((line = Console.ReadLine()) != null) _requests.Enqueue(line); _closed = true; });
        Send(new { ready = true });
    }

    public override void _Process(double delta)
    {
        if (_closed && !_busy) { QuitHygiene.Collect(); GetTree().Quit(); return; }
        if (!_busy && _requests.TryDequeue(out var line)) Handle(line);
    }

    public override void _ExitTree() => _lookStage.Free();

    private async void Handle(string line)
    {
        _busy = true;
        int id = 0;
        try
        {
            using var message = JsonDocument.Parse(line);
            var request = message.RootElement;
            id = request.GetProperty("id").GetInt32();
            var method = request.GetProperty("method").GetString();
            if (method == "catalog") Send(new { id, result = Catalog() });
            else if (method == "roster") Send(new { id, result = RosterCatalog() });
            else if (method == "roster_render") Send(new { id, result = await RenderRoster(request.GetProperty("data")) });
            else if (method == "statuses") Send(new { id, result = StatusCatalog() });
            else if (method == "status_render") Send(new { id, result = await RenderStatuses(request.GetProperty("data")) });
            else if (method == "library") Send(new { id, result = Library() });
            else if (method == "weapons") Send(new { id, result = Weapons() });
            else if (method == "media") Send(new { id, result = new { effects = EffectEntries(), sounds = SoundEntries(),
                loadedAt = DateTimeOffset.Now.ToString("O"), renderer = "Godot Forward+ · 실제 게임 이펙트 / 현재 로컬 음원" } });
            else if (method == "effect_render") Send(new { id, result = await RenderEffect(request.GetProperty("data")) });
            else if (method == "sound_preview") Send(new { id, result = PreviewSound(request.GetProperty("data")) });
            else if (method == "library_render") Send(new { id, result = await RenderLibrary(request.GetProperty("data")) });
            else if (method == "render")
            {
                var data = request.GetProperty("data");
                var species = data.GetProperty("species").GetString();
                var def = _characters.SingleOrDefault(d => d.Id == species) ?? throw new ArgumentException("알 수 없는 캐릭터입니다.");
                var profile = new AppearanceProfile();
                foreach (var field in data.GetProperty("fields").EnumerateObject())
                {
                    Variant value = field.Value.ValueKind switch {
                        JsonValueKind.String => field.Value.GetString()!, JsonValueKind.Number => field.Value.GetDouble(),
                        JsonValueKind.True => true, JsonValueKind.False => false,
                        _ => throw new ArgumentException("지원하지 않는 값입니다.") };
                    var error = profile.TrySet(field.Name, value);
                    if (error != null) throw new ArgumentException(error);
                }
                profile.Normalize();
                ReplaceRig(def);
                _rig!.ApplyAppearance(profile);
                _rig.HideBuiltInWeapon();
                string weaponId = data.TryGetProperty("weapon_id", out var chosenWeapon) ? chosenWeapon.GetString()! : "m4";
                var weapon = WeaponVisual.Build(ArmoryCatalog.Weapon(weaponId), _rig.HandColor);
                _rig.Weapon.AddChild(weapon.Root);
                _rig.BindWeaponGrip(weapon);
                _rig.SetPreviewVisibility(data.GetProperty("gear").GetBoolean(), data.GetProperty("weapon").GetBoolean());
                _rig.RotationDegrees = new Vector3(0, Mathf.Clamp(data.GetProperty("rotation").GetSingle(), -180, 180), 0);
                float zoom = Mathf.Clamp(data.GetProperty("zoom").GetSingle(), .75f, 1.45f);
                float h = def.ColliderHeight;
                _camera.Projection = Camera3D.ProjectionType.Perspective;
                _platform.Visible = true;
                _camera.Position = new Vector3(0, h * .75f, (h * 2.6f + 1.5f) / zoom);
                _camera.LookAt(new Vector3(0, h * .45f, 0), Vector3.Up);
                _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
                // Permit deferred mesh changes and GPU uploads before reading the rendered viewport.
                for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var image = _viewport.GetTexture().GetImage();
                Send(new { id, result = new { png = Convert.ToBase64String(image.SavePngToBuffer()), species,
                    weapon_id = weaponId, pose_family = weapon.Pose.ToString() } });
            }
            else throw new ArgumentException("Unknown Studio method");
        }
        catch (Exception e) { Send(new { id, error = e.Message }); }
        finally { _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled; _busy = false; }
    }

    private void ReplaceRig(CharacterDefinition def)
    {
        ClearLibrarySubject();
        if (_rig != null) { _viewport.RemoveChild(_rig); _rig.QueueFree(); }
        _rig = CharacterRig.Create(def);
        _viewport.AddChild(_rig);
    }

    private object Catalog()
    {
        var species = new List<object>();
        foreach (var def in _characters)
        {
            ReplaceRig(def);
            // Read the real editor's controls without invoking any edit signals. The deliberately
            // nonexistent path prevents loading the developer's saved appearance; ShowRig only reads.
            var editor = new AppearanceEditor { ProfilePath = "res://tests/__studio_unsaved__.cfg" };
            editor.ShowRig(_rig!);
            var fields = new List<object>();
            foreach (var node in Descendants(editor))
            {
                var name = node.Name.ToString();
                string tab = "";
                for (Node? parent = node; parent != null; parent = parent.GetParent())
                    if (parent.Name.ToString().StartsWith("Tab_")) tab = parent.Name.ToString()[4..];
                string label = node.GetParent()?.GetChildren().OfType<Label>().FirstOrDefault()?.Text ?? name;
                if (node is HSlider slider && name.StartsWith("Scale_"))
                    fields.Add(new { key = name[6..], label, tab, type = "range", value = slider.Value,
                        min = slider.MinValue, max = slider.MaxValue, step = slider.Step });
                else if (node is ColorPickerButton color && name.StartsWith("Color_") && !color.Disabled)
                {
                    string channel = name[6..];
                    string key = channel switch { "body2" => "body_secondary", "body3" => "body_marking",
                        "hat" => "hat_color", "vest" => "vest_color", "pack" => "pack_color", _ => channel };
                    fields.Add(new { key, label, tab, type = "color", value = "#" + color.Color.ToHtml(false) });
                }
                else if (node is OptionButton choice && name != "PresetChoice")
                {
                    string key = name == "HatChoice" ? "hat" : Snake(name.Replace("Choice_", ""));
                    fields.Add(new { key, label, tab, type = "select", value = choice.Selected,
                        options = Enumerable.Range(0, choice.ItemCount).Select(i => choice.GetItemText(i)).ToArray() });
                }
                else if (node is CheckButton toggle && name.StartsWith("Wear"))
                    fields.Add(new { key = name[4..].ToLowerInvariant(), label = toggle.Text, tab, type = "checkbox", value = toggle.ButtonPressed });
            }
            species.Add(new { id = def.Id, name = Loc.Text(def.DisplayName), description = Loc.Text(def.Description),
                source = def.ResourcePath, fields });
            editor.Free();
        }
        return new { species, tabs = new[] { "body", "face", "head", "limbs", "tail", "gear" }
            .Select(key => new { key, label = Loc.Text("custom_tab_" + key) }).ToArray(),
            renderer = "Godot Forward+ · 게임 커스터마이징 조명", loadedAt = DateTimeOffset.Now.ToString("O") };
    }

    private static string Snake(string value) => Regex.Replace(value, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();
    private static IEnumerable<Node> Descendants(Node parent)
    {
        foreach (var node in parent.GetChildren())
        { yield return node; foreach (var child in Descendants(node)) yield return child; }
    }
}
