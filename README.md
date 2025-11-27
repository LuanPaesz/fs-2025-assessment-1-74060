# DublinBikes API – Full Stack Development CA1

Bachelor of Computer Science – Full Stack Development
Continuous Assessment 1 
Student: Luan Bernardes Paes – Student Number: 74060 

---

## 1. Overview

This project is a **.NET 8 Web API** that exposes **DublinBikes** station data via two versions:

- **V1** – Loads station data from a local JSON file at startup.
- **V2** – Loads and persists station data in **Azure Cosmos DB** (using the local Cosmos DB Emulator).

The API supports:

- Searching, filtering, sorting and paging over stations.
- Summary/aggregate endpoint.
- Background service that **periodically updates availability** to simulate a live feed.
- In-memory caching of query results.
- Two separate versions of the API (**/api/v1/** and **/api/v2/**) with the same contracts.
- Postman collection with automated tests.
- Basic automated tests (xUnit) for service logic and endpoints.

---

## 2. Project Structure

Solution root:

fs-2025-assessment-1-74060/
 ├─ DublinBikes.Api/              # Main Web API project (.NET 8)
 │   ├─ Controllers/ or Endpoints/  # Minimal API endpoint mapping
 │   ├─ Models/                   # Station, GeoPosition, StationQueryParameters
 │   ├─ Dtos/                     # StationDto, StationsSummaryDto
 │   ├─ Services/                 # IStationService, FileStationService, CosmosStationService
 │   ├─ Background/              # Background service for random updates
 │   ├─ Data/dublinbike.json      # Original Dublin Bikes data
 │   ├─ appsettings.json          # CosmosDb configuration
 │   └─ Program.cs                # App configuration & endpoint mapping
 └─ DublinBikes.Api.Tests/        # xUnit tests (service + endpoint tests)
