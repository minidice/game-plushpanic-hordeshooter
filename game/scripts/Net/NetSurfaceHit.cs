using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Host-confirmed environmental contact. No collider lookup or damage occurs on replay.</summary>
public readonly record struct NetSurfaceHit(SurfaceProfile Profile, Vector3 Position)
{
    public const int MaxPerShot = 3;

    public static void Write(NetWriter writer, IReadOnlyList<NetSurfaceHit>? hits)
    {
        int count = hits?.Count ?? 0;
        if (count > MaxPerShot) throw new ArgumentOutOfRangeException(nameof(hits));
        writer.U8(count);
        if (hits == null) return;
        foreach (var hit in hits)
        {
            writer.U16(NetIds.Surface(hit.Profile.Id));
            writer.Pos(hit.Position);
        }
    }

    public static NetSurfaceHit[] Read(NetReader reader)
    {
        int count = reader.U8();
        if (count > MaxPerShot || reader.Remaining < count * (2 + NetWriter.PositionBytes)) throw new FormatException("Invalid shot surface contacts");
        var hits = new NetSurfaceHit[count];
        for (int i = 0; i < count; i++) hits[i] = new(NetIds.SurfaceAt(reader.U16()), reader.Pos());
        return hits;
    }
}
