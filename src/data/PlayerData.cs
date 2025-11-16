using Godot;
using System;
using Godot.Collections;

/// <summary>
/// Data model representing a player in the multiplayer lobby.
/// Includes JSON serialization for network transmission via RPC.
/// </summary>
public partial class PlayerData : RefCounted
{
    public string Name { get; set; }
    public long PeerId { get; set; }

    public PlayerData(string name, long peerId)
    {
        Name = name;
        PeerId = peerId;
    }

    /// <summary>
    /// Constructor for deserializing from Godot Dictionary
    /// </summary>
    public PlayerData(Dictionary dict)
    {
        Name = dict["name"].AsString();
        PeerId = dict["peer_id"].AsInt64();
    }

    /// <summary>
    /// Convert to Godot Dictionary for JSON serialization
    /// </summary>
    public Dictionary AsDict()
    {
        return new Dictionary
        {
            ["name"] = Name,
            ["peer_id"] = PeerId
        };
    }

    /// <summary>
    /// Serialize to JSON string for RPC transmission
    /// </summary>
    public string AsJsonString()
    {
        return Json.Stringify(AsDict());
    }

    /// <summary>
    /// Deserialize from JSON string received via RPC
    /// </summary>
    public static PlayerData FromJson(string json)
    {
        Variant parsed = Json.ParseString(json);
        Dictionary dict = (Dictionary)parsed;
        return new PlayerData(dict);
    }

    public override string ToString()
    {
        return $"PlayerData(Name: {Name}, PeerId: {PeerId})";
    }
}
