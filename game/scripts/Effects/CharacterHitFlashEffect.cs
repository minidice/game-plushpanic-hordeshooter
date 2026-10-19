using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Lives with the rig, including after it is transferred to a corpse.</summary>
public partial class CharacterHitFlashEffect : Node
{
    private const float Duration = .1f;
    public const float PeakAlpha = .72f;
    private readonly StandardMaterial3D _white = new()
    {
        AlbedoColor = Colors.White,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        RenderPriority = 127,
    };
    private readonly List<(MeshInstance3D Mesh, Material? Original, Material Flash)> _meshes = new();
    private readonly Dictionary<Material, Material> _overlayCopies = new();
    private float _remaining;

    public override void _Ready() => SetProcess(_remaining > 0);

    public void Play(Node3D model)
    {
        // Newly equipped hands/weapons are included; existing meshes and overlay chains are reused.
        _meshes.RemoveAll(entry => !IsInstanceValid(entry.Mesh));
        CacheMeshes(model);
        _remaining = Duration;
        _white.AlbedoColor = new Color(1, 1, 1, PeakAlpha);
        foreach (var entry in _meshes) entry.Mesh.MaterialOverlay = entry.Flash;
        SetProcess(true);
    }

    private void CacheMeshes(Node node)
    {
        if (node is MeshInstance3D mesh && !_meshes.Exists(entry => entry.Mesh == mesh))
        {
            var original = mesh.MaterialOverlay;
            _meshes.Add((mesh, original, original == null ? _white : CopyOverlay(original)));
        }
        foreach (var child in node.GetChildren()) CacheMeshes(child);
    }

    private Material CopyOverlay(Material original)
    {
        if (_overlayCopies.TryGetValue(original, out var cached)) return cached;
        // Never append to the shared zombie material or any imported material itself.
        var copy = (Material)original.Duplicate();
        copy.NextPass = original.NextPass == null ? _white : CopyOverlay(original.NextPass);
        _overlayCopies.Add(original, copy);
        return copy;
    }

    public override void _Process(double delta)
    {
        _remaining = Mathf.Max(0, _remaining - (float)delta);
        _white.AlbedoColor = new Color(1, 1, 1, PeakAlpha * _remaining / Duration);
        if (_remaining > 0) return;
        foreach (var entry in _meshes)
            if (IsInstanceValid(entry.Mesh) && entry.Mesh.MaterialOverlay == entry.Flash)
                entry.Mesh.MaterialOverlay = entry.Original;
        SetProcess(false);
    }
}
