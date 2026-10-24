using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Read-only adapters: timers and damage remain owned by gameplay/network code.</summary>
public static class StatusVisualBinding
{
    public static void Attach(Player player) => StatusVisualController.Attach(player.Rig).LiveSource = () => Read(player);
    public static void Attach(Enemy enemy) => StatusVisualController.Attach(enemy.Rig).LiveSource = () => Read(enemy);

    public static IEnumerable<StatusVisualState> Read(Player player)
    {
        if (!GodotObject.IsInstanceValid(player) || player.IsQueuedForDeletion() || player.IsDead) yield break;
        if (player.IsRestrained) yield return new("restrained");
        float stun = Math.Max(player.StunTime, player.DazeTime);
        if (stun > 0) yield return new("stun", stun);
        if (player.BlindTime > 0) yield return new("blind", player.BlindTime);
        if (player.WeakTime > 0) yield return new("weak", player.WeakTime);
    }

    public static IEnumerable<StatusVisualState> Read(Enemy enemy)
    {
        if (!GodotObject.IsInstanceValid(enemy) || enemy.IsQueuedForDeletion() || enemy.IsDead) yield break;
        if (enemy.BurnTime > 0) yield return new("burn", enemy.BurnTime);
        if (enemy.WeakTime > 0) yield return new("weak", enemy.WeakTime);
        if (enemy.IsPuppet)
        {
            if (enemy.Track != null && ((NetEnemyFlags)enemy.Track.Flags).HasFlag(NetEnemyFlags.Stunned)) yield return new("stun");
        }
        else
        {
            if (enemy.StunTime > 0) yield return new("stun", enemy.StunTime);
            if (enemy.FleeTime > 0) yield return new("fear", enemy.FleeTime);
            if (enemy.FloatTime > 0) yield return new("floating", enemy.FloatTime);
        }
    }
}
