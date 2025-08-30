using System;
using System.Threading.Tasks;
using TetrisMultiplayer.Networking;

namespace TetrisMultiplayer.Tests
{
    public class NetworkDiagnosticDemo
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== Network Diagnostic Demo ===");
            
            var network = new NetworkManager();
            
            try
            {
                var info = await network.GetSimpleNetworkInfo();
                Console.WriteLine(info);
                
                Console.WriteLine("\n=== Testing Discovery ===");
                var lobbies = await network.DiscoverLobbies(3000);
                Console.WriteLine($"Found {lobbies.Count} lobbies:");
                
                foreach (var lobby in lobbies)
                {
                    Console.WriteLine($"- {lobby.HostName} ({lobby.IpAddress}:{lobby.Port}) - {lobby.PlayerCount}/{lobby.MaxPlayers} players");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            
            Console.WriteLine("\nDemo completed.");
        }
    }
}