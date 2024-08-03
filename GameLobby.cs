using System;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace cit_y_socket
{
    public class GameLobby : WebSocketBehavior
    {

        private static MySqlConnection connection;
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
            try
            {
                Console.WriteLine("Received message from user: " + e.Data);

                var message = JObject.Parse(e.Data);
                var action = message["action"]?.ToString();

                switch (action)
                {
                    case "getBuildingData":
                        if (message["building_id"] != null)
                        {
                            int buildingId = (int)message["building_id"];
                            int userId = (int)message["user_id"];
                            string response = BuildingManager.SendBuildingData(buildingId, userId);
                            Send(response);
                        }
                        else
                        {
                            Console.WriteLine("Missing building ID.");
                            Send("{\"error\":\"Missing building ID.\"}");
                        }
                        break;
                    case "createApartment":
                        if (message["building_id"] != null && message["user_id"] != null)
                        {
                            int buildingId = (int)message["building_id"];
                            int userId = (int)message["user_id"];
                            string response = BuildingManager.CreateApartment(buildingId, userId);
                            Send(response);
                        }
                        break;
                    case "getApartmentData":
                        if (message["user_id"] != null)
                        {
                            int userId = (int)message["user_id"];
                            string response = BuildingManager.GetUserApartmentData(userId);
                            Send(response);
                        }
                        break;
                    default:
                        Console.WriteLine("Invalid action.");
                        Send("{\"error\":\"Invalid action.\"}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while processing message: " + ex.Message);
                Send("{\"error\":\"An error occurred on the server\"}");
            }
        }

        protected override void OnClose(CloseEventArgs e)
        {
            Console.WriteLine($"Connection closed: {e.Reason}");
            base.OnClose(e);
        }

        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {
            Console.Error.WriteLine(e);
        }

        // Methods to validate token and authenticate user
        private bool IsTokenValid(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            return ValidateToken(token);
        }

        private bool ValidateToken(string token)
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
}
