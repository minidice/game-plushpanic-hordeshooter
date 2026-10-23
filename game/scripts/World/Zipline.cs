using Godot;

namespace HordeShooter;

/// <summary>
/// A zipline from a high tier down to a lower one (decisions.md L-18, doc/elevation-tiers.md 11장): one way, survivors
/// only (monsters never ride it), no shooting on the way. Squadmates ride it when their route does. Placeholder posts
/// and cable until the final model (local Codex work).
/// </summary>
public partial class Zipline : Node3D
{
    /// <summary>Riding speed along the cable (`[임시]`).</summary>
    public const float Speed = 12f;
    /// <summary>Where a rider hangs below the cable.</summary>
    public const float Hang = 2.1f;

    public Vector3 Start { get; private set; }
    public Vector3 End { get; private set; }
    public float Seconds => Start.DistanceTo(End) / Speed;
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
        tiers.AddRide(start, end, line.Seconds);
        stage.AddChild(line);
        line.BuildLook();
        stage.Ziplines.Add(line);
        var use = new Interactable { Name = "ZiplineUse", Position = line.Start, Prompt = "ui_use_zipline", Radius = 1.3f };
        use.OnUse = who => (who ?? stage.Player).RideZipline(line);
        stage.AddChild(use);
        stage.AddInteractable(use);
        return line;
    }

    /// <summary>Feet position <paramref name="t"/> (0…1) along the ride, with a little sag in the middle.</summary>
    public Vector3 RidePoint(float t)
    {
        var a = Start + Vector3.Up * .4f;
        var b = End + Vector3.Up * .4f;
        return a.Lerp(b, t) + Vector3.Down * (1.2f * 4 * t * (1 - t));
    }

    private void BuildLook()
    {
        var wood = Mat.Solid("6b5236", .8f);
        var steel = Mat.Solid("8d9298", .4f);
        float postHeight = Hang + .7f;
        foreach (var at in new[] { Start, End })
            Mat.Box(this, at + Vector3.Up * postHeight / 2, new Vector3(.18f, postHeight, .18f), wood);
        var a = Start + Vector3.Up * postHeight;
        var b = End + Vector3.Up * postHeight;
        var cable = Mat.Box(this, (a + b) / 2, new Vector3(.04f, .04f, a.DistanceTo(b)), steel);
        cable.LookAt(b, Vector3.Up);
    }
}
