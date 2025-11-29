# fs-2025-assessment-1-74060 — DublinBikes API & Blazor Client

Bachelor of Computer Science   
Module: Full Stack Development (M3.3)  
Assessments: **CA1 – DublinBikes API** and

---

## 1. Overview

This solution contains:

- **DublinBikes.Api**  
  .NET 8 minimal Web API that:
  - Loads the original `dublinbike.json` file on startup (**V1**).
  - Exposes versioned endpoints (`/api/v1/...` for file-based data, `/api/v2/...` for Cosmos DB).
  - Implements searching, filtering, sorting and paging.
  - Uses in-memory caching for query results.
  - Provides a background service that periodically updates station availability.
  - Can seed a **Cosmos DB** container for the V2 API.

The GitHub repository follows the required name format:  
`fs-2025-assessment-1-74060`.

---

## 2. Solution Structure

Solution root:

- **DublinBikes.Api/** – Main .NET 8 Web API  
  - **Data/dublinbike.json** – Original Dublin Bikes data file  
  - **Models/** – `Station`, `GeoPosition`, `StationQueryParameters`  
  - **Dtos/** – `StationDto`, `StationsSummaryDto`  
  - **Services/**
    - `IStationService` – common abstraction for station operations  
    - `FileStationService` – V1 file-based implementation (+ background random updates)  
    - `CosmosStationService` – V2 Cosmos DB implementation  
    - `StationsBackgroundUpdater` – `BackgroundService` that periodically calls `RandomUpdateAllStations`  
  - **appsettings.json**
    - `CosmosDb` section with connection string, database and container name  
  - **Program.cs**
    - DI configuration, endpoint mapping and API versioning  
    - V1 endpoints (`/api/v1/...`) use **IStationService** (file data)  
    - V2 endpoints (`/api/v2/...`) use **CosmosStationService**  
    - Admin endpoint `/api/admin/seed-cosmos` to seed Cosmos DB

## 3. How to Run the API (DublinBikes.Api)

### Prerequisites

- .NET 8 SDK installed.
- **Azure Cosmos DB Emulator** running on `https://localhost:8081`  
  (connection string configured in `appsettings.json` under `CosmosDb`).

### Steps (Visual Studio, no terminal)

1. No Solution Explorer, clique com o botão direito em **DublinBikes.Api** →  
   **Set as Startup Project**.
2. Certifique-se de que o perfil de execução está como **https**.
3. Pressione **F5** para rodar em modo Debug.
4. O navegador abrirá o **Swagger UI** em algo como:  
   `https://localhost:7075/swagger`.

### Important endpoints

- **V1 – file data**
  - `GET /api/v1/stations`
  - `GET /api/v1/stations/{number}`
  - `GET /api/v1/stations/summary`
  - `POST /api/v1/stations`
  - `PUT /api/v1/stations/{number}`
  - `DELETE /api/v1/stations/{number}`

- **V2 – Cosmos DB**
  - `GET /api/v2/stations`
  - `GET /api/v2/stations/{number}`
  - `GET /api/v2/stations/summary`
  - `POST /api/v2/stations`
  - `PUT /api/v2/stations/{number}`
  - `DELETE /api/v2/stations/{number}`

- **Admin**
  - `POST /api/admin/seed-cosmos`  
    Seeds Cosmos DB with all stations loaded from `dublinbike.json`.

---

## 4. Background Updates

The class **`StationsBackgroundUpdater`** is registered as a hosted service:

```csharp
builder.Services.AddHostedService<StationsBackgroundUpdater>();

On startup it:

Runs in the background every few seconds.

Calls FileStationService.RandomUpdateAllStations(Random) which:

Randomly changes AvailableBikes for each station, keeping values between 0 and BikeStands.

Recalculates AvailableBikeStands.

Updates LastUpdateEpochMs to the current time.

Occasionally sets stations to OPEN or CLOSED.

This simulates a live feed of changing bike availability.

Caching

The API uses in-memory caching via IMemoryCache:

GetStationsAsync in both FileStationService and CosmosStationService:

Build a cache key based on query parameters (status, q, minBikes, sort, dir, page, pageSize).

Store the resulting list of stations for 5 minutes.

This reduces recomputation for repeated queries and matches the assignment requirement for caching.

-**5 Postman Collection

A Postman collection called “DublinBikes API” is organised as:

V1/

GET V1 – Get stations

GET V1 – Get Station Number

GET V1 – Get Station Summary

POST V1 – Create Station

PUT V1 – Update Station

DELETE V1 – Delete Station

V2/

GET V2 – Get stations

GET V2 – Get Station Number

GET V2 – Get Station Summary

POST V2 – Create Station

PUT V2 – Update Station

DELETE V2 – Delete Station

Admin/

POST Seed Cosmos

- **6 Each request includes basic tests checking:

Status code (200 / 201 / 204 / 404 / 409).

Response body contains expected fields.

To run:

Start the API (DublinBikes.Api) in Visual Studio.

Import the Postman collection.

Select the DublinBikes API collection → Run to execute all tests.

- **7 - Tests

A project DublinBikes.Api.Tests (xUnit) covers:

Core filtering and search logic in the station services.

At least one happy-path endpoint behaviour (GET stations).

To run tests:

No Visual Studio: Test → Run All Tests.

Assumptions and Notes

Time zone is converted from epoch milliseconds to DateTimeOffset and exposed as LastUpdateLocal in DTOs using Europe/Dublin time.

Occupancy is calculated as:
Occupancy = AvailableBikes / BikeStands (0 when BikeStands == 0).

Cosmos DB Emulator is used for development; configuration can be changed in appsettings.json.
