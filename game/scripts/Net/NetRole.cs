namespace HordeShooter;

/// <summary>
/// This PC's part in a match (doc/multiplayer-sync-design.md). Offline and Host decide the game (enemies, damage,
/// pickups, objectives); a Client only shows what the host decided, apart from its own player's movement.
/// </summary>
public enum NetRole { Offline, Host, Client }
