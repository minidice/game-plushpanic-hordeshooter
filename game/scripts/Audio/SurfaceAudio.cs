using Godot;

namespace HordeShooter;

/// <summary>Resolve the contacted collider, not its visual shader. Cover shot screens inherit their owner.</summary>
public static class SurfaceAudio
{
    public const string ProfileMeta = "surface_profile", GroundMeta = "surface_ground";
    public static void Tag(Node node, string objectId)
    {
        if (SurfaceProfiles.Current.Objects.TryGetValue(objectId, out var id) && id == null) return;
        node.SetMeta(ProfileMeta, SurfaceProfiles.Current.ForObject(objectId).Id);
    }
    public static SurfaceProfile Resolve(Stage stage, GodotObject? collider, Vector3 position, Vector3 normal)
    {
        if (collider is CoverScreen screen) collider = screen.Body;
        if (collider is Node node)
        {
            if (node.HasMeta(ProfileMeta)) return SurfaceProfiles.Current.Profiles[(string)node.GetMeta(ProfileMeta)];
            if (node.HasMeta(GroundMeta)) return normal.Y > .4f ? stage.Map.SurfaceAt(position) : SurfaceProfiles.Current.Profiles["rock"];
        }
        return SurfaceProfiles.Current.Default;
    }
    public static SurfaceProfile Under(Stage stage, Node3D actor)
    {
        var p = actor.GlobalPosition;
        var hit = Rays.Cast(actor, p + Vector3.Up * .2f, p + Vector3.Down * .65f, Layers.MovementWorld | Layers.Props);
        return hit is { } h ? Resolve(stage,h.Collider,h.Position,h.Normal) : stage.Map.SurfaceAt(p);
    }
    public static void Step(Stage stage, Node3D actor, float volumeDb = 0, float pitch = 1, bool zombie = false)
    {
        var profile = Under(stage,actor);
        string id = zombie && profile.Step == "step" ? "zombie_step" : profile.Step;
        stage.Sfx.PlayAt(id,actor.GlobalPosition,volumeDb + profile.StepDb + (zombie && id != "zombie_step" ? -5 : 0),pitch);
    }
    public static void Hit(Stage stage, GodotObject? collider, Vector3 position, Vector3 normal)
    {
        var profile = Resolve(stage,collider,position,normal);
        Hit(stage,profile,position);
    }
    public static void Hit(Stage stage, SurfaceProfile profile, Vector3 position) =>
        stage.Sfx.PlayAt(profile.Hit,position,profile.HitDb);
    public static void Break(Stage stage, Node3D body, Vector3 position)
    {
        var profile = Resolve(stage,body,position,Vector3.Up);
        stage.Sfx.PlayAt(profile.Breaking,position,profile.BreakDb);
    }
}
