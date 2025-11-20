using System.Text.Json.Serialization;

namespace DublinBikes.Api.Models
{
    public class GeoPosition
    {
        [JsonPropertyName("lat")]
        public double Latitude { get; set; }

        [JsonPropertyName("lng")]
        public double Longitude { get; set; }
    }
}
