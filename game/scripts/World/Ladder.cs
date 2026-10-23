using Godot;

namespace HordeShooter;

/// <summary>
/// A ladder up a tier's cliff (decisions.md L-16 · L-18, doc/elevation-tiers.md 5.1장). Survivors use it with the
/// interact key at either end; squadmates take it when their route goes up or down it; armed animals and wall climbers
/// path through it. Placeholder rails and rungs until the final model (local Codex work).
/// </summary>
public partial class Ladder : Node3D
{
    /// <summary>Where a survivor stands at the foot of the ladder, and where they step off on the tier above.</summary>
    public Vector3 Bottom { get; private set; }
    public Vector3 Top { get; private set; }
    /// <summary>The ladder's line on the cliff face, at the foot and at the rim.</summary>
    public Vector3 Foot { get; private set; }
    public Vector3 Rim { get; private set; }
    public int Tiers { get; private set; }
    public int BottomCell { get; private set; }
    public int TopCell { get; private set; }

    public static Ladder Create(Stage stage, Vector3 bottom, Vector3 top)
    {
        var tiers = stage.Tiers;
        var face = (bottom + top) * .5f;
        var ladder = new Ladder
        {
            Name = "Ladder",
            Bottom = bottom with { Y = tiers.GroundY(bottom) },
            Top = top with { Y = tiers.GroundY(top) },
            BottomCell = tiers.Index(bottom),
            TopCell = tiers.Index(top),
        };
        ladder.Foot = new Vector3(face.X, ladder.Bottom.Y, face.Z) + ((ladder.Bottom - ladder.Top) with { Y = 0 }) * .15f;
        ladder.Rim = new Vector3(face.X, ladder.Top.Y, face.Z);
        ladder.Tiers = Mathf.Max(1, Mathf.RoundToInt((ladder.Top.Y - ladder.Bottom.Y) / TierMap.Height));
        tiers.AddLadder(bottom, top);
        stage.AddChild(ladder);
        ladder.GlobalPosition = ladder.Foot;
        ladder.BuildLook();
        stage.Ladders.Add(ladder);

        var upUse = new Interactable { Name = "LadderUp", Position = ladder.Bottom, Prompt = "ui_use_ladder_up", Radius = 1.3f };
        upUse.OnUse = who => (who ?? stage.Player).ClimbLadder(ladder, up: true);
        var downUse = new Interactable { Name = "LadderDown", Position = ladder.Top, Prompt = "ui_use_ladder_down", Radius = 1.3f };
        downUse.OnUse = who => (who ?? stage.Player).ClimbLadder(ladder, up: false);
        foreach (var use in new[] { upUse, downUse })
        {
            stage.AddChild(use);
            stage.AddInteractable(use);
        }
        return ladder;
    }

    private void BuildLook()
    {
        float height = Rim.Y - Foot.Y + .9f;
        var across = (Top - Bottom) with { Y = 0 };
        var side = across.LengthSquared() > .001f ? across.Normalized().Cross(Vector3.Up) * .28f : Vector3.Right * .28f;
        var wood = Mat.Solid("7a5a38", .8f);
        foreach (float s in new[] { 1f, -1f })
        {
            var rail = Mat.Box(this, side * s + Vector3.Up * height / 2, new Vector3(.07f, height, .07f), wood);
            rail.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
        }
        for (float y = .3f; y < height - .1f; y += .35f)
        {
            var rung = Mat.Box(this, Vector3.Up * y, new Vector3(.05f, .05f, .05f), wood);
            rung.Scale = new Vector3(Mathf.Abs(side.X) * 2 / .05f + 1, 1, Mathf.Abs(side.Z) * 2 / .05f + 1);
        }
    }
}
