using System;
using UnityEngine;

[Serializable]
public class CoopLocationOption
{
    public string displayName = "Demo City";
    public string sceneName = "Demo_City_Universal_RenderPipeline";
}

[Serializable]
public struct CoopRoomSettings
{
    public string roomName;
    public int maxPlayers;
    public CoopLocationOption location;
    public int port;

    public CoopRoomSettings(string roomName, int maxPlayers, CoopLocationOption location, int port)
    {
        this.roomName = roomName;
        this.maxPlayers = Mathf.Clamp(maxPlayers, 2, 8);
        this.location = location;
        this.port = Mathf.Clamp(port, 1024, 65535);
    }
}

public enum CoopSessionRole
{
    None,
    Host,
    Client
}

public static class CoopSessionState
{
    public static bool IsCoopSession { get; private set; }
    public static CoopSessionRole Role { get; private set; } = CoopSessionRole.None;
    public static string RoomName { get; private set; } = string.Empty;
    public static string RoomId { get; private set; } = string.Empty;
    public static string HostAddress { get; private set; } = string.Empty;
    public static int Port { get; private set; } = 7777;
    public static int MaxPlayers { get; private set; } = 2;
    public static string LocationDisplayName { get; private set; } = "Demo City";
    public static string SceneName { get; private set; } = "Demo_City_Universal_RenderPipeline";

    public static bool IsHost => Role == CoopSessionRole.Host;
    public static bool IsClient => Role == CoopSessionRole.Client;

    public static void ConfigureHost(CoopRoomSettings settings, string roomId, string hostAddress)
    {
        IsCoopSession = true;
        Role = CoopSessionRole.Host;
        RoomName = Sanitize(settings.roomName, "Комната");
        RoomId = Sanitize(roomId, string.Empty);
        HostAddress = Sanitize(hostAddress, "127.0.0.1");
        Port = Mathf.Clamp(settings.port, 1024, 65535);
        MaxPlayers = Mathf.Clamp(settings.maxPlayers, 2, 8);
        LocationDisplayName = Sanitize(settings.location?.displayName, "Demo City");
        SceneName = Sanitize(settings.location?.sceneName, "Demo_City_Universal_RenderPipeline");
    }

    public static void ConfigureClient(
        string roomName,
        string roomId,
        string hostAddress,
        int port,
        int maxPlayers,
        string locationDisplayName,
        string sceneName)
    {
        IsCoopSession = true;
        Role = CoopSessionRole.Client;
        RoomName = Sanitize(roomName, "Комната");
        RoomId = Sanitize(roomId, string.Empty);
        HostAddress = Sanitize(hostAddress, "127.0.0.1");
        Port = Mathf.Clamp(port, 1024, 65535);
        MaxPlayers = Mathf.Clamp(maxPlayers, 2, 8);
        LocationDisplayName = Sanitize(locationDisplayName, "Demo City");
        SceneName = Sanitize(sceneName, "Demo_City_Universal_RenderPipeline");
    }

    public static void Clear()
    {
        IsCoopSession = false;
        Role = CoopSessionRole.None;
        RoomName = string.Empty;
        RoomId = string.Empty;
        HostAddress = string.Empty;
        Port = 7777;
        MaxPlayers = 2;
        LocationDisplayName = "Demo City";
        SceneName = "Demo_City_Universal_RenderPipeline";
    }

    private static string Sanitize(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim().Replace("|", " ");
    }
}
