using Godot;

namespace HordeShooter;

/// <summary>
/// Parrot weapon wing: retain imported feather topology and palette surfaces, pose the whole wing
/// about its authored shoulder. No player-specific vertex coordinates or per-frame mesh allocation.
/// Ordinary node transforms also keep hit, silhouette and status passes on the same geometry.
/// </summary>
internal partial class CharacterAuthoredWing : MeshInstance3D
{
    private Transform3D _rest;
    private Vector3 _shoulder, _restRoot, _restWrist, _meshWrist;
    private float _side;

    public static CharacterAuthoredWing Create(Node3D parent, MeshInstance3D source, Transform3D rest, Vector3 shoulder, string name, float side)
    {
        var wing = new CharacterAuthoredWing { Name = name, Mesh = source.Mesh, Transform = rest,
            _rest = rest, _shoulder = shoulder, _side = side };
        for (int i = 0; i < source.Mesh.GetSurfaceCount(); i++)
            wing.SetSurfaceOverrideMaterial(i, source.GetActiveMaterial(i));
        // Imported wings hang downward, but their proximal surface extends above the named pivot.
        // Measure both caps: using the pivot as the source root stretches that upper surface behind
        // the shoulder when aiming. Only held poses map the measured proximal cap onto the pivot.
        float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
        for (int i = 0; i < source.Mesh.GetSurfaceCount(); i++)
            foreach (var vertex in source.Mesh.SurfaceGetArrays(i)[(int)Godot.Mesh.ArrayType.Vertex].AsVector3Array())
            {
                float y = (rest * vertex).Y; bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
            }
        var distal = Vector3.Zero; var proximal = Vector3.Zero; int count = 0, proximalCount = 0;
        float cap = bottom + (top - bottom) * .12f;
        float upperCap = top - (top - bottom) * .12f;
        for (int i = 0; i < source.Mesh.GetSurfaceCount(); i++)
            foreach (var vertex in source.Mesh.SurfaceGetArrays(i)[(int)Godot.Mesh.ArrayType.Vertex].AsVector3Array())
            {
                var p = rest * vertex;
                if (p.Y <= cap) { distal += p; count++; }
                if (p.Y >= upperCap) { proximal += p; proximalCount++; }
            }
        wing._restRoot = proximalCount > 0 ? proximal / proximalCount : shoulder;
        wing._restWrist = count > 0 ? distal / count : shoulder + Vector3.Down * .1f;
        wing._meshWrist = rest.AffineInverse() * wing._restWrist;
        parent.AddChild(wing);
        return wing;
    }

    public void PoseWing(Vector3 target, bool holding, float size, float running)
    {
        Basis pose;
        var sourceRoot = holding ? _restRoot : _shoulder;
        if (!holding)
            pose = new Basis(Vector3.Back, -_side * running * .35f).Scaled(Vector3.One * size);
        else
        {
            var restDirection = _restWrist - sourceRoot;
            var direction = target - _shoulder;
            float restLength = restDirection.Length(), length = direction.Length();
            if (restLength < .001f || length < .001f) { Transform = _rest; return; }
            var axis = restDirection / restLength;
            // Stretch only along the shoulder-to-tip axis. Width and depth retain authored volume.
            float stretch = length / restLength - size;
            var axial = new Basis(Vector3.Right * size + axis * (axis.X * stretch),
                Vector3.Up * size + axis * (axis.Y * stretch), Vector3.Back * size + axis * (axis.Z * stretch));
            pose = new Basis(new Quaternion(axis, direction / length)) * axial;
        }
        Transform = new Transform3D(pose, _shoulder - pose * sourceRoot) * _rest;
    }

    public Vector3 Wrist => GlobalTransform * _meshWrist;
}
