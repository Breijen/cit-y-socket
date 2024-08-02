using System;

using WebSocketSharp;
using WebSocketSharp.Server;

using Newtonsoft.Json.Linq;

namespace cit_y_server
{
    public class Echo : WebSocketBehavior
    {
        protected override void OnOpen()
        {
            Console.WriteLine("Connection established with client.");

            // Retrieve and validate the token from the query string (or headers, if modified).
            string token = Context.QueryString["token"];
            if (!IsTokenValid(token))
            {
                Console.WriteLine("Invalid token. Closing connection.");
                Context.WebSocket.Close(CloseStatusCode.PolicyViolation, "Invalid token");
                return;
            }

            Console.WriteLine("Token is valid. Connection accepted.");
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            Console.WriteLine("Received message from client: " + e.Data);
            Send(e.Data);
        }

        // Method to validate the token.
        private bool IsTokenValid(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            return ValidateTokenWithLaravel(token);
        }

        private bool ValidateTokenWithLaravel(string token)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri("http://localhost:8000/"); // Replace with your Laravel API URL
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                try
                {
                    HttpResponseMessage response = client.GetAsync("api/authenticate-token").Result;
                    Console.WriteLine(response);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseBody = response.Content.ReadAsStringAsync().Result;
                        var jsonResponse = JObject.Parse(responseBody);

                        return jsonResponse["valid"].Value<bool>();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error validating token: " + ex.Message);
                }
            }

            return false;
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
