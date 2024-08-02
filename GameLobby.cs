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
                            SendBuildingData(buildingId, userId);
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
                            CreateApartment(buildingId, userId);
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

        private string GetBuildingDataById(int buildingId, int userId)
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                // Query to get building details
                string buildingQuery = "SELECT id, building_name, max_occupants FROM buildings WHERE id = @id";
                using (var command = new MySqlCommand(buildingQuery, connection))
                {
                    command.Parameters.AddWithValue("@id", buildingId);
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            var building = new JObject
                            {
                                ["id"] = reader.GetInt32("id"),
                                ["building_name"] = reader.GetString("building_name"),
                                ["max_occupants"] = reader.GetInt32("max_occupants")
                            };

                            reader.Close(); // Close the reader before executing another command

                            // Query to count apartments for this building
                            string apartmentQuery = "SELECT COUNT(*) FROM apartments WHERE building_id = @buildingId";
                            using (var apartmentCommand = new MySqlCommand(apartmentQuery, connection))
                            {
                                apartmentCommand.Parameters.AddWithValue("@buildingId", buildingId);
                                int apartmentCount = Convert.ToInt32(apartmentCommand.ExecuteScalar());
                                building["current_occupants"] = apartmentCount; // Add apartment count to the JSON object
                            }

                            // Query to find the first apartment with the specified user_id
                            string userApartmentQuery = "SELECT id, building_id FROM apartments WHERE user_id = @userId LIMIT 1";
                            using (var userApartmentCommand = new MySqlCommand(userApartmentQuery, connection))
                            {
                                userApartmentCommand.Parameters.AddWithValue("@userId", userId);
                                using (var apartmentReader = userApartmentCommand.ExecuteReader())
                                {
                                    if (apartmentReader.Read())
                                    {
                                        building["user_apartment_id"] = apartmentReader.GetInt32("id"); // Add user's apartment ID to the JSON object
                                        building["user_building_id"] = apartmentReader.GetInt32("building_id"); // Add user's building ID to the JSON object
                                    }
                                    else
                                    {
                                        building["user_apartment_id"] = null; // No apartment found for the user
                                        building["user_building_id"] = null; // No building found for the user's apartment
                                    }
                                }
                            }

                            return building.ToString();
                        }
                    }
                }
            }
            return "{}"; // Return an empty JSON object if no building is found
        }

        private void SendBuildingData(int buildingId, int userId)
        {
            string buildingData = GetBuildingDataById(buildingId, userId);

            var response = new JObject
            {
                ["type"] = "building_data",
                ["data"] = JObject.Parse(buildingData)
            };
            Send(response.ToString());
        }

        private string CreateApartment(int buildingId, int userId)
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                string insertQuery = "INSERT INTO apartments (building_id, user_id, apartment_type) VALUES (@buildingId, @userId, @apartmentType)";
                using (var command = new MySqlCommand(insertQuery, connection))
                {
                    command.Parameters.AddWithValue("@buildingId", buildingId);
                    command.Parameters.AddWithValue("@userId", userId);
                    command.Parameters.AddWithValue("@apartmentType", "box");

                    try
                    {
                        int rowsAffected = command.ExecuteNonQuery();
                        if (rowsAffected > 0)
                        {
                            return new JObject { ["type"] = "createApartment", ["status"] = "success" }.ToString();
                        }
                        else
                        {
                            return new JObject { ["type"] = "createApartment", ["status"] = "failure", ["error"] = "No rows affected." }.ToString();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error creating apartment: " + ex.Message);
                        return new JObject { ["type"] = "createApartment", ["status"] = "failure", ["error"] = ex.Message }.ToString();
                    }
                }
            }
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
