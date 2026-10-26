using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Feedback pack export (studio/share/README.md, decisions.md T-27): one model — a player (plain or equipped), a common monster
/// pack's first member, or a prop — built with the same code as the Studio previews, photographed on two turntable
/// rings (eye level and the game camera's 55° pitch, orthographic like the game) and written as a GLB with hidden parts
/// removed. Files go to studio/share/.build/&lt;name&gt;/ only; nothing else is written.
/// </summary>
public partial class StudioPreview
{
    private static readonly (string Name, float Pitch)[] ShareRings = { ("eye", 12f), ("game", CameraRig.Pitch) };

    private static string ShareRoot => Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "studio", "share", ".build"));

    private async Task<object> ShareItem(JsonElement data)
    {
        string kind = data.GetProperty("kind").GetString()!, id = data.GetProperty("id").GetString()!, name = data.GetProperty("name").GetString()!;
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-z0-9_-]{1,40}$")) throw new ArgumentException("공유 이름이 올바르지 않습니다.");
        int frames = Math.Clamp(data.TryGetProperty("frames", out var f) ? f.GetInt32() : 16, 4, 36);
        int size = Math.Clamp(data.TryGetProperty("size", out var s) ? s.GetInt32() : 768, 256, 1400);
        string outDir = Path.Combine(ShareRoot, name);
        if (!outDir.StartsWith(ShareRoot)) throw new ArgumentException("공유 폴더 밖입니다.");
        Directory.CreateDirectory(outDir);

        ClearLibrarySubject();
        if (_rig != null) { _viewport.RemoveChild(_rig); _rig.QueueFree(); _rig = null; }
        _platform.Visible = false;
        Node3D subject;
        string title;
        if (kind == "player")
        {
            var def = _characters.SingleOrDefault(d => d.Id == id) ?? throw new ArgumentException("알 수 없는 캐릭터입니다.");
            bool gear = data.TryGetProperty("gear", out var g) && g.GetBoolean();
            ReplaceRig(def);
            var profile = new AppearanceProfile();
            profile.Normalize();
            _rig!.ApplyAppearance(profile);
            _rig.HideBuiltInWeapon();
            var weapon = WeaponVisual.Build(ArmoryCatalog.Weapon("m4"), _rig.HandColor);
            _rig.Weapon.AddChild(weapon.Root);
            _rig.BindWeaponGrip(weapon);
            _rig.SetPreviewVisibility(gear, gear);
            subject = _rig;
            title = Loc.Text(def.DisplayName);
        }
        else
        {
            _librarySubject = new Node3D { Name = "ShareSubject", ProcessMode = ProcessModeEnum.Disabled };
            _viewport.AddChild(_librarySubject);
            subject = _librarySubject;
            if (kind == "common")
            {
                var identity = CommonRoster.Current.CreatePack(id, 1)[0];
                var enemy = CreateMonsterSubject(_librarySubject, CommonRoster.Current.Compose(identity), CommonRoster.Current.LookFor(identity));
                title = Loc.Text(enemy.Monster.NameKey);
            }
            else if (kind == "object")
            {
                _librarySubject.AddChild(PropLibrary.Instance(id) ?? throw new ArgumentException("모델을 가져오지 못했습니다."));
                title = id; // the pack builder takes the display name from the library list
            }
            else throw new ArgumentException("알 수 없는 종류입니다.");
        }

        // One frame covering every turn, so the model keeps its size while it spins.
        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Aabb bounds = VisualBounds(subject);
        for (int i = 1; i < 8; i++) { subject.RotationDegrees = new Vector3(0, i * 45, 0); bounds = bounds.Merge(VisualBounds(subject)); }
        var center = bounds.GetCenter();
        float radius = Mathf.Max(bounds.Size.Length() / 2, .1f);

        var oldSize = _viewport.Size;
        _viewport.Size = new Vector2I(size, size);
        _camera.Projection = Camera3D.ProjectionType.Orthogonal;
        _camera.Far = Mathf.Max(100, radius * 8);
        float across = Mathf.Max(bounds.Size.X, bounds.Size.Z);
        try
        {
            foreach (var (ring, pitch) in ShareRings)
            {
                float rad = Mathf.DegToRad(pitch);
                // Square frame: the larger of the projected height and the width, with a small margin.
                _camera.Size = Mathf.Max(bounds.Size.Y * Mathf.Cos(rad) + across * Mathf.Sin(rad), across) * 1.12f;
                var dir = new Vector3(0, Mathf.Sin(rad), Mathf.Cos(rad));
                _camera.Position = center + dir * radius * 3;
                _camera.LookAt(center, Vector3.Up);
                for (int i = 0; i < frames; i++)
                {
                    subject.RotationDegrees = new Vector3(0, -i * 360f / frames, 0);
                    _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
                    for (int k = 0; k < 2; k++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var image = _viewport.GetTexture().GetImage();
                    var error = image.SaveWebp(Path.Combine(outDir, $"{ring}_{i:00}.webp"), true, .84f);
                    if (error != Error.Ok) throw new IOException("사진을 저장하지 못했습니다: " + error);
                }
            }
        }
        finally { _viewport.Size = oldSize; }

        // The 3D copy: facing front, without the parts the preview hides (built-in weapon, gear when plain).
        subject.RotationDegrees = Vector3.Zero;
        foreach (var hidden in Descendants(subject).OfType<VisualInstance3D>().Where(v => !v.IsVisibleInTree()).ToList())
            if (IsInstanceValid(hidden)) hidden.Free();
        var document = new GltfDocument();
        var state = new GltfState();
        var append = document.AppendFromScene(subject, state);
        if (append != Error.Ok) throw new IOException("GLB로 바꾸지 못했습니다: " + append);
        var write = document.WriteToFilesystem(state, Path.Combine(outDir, "model.glb"));
        if (write != Error.Ok) throw new IOException("GLB를 저장하지 못했습니다: " + write);
        int meshes = Descendants(subject).OfType<MeshInstance3D>().Count();
        return new { name, kind, id, title, frames, size, rings = ShareRings.Select(r => new { r.Name, r.Pitch }).ToArray(), meshes,
            height = bounds.Size.Y, width = Mathf.Max(bounds.Size.X, bounds.Size.Z), glb = new FileInfo(Path.Combine(outDir, "model.glb")).Length };
    }
}
