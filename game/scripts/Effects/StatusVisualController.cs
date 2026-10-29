using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>One visual composer per rig. It never owns combat timers, mesh materials or collision.</summary>
public partial class StatusVisualController : Node3D
{
    public const string InternalMeta = "status_visual_layer";
    public const int MaxDecorations = 2, MaxIcons = 2;
    public CharacterRig Rig { get; private set; } = null!;
    public IReadOnlyList<StatusVisualState> Active => _active;
    public string? SurfaceId { get; private set; }
    public int DecorationCount => _parts.Count;
    public bool Displayed { get; private set; }
    private StatusVisualState[] _active = Array.Empty<StatusVisualState>();
    private readonly List<StatusVisualParts> _parts = new();
    private readonly List<(MeshInstance3D source, MeshInstance3D layer)> _surfaces = new();
    private readonly List<Node3D> _badges = new();
    private Node3D _body = null!, _icons = null!;
    private ShaderMaterial _surface = null!;
    private float _age, _scan;
    private string _signature = "";
    public Func<IEnumerable<StatusVisualState>>? LiveSource { get; set; }
    private float _sample;

    public static StatusVisualController Attach(CharacterRig rig)
    {
        if (rig.GetNodeOrNull<StatusVisualController>("StatusVisuals") is { } existing) return existing;
        var effect = new StatusVisualController { Name = "StatusVisuals", Rig = rig, ProcessPriority = 5 };
        rig.AddChild(effect);
        effect._body = new Node3D { Name = "Attachments" }; effect.AddChild(effect._body);
        effect._icons = new Node3D { Name = "StatusIcons" }; effect.AddChild(effect._icons);
        effect._surface = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/status_surface.gdshader") };
        return effect;
    }
    public void SetStates(IEnumerable<StatusVisualState> states)
    {
        var byId = new Dictionary<string, StatusVisualState>();
        foreach (var state in states)
        {
            StatusVisualCatalog.Current.Get(state.Id);
            if (!float.IsFinite(state.Remaining) || state.Remaining < 0 || state.Stacks < 1 || state.Stacks > 5)
                throw new ArgumentException("Invalid visual state");
            var old = byId.GetValueOrDefault(state.Id);
            byId[state.Id] = state with { Remaining = Math.Max(state.Remaining, old.Remaining), Stacks = Math.Max(state.Stacks, old.Stacks) };
        }
        _active = byId.Values.OrderByDescending(s => StatusVisualCatalog.Current.Get(s.Id).Priority).ThenBy(s => s.Id, StringComparer.Ordinal).ToArray();
        string signature = string.Join(";", _active.Select(s => s.Id + ":" + s.Stacks));
        if (signature != _signature)
        {
            _signature = signature;
            Rebuild();
        }
        UpdateVisual(0);
    }
    public void Clear() => SetStates(Array.Empty<StatusVisualState>());
    private void Rebuild()
    {
        foreach (var p in _parts) { p.Visible = false; p.QueueFree(); }
        foreach (var b in _badges) { b.Visible = false; b.QueueFree(); }
        _parts.Clear(); _badges.Clear();
        var skin = _active.Select(s => StatusVisualCatalog.Current.Get(s.Id)).FirstOrDefault(d => d.SurfaceAlpha > 0);
        SurfaceId = skin?.Id;
        _surface.SetShaderParameter("tint", skin?.Color ?? Colors.Transparent);
        _surface.SetShaderParameter("pattern", skin?.Surface ?? 0);
        _surface.SetShaderParameter("strength", skin?.SurfaceAlpha ?? 0);
        if (_active.Length == 0)
        {
            foreach (var (_, layer) in _surfaces) if (IsInstanceValid(layer)) { layer.Visible = false; layer.QueueFree(); }
            _surfaces.Clear(); return;
        }
        foreach (var state in _active.Take(MaxDecorations))
            _parts.Add(StatusVisualParts.Create(_body, Rig.Definition, StatusVisualCatalog.Current.Get(state.Id), state.Stacks));
        float h = Rig.Definition.ColliderHeight;
        _icons.Position = Vector3.Up * h * 1.12f;
        int count = Math.Min(MaxIcons, _active.Length);
        for (int i = 0; i < count; i++)
        {
            var state = _active[i];
            var badge = new Sprite3D { Texture = StatusVisualIcons.Texture(state.Id), PixelSize = h * .0013f,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Shaded = false,
                Position = new((i - (count - 1) * .5f) * h * .2f, 0, 0) };
            _icons.AddChild(badge); _badges.Add(badge);
            if (state.Stacks > 1) AddLabel("×" + state.Stacks, badge.Position + new Vector3(h*.07f,-h*.09f,0));
        }
        if (_active.Length > MaxIcons) AddLabel("+" + (_active.Length-MaxIcons), new(h*.27f,0,0));
        _scan = 0;
    }
    private void AddLabel(string text, Vector3 at)
    {
        var label = new Label3D { Text = text, Font = UiFonts.Get(), FontSize = 40, OutlineSize = 10, PixelSize = Rig.Definition.ColliderHeight*.0011f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Position = at, Modulate = Colors.White, OutlineModulate = new Color("172024") };
        _icons.AddChild(label); _badges.Add(label);
    }
    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (LiveSource != null)
        {
            _sample -= dt;
            if (_sample <= 0) { _sample = .05f; SetStates(LiveSource()); }
        }
        _age += dt; UpdateVisual(dt);
    }
    private void Collect(Node node)
    {
        if (node.HasMeta(InternalMeta) || node == Rig.Weapon) return;
        if (node is MeshInstance3D mesh && mesh.Mesh != null && !_surfaces.Any(s => s.source == mesh))
        {
            var layer = new MeshInstance3D { Name = "StatusSurface", Mesh = mesh.Mesh, MaterialOverride = _surface,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, CustomAabb = mesh.CustomAabb };
            layer.SetMeta(InternalMeta, true); mesh.AddChild(layer); _surfaces.Add((mesh, layer));
        }
        foreach (var child in node.GetChildren()) Collect(child);
    }
    private void UpdateVisual(float dt)
    {
        if (_body == null) return;
        Displayed = _active.Length != 0 && Rig.IsVisibleInTree() && !Rig.IsSilhouette && Rig.ColorFill >= .999f;
        _body.Visible = _icons.Visible = Displayed;
        _body.Transform = Rig.Pose.Transform;
        if (_active.Length == 0) return;
        // Rotate the layout, not only each quad: heading must never collapse the badge row.
        if (GetViewport().GetCamera3D() is { } camera)
            _icons.GlobalBasis = camera.GlobalBasis.Orthonormalized().Scaled(Rig.GlobalBasis.Scale);
        _scan -= dt;
        if (SurfaceId != null && _scan <= 0)
        {
            _scan = .25f;
            _surfaces.RemoveAll(s => !IsInstanceValid(s.source) || !IsInstanceValid(s.layer));
            Collect(Rig.Pose);
        }
        _surface.SetShaderParameter("clock", _age);
        _surface.SetShaderParameter("actor_origin", Rig.Pose.GlobalPosition);
        _surface.SetShaderParameter("actor_up", Rig.Pose.GlobalBasis.Y.Normalized());
        _surface.SetShaderParameter("actor_height", Rig.Definition.ColliderHeight * Rig.Pose.GlobalBasis.Y.Length());
        foreach (var (source, layer) in _surfaces)
        {
            if (!IsInstanceValid(source) || !IsInstanceValid(layer)) continue;
            if(layer.Mesh!=source.Mesh)layer.Mesh=source.Mesh;
            if(layer.Skin!=source.Skin)
            {
                layer.Skin=source.Skin;
                layer.Skeleton=source.Skin!=null&&source.GetNodeOrNull<Skeleton3D>(source.Skeleton) is { } skeleton
                    ?layer.GetPathTo(skeleton):new NodePath("");
            }
            layer.CustomAabb=source.CustomAabb;
            layer.Visible = Displayed && SurfaceId != null && !Rig.HitFlashing;
            if (CharacterGripSurface.IsSurface(source))
                foreach (string key in new[] { "grip_surface", "grip", "side", "wing_size", "running", "holding", "bird" })
                    layer.SetInstanceShaderParameter(key, source.GetInstanceShaderParameter(key));
        }
        foreach (var part in _parts) part.Step(_age);
    }
    public override void _ExitTree()
    {
        // Surfaces are children of the original meshes, not of this controller.
        foreach (var (_, layer) in _surfaces) if (IsInstanceValid(layer) && !layer.IsQueuedForDeletion()) layer.QueueFree();
    }
}
