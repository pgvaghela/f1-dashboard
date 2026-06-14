# F1 Dashboard

A full-stack F1 race analytics dashboard built to practice modern web development patterns.

## Stack

- **Backend:** ASP.NET Core 9 Web API with Entity Framework Core
- **Database:** PostgreSQL
- **Frontend:** Angular 19 with standalone components and signals
- **Hosting:** Local development; deployment TBD

## Status

Work in progress. See the project plan below.

## API endpoints

Base URL (local dev): `http://localhost:5197`. Interactive docs (Scalar) at `/scalar/v1`.

| Method | Route | Description |
| ------ | ----- | ----------- |
| GET | `/api/drivers` | All drivers, ordered by last name |
| GET | `/api/drivers/{id}` | A single driver (`404` if not found) |
| GET | `/api/constructors` | All constructors, ordered by team name |
| GET | `/api/constructors/{id}` | A single constructor (`404` if not found) |
| GET | `/api/races?season={year}` | Races for a season (omit `season` for all), with circuit details |
| GET | `/api/races/{id}` | A single race with circuit details (`404` if not found) |
| GET | `/api/races/{id}/results` | Results for a race, with driver and constructor names, ordered by finish position |
| GET | `/api/standings/drivers/{season}` | Driver standings for a season (`404` if no results) |
| GET | `/api/standings/constructors/{season}` | Constructor standings for a season (`404` if no results) |

## Configuration (local dev)

The Postgres connection string is **not** committed. Set it via [.NET user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) before running the backend:

```bash
cd src/F1Dashboard.Api
dotnet user-secrets set "ConnectionStrings:F1Database" "Host=localhost;Port=5432;Database=f1_dashboard;Username=postgres;Password=YOUR_PASSWORD"
```