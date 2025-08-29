using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json.Serialization;

namespace TetrisMultiplayer.Networking
{
    public class NetworkManager
    {
        private TcpListener? _listener;
        private readonly List<TcpClient> _clients = new();
        private readonly ConcurrentDictionary<string, TcpClient> _playerClients = new();
        private readonly Dictionary<string, string> _playerNames = new();
        private readonly ConcurrentQueue<PlacedPieceMsg> _placedPieceQueue = new();
        public ICollection<string> ConnectedPlayerIds => _playerClients.Keys;
        public IReadOnlyDictionary<string, string> PlayerNames => _playerNames;

    // Client-side message queue for proper StartGame handling
    private readonly Queue<JsonElement> _clientMessageQueue = new();
    private readonly object _queueLock = new object();
    // Single client receiver state to prevent concurrent stream reads
    private CancellationTokenSource? _clientReceiveCts;
    private Task? _clientReceiveTask;

        // Lobby-Discovery-System
        private UdpClient? _discoveryServer;
        private Task? _discoveryTask;
        private string? _hostName;
        private const int DISCOVERY_PORT = 5001;

        // Helper method for debug logging that won't interfere with UI
        private static void LogDebugToFile(string message)
        {
            try
            {
                var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [NetworkManager] {message}";
                File.AppendAllText("tetris_multiplayer_network.log", logMessage + Environment.NewLine);
            }
            catch
            {
                // Silently ignore logging errors
            }
        }

        public async Task StartHost(int port, CancellationToken cancellationToken = default)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            Console.WriteLine($"Host lauscht auf Port {port}...");
            _ = AcceptClientsAsync(_listener, cancellationToken); // Fire-and-forget, nicht awaiten!
            await Task.CompletedTask; // sofort zurückkehren, damit Lobby angezeigt wird
        }

        // Host: Startet Host mit Lobby-Broadcasting
        public async Task StartHostWithDiscovery(int port, string hostName, CancellationToken cancellationToken = default)
        {
            await StartHost(port, cancellationToken);
            await StartLobbyBroadcast(hostName, port, cancellationToken);
        }

        private async Task AcceptClientsAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync(cancellationToken);
                    _clients.Add(client);
                    _ = HandleClientAsync(client, cancellationToken); // Fire-and-forget bleibt
                }
                catch (Exception) when (cancellationToken.IsCancellationRequested)
                {
                    break; // Expected when cancelling
                }
                catch (Exception ex)
                {
                    LogDebugToFile($"Error accepting client: {ex.Message}");
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            string? playerId = null;
            string? playerName = null;
            
            try
            {
                client.ReceiveTimeout = 30000; // 30 second timeout
                client.SendTimeout = 30000;
                var stream = client.GetStream();
                
                while (!cancellationToken.IsCancellationRequested && client.Connected)
                {
                    try
                    {
                        var msg = await ReadFramedJsonAsync(stream, cancellationToken);
                        if (msg == null) break;
                        
                        var element = msg.Value;
                        if (element.TryGetProperty("type", out var typeProp))
                        {
                            var messageType = typeProp.GetString();
                            
                            if (messageType == "ConnectRequest")
                            {
                                if (element.TryGetProperty("playerName", out var pnameProp))
                                    playerName = pnameProp.GetString();
                                playerId = Guid.NewGuid().ToString();
                                _playerClients[playerId] = client;
                                if (!string.IsNullOrEmpty(playerName))
                                    _playerNames[playerId] = playerName;
                                var response = new { type = "ConnectResponse", playerId, success = true, error = (string?)null, rejoined = false };
                                await WriteFramedJsonAsync(stream, response, cancellationToken);
                                LogDebugToFile($"Neuer Spieler verbunden: {playerId} ({playerName})");
                            }
                else if (messageType == "PlacedPiece" && !string.IsNullOrEmpty(playerId))
                            {
                                // Handle PlacedPiece messages from clients
                                var placedPiece = new PlacedPieceMsg
                                {
                                    PlayerId = playerId,
                                    PieceId = element.GetProperty("pieceId").GetInt32(),
                                    PlacedAt = element.GetProperty("placedAt").GetInt64(),
                    Locks = element.GetProperty("locks").GetBoolean(),
                    ScoreDelta = element.TryGetProperty("scoreDelta", out var sdProp) && sdProp.TryGetInt32(out int sd) ? sd : 0,
                    LinesCleared = element.TryGetProperty("linesCleared", out var lcProp) && lcProp.TryGetInt32(out int lc) ? lc : 0
                                };
                                
                                _placedPieceQueue.Enqueue(placedPiece);
                                // Use file-only logging during gameplay to avoid UI interference
                                LogDebugToFile($"PlacedPiece received from {playerId}: Piece {placedPiece.PieceId}");
                            }
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogDebugToFile($"Error reading from client {playerId}: {ex.Message}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebugToFile($"Fehler im Client-Handler {playerId}: {ex.Message}");
            }
            finally
            {
                if (!string.IsNullOrEmpty(playerId))
                {
                    _playerClients.TryRemove(playerId, out _);
                    _playerNames.Remove(playerId);
                    LogDebugToFile($"Spieler {playerId} getrennt.");
                }
                
                try
                {
                    client?.Close();
                    client?.Dispose();
                }
                catch { }
            }
        }

        public async Task BroadcastAsync(object message)
        {
            try
            {
                var json = JsonSerializer.Serialize(message);
                var bytes = Encoding.UTF8.GetBytes(json);
                var length = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(bytes.Length));
                
                var clientsToRemove = new List<string>();
                
                foreach (var kvp in _playerClients)
                {
                    var playerId = kvp.Key;
                    var client = kvp.Value;
                    
                    try
                    {
                        if (client.Connected)
                        {
                            var stream = client.GetStream();
                            await stream.WriteAsync(length);
                            await stream.WriteAsync(bytes);
                            await stream.FlushAsync();
                        }
                        else
                        {
                            clientsToRemove.Add(playerId);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogDebugToFile($"Error broadcasting to {playerId}: {ex.Message}");
                        clientsToRemove.Add(playerId);
                    }
                }
                
                // Clean up disconnected clients
                foreach (var playerId in clientsToRemove)
                {
                    _playerClients.TryRemove(playerId, out _);
                    _playerNames.Remove(playerId);
                }
            }
            catch (JsonException ex)
            {
                LogDebugToFile($"JSON Serialization Error in BroadcastAsync: {ex.Message}");
                LogDebugToFile($"Message type: {message?.GetType()?.Name ?? "null"}");
                // Don't re-throw - just log and continue
            }
            catch (Exception ex)
            {
                LogDebugToFile($"Unexpected error in BroadcastAsync: {ex.Message}");
                // Don't re-throw - just log and continue
            }
        }

        // Client: Empfange LobbyUpdate (queue-only to avoid concurrent stream reads)
        public Task<LobbyUpdateDto?> ReceiveLobbyUpdateAsync(CancellationToken cancellationToken)
        {
            lock (_queueLock)
            {
                var tempQueue = new Queue<JsonElement>();
                LobbyUpdateDto? result = null;
                while (_clientMessageQueue.Count > 0)
                {
                    var element = _clientMessageQueue.Dequeue();
                    if (element.TryGetProperty("type", out var typeProp))
                    {
                        var messageType = typeProp.GetString();
                        if (messageType == "LobbyUpdate")
                        {
                            var players = new List<Model.PlayerState>();
                            if (element.TryGetProperty("players", out var playersProp) && playersProp.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var p in playersProp.EnumerateArray())
                                {
                                    string playerId = p.TryGetProperty("playerId", out var pidProp) ? pidProp.GetString() ?? string.Empty : string.Empty;
                                    string name = p.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? string.Empty : string.Empty;
                                    int hp = p.TryGetProperty("hp", out var hpProp) && hpProp.TryGetInt32(out int hpVal) ? hpVal : 100;
                                    bool isSpectator = p.TryGetProperty("isSpectator", out var specProp) && specProp.ValueKind == JsonValueKind.True;
                                    players.Add(new Model.PlayerState
                                    {
                                        PlayerId = playerId,
                                        Name = name,
                                        Hp = hp,
                                        IsSpectator = isSpectator
                                    });
                                }
                            }
                            result = new LobbyUpdateDto { Players = players };
                            break;
                        }
                    }
                    tempQueue.Enqueue(element);
                }
                // Put back all non-lobby messages
                while (tempQueue.Count > 0)
                    _clientMessageQueue.Enqueue(tempQueue.Dequeue());
                return Task.FromResult<LobbyUpdateDto?>(result);
            }
        }

        private TetrisMultiplayer.Game.GameManager? _gameManager;

        private NetworkStream? _clientStream;
        public async Task ConnectToHost(string ip, int port, string playerName, CancellationToken cancellationToken = default)
        {
            var client = new TcpClient();
            client.ReceiveTimeout = 30000;
            client.SendTimeout = 30000;
            
            await client.ConnectAsync(ip, port, cancellationToken);
            _clientStream = client.GetStream();
            var connectReq = new { type = "ConnectRequest", playerName = playerName };
            await WriteFramedJsonAsync(_clientStream, connectReq, cancellationToken);
            var response = await ReadFramedJsonAsync(_clientStream, cancellationToken);
            if (response != null)
            {
                var element = response.Value;
                if (element.TryGetProperty("success", out var success) && success.GetBoolean())
                {
                    _clientPlayerId = element.GetProperty("playerId").GetString();
                    LogDebugToFile($"Erfolgreich mit Host verbunden. PlayerId: {_clientPlayerId}");
                    // Start a single background receiver to avoid concurrent stream reads
                    _clientReceiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    _clientReceiveTask = Task.Run(() => ClientReceiveLoop(_clientReceiveCts.Token));
                }
                else
                {
                    LogDebugToFile($"Verbindung fehlgeschlagen: {element.GetProperty("error").GetString()}");
                }
            }
        }

        private string? _clientPlayerId;
        public string? ClientPlayerId => _clientPlayerId;

        // Improved PlacedPiece reception using the queue
        public async Task<PlacedPieceMsg?> ReceivePlacedPieceAsync(CancellationToken cancellationToken, int timeoutMs)
        {
            var startTime = DateTime.UtcNow;
            
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs && !cancellationToken.IsCancellationRequested)
            {
                if (_placedPieceQueue.TryDequeue(out var placedPiece))
                {
                    return placedPiece;
                }
                
                await Task.Delay(10, cancellationToken);
            }
            
            return null;
        }

        public TetrisMultiplayer.Game.GameManager? GameManager => _gameManager;

        // Client: Warte auf NextPiece
    public Task<int?> ReceiveNextPieceAsync(CancellationToken cancellationToken)
        {
            // First check the queue for buffered messages
            lock (_queueLock)
            {
                if (_clientMessageQueue.Count > 0)
                {
                    var queuedElement = _clientMessageQueue.Dequeue();
                    if (queuedElement.TryGetProperty("type", out var qTypeProp) && qTypeProp.GetString() == "NextPiece")
                    {
            return Task.FromResult<int?>(queuedElement.GetProperty("pieceId").GetInt32());
                    }
                    // Put it back if not NextPiece
                    var tempQueue = new Queue<JsonElement>();
                    tempQueue.Enqueue(queuedElement);
                    while (_clientMessageQueue.Count > 0)
                        tempQueue.Enqueue(_clientMessageQueue.Dequeue());
                    _clientMessageQueue.Clear();
                    while (tempQueue.Count > 0)
                        _clientMessageQueue.Enqueue(tempQueue.Dequeue());
                }
            }
        return Task.FromResult<int?>(null);
        }

        // NEW: Client receives real-time leaderboard updates
    public Task<LeaderboardUpdateDto?> ReceiveLeaderboardUpdateAsync(CancellationToken cancellationToken)
        {
            // First check the queue for buffered messages
            lock (_queueLock)
            {
                var tempQueue = new Queue<JsonElement>();
                while (_clientMessageQueue.Count > 0)
                {
                    var queuedElement = _clientMessageQueue.Dequeue();
                    if (queuedElement.TryGetProperty("type", out var qTypeProp) && qTypeProp.GetString() == "LeaderboardUpdate")
                    {
                        // Put back remaining messages
                        while (tempQueue.Count > 0)
                            _clientMessageQueue.Enqueue(tempQueue.Dequeue());
            return Task.FromResult<LeaderboardUpdateDto?>(ParseLeaderboardUpdate(queuedElement));
                    }
                    tempQueue.Enqueue(queuedElement);
                }
                // Put back all messages
                while (tempQueue.Count > 0)
                    _clientMessageQueue.Enqueue(tempQueue.Dequeue());
            }
        return Task.FromResult<LeaderboardUpdateDto?>(null); // Don't block - this is called frequently
        }

        private LeaderboardUpdateDto ParseLeaderboardUpdate(JsonElement element)
        {
            var result = new LeaderboardUpdateDto();
            
            if (element.TryGetProperty("scores", out var scoresProp) && scoresProp.ValueKind == JsonValueKind.Object)
            {
                foreach (var kvp in scoresProp.EnumerateObject())
                {
                    result.Scores[kvp.Name] = kvp.Value.GetInt32();
                }
            }
            
            if (element.TryGetProperty("hp", out var hpProp) && hpProp.ValueKind == JsonValueKind.Object)
            {
                foreach (var kvp in hpProp.EnumerateObject())
                {
                    result.Hp[kvp.Name] = kvp.Value.GetInt32();
                }
            }
            
            if (element.TryGetProperty("spectators", out var spectatorsProp) && spectatorsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var spectator in spectatorsProp.EnumerateArray())
                {
                    var spectatorId = spectator.GetString();
                    if (!string.IsNullOrEmpty(spectatorId))
                        result.Spectators.Add(spectatorId);
                }
            }
            
            if (element.TryGetProperty("playerNames", out var namesProp) && namesProp.ValueKind == JsonValueKind.Object)
            {
                foreach (var kvp in namesProp.EnumerateObject())
                {
                    var name = kvp.Value.GetString();
                    if (!string.IsNullOrEmpty(name))
                        result.PlayerNames[kvp.Name] = name;
                }
            }
            
            if (element.TryGetProperty("playersPlaced", out var placedProp) && placedProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in placedProp.EnumerateArray())
                {
                    var id = p.GetString();
                    if (!string.IsNullOrEmpty(id))
                        result.PlayersPlaced.Add(id);
                }
            }
            
            return result;
        }

        // Client: Warte auf NextPiece oder GameOver
    public Task<dynamic?> ReceiveNextPieceOrGameOverAsync(CancellationToken cancellationToken)
        {
            // First check the queue for buffered messages
            lock (_queueLock)
            {
                var tempQueue = new Queue<JsonElement>();
                while (_clientMessageQueue.Count > 0)
                {
                    var queuedElement = _clientMessageQueue.Dequeue();
                    if (queuedElement.TryGetProperty("type", out var qTypeProp))
                    {
                        var qType = qTypeProp.GetString();
                        if (qType == "NextPiece")
                        {
                            // Put back remaining messages
                            while (tempQueue.Count > 0)
                                _clientMessageQueue.Enqueue(tempQueue.Dequeue());
                            
                            var pieceId = queuedElement.GetProperty("pieceId").GetInt32();
                            int? previewPieceId = null;
                            if (queuedElement.TryGetProperty("previewPieceId", out var previewProp))
                            {
                                previewPieceId = previewProp.GetInt32();
                            }
                            
                return Task.FromResult<dynamic?>(new { type = "NextPiece", pieceId, previewPieceId });
                        }
                        else if (qType == "GameOver")
                        {
                            // Put back remaining messages
                            while (tempQueue.Count > 0)
                                _clientMessageQueue.Enqueue(tempQueue.Dequeue());
                            var winnerId = queuedElement.GetProperty("winnerId").GetString();
                            var stats = queuedElement.GetProperty("stats");
                return Task.FromResult<dynamic?>(new { type = "GameOver", winnerId, stats });
                        }
                    }
                    tempQueue.Enqueue(queuedElement);
                }
                // Put back all messages
                while (tempQueue.Count > 0)
                    _clientMessageQueue.Enqueue(tempQueue.Dequeue());
            }
        return Task.FromResult<dynamic?>(null);
        }

        // Client: Sende PlacedPiece
        public async Task SendPlacedPieceAsync(object placedPiece)
        {
            if (_clientStream == null) return;
            
            try
            {
                var json = JsonSerializer.Serialize(placedPiece);
                var bytes = Encoding.UTF8.GetBytes(json);
                var length = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(bytes.Length));
                await _clientStream.WriteAsync(length);
                await _clientStream.WriteAsync(bytes);
                await _clientStream.FlushAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending PlacedPiece: {ex.Message}");
            }
        }

        // Client: Allgemeine Methode zum Senden von Nachrichten an Host
        public async Task SendToHostAsync(object message)
        {
            if (_clientStream == null) return;
            
            try
            {
                await WriteFramedJsonAsync(_clientStream, message, CancellationToken.None);
            }
            catch (Exception ex)
            {
                LogDebugToFile($"Error sending message to host: {ex.Message}");
            }
        }

        // Client: Warte auf RoundResults
    public Task<RoundResultsDto?> ReceiveRoundResultsAsync(CancellationToken cancellationToken)
        {
            // First check the queue for buffered messages
            lock (_queueLock)
            {
                var tempQueue = new Queue<JsonElement>();
                while (_clientMessageQueue.Count > 0)
                {
                    var queuedElement = _clientMessageQueue.Dequeue();
                    if (queuedElement.TryGetProperty("type", out var qTypeProp) && qTypeProp.GetString() == "RoundResults")
                    {
                        // Put back remaining messages
                        while (tempQueue.Count > 0)
                            _clientMessageQueue.Enqueue(tempQueue.Dequeue());
            return Task.FromResult<RoundResultsDto?>(ParseRoundResults(queuedElement));
                    }
                    tempQueue.Enqueue(queuedElement);
                }
                // Put back all messages
                while (tempQueue.Count > 0)
                    _clientMessageQueue.Enqueue(tempQueue.Dequeue());
            }
        return Task.FromResult<RoundResultsDto?>(null);
        }

        private RoundResultsDto ParseRoundResults(JsonElement element)
        {
            var roundResults = new RoundResultsDto();
            
            if (element.TryGetProperty("deletedRowsPerPlayer", out var deletedRowsProp) && deletedRowsProp.ValueKind == JsonValueKind.Object)
            {
                foreach (var kvp in deletedRowsProp.EnumerateObject())
                {
                    roundResults.DeletedRowsPerPlayer[kvp.Name] = kvp.Value.GetInt32();
                }
            }
            
            if (element.TryGetProperty("newScores", out var newScoresProp) && newScoresProp.ValueKind == JsonValueKind.Object)
            {
                foreach (var kvp in newScoresProp.EnumerateObject())
                {
                    roundResults.NewScores[kvp.Name] = kvp.Value.GetInt32();
                }
            }
            
            if (element.TryGetProperty("hp", out var hpProp) && hpProp.ValueKind == JsonValueKind.Object)
            {
                foreach (var kvp in hpProp.EnumerateObject())
                {
                    roundResults.Hp[kvp.Name] = kvp.Value.GetInt32();
                }
            }
            
            if (element.TryGetProperty("hpChanges", out var hpChangesProp) && hpChangesProp.ValueKind == JsonValueKind.Object)
            {
                foreach (var kvp in hpChangesProp.EnumerateObject())
                {
                    roundResults.HpChanges[kvp.Name] = kvp.Value.GetInt32();
                }
            }
            
            if (element.TryGetProperty("spectators", out var spectatorsProp) && spectatorsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var spectator in spectatorsProp.EnumerateArray())
                {
                    var spectatorId = spectator.GetString();
                    if (!string.IsNullOrEmpty(spectatorId))
                        roundResults.Spectators.Add(spectatorId);
                }
            }
            
            return roundResults;
        }

        public class RoundResultsDto
        {
            public Dictionary<string, int> DeletedRowsPerPlayer { get; set; } = new();
            public Dictionary<string, int> NewScores { get; set; } = new();
            public Dictionary<string, int> Hp { get; set; } = new();
            public Dictionary<string, int> HpChanges { get; set; } = new();
            public List<string> Spectators { get; set; } = new();
        }

        public class LeaderboardUpdateDto
        {
            public Dictionary<string, int> Scores { get; set; } = new();
            public Dictionary<string, int> Hp { get; set; } = new();
            public List<string> Spectators { get; set; } = new();
            public Dictionary<string, string> PlayerNames { get; set; } = new();
            public HashSet<string> PlayersPlaced { get; set; } = new();
        }

        // Client: Empfange SpectatorSnapshot
        public class SpectatorSnapshotDto
        {
            public Dictionary<string, int[,]> Fields { get; set; } = new();
            public Dictionary<string, int> Scores { get; set; } = new();
            public Dictionary<string, int> Hp { get; set; } = new();
            public List<string> Spectators { get; set; } = new();
        }
        
        public Task<SpectatorSnapshotDto?> ReceiveSpectatorSnapshotAsync(CancellationToken cancellationToken)
        {
            // Dequeue SpectatorSnapshot if present
            lock (_queueLock)
            {
                var tempQueue = new Queue<JsonElement>();
                SpectatorSnapshotDto? result = null;
                while (_clientMessageQueue.Count > 0)
                {
                    var element = _clientMessageQueue.Dequeue();
                    if (result == null && element.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "SpectatorSnapshot")
                    {
                        var fields = new Dictionary<string, int[,]>();
                        var scores = new Dictionary<string, int>();
                        var hp = new Dictionary<string, int>();
                        var spectators = new List<string>();
                        if (element.TryGetProperty("fields", out var fieldsProp) && fieldsProp.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var player in fieldsProp.EnumerateObject())
                            {
                                var jaggedArray = player.Value;
                                if (jaggedArray.ValueKind == JsonValueKind.Array)
                                {
                                    int rows = jaggedArray.GetArrayLength();
                                    if (rows > 0)
                                    {
                                        var firstRow = jaggedArray[0];
                                        int cols = firstRow.GetArrayLength();
                                        int[,] grid = new int[rows, cols];
                                        for (int y = 0; y < rows; y++)
                                        {
                                            var row = jaggedArray[y];
                                            for (int x = 0; x < cols && x < row.GetArrayLength(); x++)
                                                grid[y, x] = row[x].GetInt32();
                                        }
                                        fields[player.Name] = grid;
                                    }
                                }
                            }
                        }
                        if (element.TryGetProperty("scores", out var scoresProp) && scoresProp.ValueKind == JsonValueKind.Object)
                            foreach (var s in scoresProp.EnumerateObject()) scores[s.Name] = s.Value.GetInt32();
                        if (element.TryGetProperty("hp", out var hpProp) && hpProp.ValueKind == JsonValueKind.Object)
                            foreach (var hpe in hpProp.EnumerateObject()) hp[hpe.Name] = hpe.Value.GetInt32();
                        if (element.TryGetProperty("spectators", out var specProp) && specProp.ValueKind == JsonValueKind.Array)
                            foreach (var s in specProp.EnumerateArray()) spectators.Add(s.GetString() ?? "");
                        result = new SpectatorSnapshotDto { Fields = fields, Scores = scores, Hp = hp, Spectators = spectators };
                    }
                    else tempQueue.Enqueue(element);
                }
                while (tempQueue.Count > 0) _clientMessageQueue.Enqueue(tempQueue.Dequeue());
                return Task.FromResult<SpectatorSnapshotDto?>(result);
            }
        }

        public class LobbyUpdateDto
        {
            public List<Model.PlayerState> Players { get; set; } = new();
        }

        public class PlacedPieceMsg
        {
            public string PlayerId { get; set; } = string.Empty;
            public int PieceId { get; set; }
            public long PlacedAt { get; set; }
            public bool Locks { get; set; }
            public int ScoreDelta { get; set; }
            public int LinesCleared { get; set; }
        }

        private static async Task WriteFramedJsonAsync(Stream stream, object obj, CancellationToken cancellationToken)
        {
            try
            {
                var json = JsonSerializer.Serialize(obj);
                var bytes = Encoding.UTF8.GetBytes(json);
                var length = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(bytes.Length));
                await stream.WriteAsync(length, cancellationToken);
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in WriteFramedJsonAsync: {ex.Message}");
                throw;
            }
        }

        private static async Task<JsonElement?> ReadFramedJsonAsync(Stream stream, CancellationToken cancellationToken)
        {
            try
            {
                var lengthBytes = new byte[4];
                int read = 0;
                while (read < 4)
                {
                    int bytesRead = await stream.ReadAsync(lengthBytes.AsMemory(read, 4 - read), cancellationToken);
                    if (bytesRead == 0) return null; // Connection closed
                    read += bytesRead;
                }
                
                int length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lengthBytes, 0));
                if (length <= 0 || length > 1024 * 1024) // Max 1MB message
                {
                    LogDebugToFile($"Invalid message length: {length}");
                    return null;
                }
                
                var buffer = new byte[length];
                int offset = 0;
                while (offset < length)
                {
                    int r = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);
                    if (r == 0) return null; // Connection closed
                    offset += r;
                }
                
                var doc = JsonDocument.Parse(buffer);
                return doc.RootElement.Clone();
            }
            catch (Exception ex)
            {
                LogDebugToFile($"Error in ReadFramedJsonAsync: {ex.Message}");
                return null;
            }
        }

        // Single background client receiver that reads from the stream and enqueues messages
        private async Task ClientReceiveLoop(CancellationToken cancellationToken)
        {
            if (_clientStream == null) return;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var msg = await ReadFramedJsonAsync(_clientStream, cancellationToken);
                    if (msg == null)
                    {
                        await Task.Delay(10, cancellationToken);
                        continue;
                    }
                    var element = msg.Value;
                    // Handle StartGame immediately to set GameManager
                    if (element.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "StartGame")
                    {
                        if (element.TryGetProperty("seed", out var seedProp) && seedProp.TryGetInt32(out int seed))
                        {
                            _gameManager = new TetrisMultiplayer.Game.GameManager(seed);
                            LogDebugToFile($"[Client] StartGame received and processed successfully with seed: {seed}");
                            LogDebugToFile($"[Client] GameManager initialized: {_gameManager != null}");
                        }
                        else
                        {
                            LogDebugToFile("[Client] ERROR: StartGame message received but no valid seed found");
                        }
                        continue;
                    }
                    lock (_queueLock)
                    {
                        _clientMessageQueue.Enqueue(element);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LogDebugToFile($"Error in ClientReceiveLoop: {ex.Message}");
            }
        }

        // Client: Wait for PrepareNextPiece message
        public Task<bool> ReceivePrepareNextPieceAsync(CancellationToken cancellationToken)
        {
            lock (_queueLock)
            {
                var tempQueue = new Queue<JsonElement>();
                while (_clientMessageQueue.Count > 0)
                {
                    var queuedElement = _clientMessageQueue.Dequeue();
                    if (queuedElement.TryGetProperty("type", out var qTypeProp) && qTypeProp.GetString() == "PrepareNextPiece")
                    {
                        // Put back remaining messages
                        while (tempQueue.Count > 0)
                            _clientMessageQueue.Enqueue(tempQueue.Dequeue());
                        return Task.FromResult(true);
                    }
                    tempQueue.Enqueue(queuedElement);
                }
                // Put back all messages
                while (tempQueue.Count > 0)
                    _clientMessageQueue.Enqueue(tempQueue.Dequeue());
            }
            return Task.FromResult(false);
        }

        // Client: Wait for WaitForNextRound message  
        public Task<string?> ReceiveWaitForNextRoundAsync(CancellationToken cancellationToken)
        {
            lock (_queueLock)
            {
                var tempQueue = new Queue<JsonElement>();
                while (_clientMessageQueue.Count > 0)
                {
                    var queuedElement = _clientMessageQueue.Dequeue();
                    if (queuedElement.TryGetProperty("type", out var qTypeProp) && qTypeProp.GetString() == "WaitForNextRound")
                    {
                        // Put back remaining messages
                        while (tempQueue.Count > 0)
                            _clientMessageQueue.Enqueue(tempQueue.Dequeue());
                        
                        string message = "Waiting for next round...";
                        if (queuedElement.TryGetProperty("message", out var msgProp))
                        {
                            message = msgProp.GetString() ?? message;
                        }
                        return Task.FromResult<string?>(message);
                    }
                    tempQueue.Enqueue(queuedElement);
                }
                // Put back all messages
                while (tempQueue.Count > 0)
                    _clientMessageQueue.Enqueue(tempQueue.Dequeue());
            }
            return Task.FromResult<string?>(null);
        }

        // Host: Sende RoundReadyConfirmation an alle Clients
        public async Task BroadcastRoundReadyRequest(int round)
        {
            var msg = new { type = "RoundReadyRequest", round };
            await BroadcastAsync(msg);
        }

        // Client: Sende RoundReadyConfirmation zurück an Host
        public async Task SendRoundReadyConfirmation(int round)
        {
            var msg = new { type = "RoundReadyConfirmation", round };
            await SendToHostAsync(msg);
        }

        // Host: Empfange RoundReadyConfirmation von einem Client
        public Task<RoundReadyMsg?> ReceiveRoundReadyConfirmationAsync(CancellationToken cancellationToken, int timeoutMs = 1000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            
            while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
            {
                if (_placedPieceQueue.TryDequeue(out var placedMsg))
                {
                    // Wrong queue, put it back - this should not happen
                    _placedPieceQueue.Enqueue(placedMsg);
                    continue;
                }
                
                // Check in individual client streams
                foreach (var kvp in _playerClients)
                {
                    var playerId = kvp.Key;
                    var client = kvp.Value;
                    
                    try
                    {
                        if (!client.Connected) continue;
                        var stream = client.GetStream();
                        if (!stream.DataAvailable) continue;
                        
                        var msg = ReadFramedJsonAsync(stream, cancellationToken).Result;
                        if (msg == null) continue;
                        
                        var element = msg.Value;
                        if (element.TryGetProperty("type", out var typeProp) && 
                            typeProp.GetString() == "RoundReadyConfirmation" &&
                            element.TryGetProperty("round", out var roundProp) &&
                            roundProp.TryGetInt32(out int confirmedRound))
                        {
                            return Task.FromResult<RoundReadyMsg?>(new RoundReadyMsg 
                            { 
                                PlayerId = playerId,
                                Round = confirmedRound
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        LogDebugToFile($"Error reading round ready confirmation from {playerId}: {ex.Message}");
                    }
                }
                
                Thread.Sleep(10); // Kurze Pause
            }
            
            return Task.FromResult<RoundReadyMsg?>(null);
        }

        // Client: Empfange RoundReadyRequest vom Host
        public Task<int?> ReceiveRoundReadyRequestAsync(CancellationToken cancellationToken)
        {
            lock (_queueLock)
            {
                var tempQueue = new Queue<JsonElement>();
                while (_clientMessageQueue.Count > 0)
                {
                    var queuedElement = _clientMessageQueue.Dequeue();
                    if (queuedElement.TryGetProperty("type", out var qTypeProp) && qTypeProp.GetString() == "RoundReadyRequest")
                    {
                        // Put back remaining messages
                        while (tempQueue.Count > 0)
                            _clientMessageQueue.Enqueue(tempQueue.Dequeue());
                        
                        if (queuedElement.TryGetProperty("round", out var roundProp) && roundProp.TryGetInt32(out int round))
                        {
                            return Task.FromResult<int?>(round);
                        }
                    }
                    tempQueue.Enqueue(queuedElement);
                }
                // Put back all messages
                while (tempQueue.Count > 0)
                    _clientMessageQueue.Enqueue(tempQueue.Dequeue());
            }
            return Task.FromResult<int?>(null);
        }

        // Data structure for round ready confirmations
        public class RoundReadyMsg
        {
            public string PlayerId { get; set; } = "";
            public int Round { get; set; }
        }

        // Lobby-Discovery-Datenstrukturen
        public class LobbyInfo
        {
            public string HostName { get; set; } = "";
            public string IpAddress { get; set; } = "";
            public int Port { get; set; }
            public int PlayerCount { get; set; }
            public int MaxPlayers { get; set; } = 8;
            public DateTime LastSeen { get; set; }
        }

        // Host: Startet Lobby-Broadcasting für Discovery mit Multi-Interface-Support
        public async Task StartLobbyBroadcast(string hostName, int gamePort, CancellationToken cancellationToken = default)
        {
            _hostName = hostName;
            try
            {
                // Verbesserte Discovery-Server-Initialisierung für alle Interfaces
                _discoveryServer = new UdpClient();
                _discoveryServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _discoveryServer.Client.Bind(new IPEndPoint(IPAddress.Any, DISCOVERY_PORT));
                _discoveryServer.EnableBroadcast = true;
                
                _discoveryTask = Task.Run(() => LobbyBroadcastLoop(gamePort, cancellationToken), cancellationToken);
                LogDebugToFile($"Verbesserte Lobby-Broadcasting gestartet für Host '{hostName}' auf Port {DISCOVERY_PORT}");
                
                // Zeige alle verfügbaren Interfaces für Debugging
                var localIPs = GetAllLocalIPAddresses();
                LogDebugToFile($"Host lauscht auf folgenden Interfaces: {string.Join(", ", localIPs)}");
            }
            catch (Exception ex)
            {
                LogDebugToFile($"Fehler beim Starten des verbesserten Lobby-Broadcasts: {ex.Message}");
                throw;
            }
        }

        // Host: Verbesserter Broadcast-Loop mit Multi-Interface-Unterstützung
        private async Task LobbyBroadcastLoop(int gamePort, CancellationToken cancellationToken)
        {
            var broadcastEndpoints = GetBroadcastEndpoints();
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Lobby-Information erstellen
                    var lobbyInfo = new
                    {
                        type = "LobbyBroadcast",
                        hostName = _hostName ?? "Unbekannter Host",
                        port = gamePort,
                        playerCount = ConnectedPlayerIds.Count + 1, // +1 für Host
                        maxPlayers = 8,
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };

                    var json = JsonSerializer.Serialize(lobbyInfo);
                    var data = Encoding.UTF8.GetBytes(json);
                    
                    // Broadcast an alle verfügbaren Netzwerk-Segmente
                    foreach (var endpoint in broadcastEndpoints)
                    {
                        try
                        {
                            await _discoveryServer!.SendAsync(data, data.Length, endpoint);
                            LogDebugToFile($"Lobby-Broadcast gesendet an {endpoint}");
                        }
                        catch (Exception ex)
                        {
                            LogDebugToFile($"Fehler beim Broadcast an {endpoint}: {ex.Message}");
                        }
                    }
                    
                    // Warte auf Discovery-Requests mit Timeout
                    var requestsHandled = 0;
                    var requestStartTime = DateTime.UtcNow;
                    
                    while (_discoveryServer!.Available > 0 && 
                           (DateTime.UtcNow - requestStartTime).TotalMilliseconds < 1000 &&
                           requestsHandled < 10) // Limit pro Zyklus
                    {
                        var result = await _discoveryServer.ReceiveAsync();
                        await HandleDiscoveryRequest(result, gamePort);
                        requestsHandled++;
                    }
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    LogDebugToFile($"Fehler im verbesserten Lobby-Broadcast-Loop: {ex.Message}");
                }
                
                await Task.Delay(3000, cancellationToken); // Broadcast alle 3 Sekunden
            }
        }

        // Hilfsmethode: Ermittelt alle Broadcast-Endpunkte für verfügbare Netzwerke
        private List<IPEndPoint> GetBroadcastEndpoints()
        {
            var endpoints = new List<IPEndPoint>();
            
            try
            {
                // Standard-Broadcast hinzufügen
                endpoints.Add(new IPEndPoint(IPAddress.Broadcast, DISCOVERY_PORT));
                
                // Dirigierte Broadcasts für alle Netzwerk-Interfaces
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up && 
                                ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                foreach (var ni in interfaces)
                {
                    var ipProps = ni.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork && 
                            !IPAddress.IsLoopback(addr.Address) && 
                            addr.IPv4Mask != null)
                        {
                            // Berechne Broadcast-Adresse für dieses Netzwerk-Segment
                            var broadcastAddr = CalculateBroadcastAddress(addr.Address, addr.IPv4Mask);
                            if (broadcastAddr != null)
                            {
                                var endpoint = new IPEndPoint(broadcastAddr, DISCOVERY_PORT);
                                if (!endpoints.Any(ep => ep.Address.Equals(broadcastAddr)))
                                {
                                    endpoints.Add(endpoint);
                                    LogDebugToFile($"Broadcast-Endpunkt hinzugefügt: {endpoint} für Interface {ni.Name}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebugToFile($"Fehler beim Ermitteln der Broadcast-Endpunkte: {ex.Message}");
            }
            
            return endpoints;
        }

        // Hilfsmethode: Berechnet Broadcast-Adresse für ein Netzwerk-Segment
        private IPAddress? CalculateBroadcastAddress(IPAddress address, IPAddress subnetMask)
        {
            try
            {
                var addressBytes = address.GetAddressBytes();
                var maskBytes = subnetMask.GetAddressBytes();
                var broadcastBytes = new byte[4];

                for (int i = 0; i < 4; i++)
                {
                    broadcastBytes[i] = (byte)(addressBytes[i] | (~maskBytes[i] & 0xFF));
                }

                return new IPAddress(broadcastBytes);
            }
            catch (Exception ex)
            {
                LogDebugToFile($"Fehler beim Berechnen der Broadcast-Adresse: {ex.Message}");
                return null;
            }
        }

        // Host: Verbesserte Behandlung von Discovery-Requests
        private async Task HandleDiscoveryRequest(UdpReceiveResult result, int gamePort)
        {
            try
            {
                var json = Encoding.UTF8.GetString(result.Buffer);
                var request = JsonSerializer.Deserialize<JsonElement>(json);
                
                if (request.TryGetProperty("type", out var typeProp) && 
                    typeProp.GetString() == "DiscoveryRequest")
                {
                    // Ermittle beste IP-Adresse für Response basierend auf Client-Netzwerk
                    var clientIP = result.RemoteEndPoint.Address.ToString();
                    var bestHostIP = GetBestIPForClient(clientIP);
                    
                    LogDebugToFile($"Discovery-Request von {result.RemoteEndPoint}, verwende Host-IP: {bestHostIP}");
                    
                    // Antwort mit optimaler IP-Adresse senden
                    var response = new
                    {
                        type = "DiscoveryResponse",
                        hostName = _hostName ?? "Unbekannter Host",
                        ipAddress = bestHostIP,
                        port = gamePort,
                        playerCount = ConnectedPlayerIds.Count + 1,
                        maxPlayers = 8,
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        allHostIPs = GetAllLocalIPAddresses().Select(ip => ip.ToString()).ToArray()
                    };

                    var responseJson = JsonSerializer.Serialize(response);
                    var responseData = Encoding.UTF8.GetBytes(responseJson);
                    
                    await _discoveryServer!.SendAsync(responseData, responseData.Length, result.RemoteEndPoint);
                    LogDebugToFile($"Discovery-Response gesendet an {result.RemoteEndPoint} mit IP {bestHostIP}");
                }
            }
            catch (Exception ex)
            {
                LogDebugToFile($"Fehler beim Behandeln von Discovery-Request: {ex.Message}");
            }
        }

        // Hilfsmethode: Ermittelt beste Host-IP-Adresse für einen bestimmten Client
        private string GetBestIPForClient(string clientIP)
        {
            try
            {
                var hostIPs = GetAllLocalIPAddresses();
                var clientAddr = IPAddress.Parse(clientIP);
                
                // Versuche, IP im gleichen Netzwerk-Segment zu finden
                foreach (var hostIP in hostIPs)
                {
                    if (AreInSameSubnet(hostIP, clientAddr))
                    {
                        LogDebugToFile($"Gleiche Subnet gefunden: Host {hostIP} und Client {clientIP}");
                        return hostIP.ToString();
                    }
                }
                
                // Fallback: Beste verfügbare lokale Adresse
                var bestIP = GetLocalIPAddress();
                LogDebugToFile($"Fallback IP gewählt: {bestIP}");
                return bestIP;
            }
            catch (Exception ex)
            {
                LogDebugToFile($"Fehler beim Ermitteln der besten IP für Client {clientIP}: {ex.Message}");
                return GetLocalIPAddress();
            }
        }

        // Hilfsmethode: Prüft, ob zwei IP-Adressen im gleichen Subnet sind
        private bool AreInSameSubnet(IPAddress ip1, IPAddress ip2)
        {
            try
            {
                // Vereinfachte Subnet-Prüfung für common cases
                var bytes1 = ip1.GetAddressBytes();
                var bytes2 = ip2.GetAddressBytes();
                
                // Prüfe /24 Netzwerk (erste 3 Oktetts)
                if (bytes1[0] == bytes2[0] && bytes1[1] == bytes2[1] && bytes1[2] == bytes2[2])
                {
                    return true;
                }
                
                // Prüfe /16 Netzwerk für private Bereiche
                if ((bytes1[0] == 192 && bytes1[1] == 168) && 
                    (bytes2[0] == 192 && bytes2[1] == 168))
                {
                    return bytes1[2] == bytes2[2]; // Gleiche /24
                }
                
                // Prüfe /8 für 10.x.x.x
                if (bytes1[0] == 10 && bytes2[0] == 10)
                {
                    return bytes1[1] == bytes2[1]; // Gleiche /16
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        // Client: Verbesserte Suche nach verfügbaren Lobbys mit Multi-Interface-Support
        public async Task<List<LobbyInfo>> DiscoverLobbies(int timeoutMs = 5000, CancellationToken cancellationToken = default)
        {
            var lobbies = new List<LobbyInfo>();
            var seenHosts = new HashSet<string>();
            
            LogDebugToFile("=== Starte verbesserte Lobby-Discovery ===");
            
            try
            {
                // Methode 1: Aktive Discovery-Requests an alle Netzwerk-Segmente
                var activeLobbies = await PerformActiveDiscovery(timeoutMs / 2, cancellationToken);
                foreach (var lobby in activeLobbies)
                {
                    var hostKey = $"{lobby.IpAddress}:{lobby.Port}";
                    if (!seenHosts.Contains(hostKey))
                    {
                        lobbies.Add(lobby);
                        seenHosts.Add(hostKey);
                    }
                }
                
                LogDebugToFile($"Aktive Discovery: {activeLobbies.Count} Lobby(s) gefunden");
                
                // Methode 2: Passive Listening auf Broadcasts
                var passiveLobbies = await PerformPassiveListening(timeoutMs / 2, cancellationToken);
                foreach (var lobby in passiveLobbies)
                {
                    var hostKey = $"{lobby.IpAddress}:{lobby.Port}";
                    if (!seenHosts.Contains(hostKey))
                    {
                        lobbies.Add(lobby);
                        seenHosts.Add(hostKey);
                    }
                }
                
                LogDebugToFile($"Passive Discovery: {passiveLobbies.Count} zusätzliche Lobby(s) gefunden");
                
                LogDebugToFile($"=== Discovery abgeschlossen. Gesamt: {lobbies.Count} Lobby(s) gefunden ===");
            }
            catch (Exception ex)
            {
                LogDebugToFile($"Fehler bei verbesserter Lobby-Discovery: {ex.Message}");
            }
            
            return lobbies;
        }

        // Aktive Discovery: Sendet Requests an alle Netzwerk-Segmente
        private async Task<List<LobbyInfo>> PerformActiveDiscovery(int timeoutMs, CancellationToken cancellationToken)
        {
            var lobbies = new List<LobbyInfo>();
            
            try
            {
                using var client = new UdpClient();
                client.EnableBroadcast = true;
                client.Client.ReceiveTimeout = Math.Max(timeoutMs, 2000);
                
                // Discovery-Request erstellen
                var request = new
                {
                    type = "DiscoveryRequest",
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    clientInfo = GetLocalIPAddress()
                };
                
                var requestJson = JsonSerializer.Serialize(request);
                var requestData = Encoding.UTF8.GetBytes(requestJson);
                
                // Alle möglichen Broadcast-Ziele
                var broadcastTargets = GetBroadcastTargets();
                
                LogDebugToFile($"Sende Discovery-Requests an {broadcastTargets.Count} Broadcast-Ziele");
                
                // Requests an alle Ziele senden
                foreach (var target in broadcastTargets)
                {
                    try
                    {
                        await client.SendAsync(requestData, requestData.Length, target);
                        LogDebugToFile($"Discovery-Request gesendet an {target}");
                    }
                    catch (Exception ex)
                    {
                        LogDebugToFile($"Fehler beim Senden an {target}: {ex.Message}");
                    }
                }
                
                // Auf Antworten warten
                var endTime = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                var seenHosts = new HashSet<string>();
                
                while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var result = await client.ReceiveAsync();
                        var json = Encoding.UTF8.GetString(result.Buffer);
                        var response = JsonSerializer.Deserialize<JsonElement>(json);
                        
                        if (response.TryGetProperty("type", out var typeProp) && 
                            typeProp.GetString() == "DiscoveryResponse")
                        {
                            var lobby = ParseLobbyResponse(response, result.RemoteEndPoint.Address.ToString());
                            if (lobby != null)
                            {
                                var hostKey = $"{lobby.IpAddress}:{lobby.Port}";
                                if (!seenHosts.Contains(hostKey))
                                {
                                    lobbies.Add(lobby);
                                    seenHosts.Add(hostKey);
                                    LogDebugToFile($"Discovery-Response: {lobby.HostName} ({lobby.IpAddress}:{lobby.Port})");
                                }
                            }
                        }
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                    {
                        // Timeout ist normal
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogDebugToFile($"Fehler beim Empfangen von Discovery-Response: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebugToFile($"Fehler bei aktiver Discovery: {ex.Message}");
            }
            
            return lobbies;
        }

        // Passive Discovery: Lauscht auf Lobby-Broadcasts  
        private async Task<List<LobbyInfo>> PerformPassiveListening(int timeoutMs, CancellationToken cancellationToken)
        {
            var lobbies = new List<LobbyInfo>();
            
            try
            {
                using var client = new UdpClient();
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                client.Client.Bind(new IPEndPoint(IPAddress.Any, DISCOVERY_PORT));
                client.Client.ReceiveTimeout = Math.Max(timeoutMs, 2000);
                
                LogDebugToFile($"Passive Listening gestartet für {timeoutMs}ms");
                
                var endTime = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                var seenHosts = new HashSet<string>();
                
                while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var result = await client.ReceiveAsync();
                        var json = Encoding.UTF8.GetString(result.Buffer);
                        var broadcast = JsonSerializer.Deserialize<JsonElement>(json);
                        
                        if (broadcast.TryGetProperty("type", out var typeProp) && 
                            typeProp.GetString() == "LobbyBroadcast")
                        {
                            var lobby = ParseLobbyBroadcast(broadcast, result.RemoteEndPoint.Address.ToString());
                            if (lobby != null)
                            {
                                var hostKey = $"{lobby.IpAddress}:{lobby.Port}";
                                if (!seenHosts.Contains(hostKey))
                                {
                                    lobbies.Add(lobby);
                                    seenHosts.Add(hostKey);
                                    LogDebugToFile($"Lobby-Broadcast empfangen: {lobby.HostName} ({lobby.IpAddress}:{lobby.Port})");
                                }
                            }
                        }
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                    {
                        // Timeout ist normal
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogDebugToFile($"Fehler beim Empfangen von Lobby-Broadcast: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebugToFile($"Fehler bei passivem Listening: {ex.Message}");
            }
            
            return lobbies;
        }

        // Ermittelt alle möglichen Broadcast-Ziele für Discovery
        private List<IPEndPoint> GetBroadcastTargets()
        {
            var targets = new List<IPEndPoint>();
            
            try
            {
                // Standard-Broadcast
                targets.Add(new IPEndPoint(IPAddress.Broadcast, DISCOVERY_PORT));
                
                // Alle Netzwerk-Interface-spezifischen Broadcasts
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up && 
                                ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                foreach (var ni in interfaces)
                {
                    var ipProps = ni.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork && 
                            !IPAddress.IsLoopback(addr.Address) && 
                            addr.IPv4Mask != null)
                        {
                            var broadcastAddr = CalculateBroadcastAddress(addr.Address, addr.IPv4Mask);
                            if (broadcastAddr != null)
                            {
                                var target = new IPEndPoint(broadcastAddr, DISCOVERY_PORT);
                                if (!targets.Any(t => t.Address.Equals(broadcastAddr)))
                                {
                                    targets.Add(target);
                                }
                            }
                        }
                    }
                }
                
                // Zusätzliche bekannte lokale Netzwerk-Bereiche als Fallback
                var commonNetworks = new[]
                {
                    "192.168.1.255", "192.168.0.255", "192.168.178.255",
                    "10.0.0.255", "10.0.1.255", "172.16.0.255"
                };
                
                foreach (var networkBroadcast in commonNetworks)
                {
                    try
                    {
                        var addr = IPAddress.Parse(networkBroadcast);
                        var target = new IPEndPoint(addr, DISCOVERY_PORT);
                        if (!targets.Any(t => t.Address.Equals(addr)))
                        {
                            targets.Add(target);
                        }
                    }
                    catch { }
                }
                
                LogDebugToFile($"Broadcast-Ziele ermittelt: {string.Join(", ", targets)}");
            }
            catch (Exception ex)
            {
                LogDebugToFile($"Fehler beim Ermitteln der Broadcast-Ziele: {ex.Message}");
            }
            
            return targets;
        }

        // Verbesserte Hilfsmethode: Parse Discovery-Response
        private LobbyInfo? ParseLobbyResponse(JsonElement response, string senderIp)
        {
            try
            {
                var hostName = response.GetProperty("hostName").GetString() ?? "Unbekannt";
                
                // Verwende explizit angegebene IP-Adresse oder Sender-IP als Fallback
                string ipAddress = senderIp;
                if (response.TryGetProperty("ipAddress", out var ipProp))
                {
                    var explicitIP = ipProp.GetString();
                    if (!string.IsNullOrEmpty(explicitIP))
                    {
                        ipAddress = explicitIP;
                    }
                }
                
                var port = response.GetProperty("port").GetInt32();
                var playerCount = response.GetProperty("playerCount").GetInt32();
                var maxPlayers = response.TryGetProperty("maxPlayers", out var maxProp) && maxProp.TryGetInt32(out int max) ? max : 8;
                
                // Zusätzliche Informationen für Debugging loggen
                if (response.TryGetProperty("allHostIPs", out var allIPsProp) && allIPsProp.ValueKind == JsonValueKind.Array)
                {
                    var allIPs = allIPsProp.EnumerateArray().Select(ip => ip.GetString()).Where(ip => !string.IsNullOrEmpty(ip));
                    LogDebugToFile($"Host {hostName} verfügbare IPs: {string.Join(", ", allIPs)}");
                }
                
                var lobby = new LobbyInfo
                {
                    HostName = hostName,
                    IpAddress = ipAddress,
                    Port = port,
                    PlayerCount = playerCount,
                    MaxPlayers = maxPlayers,
                    LastSeen = DateTime.UtcNow
                };
                
                LogDebugToFile($"Lobby geparst: {lobby.HostName} ({lobby.IpAddress}:{lobby.Port}) - {lobby.PlayerCount}/{lobby.MaxPlayers} Spieler");
                return lobby;
            }
            catch (Exception ex)
            {
                LogDebugToFile($"Fehler beim Parsen der Discovery-Response: {ex.Message}");
                return null;
            }
        }

        // Hilfsmethode: Parse Lobby-Broadcast
        private LobbyInfo? ParseLobbyBroadcast(JsonElement broadcast, string senderIp)
        {
            try
            {
                return new LobbyInfo
                {
                    HostName = broadcast.GetProperty("hostName").GetString() ?? "Unbekannt",
                    IpAddress = senderIp,
                    Port = broadcast.GetProperty("port").GetInt32(),
                    PlayerCount = broadcast.GetProperty("playerCount").GetInt32(),
                    MaxPlayers = broadcast.TryGetProperty("maxPlayers", out var maxProp) && maxProp.TryGetInt32(out int max) ? max : 8,
                    LastSeen = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                LogDebugToFile($"Fehler beim Parsen des Lobby-Broadcasts: {ex.Message}");
                return null;
            }
        }

        // Verbesserte Methode: Alle lokalen IP-Adressen ermitteln  
        private List<IPAddress> GetAllLocalIPAddresses()
        {
            var addresses = new List<IPAddress>();
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up && 
                                ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                foreach (var ni in interfaces)
                {
                    var ipProps = ni.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork && 
                            !IPAddress.IsLoopback(addr.Address))
                        {
                            addresses.Add(addr.Address);
                        }
                    }
                }
                
                LogDebugToFile($"Gefundene lokale IP-Adressen: {string.Join(", ", addresses)}");
            }
            catch (Exception ex)
            {
                LogDebugToFile($"Fehler beim Ermitteln der IP-Adressen: {ex.Message}");
            }
            
            return addresses;
        }

        // Hilfsmethode: Beste lokale IP-Adresse für Discovery ermitteln
        private string GetLocalIPAddress()
        {
            try
            {
                var addresses = GetAllLocalIPAddresses();
                
                // Priorisierung: Lokale Netzwerke vor VPN-Adressen
                var localNetworkAddress = addresses.FirstOrDefault(ip => 
                    ip.ToString().StartsWith("192.168.") || 
                    ip.ToString().StartsWith("10.") || 
                    (ip.ToString().StartsWith("172.") && IsBetween(ip.GetAddressBytes()[1], 16, 31)));
                
                if (localNetworkAddress != null)
                {
                    LogDebugToFile($"Beste lokale IP-Adresse gewählt: {localNetworkAddress}");
                    return localNetworkAddress.ToString();
                }
                
                // Fallback: erste verfügbare Adresse
                var fallback = addresses.FirstOrDefault()?.ToString() ?? "127.0.0.1";
                LogDebugToFile($"Fallback IP-Adresse gewählt: {fallback}");
                return fallback;
            }
            catch (Exception ex)
            {
                LogDebugToFile($"Fehler beim Ermitteln der IP-Adresse: {ex.Message}");
                return "127.0.0.1";
            }
        }

        private static bool IsBetween(byte value, int min, int max)
        {
            return value >= min && value <= max;
        }

        // Diagnose-Methode für Netzwerk-Probleme
        public async Task<string> DiagnoseNetworkConnectivity()
        {
            var report = new StringBuilder();
            report.AppendLine("=== NETZWERK-DIAGNOSE ===");
            
            try
            {
                // 1. Lokale IP-Adressen
                var localIPs = GetAllLocalIPAddresses();
                report.AppendLine($"Lokale IP-Adressen ({localIPs.Count}):");
                foreach (var ip in localIPs)
                {
                    report.AppendLine($"  - {ip}");
                }
                
                // 2. Netzwerk-Interfaces
                report.AppendLine("\nNetzwerk-Interfaces:");
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up);
                    
                foreach (var ni in interfaces)
                {
                    report.AppendLine($"  - {ni.Name} ({ni.NetworkInterfaceType}): {ni.OperationalStatus}");
                    var ipProps = ni.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            report.AppendLine($"    IP: {addr.Address}, Mask: {addr.IPv4Mask}");
                        }
                    }
                }
                
                // 3. Broadcast-Ziele
                var broadcastTargets = GetBroadcastTargets();
                report.AppendLine($"\nBroadcast-Ziele ({broadcastTargets.Count}):");
                foreach (var target in broadcastTargets)
                {
                    report.AppendLine($"  - {target}");
                }
                
                // 4. Port-Test
                report.AppendLine($"\nPort-Test für {DISCOVERY_PORT}:");
                try
                {
                    using var testClient = new UdpClient(DISCOVERY_PORT);
                    report.AppendLine($"  ✓ Port {DISCOVERY_PORT} erfolgreich gebunden");
                    testClient.Close();
                }
                catch (Exception ex)
                {
                    report.AppendLine($"  ✗ Port {DISCOVERY_PORT} Fehler: {ex.Message}");
                }
                
                // 5. Broadcast-Test
                report.AppendLine("\nBroadcast-Test:");
                try
                {
                    using var testClient = new UdpClient();
                    testClient.EnableBroadcast = true;
                    
                    var testData = Encoding.UTF8.GetBytes("DIAGNOSE-TEST");
                    await testClient.SendAsync(testData, testData.Length, new IPEndPoint(IPAddress.Broadcast, DISCOVERY_PORT + 1));
                    report.AppendLine("  ✓ Broadcast erfolgreich gesendet");
                }
                catch (Exception ex)
                {
                    report.AppendLine($"  ✗ Broadcast-Fehler: {ex.Message}");
                }
                
            }
            catch (Exception ex)
            {
                report.AppendLine($"Diagnose-Fehler: {ex.Message}");
            }
            
            report.AppendLine("=== DIAGNOSE ENDE ===");
            var result = report.ToString();
            LogDebugToFile(result);
            return result;
        }

        // Cleanup-Methoden für Discovery-Ressourcen
        public void StopLobbyBroadcast()
        {
            try
            {
                _discoveryTask?.Wait(1000);
                _discoveryServer?.Close();
                _discoveryServer?.Dispose();
                LogDebugToFile("Lobby-Broadcasting gestoppt");
            }
            catch (Exception ex)
            {
                LogDebugToFile($"Fehler beim Stoppen des Lobby-Broadcasts: {ex.Message}");
            }
        }
    }
}
