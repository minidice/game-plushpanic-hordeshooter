using Godot;
using System.Collections.Generic;

namespace HordeShooter;
public partial class Effects
{
    // Soft target: never remove a visible/nearby corpse just to satisfy a count.
    public CorpseRetentionSettings RetentionSettings { get; set; } = CorpseRetentionSettings.Load();
    public int CorpseTargetCount { get=>RetentionSettings.CommonTargetCount; set=>RetentionSettings.CommonTargetCount=value; }
    public int CommonCorpseCount=>_corpses.Count;
    public int NamedCorpseCount=>_namedCorpses.Count;
    public IReadOnlyList<NamedCorpseRecord> NamedCorpses=>_namedCorpses;
    private sealed class CorpseEntry(Corpse body,double created,float radius)
    {
        public readonly Corpse Body=body;
        public readonly double Created=created;
        public readonly float Radius=radius;
        public double ProtectedAt=created;
    }
    private readonly List<CorpseEntry> _corpses=new();
    private readonly List<NamedCorpseRecord> _namedCorpses=new();
    private long _nextCorpseId;
    private readonly List<(Node3D Anchor,Camera3D? Camera)> _corpseObservers=new();
    private double _corpseClock, _corpseScanElapsed;

    /// <summary>Register every player/spectator whose surroundings must remain. Remote views need
    /// their own camera proxy when multiplayer is implemented; this is not network synchronization.</summary>
    public void RegisterCorpseObserver(Node3D anchor,Camera3D? camera=null)
    {
        UnregisterCorpseObserver(anchor);
        _corpseObservers.Add((anchor,camera));
    }
    public void UnregisterCorpseObserver(Node3D anchor) => _corpseObservers.RemoveAll(o=>o.Anchor==anchor);
    private void TrackCorpse(Corpse body,float radius,MonsterDefinition? monster,Vector3 deathPosition)
    {
        if(monster?.KeepsCorpse!=true){_corpses.Add(new(body,_corpseClock,radius));return;}
        var record=new NamedCorpseRecord{Name="NamedCorpse",CorpseId=++_nextCorpseId,MonsterId=monster.Id,
            CreatedAt=_corpseClock,ExpiresAt=_corpseClock+RetentionSettings.NamedLifetime,Visual=body};
        AddChild(record);record.GlobalPosition=deathPosition;
        body.Reparent(record);body.NamedRecord=record;_namedCorpses.Add(record);
    }
    public override void _Process(double delta) => AdvanceCorpseCleanup(delta);

    /// <summary>
    /// A common corpse below this height has left the level (a blast threw it over a wall): it would fall forever,
    /// never sleep and never be seen again, so the next pass removes it whatever the count or camera (I-70).
    /// </summary>
    public const float FellOutY = -8f;

    // Also used by deterministic tests without waiting ten wall-clock seconds.
    internal void AdvanceCorpseCleanup(double delta)
    {
        _corpseClock+=delta;_corpseScanElapsed+=delta;
        if(_corpseScanElapsed<RetentionSettings.CleanupInterval)return;
        _corpseScanElapsed=0;
        _corpseObservers.RemoveAll(o=>!IsInstanceValid(o.Anchor)||o.Anchor.IsQueuedForDeletion()||!o.Anchor.IsInsideTree());
        _corpses.RemoveAll(e=>!IsInstanceValid(e.Body)||e.Body.IsQueuedForDeletion());
        for(int i=_namedCorpses.Count-1;i>=0;i--)
        {
            var record=_namedCorpses[i];
            if(!IsInstanceValid(record)||record.IsQueuedForDeletion()){_namedCorpses.RemoveAt(i);continue;}
            if(_corpseClock<record.ExpiresAt)continue;
            // Independent lifetime: neither camera protection nor common count can change this deadline.
            record.QueueFree();_namedCorpses.RemoveAt(i);
        }
        for(int i=_corpses.Count-1;i>=0;i--)
            if(_corpses[i].Body.GlobalPosition.Y<FellOutY){_corpses[i].Body.QueueFree();_corpses.RemoveAt(i);}
        int removed=0;
        // Insertion order is death order. Protected older corpses do not block eligible newer ones.
        for(int i=0;i<_corpses.Count;i++)
        {
            var entry=_corpses[i];
            if(IsCorpseProtected(entry)) {entry.ProtectedAt=_corpseClock;continue;}
            if(_corpses.Count<=Mathf.Max(0,CorpseTargetCount)||removed>=RetentionSettings.RemovalsPerPass||
                _corpseClock-entry.Created<RetentionSettings.MinimumAge||_corpseClock-entry.ProtectedAt<RetentionSettings.UnseenGrace)continue;
            entry.Body.QueueFree();_corpses.RemoveAt(i--);removed++;
        }
    }
    private bool IsCorpseProtected(CorpseEntry entry)
    {
        // During scene/camera setup, missing observers must never mean that nobody can see a corpse.
        if(_corpseObservers.Count==0)return true;
        var at=entry.Body.GlobalPosition;
        foreach(var observer in _corpseObservers)
        {
            var offset=observer.Anchor.GlobalPosition-at;offset.Y=0;
            if(offset.LengthSquared()<=RetentionSettings.KeepDistance*RetentionSettings.KeepDistance)return true;
            if(IsInstanceValid(observer.Camera)&&observer.Camera!.IsInsideTree()&&
                OverlapsCorpseView(observer.Camera,at,entry.Radius))return true;
        }
        return false;
    }
    /// <summary>On any registered observer's screen (no distance rule). With no observers yet, assume visible.</summary>
    public bool IsOnScreen(Vector3 at,float radius)
    {
        if(_corpseObservers.Count==0)return true;
        foreach(var observer in _corpseObservers)
            if(IsInstanceValid(observer.Camera)&&observer.Camera!.IsInsideTree()&&OverlapsCorpseView(observer.Camera,at,radius))return true;
        return false;
    }
    private static bool OverlapsCorpseView(Camera3D camera,Vector3 center,float radius)
    {
        // Plane/bounds overlap also handles bodies straddling the camera plane far off to its side.
        // Merely finding a corner behind the camera must not protect an otherwise invisible corpse.
        var transform=camera.GetCameraTransform();
        var inside=transform.Origin-transform.Basis.Z*((camera.Near+camera.Far)*.5f);
        var viewport=camera.GetViewport().GetVisibleRect().Size;
        float span=camera.Projection==Camera3D.ProjectionType.Orthogonal?camera.Size:
            2*Mathf.Tan(Mathf.DegToRad(camera.Fov)*.5f)*center.DistanceTo(transform.Origin);
        float margin=span*96/Mathf.Max(1,Mathf.Min(viewport.X,viewport.Y));
        foreach(var plane in camera.GetFrustum())
        {
            float sign=plane.DistanceTo(inside)>=0?1:-1;
            var n=plane.Normal.Abs();
            float bound=radius*(n.X+n.Y+n.Z)+margin;
            if(plane.DistanceTo(center)*sign < -bound)return false;
        }
        return true; // Wall occlusion is deliberately ignored to avoid popping behind props.
    }
}
