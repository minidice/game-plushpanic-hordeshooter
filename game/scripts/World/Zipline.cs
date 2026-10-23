using Godot;

namespace HordeShooter;

/// <summary>
/// A zipline from a high tier down to a lower one (decisions.md L-18, doc/elevation-tiers.md 11장): one way, survivors
/// only (monsters never ride it), no shooting on the way. Squadmates ride it when their route does.
/// </summary>
public partial class Zipline : Node3D
{
    /// <summary>Acceleration profile in metres/seconds (`[임시]`).</summary>
    public const float StartSpeed = 5f, Acceleration = 12f, MaxSpeed = 24f;
    public const float ReleaseProgress = .97f, DismountSeconds = .45f;
    /// <summary>Where a rider hangs below the cable.</summary>
    public const float Hang = 2.1f;
    public const float CableHeight = 3f;
    public const int CableSegments = 24;

    public Vector3 Start { get; private set; }
    public Vector3 End { get; private set; }
    /// <summary>Step off beside the end post, instead of jumping through its centre.</summary>
    public Vector3 Landing { get; private set; }
    private float RideDistance => Start.DistanceTo(End) * ReleaseProgress;
    private static float AccelerationSeconds => (MaxSpeed-StartSpeed)/Acceleration;
    private static float AccelerationDistance => (StartSpeed+MaxSpeed)*.5f*AccelerationSeconds;
    public float TravelSeconds => RideDistance<=AccelerationDistance
        ? (Mathf.Sqrt(StartSpeed*StartSpeed+2*Acceleration*RideDistance)-StartSpeed)/Acceleration
        : AccelerationSeconds+(RideDistance-AccelerationDistance)/MaxSpeed;
    public float Seconds => TravelSeconds+DismountSeconds;
    public float SpeedAt(float seconds) => Mathf.Min(MaxSpeed,StartSpeed+Acceleration*Mathf.Max(0,seconds));
    public float ProgressAt(float seconds)
    {
        float t=Mathf.Max(0,seconds), a=Mathf.Min(t,AccelerationSeconds);
        float distance=StartSpeed*a+.5f*Acceleration*a*a+MaxSpeed*Mathf.Max(0,t-a);
        return RideDistance<.001f?1:Mathf.Clamp(distance/RideDistance,0,1);
    }
    public int StartCell { get; private set; }
    public int EndCell { get; private set; }

    public static Zipline Create(Stage stage, Vector3 start, Vector3 end)
    {
        var tiers = stage.Tiers;
        var line = new Zipline
        {
            Name = "Zipline",
            Start = start with { Y = tiers.GroundY(start) },
            End = end with { Y = tiers.GroundY(end) },
            StartCell = tiers.Index(start),
            EndCell = tiers.Index(end),
        };
        line.Landing=line.End;
        var side=((line.End-line.Start) with {Y=0}).Normalized().Cross(Vector3.Up);
        foreach(float sign in new[]{1f,-1f})
        {
            var candidate=line.End+side*(1.25f*sign);
            bool clear=true;
            foreach(var offset in new[]{Vector3.Zero,Vector3.Right*.55f,Vector3.Left*.55f,Vector3.Forward*.55f,Vector3.Back*.55f})
                clear &= !stage.Flow.IsBlocked(candidate+offset) && Mathf.Abs(tiers.GroundY(candidate+offset)-line.End.Y)<.1f;
            if(clear) { line.Landing=candidate; break; }
        }
        tiers.AddRide(start, end, line.Seconds);
        stage.AddChild(line);
        line.BuildLook(stage);
        stage.Ziplines.Add(line);
        var use = new Interactable { Name = "ZiplineUse", Position = line.Start, Prompt = "ui_use_zipline", Radius = 1.3f };
        use.OnUse = who => (who ?? stage.Player).RideZipline(line);
        stage.AddChild(use);
        stage.AddInteractable(use);
        return line;
    }

    /// <summary>Feet position <paramref name="t"/> (0…1) along the ride, with a little sag in the middle.</summary>
    public Vector3 RidePoint(float t) => CablePoint(t) - Vector3.Up * Hang;

    /// <summary>The visible cable and the rider share the same sag, attached to both model pulleys.</summary>
    public Vector3 CablePoint(float t)
    {
        var a = Start + Vector3.Up * CableHeight;
        var b = End + Vector3.Up * CableHeight;
        return a.Lerp(b, t) + Vector3.Down * (1.2f * 4 * t * (1 - t));
    }

    private void BuildLook(Stage stage)
    {
        var steel = Mat.Solid("8d9298", .4f);
        var direction = (End - Start) with { Y = 0 };
        float yaw = Mathf.Atan2(-direction.X, -direction.Z);
        var basis = new Basis(Vector3.Up, yaw);
        foreach (var at in new[] { Start, End })
        {
            var anchor = PropLibrary.Instance("zipline_anchor");
            if (anchor == null) continue;
            AddChild(anchor);
            anchor.Rotation = new Vector3(0, yaw, 0);
            // The model's pulley is 0.4 m behind its pivot, not above the post's centre.
            anchor.Position = at - basis * new Vector3(0, 0, -.4f);
            PropOcclusion.Attach(stage, anchor);
        }
        var cableRoot = new Node3D { Name = "Cable" };
        AddChild(cableRoot);
        for (int i = 0; i < CableSegments; i++)
        {
            var a = CablePoint((float)i / CableSegments);
            var b = CablePoint((float)(i + 1) / CableSegments);
            var segment = Mat.Box(cableRoot, (a + b) / 2, new Vector3(.04f, .04f, a.DistanceTo(b)), steel);
            segment.LookAt(b, Vector3.Up);
        }
    }
}
