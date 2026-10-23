using Godot;

namespace HordeShooter;

/// <summary>
/// Sliding slab that blocks movement, bullets, sight and the flow field while closed.
/// <see cref="SetProgress"/> lets the crescendo shutter rise gradually while still blocking.
/// </summary>
public partial class Door : Node3D
{
    public bool IsOpen { get; private set; }
    public Vector3 Size { get; private set; }

    private Stage _stage = null!;
    private StaticBody3D _body = null!;
    private CollisionShape3D _shape = null!;
    private Node3D _slab = null!;
    private Vector3 _openOffset;
    private Vector3 _closedPosition;
    private bool _hideWhenOpen;
    private Tween? _motion;

    public static Door Create(Stage stage, Vector3 center, Vector3 size, Color color, Vector3 openOffset, bool open = false, Node3D? visual = null)
    {
        var door = new Door { Name = "Door", Position = center, Size = size, _stage = stage, _openOffset = openOffset };
        stage.AddChild(door);
        door._body = new StaticBody3D { CollisionLayer = Layers.World, CollisionMask = 0 };
        door.AddChild(door._body);
        door._shape = new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = Vector3.Up * size.Y * .5f };
        door._body.AddChild(door._shape);
        door._closedPosition = visual == null ? Vector3.Up * size.Y * .5f : Vector3.Zero;
        door._hideWhenOpen = visual != null;
        if (visual != null)
        {
            door._slab = visual;
            door.AddChild(visual);
        }
        else
        {
            door._slab = Mat.Box(door, Vector3.Up * size.Y * .5f, size, Mat.Solid(color, .6f));
            // Hazard stripes so doors read as interactive from above.
            Mat.Box(door._slab, new Vector3(0, size.Y * .5f + .01f, 0), new Vector3(size.X * 1.02f, .02f, size.Z * .9f), Mat.Solid("d9b23c", .5f));
            // Raised steel ribs travel with the slab; the jamb remains fixed around the opening.
            var edge=Mat.Solid("263943",.48f);
            for(float y=-size.Y*.5f+.18f;y<size.Y*.5f-.08f;y+=.26f)
            {
                Mat.Box(door._slab,new Vector3(size.X*.5f+.012f,y,0),new Vector3(.025f,.025f,size.Z*.96f),edge);
                Mat.Box(door._slab,new Vector3(-size.X*.5f-.012f,y,0),new Vector3(.025f,.025f,size.Z*.96f),edge);
            }
            foreach(float side in new[]{-1f,1f})
            {
                Mat.Box(door,new Vector3(0,size.Y*.5f,side*(size.Z*.5f+.06f)),new Vector3(size.X+.14f,size.Y+.16f,.12f),edge);
                Mat.Box(door,new Vector3(size.X*.5f+.08f,size.Y*.83f,side*(size.Z*.5f+.07f)),new Vector3(.035f,.18f,.07f),Mat.Glow(new Color("f5bf62"),1.4f));
            }
            Mat.Box(door,new Vector3(0,size.Y+.08f,0),new Vector3(size.X+.14f,.16f,size.Z+.25f),edge);
        }
        door.SetOpen(open, 0);
        return door;
    }

    /// <summary>How long it takes to swing or roll open when another PC is told it opened (the shutter is slower).</summary>
    public float OpenSeconds { get; set; } = .5f;

    public void SetOpen(bool open, float seconds = .5f)
    {
        IsOpen = open;
        _body.CollisionLayer = open ? 0 : Layers.World;
        _shape.Disabled = open;
        _stage.RebuildFlow();
        _motion?.Kill();
        _slab.Visible = true;
        var target = _closedPosition + (open ? _openOffset : Vector3.Zero);
        if (seconds <= 0)
        {
            _slab.Position = target;
            _slab.Visible = !(_hideWhenOpen && open);
        }
        else
        {
            _motion = CreateTween();
            _motion.TweenProperty(_slab, "position", target, seconds).SetTrans(Tween.TransitionType.Back);
            if (_hideWhenOpen) _motion.TweenCallback(Callable.From(() => _slab.Visible = !IsOpen));
        }
    }

    /// <summary>Stamp this door into the flow field if it is closed (called from a full rebuild).</summary>
    public void StampFlow()
    {
        if (!IsOpen) _stage.Flow.SetBlocked(Position, Size, Stage.WallPad, true);
    }

    /// <summary>Visual-only partial opening (0..1); collision stays until <see cref="SetOpen"/>.</summary>
    public void SetProgress(float t)
    {
        if (IsOpen) return;
        _slab.Position = _closedPosition + _openOffset * Mathf.Clamp(t, 0, 1) * .35f;
    }
}
