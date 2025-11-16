using Godot;
using System;
using System.Collections.Generic;
using Godot.Collections;

/// <summary>
/// Core multiplayer manager handling:
/// - Server creation (listen server model)
/// - Client connections
/// - Player data synchronization via RPC
/// - Name updates across all peers
/// 
/// This is an autoload singleton that persists across scene changes.
/// 
/// RPC Flow:
/// 1. Client connects -> Server detects via PeerConnected signal
/// 2. Server asks client for name via RPC: RequestPlayerName()
/// 3. Client sends name back via RPC: Server_ReceivePlayerName()
/// 4. Server updates internal dictionary and multicasts to all clients via RPC: Multicast_SyncAllPlayers()
/// 5. All clients update their local player list
/// </summary>
public partial class MultiplayerManager : Node
{
    public static MultiplayerManager Instance { get; private set; }

    // Host or client
    public static string Type { get; private set; } = "TBD";

    const string HOST = "HOST";
    const string CLIENT = "CLIENT";

    /// <summary>
    /// Dictionary storing all connected players.
    /// Key: Peer ID (long), Value: PlayerData
    /// </summary>
    public static System.Collections.Generic.Dictionary<long, PlayerData> PlayersData = 
        new System.Collections.Generic.Dictionary<long, PlayerData>();

    [Export]
    private int port = 7000;

    [Export]
    private int maxPlayers = 8;

    /// <summary>
    /// Stores the local player's name (set when hosting or joining).
    /// </summary>
    private static string localPlayerName = "";

    /// <summary>
    /// Emitted when a new player successfully connects and provides their data.
    /// Clients can listen to this to update UI.
    /// </summary>
    [Signal]
    public delegate void PlayerConnectedEventHandler(PlayerData playerData);

    /// <summary>
    /// Emitted when player list is synchronized from server.
    /// UI should refresh the entire player list when this fires.
    /// </summary>
    [Signal]
    public delegate void PlayerListUpdatedEventHandler();

    /// <summary>
    /// Emitted when a player disconnects from the lobby.
    /// </summary>
    [Signal]
    public delegate void PlayerDisconnectedEventHandler(long peerId);

    public override void _Ready()
    {
        Instance = this;
        
        // Subscribe to Godot's built-in multiplayer signals
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        
        Logger.Info("MultiplayerManager initialized");
    }

    #region Server/Client Creation

    /// <summary>
    /// Creates a listen server (host acts as both server and player).
    /// Call this when the player clicks "Host".
    /// </summary>
    public void CreateServer(string playerName)
    {
        localPlayerName = playerName;
        
        var peer = new ENetMultiplayerPeer();
        Error error = peer.CreateServer(port, maxPlayers);

        if (error != Error.Ok)
        {
            Logger.Error($"Failed to create server: {error}");
            return;
        }

        Multiplayer.MultiplayerPeer = peer;
        Logger.Info($"Server created on port {port}");
        
        // Add the host as a player
        long hostId = Multiplayer.GetUniqueId();
        PlayersData[hostId] = new PlayerData(playerName, hostId);
        Logger.Debug($"Host added with ID: {hostId}");
        Type = HOST;
    }

    /// <summary>
    /// Connects to a server as a client.
    /// Call this when the player clicks "Join".
    /// </summary>
    public void JoinServer(string playerName, string hostIp = "127.0.0.1")
    {
        localPlayerName = playerName;
        
        var peer = new ENetMultiplayerPeer();
        Error error = peer.CreateClient(hostIp, port);

        if (error != Error.Ok)
        {
            Logger.Error($"Failed to connect to server: {error}");
            return;
        }

        Multiplayer.MultiplayerPeer = peer;
        Logger.Info($"Connecting to server at {hostIp}:{port}");
        Type = CLIENT;
    }

    #endregion

    #region Connection Callbacks

    /// <summary>
    /// Called on server when a new peer connects.
    /// Server asks the client for their name.
    /// </summary>
    private void OnPeerConnected(long peerId)
    {
        Logger.Debug($"Peer connected: {peerId}");
        
        if (Multiplayer.IsServer())
        {
            // Create placeholder entry
            PlayersData[peerId] = new PlayerData("<Connecting...>", peerId);            
            // Ask the client for their name. Since we have the client peerID, we execute this
            // method in the computer of our recently connected client.
            RpcId(peerId, MethodName.Client_GetRequestPlayerName);
        }
    }

    /// <summary>
    /// Called when a peer disconnects.
    /// </summary>
    private void OnPeerDisconnected(long peerId)
    {
        Logger.Info($"Peer disconnected: {peerId}");
        
        if (PlayersData.ContainsKey(peerId))
        {
            string playerName = PlayersData[peerId].Name;
            PlayersData.Remove(peerId);
            Logger.Info($"Player '{playerName}' left the lobby");
            
            // Notify all clients about the updated player list
            if (Multiplayer.IsServer())
            {
                Rpc(MethodName.Multicast_SyncAllPlayers, SerializePlayersDictionary(PlayersData));
            }
            
            EmitSignal(SignalName.PlayerDisconnected, peerId);
        }
    }

    #endregion

    #region RPC Methods - Player Name Synchronization

    /// <summary>
    /// [SERVER -> CLIENT RPC]
    /// Server asks client for their player name.
    /// Executed on the client side.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Client_GetRequestPlayerName()
    {
        Logger.Trace("Server requesting player name");
        
        // Get local player name (you'd get this from UI input)
        string localPlayerName = GetPlayerNameFromUI();
        long localPeerId = Multiplayer.GetUniqueId();
        
        PlayerData playerData = new PlayerData(localPlayerName, localPeerId);
        
        // Send back to server
        RpcId(1, MethodName.Server_ReceivePlayerName, playerData.AsJsonString());
        Logger.Debug($"Sent player name to server: {localPlayerName}");
    }

    /// <summary>
    /// [CLIENT -> SERVER RPC]
    /// Client sends their player name to the server.
    /// Executed on the server side.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Server_ReceivePlayerName(string playerDataJson)
    {
        if (!Multiplayer.IsServer())
            return;
        
        PlayerData playerData = PlayerData.FromJson(playerDataJson);
        Logger.Debug($"Server received player data: {playerData}");
        
        // Update server's player dictionary
        PlayersData[playerData.PeerId] = playerData;
        
        // Multicast updated player list to all clients
        Rpc(MethodName.Multicast_SyncAllPlayers, SerializePlayersDictionary(PlayersData));
        
        Logger.Info($"Player '{playerData.Name}' joined (ID: {playerData.PeerId})");
    }

    /// <summary>
    /// [SERVER -> ALL CLIENTS RPC]
    /// Server sends complete player list to all clients.
    /// Executed on all clients (and server with CallLocal = true).
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Multicast_SyncAllPlayers(string allPlayersJson)
    {
        Logger.Trace("Synchronizing player list from server");
        
        PlayersData = DeserializePlayersDictionary(allPlayersJson);
        
        Logger.Debug($"Updated player list: {PlayersData.Count} players");
        
        // Emit signal so UI can update
        EmitSignal(SignalName.PlayerListUpdated);
    }

    /// <summary>
    /// Updates a player's name (can be called by client when they change their name in UI).
    /// Sends update to server which then multicasts to all clients.
    /// </summary>
    public void UpdatePlayerName(string newName)
    {
        long localPeerId = Multiplayer.GetUniqueId();
        
        if (Multiplayer.IsServer())
        {
            // Server updates directly
            if (PlayersData.ContainsKey(localPeerId))
            {
                PlayersData[localPeerId].Name = newName;
                Rpc(MethodName.Multicast_SyncAllPlayers, SerializePlayersDictionary(PlayersData));
                Logger.Info($"Host updated name to: {newName}");
            }
        }
        else
        {
            // Client sends update to server
            PlayerData updatedData = new PlayerData(newName, localPeerId);
            RpcId(1, MethodName.Server_ReceivePlayerName, updatedData.AsJsonString());
            Logger.Info($"Sent name update to server: {newName}");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets the stored local player name.
    /// This is set when CreateServer() or JoinServer() is called.
    /// </summary>
    private string GetPlayerNameFromUI()
    {
        return string.IsNullOrWhiteSpace(localPlayerName) 
            ? $"Player_{Multiplayer.GetUniqueId()}" 
            : localPlayerName;
    }

    /// <summary>
    /// Serializes player dictionary to JSON for RPC transmission.
    /// </summary>
    private string SerializePlayersDictionary(System.Collections.Generic.Dictionary<long, PlayerData> players)
    {
        var godotDict = new Dictionary();
        foreach (var kvp in players)
        {
            godotDict[kvp.Key.ToString()] = kvp.Value.AsDict();
        }
        return Json.Stringify(godotDict);
    }

    /// <summary>
    /// Deserializes JSON to player dictionary after RPC reception.
    /// </summary>
    private System.Collections.Generic.Dictionary<long, PlayerData> DeserializePlayersDictionary(string json)
    {
        var result = new System.Collections.Generic.Dictionary<long, PlayerData>();
        Variant parsed = Json.ParseString(json);
        var godotDict = (Dictionary)parsed;
        
        foreach (var key in godotDict.Keys)
        {
            long peerId = long.Parse((string)key);
            var playerDict = (Dictionary)godotDict[key];
            result[peerId] = new PlayerData(playerDict);
        }
        return result;
    }

    #endregion
}
