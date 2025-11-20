using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace DublinBikes.Api.Models
{
    public class Station
    {
        [JsonProperty("id")]              // Usado pelo Cosmos / Newtonsoft
        [JsonPropertyName("id")]          // Usado pelo System.Text.Json
        public string Id { get; set; } = string.Empty;

        [JsonProperty("number")]
        [JsonPropertyName("number")]
        public int Number { get; set; }

        [JsonProperty("name")]
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("address")]
        [JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;

        [JsonProperty("position")]
        [JsonPropertyName("position")]
        public GeoPosition Position { get; set; } = new GeoPosition();

        [JsonProperty("bike_stands")]
        [JsonPropertyName("bike_stands")]
        public int BikeStands { get; set; }

        [JsonProperty("available_bikes")]
        [JsonPropertyName("available_bikes")]
        public int AvailableBikes { get; set; }

        [JsonProperty("available_bike_stands")]
        [JsonPropertyName("available_bike_stands")]
        public int AvailableBikeStands { get; set; }

        [JsonProperty("status")]
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty("last_update")]
        [JsonPropertyName("last_update")]
        public long LastUpdateEpochMs { get; set; } 

        public DateTimeOffset LastUpdateUtc =>
            DateTimeOffset.FromUnixTimeMilliseconds(LastUpdateEpochMs);

        public DateTimeOffset LastUpdateLocal =>
            TimeZoneInfo.ConvertTime(
                LastUpdateUtc,
                TimeZoneInfo.FindSystemTimeZoneById("Europe/Dublin")
            );

        public double Occupancy =>
            BikeStands == 0 ? 0 : (double)AvailableBikes / BikeStands;
    }
}
