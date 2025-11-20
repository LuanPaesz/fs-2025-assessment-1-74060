using DublinBikes.Api.Models;

namespace DublinBikes.Api.Dtos
{
    public class StationDto
    {
        public int Number { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int BikeStands { get; set; }
        public int AvailableBikes { get; set; }
        public int AvailableBikeStands { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset LastUpdateLocal { get; set; }
        public double Occupancy { get; set; }

        public static StationDto FromModel(Station s) => new StationDto
        {
            Number = s.Number,
            Name = s.Name,
            Address = s.Address,
            Latitude = s.Position.Latitude,
            Longitude = s.Position.Longitude,
            BikeStands = s.BikeStands,
            AvailableBikes = s.AvailableBikes,
            AvailableBikeStands = s.AvailableBikeStands,
            Status = s.Status,
            LastUpdateLocal = s.LastUpdateLocal,
            Occupancy = s.Occupancy
        };
    }
}
