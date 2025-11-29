using DublinBikes.Api.Dtos;
using DublinBikes.Api.Models;
using DublinBikes.Api.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Cache in memory
builder.Services.AddMemoryCache();

// Registers the file-based service (V1)
builder.Services.AddSingleton<FileStationService>();
builder.Services.AddSingleton<IStationService>(sp =>
    sp.GetRequiredService<FileStationService>());

// Background service for random updates
builder.Services.AddHostedService<StationsBackgroundUpdater>();

// Read the CosmosDb section from appsettings.json
builder.Services.Configure<CosmosOptions>(
    builder.Configuration.GetSection("CosmosDb"));

// Registers the V2 service based on CosmosDb

builder.Services.AddSingleton<CosmosStationService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();


// ----------------- V1 - JSON / FILE -----------------

// GET /api/v1/stations
app.MapGet("/api/v1/stations", async (
    [FromServices] IStationService service,
    [FromQuery] string? status,
    [FromQuery] int? minBikes,
    [FromQuery(Name = "q")] string? searchTerm,
    [FromQuery] string? sort,
    [FromQuery] string? dir,
    [FromQuery] int? page,
    [FromQuery] int? pageSize) =>
{
    var parameters = new StationQueryParameters
    {
        Status = status,
        MinBikes = minBikes,
        SearchTerm = searchTerm,
        Sort = string.IsNullOrWhiteSpace(sort) ? "name" : sort!,
        Dir = string.IsNullOrWhiteSpace(dir) ? "asc" : dir!,
        Page = page ?? 1,
        PageSize = pageSize ?? 20
    };

    // ensures valid values (sort, dir, page, pageSize)
    parameters.Normalize();

    var stations = await service.GetStationsAsync(parameters);
    var dtos = stations.Select(StationDto.FromModel);
    return Results.Ok(dtos);
})
.WithName("GetStationsV1")
.Produces<IEnumerable<StationDto>>(StatusCodes.Status200OK);

// GET /api/v1/stations/{number}
app.MapGet("/api/v1/stations/{number:int}", async (
    [FromServices] IStationService service,
    int number) =>
{
    var station = await service.GetStationByNumberAsync(number);
    if (station == null)
    {
        return Results.NotFound(new { message = $"Station {number} not found" });
    }

    return Results.Ok(StationDto.FromModel(station));
})
.WithName("GetStationByNumberV1")
.Produces<StationDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

// GET /api/v1/stations/summary
app.MapGet("/api/v1/stations/summary", async (
    [FromServices] IStationService service) =>
{
    var summary = await service.GetSummaryAsync();
    return Results.Ok(summary);
})
.WithName("GetStationsSummaryV1")
.Produces<StationsSummaryDto>(StatusCodes.Status200OK);

// POST /api/v1/stations
app.MapPost("/api/v1/stations", async (
    [FromServices] IStationService service,
    StationDto dto) =>
{
    var station = new Station
    {
        Number = dto.Number,
        Name = dto.Name,
        Address = dto.Address,
        Position = new GeoPosition { Latitude = dto.Latitude, Longitude = dto.Longitude },
        BikeStands = dto.BikeStands,
        AvailableBikes = dto.AvailableBikes,
        AvailableBikeStands = dto.AvailableBikeStands,
        Status = dto.Status,
        LastUpdateEpochMs = dto.LastUpdateLocal.ToUniversalTime().ToUnixTimeMilliseconds()
    };

    await service.AddStationAsync(station);

    return Results.Created($"/api/v1/stations/{station.Number}", StationDto.FromModel(station));
})
.WithName("CreateStationV1")
.Produces<StationDto>(StatusCodes.Status201Created);



// PUT /api/v1/stations/{number}
app.MapPut("/api/v1/stations/{number:int}", async (
    [FromServices] IStationService service,
    int number,
    StationDto dto) =>
{
    var updated = new Station
    {
        Number = number,
        Name = dto.Name,
        Address = dto.Address,
        Position = new GeoPosition { Latitude = dto.Latitude, Longitude = dto.Longitude },
        BikeStands = dto.BikeStands,
        AvailableBikes = dto.AvailableBikes,
        AvailableBikeStands = dto.AvailableBikeStands,
        Status = dto.Status,
        LastUpdateEpochMs = dto.LastUpdateLocal.ToUniversalTime().ToUnixTimeMilliseconds()
    };

    var ok = await service.UpdateStationAsync(number, updated);
    if (!ok)
    {
        return Results.NotFound(new { message = $"Station {number} not found" });
    }

    return Results.NoContent();
})
.WithName("UpdateStationV1")
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound);

// DELETE /api/v1/stations/{number}
app.MapDelete("/api/v1/stations/{number:int}", async (
    [FromServices] IStationService service,
    int number) =>
{
    var deleted = await service.DeleteAsync(number);

    if (!deleted)
    {
        return Results.NotFound(new { message = $"Station {number} not found" });
    }

    return Results.NoContent();
})
.WithName("DeleteStationV1")
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound);

// ----------------- V2 - COSMOS DB -----------------

// GET /api/v2/stations
app.MapGet("/api/v2/stations", async (
    [FromServices] CosmosStationService cosmosService,
    [FromQuery] string? status,
    [FromQuery] int? minBikes,
    [FromQuery(Name = "q")] string? searchTerm,
    [FromQuery] string? sort,
    [FromQuery] string? dir,
    [FromQuery] int? page,
    [FromQuery] int? pageSize) =>
{
    var parameters = new StationQueryParameters
    {
        Status = status,
        MinBikes = minBikes,
        SearchTerm = searchTerm,
        Sort = string.IsNullOrWhiteSpace(sort) ? "name" : sort!,
        Dir = string.IsNullOrWhiteSpace(dir) ? "asc" : dir!,
        Page = page ?? 1,
        PageSize = pageSize ?? 20
    };

    // same normalization as V1 (sort, dir, page, pageSize)
    parameters.Normalize();

    var stations = await cosmosService.GetStationsAsync(parameters);
    var dtos = stations.Select(StationDto.FromModel);
    return Results.Ok(dtos);
})
.WithName("GetStationsV2")
.Produces<IEnumerable<StationDto>>(StatusCodes.Status200OK);

// GET /api/v2/stations/{number}
app.MapGet("/api/v2/stations/{number:int}", async (
    [FromServices] CosmosStationService cosmosService,
    int number) =>
{
    var station = await cosmosService.GetStationByNumberAsync(number);
    if (station == null)
        return Results.NotFound(new { message = $"Station {number} not found" });

    return Results.Ok(StationDto.FromModel(station));
})
.WithName("GetStationByNumberV2")
.Produces<StationDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

// GET /api/v2/stations/summary
app.MapGet("/api/v2/stations/summary", async (
    [FromServices] CosmosStationService cosmosService) =>
{
    var summary = await cosmosService.GetSummaryAsync();
    return Results.Ok(summary);
})
.WithName("GetStationsSummaryV2")
.Produces<StationsSummaryDto>(StatusCodes.Status200OK);

// POST /api/v2/stations
app.MapPost("/api/v2/stations", async (
    [FromServices] CosmosStationService cosmosService,
    StationDto dto) =>
{
    var station = new Station
    {
        Id = dto.Number.ToString(),
        Number = dto.Number,
        Name = dto.Name,
        Address = dto.Address,
        Position = new GeoPosition { Latitude = dto.Latitude, Longitude = dto.Longitude },
        BikeStands = dto.BikeStands,
        AvailableBikes = dto.AvailableBikes,
        AvailableBikeStands = dto.AvailableBikeStands,
        Status = dto.Status,
        LastUpdateEpochMs = dto.LastUpdateLocal.ToUniversalTime().ToUnixTimeMilliseconds()
    };

    await cosmosService.AddStationAsync(station);

    return Results.Created($"/api/v2/stations/{station.Number}", StationDto.FromModel(station));
})
.WithName("CreateStationV2")
.Produces<StationDto>(StatusCodes.Status201Created);

// PUT /api/v2/stations/{number}
app.MapPut("/api/v2/stations/{number:int}", async (
    [FromServices] CosmosStationService cosmosService,
    int number,
    StationDto dto) =>
{
    var updated = new Station
    {
        Id = number.ToString(),
        Number = number,
        Name = dto.Name,
        Address = dto.Address,
        Position = new GeoPosition { Latitude = dto.Latitude, Longitude = dto.Longitude },
        BikeStands = dto.BikeStands,
        AvailableBikes = dto.AvailableBikes,
        AvailableBikeStands = dto.AvailableBikeStands,
        Status = dto.Status,
        LastUpdateEpochMs = dto.LastUpdateLocal.ToUniversalTime().ToUnixTimeMilliseconds()
    };

    var ok = await cosmosService.UpdateStationAsync(number, updated);
    if (!ok)
        return Results.NotFound(new { message = $"Station {number} not found" });

    return Results.NoContent();
})
.WithName("UpdateStationV2")
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound);

// DELETE /api/v2/stations/{number}
app.MapDelete("/api/v2/stations/{number:int}", async (
    [FromServices] CosmosStationService cosmosService,
    int number) =>
{
    var deleted = await cosmosService.DeleteAsync(number);

    if (!deleted)
        return Results.NotFound(new { message = $"Station {number} not found" });

    return Results.NoContent();
})
.WithName("DeleteStationV2")
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound);

// DEV endpoint to populate Cosmos with data from the file (V1)
app.MapPost("/api/admin/seed-cosmos", async (
    [FromServices] FileStationService fileService,
    [FromServices] CosmosStationService cosmosService) =>
{
    var allStations = fileService.GetAllStations();
    await cosmosService.SeedAsync(allStations);

    return Results.Ok(new { message = $"Seeded {allStations.Count} stations into Cosmos." });
});


app.Run();
