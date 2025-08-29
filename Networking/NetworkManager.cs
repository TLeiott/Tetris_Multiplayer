using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
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
            await Task.CompletedTask; // sofort zur�ckkehren, damit Lobby angezeigt wird
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
    }
}
