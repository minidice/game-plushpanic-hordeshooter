using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HordeShooter;

/// <summary>
/// The public room list on lobby.minidice.net (Minidice docs/LOBBY_SPEC.md): hosts put their room on it, the menu
/// lists it, and players join the host directly. Every call fails quietly (null) so joining by address still works
/// when the list server cannot be reached.
/// </summary>
public static class RoomDirectory
{
    public const string DefaultUrl = "https://lobby.minidice.net/hordeshooter";
    /// <summary>This game's rooms path. Empty turns the list off (tests); --lobby=URL points at a local server.</summary>
    public static string Url { get; set; } = DefaultUrl;
    public static bool Enabled => Url.Length > 0;

    public sealed record Room(string Id, string Name, string Version, int Players, int MaxPlayers, string State,
        string Address, int Port, bool SameNetwork, string[] Lan)
    {
        public bool Playing => State == "playing";
        public bool Full => Players >= MaxPlayers;
        public bool SameVersion => Version == NetLobby.Version;
        /// <summary>Behind the same router the public address usually loops nowhere: use the host's LAN address.</summary>
        public string JoinAddress => SameNetwork && Lan is { Length: > 0 } ? Lan[0] : Address;
    }

    /// <summary>What a host tells the list about its room.</summary>
    public sealed record Ad(string Version, string Name, int Players, int MaxPlayers, string State, int Port, string[] Lan);

    public sealed record Listing(string Id, string Token);

    private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };

    /// <summary>Reads --lobby=URL once (a local lobby server for tests).</summary>
    public static void ReadArgs()
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--lobby=")) Url = arg["--lobby=".Length..].TrimEnd('/');
    }

    private sealed record ListBody(Room[] Rooms);

    /// <summary>Null when the list server could not be reached.</summary>
    public static async Task<List<Room>?> List()
    {
        if (!Enabled) return null;
        try
        {
            using var res = await Http.GetAsync(Url + "/rooms");
            if (!res.IsSuccessStatusCode) return null;
            var body = JsonSerializer.Deserialize<ListBody>(await res.Content.ReadAsStringAsync(), Json);
            return body?.Rooms?.ToList() ?? new List<Room>();
        }
        catch (Exception e) { GD.Print($"[rooms] list failed: {e.Message}"); return null; }
    }

    public static async Task<Listing?> Register(Ad ad)
    {
        if (!Enabled) return null;
        try
        {
            using var res = await Http.PostAsync(Url + "/rooms", Body(ad));
            if (!res.IsSuccessStatusCode) { GD.Print($"[rooms] register refused: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}"); return null; }
            return JsonSerializer.Deserialize<Listing>(await res.Content.ReadAsStringAsync(), Json);
        }
        catch (Exception e) { GD.Print($"[rooms] register failed: {e.Message}"); return null; }
    }

    /// <summary>True when kept, false when the list forgot the room (register again), null when unreachable.</summary>
    public static async Task<bool?> Refresh(Listing listing, Ad ad)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, $"{Url}/rooms/{listing.Id}") { Content = Body(ad) };
            request.Headers.Authorization = new("Bearer", listing.Token);
            using var res = await Http.SendAsync(request);
            if (res.IsSuccessStatusCode) return true;
            return (int)res.StatusCode is 404 or 403 ? false : null;
        }
        catch (Exception e) { GD.Print($"[rooms] refresh failed: {e.Message}"); return null; }
    }

    public static async Task Remove(Listing listing)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"{Url}/rooms/{listing.Id}");
            request.Headers.Authorization = new("Bearer", listing.Token);
            using var res = await Http.SendAsync(request);
        }
        catch (Exception e) { GD.Print($"[rooms] close failed: {e.Message}"); }
    }

    private static StringContent Body(Ad ad) => new(JsonSerializer.Serialize(ad, Json), Encoding.UTF8, "application/json");
}
