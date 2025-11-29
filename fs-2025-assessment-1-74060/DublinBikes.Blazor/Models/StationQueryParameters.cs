namespace DublinBikes.Blazor.Models
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
    }
}
