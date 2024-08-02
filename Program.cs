using System;
using WebSocketSharp.Server;
using MySql.Data.MySqlClient;

namespace cit_y_socket
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // Initialize the WebSocket server on the specified URL and port.
                WebSocketServer cws = new WebSocketServer("ws://localhost:8181");

                // Add the GameLobby WebSocket service.
                cws.AddWebSocketService<GameLobby>("/Lobby");

                // Start the WebSocket server.
                cws.Start();
                Console.WriteLine("Server started on: ws://localhost:8181");

                // Wait for user input to stop the server.
                Console.WriteLine("Press any key to stop the server...");
                Console.ReadKey(true);

                // Stop the WebSocket server gracefully.
                cws.Stop();
                Console.WriteLine("Server stopped.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
            }
        }
    }
}
