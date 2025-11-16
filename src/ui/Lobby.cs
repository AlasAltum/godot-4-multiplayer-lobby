using Godot;
using System;

/// <summary>
/// Unified lobby controller handling both Host and Client functionality.
/// Uses MVC pattern:
/// - Model: PlayerData, MultiplayerManager.PlayersData
/// - View: Lobby scene UI elements (buttons, labels, text input)
/// - Controller: This class (handles user input, updates model, refreshes view)
/// 
/// The lobby adapts its UI based on whether the player is hosting or joining.
/// </summary>
public partial class Lobby : Control
{
    #region Export Properties

    [Export]
    private string serverAddress = "127.0.0.1";

    #endregion

    #region UI Node References

    // Initial menu controls
    private Button hostButton;
    private Button joinButton;
    private LineEdit playerNameInput;
    private Control initialMenu;

    // Lobby controls (shown after host/join)
    private Control lobbyPanel;
    private VBoxContainer playerListContainer;
    private Button startButton;  // Only for host
    private Button backButton;
    private Label statusLabel;

    #endregion

    #region State

    private bool isHost = false;
    private string localPlayerName = "";

    #endregion

    public override void _Ready()
    {
        // Get UI node references
        initialMenu = GetNode<Control>("%InitialMenu");
        hostButton = GetNode<Button>("%HostButton");
        joinButton = GetNode<Button>("%JoinButton");
        playerNameInput = GetNode<LineEdit>("%PlayerNameInput");
        
        lobbyPanel = GetNode<Control>("%LobbyPanel");
        playerListContainer = GetNode<VBoxContainer>("%PlayerList");
        startButton = GetNode<Button>("%StartButton");
        backButton = GetNode<Button>("%BackButton");
        statusLabel = GetNode<Label>("%StatusLabel");

        // Connect button signals
        hostButton.Pressed += OnHostPressed;
        joinButton.Pressed += OnJoinPressed;
        startButton.Pressed += OnStartPressed;
        backButton.Pressed += OnBackPressed;
        playerNameInput.TextChanged += OnPlayerNameChanged;

        // Subscribe to multiplayer manager signals
        MultiplayerManager.Instance.PlayerListUpdated += OnPlayerListUpdated;
        MultiplayerManager.Instance.PlayerDisconnected += OnPlayerDisconnected;

        // Initialize UI state
        lobbyPanel.Hide();
        startButton.Hide();
        
        Logger.Info("Lobby initialized");
    }

    #region Button Callbacks

    private void OnHostPressed()
    {
        if (string.IsNullOrWhiteSpace(playerNameInput.Text))
        {
            Logger.Warning("Please enter a player name");
            return;
        }

        localPlayerName = playerNameInput.Text;
        isHost = true;

        Logger.Info($"Starting server as '{localPlayerName}'");
        MultiplayerManager.Instance.CreateServer(localPlayerName);
        
        // Add host to player list
        long hostId = Multiplayer.GetUniqueId();
        MultiplayerManager.PlayersData[hostId] = new PlayerData(localPlayerName, hostId);
        
        ShowLobby(true);
    }

    private void OnJoinPressed()
    {
        if (string.IsNullOrWhiteSpace(playerNameInput.Text))
        {
            Logger.Warning("Please enter a player name");
            return;
        }

        localPlayerName = playerNameInput.Text;
        isHost = false;

        Logger.Info($"Joining server as '{localPlayerName}'");
        MultiplayerManager.Instance.JoinServer(localPlayerName, serverAddress);
        
        ShowLobby(false);
    }

    private void OnStartPressed()
    {
        if (!isHost)
        {
            Logger.Warning("Only the host can start the game");
            return;
        }

        if (MultiplayerManager.PlayersData.Count < 2)
        {
            Logger.Warning("Need at least 2 players to start");
            return;
        }

        Logger.Info("Starting game...");
        // TODO: Implement game start logic
        // For example: Load game scene via RPC multicast for all players.
        // Make sure to keep the MultiplayerManager alive, since it keeps the session
    }

    private void OnBackPressed()
    {
        // Disconnect from multiplayer
        if (Multiplayer.MultiplayerPeer != null)
        {
            Multiplayer.MultiplayerPeer.Close();
        }
        
        MultiplayerManager.PlayersData.Clear();
        
        // Return to initial menu
        lobbyPanel.Hide();
        initialMenu.Show();
        
        Logger.Info("Returned to main menu");
    }

    private void OnPlayerNameChanged(string newText)
    {
        // Real-time name updates (optional feature)
        if (lobbyPanel.Visible && !string.IsNullOrWhiteSpace(newText))
        {
            localPlayerName = newText;
            MultiplayerManager.Instance.UpdatePlayerName(newText);
        }
    }

    #endregion

    #region Multiplayer Callbacks

    /// <summary>
    /// Called when the server sends updated player list.
    /// This is the main method that keeps the UI in sync.
    /// </summary>
    private void OnPlayerListUpdated()
    {
        Logger.Debug("Player list updated, refreshing UI");
        UpdatePlayerListUI();
    }

    private void OnPlayerDisconnected(long peerId)
    {
        Logger.Info($"Player {peerId} disconnected");
        UpdatePlayerListUI();
    }

    #endregion

    #region UI Update Methods

    /// <summary>
    /// Switches from initial menu to lobby view.
    /// </summary>
    private void ShowLobby(bool asHost)
    {
        initialMenu.Hide();
        lobbyPanel.Show();
        
        // Show start button only for host
        startButton.Visible = asHost;
        
        statusLabel.Text = asHost ? "Hosting - Waiting for players..." : "Connected to server";
        
        UpdatePlayerListUI();
    }

    /// <summary>
    /// Rebuilds the player list UI from MultiplayerManager.PlayersData.
    /// This is called whenever players join, leave, or update their names.
    /// </summary>
    private void UpdatePlayerListUI()
    {
        // Clear existing player labels
        foreach (Node child in playerListContainer.GetChildren())
        {
            child.QueueFree();
        }

        // Add a label for each player
        foreach (var kvp in MultiplayerManager.PlayersData)
        {
            PlayerData playerData = kvp.Value;
            
            Label playerLabel = new Label();
            playerLabel.Text = $"{playerData.Name} (ID: {playerData.PeerId})";
            
            // Highlight local player
            if (playerData.PeerId == Multiplayer.GetUniqueId())
            {
                playerLabel.Text += " [YOU]";
                playerLabel.Modulate = new Color(0.5f, 1.0f, 0.5f); // Green tint
            }
            
            playerListContainer.AddChild(playerLabel);
        }
        
        // Update player count
        statusLabel.Text = isHost 
            ? $"Hosting - {MultiplayerManager.PlayersData.Count} player(s) connected"
            : $"Connected - {MultiplayerManager.PlayersData.Count} player(s) in lobby";
        
        // Enable start button if enough players (host only)
        if (isHost)
        {
            startButton.Disabled = MultiplayerManager.PlayersData.Count < 2;
        }
    }

    #endregion
}
