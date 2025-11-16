# Godot 4 Multiplayer Lobby Example (C#)

A minimal, well-documented example demonstrating **RPC-based player name synchronization** in a multiplayer lobby using Godot 4 with C#.

## 🎯 What This Project Demonstrates

- **Listen Server Model**: Host acts as both server and player
- **RPC Communication**: Sending and receiving complex data (player names, IDs) across network
- **JSON Serialization**: How to transmit custom objects via RPC (Godot RPC only supports Variant types)
- **Unified Lobby**: Single UI script that adapts for both Host and Client roles
- **MVC Pattern**: Clean separation of data (Model), UI (View), and logic (Controller)
- **Signal-Based Updates**: Decoupled architecture using Godot signals

## 🏗️ Architecture Overview

### Project Structure

```
Godot4MultiplayerLobby/
├── src/
│   ├── managers/
│   │   └── MultiplayerManager.cs    # Autoload singleton managing connections & RPC
│   ├── data/
│   │   └── PlayerData.cs             # Player data model with JSON serialization
│   ├── ui/
│   │   └── Lobby.cs                  # Unified lobby controller (MVC pattern)
│   └── utils/
│       └── Logger.cs                 # Simple logging utility
├── scenes/
│   ├── MainMenu.tscn                 # Entry point scene
│   └── Lobby.tscn                    # Lobby UI scene
└── docs/
    └── RPC_FLOW.md                   # Detailed RPC flow documentation
```

### MVC Pattern Implementation

**Model**:
- `PlayerData`: Data structure representing a player
- `MultiplayerManager.PlayersData`: Dictionary storing all connected players

**View**:
- `Lobby.tscn`: UI elements (buttons, labels, text input)

**Controller**:
- `Lobby.cs`: Handles user input, updates model, refreshes view

## 🔄 RPC Flow Explained

### When a Client Connects:

```
1. [CLIENT] Clicks "Join" button
   └─> Creates ENetMultiplayerPeer and connects to server

2. [SERVER] Detects connection via PeerConnected signal
   └─> Creates placeholder entry in PlayersData
   └─> Sends RPC to client: RequestPlayerName()

3. [CLIENT] Receives RequestPlayerName() RPC
   └─> Retrieves local player name from UI
   └─> Sends RPC to server: ReceivePlayerName(playerDataJson)

4. [SERVER] Receives ReceivePlayerName() RPC
   └─> Deserializes JSON to PlayerData object
   └─> Updates PlayersData dictionary
   └─> Multicasts to ALL clients: SyncAllPlayers(allPlayersJson)

5. [ALL CLIENTS + SERVER] Receive SyncAllPlayers() RPC
   └─> Deserialize complete player list
   └─> Update local PlayersData dictionary
   └─> Emit PlayerListUpdated signal
   └─> UI updates automatically via signal subscription
```

### RPC Method Signatures

```csharp
// SERVER -> CLIENT: Request player info
[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
private void RequestPlayerName()

// CLIENT -> SERVER: Send player info
[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
private void ReceivePlayerName(string playerDataJson)

// SERVER -> ALL: Synchronize complete player list
[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
private void SyncAllPlayers(string allPlayersJson)
```

## 🚀 Quick Start

### Prerequisites

- Godot 4.3+ with .NET support
- .NET 8.0 SDK

### Setup

1. Clone or download this project
2. Open in Godot 4
3. Wait for C# project to build
4. Run the project (F5)

### Testing Multiplayer Locally

1. **Run Instance 1** (Host):
   - Enter a player name (e.g., "Alice")
   - Click "Host"
   - Wait for players to join

2. **Run Instance 2** (Client):
   - Export project or run another editor instance
   - Enter a player name (e.g., "Bob")
   - Click "Join"
   - See both players in the lobby

## 🔑 Key Concepts

### Why JSON Serialization?

Godot's RPC system only supports **Variant-compatible types**. Custom C# classes like `PlayerData` cannot be sent directly via RPC. 

**Solution**: Serialize to JSON string (which is Variant-compatible), transmit, then deserialize on the other end.

```csharp
// Sending
PlayerData data = new PlayerData("Alice", 12345);
RpcId(1, MethodName.ReceivePlayerName, data.AsJsonString());

// Receiving
[Rpc]
private void ReceivePlayerName(string json)
{
    PlayerData data = PlayerData.FromJson(json);
    // Now we have our custom object back!
}
```

### Why Not MultiplayerSpawner?

`MultiplayerSpawner` is for **runtime object spawning** (like bullets, enemies, etc.), not for:
- Scene transitions
- Persistent data synchronization
- Initial lobby setup

For lobbies, use **RPC multicasts** to synchronize state across all peers.

### Autoload Persistence

`MultiplayerManager` is an **autoload singleton**, meaning:
- ✅ Persists across scene changes
- ✅ Maintains network connection when loading new scenes
- ✅ Keeps player data intact throughout game session
- ✅ Accessible from any script via `MultiplayerManager.Instance`

## 📝 Common Patterns

### Adding New Player Properties

```csharp
// 1. Update PlayerData class
public class PlayerData
{
    public string Name { get; set; }
    public long PeerId { get; set; }
    public int Score { get; set; }  // NEW
    
    public Dictionary AsDict()
    {
        return new Dictionary
        {
            ["name"] = Name,
            ["peer_id"] = PeerId,
            ["score"] = Score  // NEW
        };
    }
}

// 2. Update constructor to handle new property
public PlayerData(Dictionary dict)
{
    Name = dict["name"].AsString();
    PeerId = dict["peer_id"].AsInt64();
    Score = dict.ContainsKey("score") ? dict["score"].AsInt32() : 0;
}
```

### Updating Player Data During Game

```csharp
// Client updates their own score
public void UpdatePlayerScore(int newScore)
{
    long localPeerId = Multiplayer.GetUniqueId();
    
    if (MultiplayerManager.PlayersData.ContainsKey(localPeerId))
    {
        MultiplayerManager.PlayersData[localPeerId].Score = newScore;
        
        // Send update to server
        if (!Multiplayer.IsServer())
        {
            RpcId(1, MethodName.ReceivePlayerUpdate, 
                MultiplayerManager.PlayersData[localPeerId].AsJsonString());
        }
        else
        {
            // Server multicasts to all
            Rpc(MethodName.SyncAllPlayers, 
                SerializePlayersDictionary(MultiplayerManager.PlayersData));
        }
    }
}
```

## 🐛 Debugging Tips

### Enable Verbose Logging

The `Logger` class provides multiple log levels:

```csharp
Logger.Trace("Detailed execution flow");  // Use for step-by-step debugging
Logger.Debug("Player connected");         // Use for important events
Logger.Info("Game started");              // Use for major milestones
Logger.Warning("Low player count");       // Use for potential issues
Logger.Error("Connection failed");        // Use for critical errors
```

### Common Issues

**Issue**: Players not appearing in lobby
- Check: Is `MultiplayerManager` set as autoload?
- Check: Are signals connected in `Lobby._Ready()`?
- Check: Is JSON deserialization working? (Add debug prints)

**Issue**: RPC methods not being called
- Check: RPC attribute configuration (`RpcMode`, `CallLocal`)
- Check: Is `Multiplayer.MultiplayerPeer` set correctly?
- Check: Method must be declared `private` or `public`, not `protected`

**Issue**: "Variant-incompatible type" error
- Solution: Never send custom objects directly via RPC
- Solution: Always serialize to JSON string first

## 📚 Further Reading

- [Godot High-Level Multiplayer Docs](https://docs.godotengine.org/en/stable/tutorials/networking/high_level_multiplayer.html)
- [Godot RPC Documentation](https://docs.godotengine.org/en/stable/classes/class_node.html#class-node-method-rpc)
- [ENet Peer Documentation](https://docs.godotengine.org/en/stable/classes/class_enetmultiplayerpeer.html)

## 📄 License

This project is released as public domain / MIT License - use it however you want!

## 🤝 Contributing

This is an educational example project. Feel free to:
- Fork and extend it
- Use it in your own projects
- Submit improvements via pull requests
- Share it with others learning Godot networking

## ✨ Credits

Created as a demonstration of clean multiplayer lobby architecture in Godot 4 with C#.
