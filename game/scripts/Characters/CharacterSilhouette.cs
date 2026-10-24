using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>
/// How an enemy is drawn for the local player (decisions.md C-23): hidden, a silhouette (dark unlit body with a cool rim,
/// no detail) or as it is. Appearing is never a hard pop: hidden → silhouette fills the shape in, silhouette → visible
/// drains the silhouette away to reveal the colors (both a fast screen-space dither, no transparency sorting).
/// Disappearing is immediate. Original material overrides and overlays are kept and restored.
/// </summary>
public partial class CharacterRig
{
    /// <summary>Seconds for the shape to fill in, and for the colors to show through after it.</summary>
    public const float ShapeFillSeconds = .15f, ColorFillSeconds = .2f;
    /// <summary>Meta on a mesh while the fade overlay sits on it: the overlay to restore (the hit flash reads it too).</summary>
    public const string BaseOverlayMeta = "base_overlay";
    private const string Amount = "reveal_amount";

    private static ShaderMaterial? _silhouetteMaterial;
    private readonly List<(MeshInstance3D Mesh, Material? Override)> _silhouetteMeshes = new();
    private VisionSystem.Visibility _shownAs = VisionSystem.Visibility.Visible;
    private float _shape = 1, _color = 1;
    private bool _colorFading;
    private RevealFader? _fader;

    /// <summary>The silhouette material is on the meshes (fully or filling in).</summary>
    public bool IsSilhouette { get; private set; }
    /// <summary>0..1: how much of the shape is drawn.</summary>
    public float ShapeFill => _shape;
    /// <summary>0..1: how much of the real color shows (1 = shown as it is).</summary>
    public float ColorFill => _color;

    /// <summary>Shared by every rig; released by <see cref="ClearSilhouetteCache"/> on quit.</summary>
    public static ShaderMaterial SilhouetteMaterial => _silhouetteMaterial ??= new ShaderMaterial
    {
        Shader = new Shader
        {
            Code = """
                shader_type spatial;
                render_mode unshaded, cull_back;
                #include "res://shaders/character_grip_deform.gdshaderinc"
                void vertex() { deform_grip(VERTEX, NORMAL); }
                uniform vec4 body : source_color = vec4(0.06, 0.08, 0.11, 1.0);
                uniform vec4 rim : source_color = vec4(0.52, 0.62, 0.78, 1.0);
                // 1 = every pixel drawn. Lower values leave a 4x4 ordered-dither pattern of holes.
                instance uniform float reveal_amount = 1.0;
                const float BAYER[16] = float[](0., 8., 2., 10., 12., 4., 14., 6., 3., 11., 1., 9., 15., 7., 13., 5.);
                void fragment() {
                    ivec2 p = ivec2(mod(FRAGCOORD.xy, 4.0));
                    if ((BAYER[p.x + p.y * 4] + .5) / 16. >= reveal_amount) discard;
                    float edge = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.2);
                    ALBEDO = mix(body.rgb, rim.rgb, edge);
                }
                """,
        },
    };

    public static void ClearSilhouetteCache() => _silhouetteMaterial = null;

    /// <summary>Hidden, silhouette or as it is; appearing animates, hiding is immediate.</summary>
    public void ShowAs(VisionSystem.Visibility v)
    {
        if (v == _shownAs) return;
        var from = _shownAs;
        _shownAs = v;
        switch (v)
        {
            case VisionSystem.Visibility.Hidden:
                Visible = false;
                EndColorFade();
                _shape = 0;
                _color = 0;
                ApplySilhouette(true, 0);
                break;
            case VisionSystem.Visibility.Silhouette:
                Visible = true;
                EndColorFade();
                _color = 0;
                if (from == VisionSystem.Visibility.Visible) _shape = 1; // stepping back into the dark: no animation
                ApplySilhouette(true, _shape);
                break;
            case VisionSystem.Visibility.Visible:
                Visible = true;
                if (from == VisionSystem.Visibility.Hidden) _shape = 0;
                ApplySilhouette(true, _shape);
                break;
        }
        Fader.SetProcess(true);
    }

    private RevealFader Fader
    {
        get
        {
            if (_fader == null)
            {
                _fader = new RevealFader { Name = "Reveal", Rig = this };
                AddChild(_fader);
            }
            return _fader;
        }
    }

    /// <summary>One frame of the appear animation. Returns false when nothing is left to animate.</summary>
    internal bool StepReveal(float dt)
    {
        if (_shownAs == VisionSystem.Visibility.Hidden) return false;
        if (_shape < 1)
        {
            _shape = Mathf.Min(1, _shape + dt / ShapeFillSeconds);
            SetAmount(_shape);
            return true;
        }
        if (_shownAs == VisionSystem.Visibility.Silhouette) return false;
        if (!_colorFading)
        {
            // The full shape is there: put the real materials back and cover them with a draining silhouette.
            ApplySilhouette(false, 1);
            BeginColorFade();
            _color = 0;
        }
        _color = Mathf.Min(1, _color + dt / ColorFillSeconds);
        SetOverlayAmount(1 - _color);
        if (_color < 1) return true;
        EndColorFade();
        return false;
    }

    private void ApplySilhouette(bool on, float amount)
    {
        if (on && !IsSilhouette)
        {
            // Re-collect each time: weapons and procedural arms can change between silhouettes.
            _silhouetteMeshes.Clear();
            Collect(Pose);
            foreach (var (mesh, _) in _silhouetteMeshes) mesh.MaterialOverride = SilhouetteMaterial;
            IsSilhouette = true;
        }
        else if (!on && IsSilhouette)
        {
            foreach (var (mesh, original) in _silhouetteMeshes)
                if (IsInstanceValid(mesh) && mesh.MaterialOverride == SilhouetteMaterial) mesh.MaterialOverride = original;
            IsSilhouette = false;
        }
        if (on) SetAmount(amount);
    }

    private void SetAmount(float amount)
    {
        foreach (var (mesh, _) in _silhouetteMeshes)
            if (IsInstanceValid(mesh)) mesh.SetInstanceShaderParameter(Amount, amount);
    }

    private void BeginColorFade()
    {
        _colorFading = true;
        foreach (var (mesh, _) in _silhouetteMeshes)
        {
            if (!IsInstanceValid(mesh)) continue;
            // A null overlay cannot be stored as meta (it would erase it): false stands for "none".
            mesh.SetMeta(BaseOverlayMeta, mesh.MaterialOverlay != null ? mesh.MaterialOverlay : false);
            mesh.MaterialOverlay = SilhouetteMaterial;
        }
    }

    private void SetOverlayAmount(float amount)
    {
        foreach (var (mesh, _) in _silhouetteMeshes)
            if (IsInstanceValid(mesh) && mesh.MaterialOverlay == SilhouetteMaterial) mesh.SetInstanceShaderParameter(Amount, amount);
    }

    private void EndColorFade()
    {
        if (!_colorFading) return;
        _colorFading = false;
        foreach (var (mesh, _) in _silhouetteMeshes)
        {
            if (!IsInstanceValid(mesh) || !mesh.HasMeta(BaseOverlayMeta)) continue;
            // A hit flash may own the overlay right now; it restores the base overlay itself when it ends.
            if (mesh.MaterialOverlay == SilhouetteMaterial) mesh.MaterialOverlay = mesh.GetMeta(BaseOverlayMeta).Obj as Material;
            mesh.RemoveMeta(BaseOverlayMeta);
            mesh.SetInstanceShaderParameter(Amount, 1f);
        }
    }

    private void Collect(Node node)
    {
        if (node.HasMeta(StatusVisualController.InternalMeta)) return;
        if (node is MeshInstance3D mesh) _silhouetteMeshes.Add((mesh, mesh.MaterialOverride));
        foreach (var child in node.GetChildren()) Collect(child);
    }

    /// <summary>Kept for callers that only know "silhouette or not" (no animation).</summary>
    public void SetSilhouette(bool on) => ShowAs(on ? VisionSystem.Visibility.Silhouette : VisionSystem.Visibility.Visible);
}
