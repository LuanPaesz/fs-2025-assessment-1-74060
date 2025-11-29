namespace DublinBikes.Api.Dtos
{
    public class StationQueryParameters
    {
        public string? Status { get; set; }
        public int? MinBikes { get; set; }
        public string? SearchTerm { get; set; }

        public string Sort { get; set; } = "name";
        public string Dir { get; set; } = "asc";

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public void Normalize()
        {
            // //sort can only be name, availableBikes, or occupancy
            var allowedSort = new[] { "name", "availablebikes", "occupancy" };
            if (!allowedSort.Contains(Sort.ToLowerInvariant()))
            {
                Sort = "name";
            }

            // dir can only be asc or desc
            var allowedDir = new[] { "asc", "desc" };
            if (!allowedDir.Contains(Dir.ToLowerInvariant()))
            {
                Dir = "asc";
            }

            if (Page <= 0) Page = 1;
            if (PageSize <= 0) PageSize = 20;
        }
    }
}