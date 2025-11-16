using Godot;
using System;

/// <summary>
/// Simple logging utility for debugging multiplayer flow.
/// Autoloaded globally for easy access throughout the project.
/// </summary>
public partial class Logger : Node
{
    public static Logger Instance { get; private set; }

    // Host or Client
    public static string Type { get; private set; } = "TBD";

    public static void SetType(string type)
    {
        Type = type;
    }

    public override void _Ready()
    {
        Instance = this;
    }

    public static void Trace(string message)
    {
        GD.Print($"[{Type}][TRACE] {message}");
    }

    public static void Debug(string message)
    {
        GD.Print($"[{Type}][DEBUG] {message}");
    }

    public static void Info(string message)
    {
        GD.Print($"[{Type}][INFO] {message}");
    }

    public static void Warning(string message)
    {
        GD.PushWarning($"[{Type}][WARNING] {message}");
    }

    public static void Error(string message)
    {
        GD.PushError($"[{Type}][ERROR] {message}");
    }
}
