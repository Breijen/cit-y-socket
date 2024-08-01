using WebSocketSharp;
using WebSocketSharp.Server;

namespace cit_y_server
{
    public class Echo : WebSocketBehavior
    {
        protected override void OnOpen()
        {
            Console.WriteLine("Connection established with client.");
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            Console.WriteLine("Received message from client: " + e.Data);
            Send(e.Data);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            WebSocketServer cws = new WebSocketServer("ws://localhost:8181");

            cws.AddWebSocketService<Echo>("/Echo");

            cws.Start();
            Console.WriteLine("Server started on: ws://localhost:8181");

            // Keep the server running until a key is pressed
            Console.WriteLine("Press any key to stop the server...");
            Console.ReadKey(true);

            // Stop the server
            cws.Stop();
            Console.WriteLine("Server stopped.");
        }
    }
}
