using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TetrisMultiplayer.Model;
using TetrisMultiplayer.Networking;
using System.Collections.Generic;

namespace TetrisMultiplayer
{
    // Custom logger provider that writes to a file instead of console to avoid UI interference
    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _filePath;
        public FileLoggerProvider(string filePath) => _filePath = filePath;
        public ILogger CreateLogger(string categoryName) => new GameLogger(_filePath);
        public void Dispose() { }
    }

    public class FileLogger : ILogger
    {
        private readonly string _filePath;
        private readonly object _lock = new object();
        public FileLogger(string filePath) => _filePath = filePath;
        
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;
        
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            
            lock (_lock)
            {
                try
                {
                    var message = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logLevel}] {formatter(state, exception)}";
                    File.AppendAllText(_filePath, message + Environment.NewLine);
                }
                catch
                {
                    // Silently ignore logging errors to avoid breaking the game
                }
            }
        }
    }

    // Enhanced logger that also handles debug messages during gameplay
    public class GameLogger : ILogger
    {
        private readonly string _filePath;
        private readonly object _lock = new object();
        private static bool _isGameActive = false;
        
        public GameLogger(string filePath) => _filePath = filePath;
        
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;
        
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            
            lock (_lock)
            {
                try
                {
                    var message = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logLevel}] {formatter(state, exception)}";
                    File.AppendAllText(_filePath, message + Environment.NewLine);
                }
                catch
                {
                    // Silently ignore logging errors to avoid breaking the game
                }
            }
        }
        
        // Static methods for debug logging that respect game state
        public static void LogDebug(string message)
        {
            if (_isGameActive)
            {
                // During game, write to file only
                LogToFile($"[DEBUG] {message}");
            }
            else
            {
                // During lobby/setup, can write to console
                Console.WriteLine($"[DEBUG] {message}");
            }
        }
        
        public static void LogError(string message)
        {
            if (_isGameActive)
            {
                // During game, write to file only
                LogToFile($"[ERROR] {message}");
            }
            else
            {
                // During lobby/setup, can write to console
                Console.WriteLine($"[ERROR] {message}");
            }
        }
        
        public static void SetGameActive(bool isActive)
        {
            _isGameActive = isActive;
        }
        
        private static void LogToFile(string message)
        {
            try
            {
                var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}";
                File.AppendAllText("tetris_multiplayer_debug.log", logMessage + Environment.NewLine);
            }
            catch
            {
                // Silently ignore logging errors
            }
        }
    }

    internal partial class Program
    {
        static void Main(string[] args)
        {
            // Ensure required directories exist
            string[] requiredDirs = { "Networking", "Game", "UI", "Model", "Persistence" };
            foreach (var dir in requiredDirs)
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }

            // Configure file-based logging to avoid UI interference
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddProvider(new FileLoggerProvider("tetris_multiplayer.log"));
                builder.SetMinimumLevel(LogLevel.Information);
            });
            ILogger logger = loggerFactory.CreateLogger<Program>();

            // Spielername abfragen
            Console.WriteLine("Bitte gib deinen Spielernamen ein:");
            string? playerNameInput = Console.ReadLine();
            string playerName = (playerNameInput ?? string.Empty).Trim();
            while (string.IsNullOrWhiteSpace(playerName))
            {
                Console.WriteLine("Ungültiger Name. Bitte gib einen Spielernamen ein:");
                playerNameInput = Console.ReadLine();
                playerName = (playerNameInput ?? string.Empty).Trim();
            }

            // Ask for mode
            Console.WriteLine("Bitte Modus wählen: host (h), client (c), test (t), validate (v), oder single (s):");
            string? modeInput = Console.ReadLine();
            string mode = (modeInput ?? string.Empty).Trim().ToLower();
            while (mode != "host" && mode != "h" && mode != "client" && mode != "c" && mode != "test" && mode != "t" && mode != "validate" && mode != "v" && mode != "single" && mode != "s")
            {
                Console.WriteLine("Ungültige Eingabe. Bitte 'host' (h), 'client' (c), 'test' (t), 'validate' (v) oder 'single' (s) eingeben:");
                modeInput = Console.ReadLine();
                if (modeInput == null) 
                {
                    // Input stream ended, default to single player
                    mode = "single";
                    break;
                }
                mode = modeInput.Trim().ToLower();
            }

            if (mode == "host" || mode == "h")
            {
                logger.LogInformation("Starte im Host-Modus...");
                RunHostAsync(logger, playerName).GetAwaiter().GetResult();
            }
            else if (mode == "client" || mode == "c")
            {
                logger.LogInformation("Starte im Client-Modus...");
                RunClientAsync(logger, playerName).GetAwaiter().GetResult();
            }
            else if (mode == "test" || mode == "t")
            {
                logger.LogInformation("Starte Test-Modus...");
                TetrisMultiplayer.Tests.PreviewOptimizationTest.RunManualTest();
                Console.WriteLine();
                TetrisMultiplayer.Tests.ColorUIVisualizationTest.RunColorTest();
            }
            else if (mode == "validate" || mode == "v")
            {
                logger.LogInformation("Starte Validierungs-Modus...");
                TetrisMultiplayer.Tests.PreviewValidationTest.ValidateOptimizations();
                TetrisMultiplayer.Tests.ManualPreviewSyncTest.TestPreviewSynchronization();
                Console.WriteLine();
                // Zusätzlich: Color UI Demo
                TetrisMultiplayer.Tests.SimpleColorDemo.RunDemo();
            }
            else if (mode == "single" || mode == "s")
            {
                // Einzelspieler-Modus
                logger.LogInformation("Starte Einzelspieler-Tetris...");
                TetrisMultiplayer.UI.ConsoleUI.RunSinglePlayer();
            }
            else
            {
                // Default: Einzelspieler-Modus
                logger.LogInformation("Starte Einzelspieler-Tetris...");
                TetrisMultiplayer.UI.ConsoleUI.RunSinglePlayer();
            }

            Console.WriteLine("Programm beendet. Beliebige Taste zum Schließen.");
            // Only try to read key if in interactive mode
            try
            {
                Console.ReadKey();
            }
            catch (InvalidOperationException)
            {
                // Ignore error when running in non-interactive mode (tests, CI, etc.)
            }
        }

        static async Task RunHostAsync(ILogger logger, string playerName)
        {
            using var network = new NetworkManager();
            int port = 5000;
            var cts = new CancellationTokenSource();
            
            try
            {
                // Start host immediately - don't let diagnostics prevent startup
                Console.WriteLine("Starting host...");
                await network.StartHostWithDiscovery(port, playerName, cts.Token);

            // GameManager mit Seed erzeugen
            var gameManager = new TetrisMultiplayer.Game.GameManager();
            int seed = gameManager.Seed;
            Console.WriteLine($"Game Seed: {seed}");

            // Show network info (but don't fail if this fails)
            try
            {
                var networkInfo = await network.GetSimpleNetworkInfo();
                Console.WriteLine(networkInfo);
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Could not get network info: {ex.Message}");
                Console.WriteLine("Network info not available, but host is running normally.");
            }

            // Show available IPs for manual connection
            List<string> localIPs = new List<string>();
            try
            {
                localIPs = Dns.GetHostAddresses(Dns.GetHostName())
                    .Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    .Select(ip => ip.ToString()).ToList();
                Console.WriteLine("Your IPv4 addresses for manual connection:");
                foreach (var ip in localIPs)
                {
                    var priority = "";
                    if (ip.StartsWith("192.168.") || ip.StartsWith("10.") || ip.StartsWith("172."))
                        priority = " (LAN - best choice)";
                    else if (ip.StartsWith("100.") || ip.StartsWith("25."))
                        priority = " (VPN - might work)";
                    else if (!ip.StartsWith("127."))
                        priority = " (public/other)";
                        
                    Console.WriteLine($"  {ip}:{port}{priority}");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Could not enumerate IP addresses: {ex.Message}");
                Console.WriteLine($"Host is running on port {port}");
            }

            Console.WriteLine("Lobby discovery active - clients can find this host automatically.");
            Console.WriteLine("Host started successfully - ready for players!");

            // Host-Name speichern
            var playerNames = new Dictionary<string, string>();
            string hostId = "host";
            playerNames[hostId] = playerName;

            // Lobby-Loop
            while (true)
            {
                // Namen der Clients übernehmen
                foreach (var kv in network.PlayerNames)
                    playerNames[kv.Key] = kv.Value;

                Console.Clear();
                Console.WriteLine("--- Lobby ---");
                Console.WriteLine($"Game Seed: {seed}");
                Console.WriteLine("Host running successfully!");
                Console.WriteLine($"Players can connect to any of these addresses on port {port}:");
                try
                {
                    foreach (var ip in localIPs)
                    {
                        var priority = "";
                        if (ip.StartsWith("192.168.") || ip.StartsWith("10.") || ip.StartsWith("172."))
                            priority = " (LAN)";
                        else if (ip.StartsWith("100.") || ip.StartsWith("25."))
                            priority = " (VPN)";
                        Console.WriteLine($"  {ip}:{port}{priority}");
                    }
                }
                catch
                {
                    Console.WriteLine($"  Check network settings - host running on port {port}");
                }
                Console.WriteLine();
                Console.WriteLine("Verbunden:");
                Console.WriteLine($"  Name: {playerName} (Host), ID: {hostId}");
                foreach (var id in network.ConnectedPlayerIds)
                {
                    string name = playerNames.ContainsKey(id) ? playerNames[id] : $"Spieler_{id.Substring(0, 4)}";
                    Console.WriteLine($"  Name: {name}, ID: {id}");
                }
                Console.WriteLine();
                int playerCount = network.ConnectedPlayerIds.Count + 1; // Host selbst mitzählen
                if (playerCount < 2)
                {
                    Console.WriteLine("Mindestens 2 Spieler (inkl. Host) erforderlich, um das Spiel zu starten.");
                }
                Console.WriteLine();
                Console.WriteLine("Drücke [S] um das Spiel zu starten oder [Q] zum Beenden.");
                // Broadcast LobbyUpdate (inkl. Namen)
                await BroadcastLobbyUpdate(network, null, null, playerNames);
                
                // Check for key press non-blocking to allow lobby refresh
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.S)
                    {
                        if (playerCount < 2)
                        {
                            Console.WriteLine("Nicht genug Spieler. Mindestens 2 Spieler (inkl. Host) erforderlich.");
                            await Task.Delay(1500);
                            continue;
                        }
                        // StartGame und Seed an alle senden
                        logger.LogInformation($"[Host] Broadcasting StartGame message with seed {seed} to {network.ConnectedPlayerIds.Count} clients");
                        var startGame = new { type = "StartGame", seed };
                        await network.BroadcastAsync(startGame);
                        
                        // Wait a moment for all clients to initialize their GameManagers
                        logger.LogInformation("[Host] Waiting 2000ms for clients to process StartGame message");
                        await Task.Delay(2000);
                        
                        logger.LogInformation($"Spiel wird gestartet... (Seed: {seed})");
                        // Starte lock-step Game-Loop — pieces are sent from inside the loop only
                        await HostGameLoop(network, gameManager, logger, cts.Token, playerName, hostId, playerNames);
                        break;
                    }
                    if (key.Key == ConsoleKey.Q)
                    {
                        cts.Cancel();
                        network.StopLobbyBroadcast();
                        network.Dispose(); // Properly cleanup network resources
                        break;
                    }
                }
                await Task.Delay(1000); // Short delay to allow lobby refresh
            }
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError($"Host startup failed: {ex.Message}");
                Console.WriteLine($"Failed to start host: {ex.Message}");
                Console.WriteLine("This usually means the port is already in use. Please:");
                Console.WriteLine("1. Close any existing host instances");
                Console.WriteLine("2. Wait a moment for cleanup to complete");
                Console.WriteLine("3. Check if another application is using port 5000");
            }
            catch (Exception ex)
            {
                logger.LogError($"Unexpected error in host: {ex.Message}");
                Console.WriteLine($"Unexpected error: {ex.Message}");
            }
            finally
            {
                cts.Cancel();
                // NetworkManager will be disposed automatically due to 'using' statement
            }
        }

        static async Task HostGameLoop(NetworkManager network, TetrisMultiplayer.Game.GameManager gameManager, ILogger logger, CancellationToken cancellationToken, string hostName, string hostId, Dictionary<string, string> playerNames)
        {
            // Reset UI state and initialize game
            TetrisMultiplayer.UI.ConsoleUI.ResetUI();
            Console.Clear();
            Console.WriteLine("[Host] Game starting...");
            await Task.Delay(1000); // Brief pause to show initialization

            // Mark game as active to prevent debug output from interfering with UI
            GameLogger.SetGameActive(true);

            var playerIds = new List<string> { hostId };
            playerIds.AddRange(network.ConnectedPlayerIds);
            // Host-seitige Spielfelder, Scores, HP, Spectator-Status
            var fields = new Dictionary<string, TetrisMultiplayer.Game.TetrisEngine>();
            var scores = new Dictionary<string, int>();
            var hps = new Dictionary<string, int>();
            var spectators = new HashSet<string>();
            foreach (var id in playerIds) 
            { 
                // Create engines WITHOUT seed - they'll use pieces from GameManager
                fields[id] = new TetrisMultiplayer.Game.TetrisEngine(); 
                scores[id] = 0; 
                hps[id] = 100; 
            }
            int round = 1;
            
            // Game timing
            DateTime lastGravity = DateTime.Now;
            const int gravityDelayMs = 1000; // Piece falls every second
            
            // Starte Task für Spectator-Snapshots UND Real-time leaderboard updates
            var snapshotCts = new CancellationTokenSource();
            // Track which players have placed in the current round (host + clients)
            var playersWhoPlaced = new HashSet<string>();
            var snapshotTask = Task.Run(() => BroadcastSpectatorSnapshots(network, fields, scores, hps, spectators, snapshotCts.Token));
            var leaderboardTask = Task.Run(() => BroadcastRealtimeLeaderboard(network, scores, hps, spectators, playerNames, playersWhoPlaced, snapshotCts.Token));
            
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Check for win condition
                    var activePlayers = playerIds.Where(id => !spectators.Contains(id)).ToList();
                    if (activePlayers.Count <= 1)
                    {
                        string winnerId = activePlayers.FirstOrDefault() ?? "No Winner";
                        var gameOverStats = playerIds.ToDictionary(id => id, id => new { 
                            Score = scores[id], 
                            HP = hps[id], 
                            IsSpectator = spectators.Contains(id) 
                        });
                        var gameOver = new { type = "GameOver", winnerId, stats = gameOverStats };
                        await network.BroadcastAsync(gameOver);
                        logger.LogInformation($"Game Over! Winner: {winnerId}");
                        break;
                    }

                    // Reset round placement state - CRITICAL for synchronization
                    playersWhoPlaced.Clear();

                    // Track deleted rows per player for THIS round
                    var deletedRowsPerPlayer = new Dictionary<string,int>();
                    foreach (var id in playerIds)
                        deletedRowsPerPlayer[id] = 0;

                    // SYNCHRONIZATION FIX: Send "PrepareNextPiece" first to let all players know a new round is starting
                    var prepareMsg = new { type = "PrepareNextPiece", round = round };
                    await network.BroadcastAsync(prepareMsg);
                    
                    // Brief delay to ensure all players receive the prepare message
                    await Task.Delay(100);

                    // CRITICAL: Get piece from centralized GameManager for synchronization
                    int pieceId = gameManager.GetNextPiece();
                    int previewPieceId = gameManager.PeekNextPiece(); // Get the next piece for preview
                    var nextPiece = new { type = "NextPiece", pieceId, previewPieceId, round = round };
                    await network.BroadcastAsync(nextPiece);
                    logger.LogInformation($"[Host] Runde {round}: Sende Piece {pieceId} mit Preview {previewPieceId} an alle (Seed: {gameManager.Seed})");

                    // Host-Zug - use the SAME piece ID as sent to clients
                    var hostEngine = fields[hostId];
                    hostEngine.Current = new TetrisMultiplayer.Game.Tetromino((TetrisMultiplayer.Game.TetrominoType)pieceId);
                    hostEngine.Current.X = TetrisMultiplayer.Game.TetrisEngine.Width / 2 - 2;
                    hostEngine.Current.Y = 0;
                    
                    // CRITICAL: Set the synchronized preview piece for host
                    hostEngine.SetNextPiece((TetrisMultiplayer.Game.TetrominoType)previewPieceId);
                    
                    bool placed = false;
                    lastGravity = DateTime.Now;
                    
                    // Track which players have placed their pieces (shared set for this round)
                    
                    // Check if host can place piece (game over check)
                    if (!hostEngine.IsValid(hostEngine.Current, hostEngine.Current.X, hostEngine.Current.Y, hostEngine.Current.Rotation))
                    {
                        logger.LogInformation("[Host] Game Over - Host kann das Teil nicht platicien");
                        spectators.Add(hostId);
                        hps[hostId] = 0;
                        
                        // Host becomes spectator - notify all clients immediately
                        var hostEliminatedMsg = new {
                            type = "PlayerEliminated",
                            playerId = hostId,
                            reason = "Game Over - Field Full"
                        };
                        
                        try
                        {
                            await network.BroadcastAsync(hostEliminatedMsg);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning($"Error broadcasting host elimination: {ex.Message}");
                        }
                        
                        // Continue the game loop - host becomes spectator but game continues
                        placed = true; // Skip host's turn
                    }
                    
                    while (!placed && !spectators.Contains(hostId))
                    {
                        // Handle gravity (automatic falling)
                        if (DateTime.Now.Subtract(lastGravity).TotalMilliseconds >= gravityDelayMs)
                        {
                            if (!hostEngine.Move(0, 1))
                            {
                                placed = true; // Can't move down, so place
                            }
                            lastGravity = DateTime.Now;
                        }

                        var localLeaderboard = playerIds.Select(pid => (pid, scores[pid], hps[pid], spectators.Contains(pid))).ToList();
                        TetrisMultiplayer.UI.ConsoleUI.DrawGameWithLeaderboard(hostEngine, localLeaderboard, hostId, $"[Host] Round {round} - Piece {pieceId}", playerNames, playersWhoPlaced);
                        
                        // Position cursor for controls info with safe positioning
                        Console.SetCursorPosition(2, TetrisMultiplayer.Game.TetrisEngine.Height + 8);
                        Console.Write("Host: [←][→][↓][↑] move/rotate, [Z][X] rotate, [Space] hard drop, [Q] quit");
                        
                        if (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(true);
                            bool moved = false;
                            
                            if (key.Key == ConsoleKey.LeftArrow) moved = hostEngine.Move(-1, 0);
                            else if (key.Key == ConsoleKey.RightArrow) moved = hostEngine.Move(1, 0);
                            else if (key.Key == ConsoleKey.DownArrow) 
                            {
                                if (!hostEngine.Move(0, 1))
                                    placed = true; // Can't move down, so place
                                moved = true;
                            }
                            else if (key.Key == ConsoleKey.UpArrow) { hostEngine.Rotate(1); moved = true; } // Up arrow for rotation
                            else if (key.Key == ConsoleKey.Spacebar) 
                            { 
                                while (hostEngine.Move(0, 1)) { }
                                placed = true; 
                                moved = true;
                            }
                            else if (key.Key == ConsoleKey.Z) { hostEngine.Rotate(-1); moved = true; }
                            else if (key.Key == ConsoleKey.X) { hostEngine.Rotate(1); moved = true; }
                            else if (key.Key == ConsoleKey.Q) 
                            {
                                GameLogger.SetGameActive(false);
                                return;
                            }
                            
                            if (moved)
                            {
                                // Reset gravity timer on player input
                                lastGravity = DateTime.Now;
                            }
                        }
                        await Task.Delay(50); // Smooth game loop
                    }
                    
                    // Host Piece platzieren
                    if (!spectators.Contains(hostId))
                    {
                        // Compute score delta to avoid double-clearing
                        int before = hostEngine.Score;
                        hostEngine.Place();
                        int after = hostEngine.Score;
                        int hostScoreAdd = Math.Max(0, after - before);
                        scores[hostId] += hostScoreAdd;
                        // Map score delta to cleared lines for this round
                        deletedRowsPerPlayer[hostId] += hostScoreAdd switch { 100 => 1, 300 => 2, 500 => 3, 800 => 4, _ => 0 };
                        
                        // Mark host as having placed
                        playersWhoPlaced.Add(hostId);
                        
                        // Update UI after placement
                        var localLeaderboard2 = playerIds.Select(pid => (pid, scores[pid], hps[pid], spectators.Contains(pid))).ToList();
                        TetrisMultiplayer.UI.ConsoleUI.DrawGameWithLeaderboard(hostEngine, localLeaderboard2, hostId, "HOST PIECE PLACED - WAITING FOR CLIENTS...", playerNames, playersWhoPlaced);
                    }

                    // Warte auf PlacedPiece von allen Clients oder Timeout - INCREASED timeout für bessere Synchronisation
                    var placedPieces = await WaitForPlacedPieces(network, network.ConnectedPlayerIds.ToList(), 25000, logger, cancellationToken, hps, spectators, playersWhoPlaced);

                    // Process client pieces using reported deltas (no host-side simulation)
                    foreach (var placedPiece in placedPieces)
                    {
                        playersWhoPlaced.Add(placedPiece.PlayerId);
                        int delta = Math.Max(0, placedPiece.ScoreDelta);
                        scores[placedPiece.PlayerId] = scores.GetValueOrDefault(placedPiece.PlayerId, 0) + delta;
                        int lc = placedPiece.LinesCleared > 0 ? placedPiece.LinesCleared : (delta switch { 100 => 1, 300 => 2, 500 => 3, 800 => 4, _ => 0 });
                        if (deletedRowsPerPlayer.ContainsKey(placedPiece.PlayerId))
                            deletedRowsPerPlayer[placedPiece.PlayerId] += lc;
                        else
                            deletedRowsPerPlayer[placedPiece.PlayerId] = lc;
                    }
                    
                    // Determine last place and apply HP penalty (unique last only)
                    var activePlayersWithScores = playerIds.Where(id => !spectators.Contains(id))
                        .Select(id => new { Id = id, Score = scores[id] })
                        .OrderBy(p => p.Score)
                        .ToList();
                    var hpChanges = new Dictionary<string,int>();
                    
                    if (activePlayersWithScores.Count > 1)
                    {
                        var minScore = activePlayersWithScores.First().Score;
                        var lastPlaceIds = activePlayersWithScores.Where(p => p.Score == minScore).Select(p => p.Id).ToList();

                        // Apply damage only if there is a unique last place
                        if (lastPlaceIds.Count == 1)
                        {
                            var lastPlayerId = lastPlaceIds[0];
                            hps[lastPlayerId] = Math.Max(0, hps[lastPlayerId] - 1);
                            hpChanges[lastPlayerId] = -1;
                            logger.LogInformation($"Player {lastPlayerId} lost 1 HP (now {hps[lastPlayerId]})");

                            if (hps[lastPlayerId] <= 0)
                            {
                                spectators.Add(lastPlayerId);
                                logger.LogInformation($"Player {lastPlayerId} eliminated");
                            }
                        }
                    }
                    
                    // Final leaderboard update showing all who completed
                    var finalLeaderboard = playerIds.Select(pid => (pid, scores[pid], hps[pid], spectators.Contains(pid))).ToList();
                    TetrisMultiplayer.UI.ConsoleUI.DrawGameWithLeaderboard(hostEngine, finalLeaderboard, hostId, "ROUND COMPLETE", playerNames, playersWhoPlaced);
                    
                    // Send comprehensive round results to all players
                    var roundResults = new { 
                        type = "RoundResults", 
                        newScores = scores, 
                        hp = hps, 
                        hpChanges = hpChanges, 
                        spectators = spectators.ToList(),
                        deletedRowsPerPlayer = deletedRowsPerPlayer 
                    };
                    await network.BroadcastAsync(roundResults);
                    
                    // SYNCHRONIZATION FIX: Ensure ALL players wait before next round starts
                    logger.LogInformation($"[Host] Round {round} complete. Implementing round synchronization...");
                    
                    // Send a "WaitForNextRound" message to all players to ensure synchronization
                    var waitMsg = new { type = "WaitForNextRound", round = round, message = "Round complete - preparing next round..." };
                    await network.BroadcastAsync(waitMsg);
                    
                    // NEUE SYNCHRONISATIONS-MECHANISMUS: Warte auf Bestätigung von allen aktiven Spielern
                    var confirmedPlayers = new HashSet<string>();
                    var syncStart = DateTime.UtcNow;
                    var syncTimeoutMs = 10000; // 10 Sekunden für Round Ready Confirmations
                    
                    // Sende RoundReadyRequest an alle aktiven Clients
                    await network.BroadcastRoundReadyRequest(round);
                    logger.LogInformation($"[Host] Warte auf RoundReadyConfirmation von {activePlayers.Where(id => id != hostId && !spectators.Contains(id)).Count()} Clients...");
                    
                    // Host ist automatisch "ready"
                    if (!spectators.Contains(hostId))
                        confirmedPlayers.Add(hostId);
                    
                    // Warte auf alle Client-Bestätigungen
                    var expectedClients = activePlayers.Where(id => id != hostId && !spectators.Contains(id)).ToList();
                    
                    while (confirmedPlayers.Count < activePlayers.Count && 
                           (DateTime.UtcNow - syncStart).TotalMilliseconds < syncTimeoutMs && 
                           !cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            var confirmation = await network.ReceiveRoundReadyConfirmationAsync(cancellationToken, 1000);
                            if (confirmation != null && 
                                confirmation.Round == round && 
                                expectedClients.Contains(confirmation.PlayerId) &&
                                !confirmedPlayers.Contains(confirmation.PlayerId))
                            {
                                confirmedPlayers.Add(confirmation.PlayerId);
                                logger.LogInformation($"[Host] RoundReadyConfirmation von {confirmation.PlayerId} erhalten ({confirmedPlayers.Count}/{activePlayers.Count})");
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning($"[Host] Fehler beim Empfangen von RoundReadyConfirmation: {ex.Message}");
                        }
                        
                        await Task.Delay(50, cancellationToken);
                    }
                    
                    // Prüfe Synchronisations-Erfolg
                    var notConfirmedPlayers = expectedClients.Where(id => !confirmedPlayers.Contains(id)).ToList();
                    if (notConfirmedPlayers.Count > 0)
                    {
                        logger.LogWarning($"[Host] {notConfirmedPlayers.Count} Spieler haben nicht bestätigt: {string.Join(", ", notConfirmedPlayers)}");
                        logger.LogInformation("[Host] Fahre trotzdem fort, aber markiere als potentiell disconnected");
                        
                        // Markiere nicht-bestätigte Spieler als problematisch
                        foreach (var id in notConfirmedPlayers)
                        {
                            logger.LogWarning($"[Host] Spieler {id} reagiert nicht - könnte disconnected sein");
                        }
                    }
                    else
                    {
                        logger.LogInformation($"[Host] ✓ Perfekte Synchronisation: Alle {activePlayers.Count} Spieler sind bereit für nächste Runde!");
                    }
                    
                    // Extra Pause für bessere Synchronisation, besonders wenn nicht alle bestätigt haben
                    if (notConfirmedPlayers.Count > 0)
                    {
                        await Task.Delay(3000, cancellationToken); // Längere Pause bei Problemen
                    }
                    else
                    {
                        await Task.Delay(1000, cancellationToken); // Kurze Pause bei perfekter Sync
                    }
                    
                    logger.LogInformation($"[Host] Starting round {round + 1} with {activePlayers.Count} active players");
                    round++;
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Host game loop was canceled");
            }
            catch (Exception ex)
            {
                logger.LogError($"Unexpected error in host game loop: {ex.Message}");
            }
            finally
            {
                // Mark game as no longer active
                GameLogger.SetGameActive(false);
                
                logger.LogInformation("Cleaning up host game loop tasks...");
                
                // Cancel the background tasks gracefully
                snapshotCts.Cancel();
                
                // Wait for tasks to complete with timeout to prevent hanging
                try
                {
                    await Task.WhenAll(
                        WaitForTaskWithTimeout(snapshotTask, 2000, "SpectatorSnapshot"),
                        WaitForTaskWithTimeout(leaderboardTask, 2000, "Leaderboard")
                    );
                }
                catch (Exception ex)
                {
                    logger.LogWarning($"Error during task cleanup: {ex.Message}");
                }
                
                logger.LogInformation("Host game loop cleanup completed");
            }
        }

        static async Task RunClientAsync(ILogger logger, string playerName)
        {
            var network = new NetworkManager();
            var cts = new CancellationTokenSource();
            
            Console.WriteLine("Searching for available lobbies in local network...");
            Console.WriteLine("(This may take longer on VPN networks or complex network setups)");
            
            try
            {
                var lobbies = await network.DiscoverLobbies(6000, cts.Token); // Reasonable timeout
                
                if (lobbies.Count > 0)
                {
                    Console.WriteLine($"\n{lobbies.Count} lobby(s) found:");
                    Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
                    for (int i = 0; i < lobbies.Count; i++)
                    {
                        var lobby = lobbies[i];
                        Console.WriteLine($"║ [{i + 1}] {lobby.HostName.PadRight(20)} │ {lobby.IpAddress.PadRight(15)} │ {lobby.PlayerCount}/{lobby.MaxPlayers} players ║");
                    }
                    Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
                    Console.WriteLine($"[{lobbies.Count + 1}] Manual IP entry");
                    Console.WriteLine("[0] Exit");
                    Console.Write("\nSelection: ");
                    
                    if (int.TryParse(Console.ReadLine(), out int choice))
                    {
                        if (choice == 0)
                        {
                            return; // Exit
                        }
                        else if (choice > 0 && choice <= lobbies.Count)
                        {
                            var selectedLobby = lobbies[choice - 1];
                            await ConnectToLobby(network, selectedLobby.IpAddress, selectedLobby.Port, playerName, cts.Token);
                            return;
                        }
                        else if (choice == lobbies.Count + 1)
                        {
                            // Manual IP entry
                            await ManualIPConnection(network, playerName, cts.Token);
                            return;
                        }
                    }
                    
                    Console.WriteLine("Invalid selection.");
                    return;
                }
                else
                {
                    Console.WriteLine("No lobbies found in local network.");
                    Console.WriteLine("\nPossible reasons:");
                    Console.WriteLine("- No hosts active in network");
                    Console.WriteLine("- VPN network blocks broadcasts");
                    Console.WriteLine("- Firewall blocks UDP port 5001");
                    Console.WriteLine("- Different network segments");
                    Console.WriteLine("\nWould you like to enter an IP address manually? (y/n)");
                    
                    var response = Console.ReadLine()?.ToLower();
                    if (response == "y" || response == "yes" || response == "j" || response == "ja")
                    {
                        await ManualIPConnection(network, playerName, cts.Token);
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during lobby search: {ex.Message}");
                Console.WriteLine("Falling back to manual IP entry...");
                await ManualIPConnection(network, playerName, cts.Token);
            }
        }

        static async Task ManualIPConnection(NetworkManager network, string playerName, CancellationToken cancellationToken)
        {
            Console.WriteLine("Bitte Host-IP eingeben:");
            string? ip = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(ip))
            {
                Console.WriteLine("Ungültige IP-Adresse.");
                return;
            }
            
            int port = 5000;
            await ConnectToLobby(network, ip, port, playerName, cancellationToken);
        }

        static async Task ConnectToLobby(NetworkManager network, string ip, int port, string playerName, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine($"Verbinde zu {ip}:{port} ...");
                await network.ConnectToHost(ip, port, playerName, cancellationToken);
                Console.WriteLine("Verbunden. Warte auf Lobby...");
                // Lobby-Status anzeigen
                await ClientLobbyLoop(network, cancellationToken, playerName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Verbindung fehlgeschlagen: {ex.Message}");
            }
        }

        static async Task ClientLobbyLoop(NetworkManager network, CancellationToken cancellationToken, string playerName)
        {
            List<PlayerState> lastPlayers = new();
            bool gameStarted = false;
            
            GameLogger.LogDebug("[Client] Starting lobby loop, waiting for StartGame message");
            
            while (!cancellationToken.IsCancellationRequested && !gameStarted)
            {
                // Check if game started FIRST (GameManager set by StartGame message in NetworkManager)
                if (network.GameManager != null && !gameStarted)
                {
                    GameLogger.LogDebug("[Client] GameManager detected, transitioning to game");
                    gameStarted = true;
                    await ClientGameLoop(network, cancellationToken, playerName);
                    break;
                }
                
                // Wait for lobby update messages (non-blocking)
                var msg = await network.ReceiveLobbyUpdateAsync(cancellationToken);
                if (msg != null)
                {
                    lastPlayers = msg.Players;
                    Console.Clear();
                    Console.WriteLine("--- Lobby (Client) ---");
                    Console.WriteLine("Verbindungsstatus: Verbunden");
                    Console.WriteLine("Spieler:");
                    foreach (var p in lastPlayers)
                    {
                        Console.WriteLine($"  Name: {p.Name}, ID: {p.PlayerId}, HP: {p.Hp}");
                    }
                    Console.WriteLine();
                    Console.WriteLine("Warte auf Host zum Starten des Spiels...");
                }
                
                // Shorter delay for more responsive transition
                await Task.Delay(100, cancellationToken);
            }
        }

        static async Task ClientGameLoop(NetworkManager network, CancellationToken cancellationToken, string playerName)
        {
            var gameManager = network.GameManager;
            if (gameManager == null) 
            {
                GameLogger.LogError("[Client] ERROR: GameManager is null!");
                return;
            }
            
            // Reset UI state for new game
            TetrisMultiplayer.UI.ConsoleUI.ResetUI();
            Console.Clear();
            Console.WriteLine("[Client] Game initializing...");
            await Task.Delay(500);
            
            // Mark game as active to prevent debug output from interfering with UI
            GameLogger.SetGameActive(true);
            
            // Create persistent TetrisEngine - CRITICAL: NO seed usage, rely purely on host
            var engine = new TetrisMultiplayer.Game.TetrisEngine();
            engine.Current = null; // Start without a piece
            
            int round = 1;
            bool isSpectator = false;
            int spectatorViewIdx = 0;
            Dictionary<string, int[,]> lastFields = new();
            Dictionary<string, int> lastScores = new();
            Dictionary<string, int> lastHp = new();
            List<string> lastSpectators = new();
            string playerId = network.ClientPlayerId ?? "";
            
            // Real-time leaderboard data for clients
            var realtimeScores = new Dictionary<string, int>();
            var realtimeHp = new Dictionary<string, int>();
            var realtimeSpectators = new List<string>();
            var realtimePlayerNames = new Dictionary<string, string>();
            var realtimePlayersPlaced = new HashSet<string>();
            
            // Game timing
            DateTime lastGravity = DateTime.Now;
            const int gravityDelayMs = 1000; // Piece falls every second
            
            GameLogger.LogDebug($"[Client] Starting game loop for player {playerId}");
            
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Empfange SpectatorSnapshot, falls Spectator
                        if (isSpectator)
                        {
                            var snap = await network.ReceiveSpectatorSnapshotAsync(cancellationToken);
                            if (snap != null)
                            {
                                lastFields = snap.Fields;
                                lastScores = snap.Scores;
                                lastHp = snap.Hp;
                                lastSpectators = snap.Spectators;
                                var playerIds = lastFields.Keys.Where(id => !lastSpectators.Contains(id)).ToList();
                                if (playerIds.Count == 0) continue;
                                if (spectatorViewIdx >= playerIds.Count) spectatorViewIdx = 0;
                                var viewId = playerIds[spectatorViewIdx];
                                Console.Clear();
                                Console.WriteLine($"--- SPECTATOR MODE ---");
                                Console.WriteLine($"[N]ächster Spieler, [P]revious, [Q]uit");
                                Console.WriteLine($"Scoreboard:");
                                foreach (var id in playerIds)
                                    Console.WriteLine($"{id}: Score={lastScores.GetValueOrDefault(id,0)}, HP={lastHp.GetValueOrDefault(id,0)}");
                                Console.WriteLine();
                                Console.WriteLine($"Live-View von Spieler {viewId}:");
                                TetrisMultiplayer.UI.ConsoleUI.DrawFieldRaw(lastFields[viewId]);
                                Console.WriteLine($"Score: {lastScores.GetValueOrDefault(viewId,0)}  HP: {lastHp.GetValueOrDefault(viewId,0)}");
                                if (Console.KeyAvailable)
                                {
                                    var key = Console.ReadKey(true);
                                    if (key.Key == ConsoleKey.N) spectatorViewIdx = (spectatorViewIdx + 1) % playerIds.Count;
                                    if (key.Key == ConsoleKey.P) spectatorViewIdx = (spectatorViewIdx - 1 + playerIds.Count) % playerIds.Count;
                                    if (key.Key == ConsoleKey.Q) 
                                    {
                                        GameLogger.SetGameActive(false);
                                        return;
                                    }
                                }
                                await Task.Delay(100, cancellationToken);
                                continue;
                            }
                        }
                        
                        // First, prioritize gameplay-critical messages
                        var nextOrGameOver = await network.ReceiveNextPieceOrGameOverAsync(cancellationToken);
                        if (nextOrGameOver == null) 
                        {
                            // No NextPiece/GameOver right now; try a non-blocking leaderboard update
                            var leaderboardUpdate = await network.ReceiveLeaderboardUpdateAsync(cancellationToken);
                            if (leaderboardUpdate != null)
                            {
                                realtimeScores = leaderboardUpdate.Scores;
                                realtimeHp = leaderboardUpdate.Hp;
                                realtimeSpectators = leaderboardUpdate.Spectators;
                                realtimePlayerNames = leaderboardUpdate.PlayerNames;
                                realtimePlayersPlaced = leaderboardUpdate.PlayersPlaced ?? new HashSet<string>();
                                
                                if (!isSpectator && engine.Current != null)
                                {
                                    var currentLeaderboard = realtimeScores.Keys.Select(id =>
                                    {
                                        var score = realtimeScores.GetValueOrDefault(id, 0);
                                        if (id == playerId)
                                        {
                                            score = Math.Max(score, engine.Score);
                                        }
                                        return (id, score, realtimeHp.GetValueOrDefault(id, 100), realtimeSpectators.Contains(id));
                                    }).ToList();
                                    TetrisMultiplayer.UI.ConsoleUI.DrawGameWithLeaderboard(engine, currentLeaderboard, playerId, $"Round {round} - Live Game", realtimePlayerNames, realtimePlayersPlaced);
                                }
                            }
                            await Task.Delay(50, cancellationToken);
                            continue;
                        }
                        
                        if (nextOrGameOver.type == "GameOver")
                        {
                            GameLogger.SetGameActive(false);
                            Console.Clear();
                            Console.WriteLine("=== GAME OVER ===");
                            Console.WriteLine($"Winner: {nextOrGameOver.winnerId}");
                            Console.WriteLine();
                            Console.WriteLine("Statistiken:");
                            foreach (var kv in nextOrGameOver.stats)
                            {
                                string id = kv.Name;
                                var stat = kv.Value;
                                Console.WriteLine($"{id}: Score={stat.Score}, HP={stat.HP}, Status={(stat.IsSpectator ? "Spectator" : "Playing")}");
                            }
                            Console.WriteLine();
                            Console.WriteLine("[R] Restart Lobby  [Q] Quit");
                            while (true)
                            {
                                var key = Console.ReadKey(true);
                                if (key.Key == ConsoleKey.R)
                                {
                                    await ClientLobbyLoop(network, cancellationToken, playerName);
                                    return;
                                }
                                if (key.Key == ConsoleKey.Q) return;
                            }
                        }
                        
                        if (nextOrGameOver.type == "NextPiece")
                        {
                            int? pieceId = nextOrGameOver.pieceId;
                            int? previewPieceId = nextOrGameOver.previewPieceId;
                            if (pieceId == null) 
                            {
                                GameLogger.LogError("[Client] ERROR: Received null pieceId");
                                continue;
                            }
                            
                            GameLogger.LogDebug($"[Client] Round {round}: Received piece {pieceId} with preview {previewPieceId} from host");
                            
                            // CRITICAL: Use the exact piece ID from host - this ensures synchronization
                            try
                            {
                                engine.Current = new TetrisMultiplayer.Game.Tetromino((TetrisMultiplayer.Game.TetrominoType)pieceId.Value);
                                engine.Current.X = TetrisMultiplayer.Game.TetrisEngine.Width / 2 - 2;
                                engine.Current.Y = 0;
                                
                                // CRITICAL: Set the synchronized preview piece for client
                                if (previewPieceId != null)
                                {
                                    engine.SetNextPiece((TetrisMultiplayer.Game.TetrominoType)previewPieceId.Value);
                                }
                                
                                // Validate initial position
                                if (!engine.IsValid(engine.Current, engine.Current.X, engine.Current.Y, engine.Current.Rotation))
                                {
                                    GameLogger.SetGameActive(false);
                                    Console.Clear();
                                    Console.WriteLine("Game Over - No space for new piece!");
                                    await Task.Delay(2000);
                                    return;
                                }
                            }
                            catch (Exception ex)
                            {
                                GameLogger.LogError($"[Client] ERROR creating piece {pieceId}: {ex.Message}");
                                continue;
                            }
                            
                            bool placed = false;
                            bool waiting = false;
                            lastGravity = DateTime.Now;
                            
                            GameLogger.LogDebug($"[Client] Starting piece placement for round {round}");
                            
                            while (!placed && !waiting && !cancellationToken.IsCancellationRequested)
                            {
                                // Handle gravity (automatic falling)
                                if (DateTime.Now.Subtract(lastGravity).TotalMilliseconds >= gravityDelayMs)
                                {
                                    if (!engine.Move(0, 1))
                                    {
                                        // Can't move down, so place the piece
                                        placed = true;
                                    }
                                    lastGravity = DateTime.Now;
                                }

                                // Use real-time leaderboard if available, otherwise use local engine data
                                var displayLeaderboard = realtimeScores.Count > 0 
                                    ? realtimeScores.Keys.Select(id => 
                                    {
                                        var score = realtimeScores.GetValueOrDefault(id, 0);
                                        if (id == playerId)
                                            score = Math.Max(score, engine.Score);
                                        return (id, score, realtimeHp.GetValueOrDefault(id, 20), realtimeSpectators.Contains(id));
                                    }).ToList()
                                    : new List<(string, int, int, bool)>{ (playerId, engine.Score, 100, false) };
                                
                                TetrisMultiplayer.UI.ConsoleUI.DrawGameWithLeaderboard(engine, displayLeaderboard, playerId, $"Round {round} - Piece {pieceId}", realtimePlayerNames);
                                
                                // Position cursor for controls info with safe positioning
                                Console.SetCursorPosition(2, TetrisMultiplayer.Game.TetrisEngine.Height + 8);
                                Console.Write("Controls: [←][→][↓][↑] move/rotate, [Z][X] rotate, [Space] hard drop, [Q] quit");
                                
                                if (waiting)
                                {
                                    Console.SetCursorPosition(2, TetrisMultiplayer.Game.TetrisEngine.Height + 9);
                                    Console.Write("WARTEN AUF ANDERE...".PadRight(50));
                                }
                                
                                if (Console.KeyAvailable)
                                {
                                    var key = Console.ReadKey(true);
                                    bool moved = false;
                                    
                                    if (key.Key == ConsoleKey.LeftArrow) 
                                    {
                                        moved = engine.Move(-1, 0);
                                    }
                                    else if (key.Key == ConsoleKey.RightArrow) 
                                    {
                                        moved = engine.Move(1, 0);
                                    }
                                    else if (key.Key == ConsoleKey.DownArrow) 
                                    {
                                        if (!engine.Move(0, 1))
                                        {
                                            // Can't move down, so place the piece
                                            placed = true;
                                        }
                                        moved = true;
                                    }
                                    else if (key.Key == ConsoleKey.UpArrow) { engine.Rotate(1); moved = true; } // Up arrow for rotation
                                    else if (key.Key == ConsoleKey.Spacebar) 
                                    { 
                                        // Hard drop
                                        while (engine.Move(0, 1)) { }
                                        placed = true; 
                                        moved = true;
                                    }
                                    else if (key.Key == ConsoleKey.Z) { engine.Rotate(-1); moved = true; }
                                    else if (key.Key == ConsoleKey.X) { engine.Rotate(1); moved = true; }
                                    else if (key.Key == ConsoleKey.Q) return;
                                    
                                    if (moved)
                                    {
                                        // Reset gravity timer on player input
                                        lastGravity = DateTime.Now;
                                    }
                                }
                                
                                if (placed)
                                {
                                    // Place and compute local deltas for immediate and host-accurate updates
                                    int before = engine.Score;
                                    engine.Place();
                                    int after = engine.Score;
                                    int scoreDelta = Math.Max(0, after - before);
                                    int linesCleared = scoreDelta switch { 100 => 1, 300 => 2, 500 => 3, 800 => 4, _ => 0 };
                                    
                                    GameLogger.LogDebug($"[Client] Piece {pieceId} placed, sending to host...");
                                    
                                    // Send placement message to host
                                    var placedMsg = new { 
                                        type = "PlacedPiece", 
                                        playerId = playerId, 
                                        pieceId = pieceId.Value, 
                                        placedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 
                                        locks = true,
                                        scoreDelta,
                                        linesCleared
                                    };
                                    await network.SendPlacedPieceAsync(placedMsg);
                                    waiting = true;
                                    
                                    // Update UI to show placement
                                    var placedLeaderboard = realtimeScores.Count > 0 
                                        ? realtimeScores.Keys.Select(id => 
                                        {
                                            var score = realtimeScores.GetValueOrDefault(id, 0);
                                            if (id == playerId)
                                                score = Math.Max(score, engine.Score);
                                            return (id, score, realtimeHp.GetValueOrDefault(id, 100), realtimeSpectators.Contains(id));
                                        }).ToList()
                                        : new List<(string, int, int, bool)>{ (playerId, engine.Score, 100, false) };
                                    TetrisMultiplayer.UI.ConsoleUI.DrawGameWithLeaderboard(engine, placedLeaderboard, playerId, "PIECE PLACED - WAITING FOR OTHERS...", realtimePlayerNames, realtimePlayersPlaced);
                                }
                                
                                if (waiting)
                                {
                                    // While waiting, also process live leaderboard updates to reflect who has finished
                                    var liveLb = await network.ReceiveLeaderboardUpdateAsync(cancellationToken);
                                    if (liveLb != null)
                                    {
                                        realtimeScores = liveLb.Scores;
                                        realtimeHp = liveLb.Hp;
                                        realtimeSpectators = liveLb.Spectators;
                                        realtimePlayerNames = liveLb.PlayerNames;
                                        realtimePlayersPlaced = liveLb.PlayersPlaced ?? new HashSet<string>();

                                        var waitingLeaderboard = realtimeScores.Count > 0 
                                            ? realtimeScores.Keys.Select(id => 
                                            {
                                                var score = realtimeScores.GetValueOrDefault(id, 0);
                                                if (id == playerId)
                                                    score = Math.Max(score, engine.Score);
                                                return (id, score, realtimeHp.GetValueOrDefault(id, 100), realtimeSpectators.Contains(id));
                                            }).ToList()
                                            : new List<(string, int, int, bool)>{ (playerId, engine.Score, 100, false) };
                                        TetrisMultiplayer.UI.ConsoleUI.DrawGameWithLeaderboard(engine, waitingLeaderboard, playerId, "WAITING FOR OTHERS...", realtimePlayerNames, realtimePlayersPlaced);
                                    }

                                    GameLogger.LogDebug($"[Client] Waiting for round results...");
                                    var rr = await network.ReceiveRoundResultsAsync(cancellationToken);
                                    if (rr != null)
                                    {
                                        GameLogger.LogDebug($"[Client] Received round results for round {round}");
                                        
                                        var roundLeaderboard = new List<(string, int, int, bool)>();
                                        foreach (var id in rr.NewScores.Keys)
                                        {
                                            int score = rr.NewScores[id];
                                            int hp = rr.Hp.GetValueOrDefault(id, 100);
                                            bool isSpec = rr.Spectators.Contains(id);
                                            roundLeaderboard.Add((id, score, hp, isSpec));
                                        }
                                        
                                        // Show who completed the round
                                        var completedPlayers = new HashSet<string>(rr.NewScores.Keys);
                                        TetrisMultiplayer.UI.ConsoleUI.DrawGameWithLeaderboard(engine, roundLeaderboard, playerId, "ROUND COMPLETE - ALL PLAYERS FINISHED", realtimePlayerNames, completedPlayers);
                                        
                                        // Check if we became a spectator
                                        if (rr.Spectators.Contains(playerId))
                                        {
                                            isSpectator = true;
                                            Console.SetCursorPosition(2, TetrisMultiplayer.Game.TetrisEngine.Height + 10);
                                            Console.WriteLine("You have been eliminated and are now a spectator!");
                                            await Task.Delay(2000);
                                            break; // Break out of the piece loop, continue in spectator mode
                                        }
                                        
                                        // SYNCHRONIZATION FIX: Wait for host's "WaitForNextRound" message before proceeding
                                        GameLogger.LogDebug($"[Client] Waiting for synchronization message from host...");
                                        TetrisMultiplayer.UI.ConsoleUI.DrawGameWithLeaderboard(engine, roundLeaderboard, playerId, "SYNCHRONIZING - WAITING FOR ALL PLAYERS...", realtimePlayerNames, completedPlayers);
                                        
                                        // Wait for the host's synchronization message
                                        while (!cancellationToken.IsCancellationRequested)
                                        {
                                            var waitMsg = await network.ReceiveWaitForNextRoundAsync(cancellationToken);
                                            if (waitMsg != null)
                                            {
                                                GameLogger.LogDebug($"[Client] Received synchronization message: {waitMsg}");
                                                TetrisMultiplayer.UI.ConsoleUI.DrawGameWithLeaderboard(engine, roundLeaderboard, playerId, waitMsg, realtimePlayerNames, completedPlayers);
                                                break;
                                            }
                                            await Task.Delay(100, cancellationToken);
                                        }
                                        
                                        // NEUE SYNCHRONISATIONS-MECHANISMUS: Warte auf RoundReadyRequest und bestätige
                                        GameLogger.LogDebug($"[Client] Waiting for RoundReadyRequest from host...");
                                        TetrisMultiplayer.UI.ConsoleUI.DrawGameWithLeaderboard(engine, roundLeaderboard, playerId, "WARTE AUF BEREITSCHAFTSANFRAGE...", realtimePlayerNames, completedPlayers);
                                        
                                        var readyTimeout = DateTime.UtcNow.AddSeconds(15); // 15 Sekunden Timeout für Ready-Request
                                        bool receivedReadyRequest = false;
                                        
                                        while (!receivedReadyRequest && DateTime.UtcNow < readyTimeout && !cancellationToken.IsCancellationRequested)
                                        {
                                            var readyRequestRound = await network.ReceiveRoundReadyRequestAsync(cancellationToken);
                                            if (readyRequestRound.HasValue && readyRequestRound.Value == round)
                                            {
                                                GameLogger.LogDebug($"[Client] Received RoundReadyRequest for round {readyRequestRound.Value}, sending confirmation...");
                                                TetrisMultiplayer.UI.ConsoleUI.DrawGameWithLeaderboard(engine, roundLeaderboard, playerId, "BESTÄTIGE BEREITSCHAFT FÜR NÄCHSTE RUNDE...", realtimePlayerNames, completedPlayers);
                                                
                                                // Bestätige Bereitschaft an Host
                                                await network.SendRoundReadyConfirmation(round);
                                                receivedReadyRequest = true;
                                                
                                                GameLogger.LogDebug($"[Client] RoundReadyConfirmation sent for round {round}");
                                                break;
                                            }
                                            await Task.Delay(100, cancellationToken);
                                        }
                                        
                                        if (!receivedReadyRequest)
                                        {
                                            GameLogger.LogError($"[Client] Timeout waiting for RoundReadyRequest - proceeding anyway");
                                        }
                                        else
                                        {
                                            GameLogger.LogDebug($"[Client] ✓ Synchronisation erfolgreich abgeschlossen für Runde {round}");
                                        }
                                        
                                        await Task.Delay(500); // Brief pause after synchronization
                                        break; // CRITICAL: Break out of the waiting loop to continue to next piece
                                    }
                                    else
                                    {
                                        GameLogger.LogDebug($"[Client] No round results received, continuing...");
                                    }
                                }
                                
                                await Task.Delay(50, cancellationToken); // Smooth game loop
                            }
                            
                            round++;
                            GameLogger.LogDebug($"[Client] Round {round - 1} completed, moving to round {round}");
                        }
                    }
                    catch (Exception ex)
                    {
                        GameLogger.LogError($"[Client] Error in game loop: {ex.Message}");
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }
            finally
            {
                GameLogger.SetGameActive(false);
            }
        }

        public static async Task BroadcastLobbyUpdate(NetworkManager network, Dictionary<string, int>? hps = null, HashSet<string>? spectators = null, Dictionary<string, string>? playerNames = null)
        {
            // PlayerState-Liste korrekt pflegen, Namen berücksichtigen
            var players = network.ConnectedPlayerIds.Select(id => new PlayerState {
                PlayerId = id,
                Name = playerNames != null && playerNames.ContainsKey(id) ? playerNames[id] : $"Spieler_{id.Substring(0, 4)}",
                Hp = hps != null && hps.ContainsKey(id) ? hps[id] : 100,
                IsSpectator = spectators != null && spectators.Contains(id)
            }).ToList();
            // Host selbst hinzufügen, falls nicht in ConnectedPlayerIds
            if (playerNames != null && playerNames.ContainsKey("host"))
            {
                players.Insert(0, new PlayerState {
                    PlayerId = "host",
                    Name = playerNames["host"],
                    Hp = hps != null && hps.ContainsKey("host") ? hps["host"] : 100,
                    IsSpectator = spectators != null && spectators.Contains("host")
                });
            }
            var lobbyUpdate = new { type = "LobbyUpdate", players };
            await network.BroadcastAsync(lobbyUpdate);
        }

        public static async Task BroadcastRealtimeLeaderboard(NetworkManager network, Dictionary<string, int> scores, Dictionary<string, int> hps, HashSet<string> spectators, Dictionary<string, string> playerNames, HashSet<string> playersWhoPlaced, CancellationToken cancellationToken)
        {
            // Send ALL players, not just top 3, to fix the "some players only see themselves" issue
            var allPlayers = scores.Keys.ToList();
            
            // Prepare data in the format expected by ParseLeaderboardUpdate
            var leaderboardUpdate = new { 
                type = "LeaderboardUpdate", 
                scores = scores,                    // Flat scores dictionary
                hp = hps,                          // Flat hp dictionary  
                spectators = spectators.ToList(),  // Flat spectators list
                playerNames = playerNames,          // Player names dictionary
                playersPlaced = playersWhoPlaced.ToList()
            };
            await network.BroadcastAsync(leaderboardUpdate);
            
            // Logge ausführliche Informationen zum Leaderboard für alle Spieler
            foreach (var playerId in allPlayers)
            {
                var status = spectators.Contains(playerId) ? "Spectator" : "Player";
                var score = scores.GetValueOrDefault(playerId, 0);
                var hp = hps.GetValueOrDefault(playerId, 100);
                var placed = playersWhoPlaced.Contains(playerId) ? "PLACED" : "waiting";
                GameLogger.LogDebug($"[Leaderboard] {status} {playerId}: Score={score}, HP={hp}, Status={placed}");
            }
        }

        public static async Task BroadcastSpectatorSnapshots(NetworkManager network, Dictionary<string, TetrisMultiplayer.Game.TetrisEngine> fields, Dictionary<string, int> scores, Dictionary<string, int> hps, HashSet<string> spectators, CancellationToken cancellationToken)
        {
            var snapshot = new {
                Fields = fields.ToDictionary(kv => kv.Key, kv => ConvertToJaggedArray(kv.Value.GetGameField())),
                Scores = scores,
                Hp = hps,
                Spectators = spectators.ToList()
            };
            
            var snapshotMessage = new { type = "SpectatorSnapshot", snapshot };
            await network.BroadcastAsync(snapshotMessage);
        }

        public static int[][] ConvertToJaggedArray(int[,] array2D)
        {
            int rows = array2D.GetLength(0);
            int cols = array2D.GetLength(1);
            var jaggedArray = new int[rows][];
            for (int i = 0; i < rows; i++)
            {
                jaggedArray[i] = new int[cols];
                for (int j = 0; j < cols; j++)
                {
                    jaggedArray[i][j] = array2D[i, j];
                }
            }
            return jaggedArray;
        }

        public static bool JaggedArrayEquals(int[][]? a, int[][]? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] == null && b[i] == null) continue;
                if (a[i] == null || b[i] == null) return false;
                if (a[i].Length != b[i].Length) return false;
                for (int j = 0; j < a[i].Length; j++)
                {
                    if (a[i][j] != b[i][j]) return false;
                }
            }
            return true;
        }

    static async Task<List<Networking.NetworkManager.PlacedPieceMsg>> WaitForPlacedPieces(NetworkManager network, List<string> playerIds, int timeoutMs, ILogger logger, CancellationToken cancellationToken, Dictionary<string, int>? hps = null, HashSet<string>? spectators = null, HashSet<string>? playersWhoPlaced = null)
        {
            var results = new List<Networking.NetworkManager.PlacedPieceMsg>();
            var received = new HashSet<string>();
            var start = DateTime.UtcNow;
            var missedRounds = new Dictionary<string, int>();
            
            // Filter out spectators from expected players
            var activePlayers = playerIds.Where(id => spectators == null || !spectators.Contains(id)).ToList();
            
            foreach (var id in activePlayers) 
                missedRounds[id] = 0;
            
            logger.LogInformation($"[Host] Warte auf PlacedPiece von {activePlayers.Count} aktiven Spielern: {string.Join(", ", activePlayers)}");
            
            // SYNCHRONIZATION FIX: Keine adaptive Timeout-Erweiterung mehr - feste Zeit für alle
            var baseTimeout = Math.Max(timeoutMs, 15000); // Mindestens 15 Sekunden für echte Synchronisation
            var phaseTimeout = baseTimeout / 3; // Teile in 3 Phasen auf
            
            // Phase 1: Standardzeit warten (erste 1/3 der Zeit)
            var phase1End = start.AddMilliseconds(phaseTimeout);
            logger.LogInformation($"[Host] Phase 1: Warte {phaseTimeout/1000}s auf alle Spieler");
            
            while (DateTime.UtcNow < phase1End && received.Count < activePlayers.Count && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var msg = await network.ReceivePlacedPieceAsync(cancellationToken, 1000);
                    if (msg != null && !received.Contains(msg.PlayerId) && activePlayers.Contains(msg.PlayerId))
                    {
                        results.Add(msg);
                        received.Add(msg.PlayerId);
                        if (playersWhoPlaced != null)
                            playersWhoPlaced.Add(msg.PlayerId);
                        missedRounds[msg.PlayerId] = 0;
                        logger.LogInformation($"[Host] PlacedPiece von {msg.PlayerId} erhalten ({received.Count}/{activePlayers.Count})");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning($"[Host] Fehler beim Empfangen von PlacedPiece: {ex.Message}");
                }
                
                await Task.Delay(10, cancellationToken);
            }
            
            // Phase 2: Wenn noch nicht alle da sind, nochmals 1/3 der Zeit warten
            if (received.Count < activePlayers.Count)
            {
                var missingInPhase1 = activePlayers.Where(id => !received.Contains(id)).ToList();
                logger.LogInformation($"[Host] Phase 2: Noch {missingInPhase1.Count} Spieler fehlen: {string.Join(", ", missingInPhase1)}. Warte weitere {phaseTimeout/1000}s");
                
                var phase2End = DateTime.UtcNow.AddMilliseconds(phaseTimeout);
                
                while (DateTime.UtcNow < phase2End && received.Count < activePlayers.Count && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var msg = await network.ReceivePlacedPieceAsync(cancellationToken, 1000);
                        if (msg != null && !received.Contains(msg.PlayerId) && activePlayers.Contains(msg.PlayerId))
                        {
                            results.Add(msg);
                            received.Add(msg.PlayerId);
                            if (playersWhoPlaced != null)
                                playersWhoPlaced.Add(msg.PlayerId);
                            missedRounds[msg.PlayerId] = 0;
                            logger.LogInformation($"[Host] PlacedPiece von {msg.PlayerId} in Phase 2 erhalten ({received.Count}/{activePlayers.Count})");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning($"[Host] Fehler beim Empfangen von PlacedPiece: {ex.Message}");
                    }
                    
                    await Task.Delay(10, cancellationToken);
                }
            }
            
            // Phase 3: Finale Wartezeit für ganz langsame Spieler oder Prüfung auf Disconnect
            if (received.Count < activePlayers.Count)
            {
                var missingInPhase2 = activePlayers.Where(id => !received.Contains(id)).ToList();
                logger.LogInformation($"[Host] Phase 3: Letzte Chance für {missingInPhase2.Count} Spieler: {string.Join(", ", missingInPhase2)}. Warte finale {phaseTimeout/1000}s");
                
                var phase3End = DateTime.UtcNow.AddMilliseconds(phaseTimeout);
                
                while (DateTime.UtcNow < phase3End && received.Count < activePlayers.Count && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var msg = await network.ReceivePlacedPieceAsync(cancellationToken, 1000);
                        if (msg != null && !received.Contains(msg.PlayerId) && activePlayers.Contains(msg.PlayerId))
                        {
                            results.Add(msg);
                            received.Add(msg.PlayerId);
                            if (playersWhoPlaced != null)
                                playersWhoPlaced.Add(msg.PlayerId);
                            missedRounds[msg.PlayerId] = 0;
                            logger.LogInformation($"[Host] PlacedPiece von {msg.PlayerId} in finaler Phase erhalten ({received.Count}/{activePlayers.Count})");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning($"[Host] Fehler beim Empfangen von PlacedPiece: {ex.Message}");
                    }
                    
                    await Task.Delay(10, cancellationToken);
                }
            }
            
            // Prüfe fehlende Spieler - unterscheide zwischen langsam und disconnected
            var missingPlayers = activePlayers.Where(id => !received.Contains(id)).ToList();
            if (missingPlayers.Count > 0)
            {
                logger.LogWarning($"[Host] Nach allen Phasen fehlen {missingPlayers.Count} Spieler: {string.Join(", ", missingPlayers)}");
                
                // Vorsichtigere Behandlung von Timeouts
                foreach (var id in missingPlayers)
                {
                    if (!missedRounds.ContainsKey(id))
                        missedRounds[id] = 0;
                    missedRounds[id]++;
                }
            }
            else
            {
                logger.LogInformation($"[Host] ✓ Alle {activePlayers.Count} Spieler haben ihre Pieces platziert - perfekte Synchronisation!");
            }
            
            // Eliminiere Spieler nur nach mehr verpassten Runden (3 statt 2) für bessere Toleranz
            if (hps != null && spectators != null)
            {
                foreach (var id in missingPlayers)
                {
                    if (missedRounds[id] >= 3 && !spectators.Contains(id))
                    {
                        logger.LogWarning($"[Host] Spieler {id} nach 3 verpassten Runden eliminiert (wahrscheinlich Disconnect)");
                        hps[id] = 0;
                        spectators.Add(id);
                    }
                    else if (missedRounds[id] >= 1)
                    {
                        logger.LogInformation($"[Host] Spieler {id} hat Runde verpasst ({missedRounds[id]}/3 Versuche)");
                    }
                }
            }
            
            logger.LogInformation($"[Host] Received {results.Count}/{activePlayers.Count} PlacedPiece messages in {(DateTime.UtcNow - start).TotalMilliseconds:F0}ms");
            return results;
        }

        static void DrawFieldRaw(int[,] grid)
        {
            int h = grid.GetLength(0), w = grid.GetLength(1);
            for (int y = 0; y < h; y++)
            {
                Console.Write("|");
                for (int x = 0; x < w; x++)
                {
                    Console.Write(grid[y, x] == 0 ? " ." : "[]");
                }
                Console.WriteLine("|");
            }
            Console.WriteLine("+" + new string('-', w * 2) + "+");
        }

        // Helper method to wait for a task with timeout and proper error handling
        static async Task WaitForTaskWithTimeout(Task task, int timeoutMs, string taskName)
        {
            try
            {
                using var cts = new CancellationTokenSource(timeoutMs);
                await task.WaitAsync(cts.Token);
            }
            catch (TaskCanceledException)
            {
                // Task was canceled - this is expected during cleanup
                GameLogger.LogDebug($"{taskName} task was canceled (expected during cleanup)");
            }
            catch (OperationCanceledException)
            {
                // Task timeout or cancellation - this is expected during cleanup
                GameLogger.LogDebug($"{taskName} task cleanup completed (timeout/cancellation)");
            }
            catch (Exception ex)
            {
                GameLogger.LogError($"Error cleaning up {taskName} task: {ex.Message}");
            }
        }
    }
}