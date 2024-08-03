using System;
using Newtonsoft.Json.Linq;
using MySql.Data.MySqlClient;
using WebSocketSharp.Server;

namespace cit_y_socket
{
    public class BuildingManager
    {
        public static string SendBuildingData(int buildingId, int userId)
        {
            string buildingData = GetBuildingDataById(buildingId, userId);

            var response = new JObject
            {
                ["type"] = "building_data",
                ["data"] = JObject.Parse(buildingData)
            };

            return response.ToString();
        }

        private static string GetBuildingDataById(int buildingId, int userId)
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

        public static string GetUserApartmentData(int userId)
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                string userApartmentQuery = @"
                    SELECT id AS apartment_id, apartment_type, building_id
                    FROM apartments
                    WHERE user_id = @userId
                    LIMIT 1";

                using (var command = new MySqlCommand(userApartmentQuery, connection))
                {
                    command.Parameters.AddWithValue("@userId", userId);
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            // Construct JSON object with apartment details
                            var apartmentData = new JObject
                            {
                                ["apartment_id"] = reader.GetInt32("apartment_id"),
                                ["apartment_type"] = reader.GetString("apartment_type"),
                                ["building_id"] = reader.GetInt32("building_id")
                            };

                            return new JObject
                            {
                                ["type"] = "user_apartment_data",
                                ["status"] = "success",
                                ["data"] = apartmentData
                            }.ToString();
                        }
                    }
                }
            }
            // Return a JSON object indicating failure if no apartment is found
            return new JObject
            {
                ["type"] = "user_apartment_data",
                ["status"] = "failure",
                ["error"] = "No apartment found for the user."
            }.ToString();
        }
        public static string CreateApartment(int buildingId, int userId)
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                string insertQuery = "INSERT INTO apartments (building_id, user_id, apartment_type) VALUES (@buildingId, @userId, @apartmentType)";
                using (var command = new MySqlCommand(insertQuery, connection))
                {
                    command.Parameters.AddWithValue("@buildingId", buildingId);
                    command.Parameters.AddWithValue("@userId", userId);
                    command.Parameters.AddWithValue("@apartmentType", "railroad");

                    try
                    {
                        int rowsAffected = command.ExecuteNonQuery();
                        var response = new JObject();
                        if (rowsAffected > 0)
                        {
                            response = new JObject { ["type"] = "createApartment", ["status"] = "success" };
                        }
                        else
                        {
                            response = new JObject { ["type"] = "createApartment", ["status"] = "failure", ["error"] = "No rows affected." };
                        }
                        return response.ToString();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error creating apartment: " + ex.Message);
                        var errorResponse = new JObject { ["type"] = "createApartment", ["status"] = "failure", ["error"] = ex.Message };
                        return errorResponse.ToString();
                    }
                }
            }
        }
    }
}
