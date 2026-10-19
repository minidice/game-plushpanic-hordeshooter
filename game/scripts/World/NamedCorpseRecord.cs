using Godot;

namespace HordeShooter;
/// <summary>Fixed, nonphysical death anchor. Future loot belongs here, never to local ragdoll motion.
/// IDs are stage-local only; host replication/persistence and actual loot are not implemented.</summary>
public partial class NamedCorpseRecord : Node3D
{
    public long CorpseId { get; internal set; }
    public string MonsterId { get; internal set; }="";
    public double CreatedAt { get; internal set; }
    public double ExpiresAt { get; internal set; }
    private Corpse? _visual;
    public Corpse? Visual { get=>IsInstanceValid(_visual)&&!_visual!.IsQueuedForDeletion()?_visual:null; internal set=>_visual=value; }
}
