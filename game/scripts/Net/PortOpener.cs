using Godot;
using System.Threading.Tasks;

namespace HordeShooter;

/// <summary>
/// Asks the host's router to let the game port in (UPnP), so a friend outside the house can join from the room list
/// (menu-multiplayer-plan.md 5.3). Players on the same router never need it. Routers without UPnP need the UDP port
/// forwarded once by hand.
/// </summary>
public static class PortOpener
{
    public enum Result { Idle, Trying, Opened, Failed }

    public static Result State { get; private set; } = Result.Idle;
    /// <summary>The router's outside address when the port was opened.</summary>
    public static string External { get; private set; } = "";
    private static int _port;

    /// <summary>Runs off the main thread: discovery waits up to two seconds for the router.</summary>
    public static async void Open(int port)
    {
        if (State is Result.Trying or Result.Opened && _port == port) return;
        State = Result.Trying;
        _port = port;
        var (ok, external) = await Task.Run(() =>
        {
            var upnp = new Upnp();
            if (upnp.Discover(2000, 2, "") != (int)Upnp.UpnpResult.Success || upnp.GetGateway() is not { } gateway || !gateway.IsValidGateway())
                return (false, "");
            bool mapped = upnp.AddPortMapping(port, port, "Horde Shooter", "UDP", 0) == (int)Upnp.UpnpResult.Success;
            return (mapped, mapped ? upnp.QueryExternalAddress() : "");
        });
        // Closed again while we were asking: take the mapping back down.
        if (State != Result.Trying || _port != port) { if (ok) RemoveMapping(port); return; }
        State = ok ? Result.Opened : Result.Failed;
        External = external;
        GD.Print(ok ? $"[net] router opened UDP {port} (outside address {external})" : $"[net] router did not open UDP {port} (no UPnP)");
    }

    /// <summary>Removes the mapping this game made. Nothing to do if it never opened one.</summary>
    public static void Close()
    {
        bool had = State == Result.Opened;
        State = Result.Idle;
        External = "";
        if (had) RemoveMapping(_port);
    }

    private static void RemoveMapping(int port) => Task.Run(() =>
    {
        var upnp = new Upnp();
        if (upnp.Discover(2000, 2, "") == (int)Upnp.UpnpResult.Success) upnp.DeletePortMapping(port, "UDP");
    });
}
