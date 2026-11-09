# RaceDay

[![CI](https://github.com/YOUR-USERNAME/YOUR-REPO/actions/workflows/ci.yml/badge.svg)](https://github.com/YOUR-USERNAME/YOUR-REPO/actions/workflows/ci.yml)

RaceDay is a platform for South African road running, walking and cycling events. Organisers publish events, set up distances and capture results. Participants browse events, enter a distance and follow their results.

![Green build](docs/green-build.png)

## Run it (one line)

You need [Docker Desktop](https://www.docker.com/products/docker-desktop/). From this folder:

```bash
docker compose up --build
```

| What | Where |
|---|---|
| Website | http://localhost:5200 |
| API and Swagger | http://localhost:5012/swagger |

The first start takes a few minutes while the images build and SQL Server starts. The database is created automatically. Stop everything with `Ctrl+C`; add `-v` to `docker compose down` to also delete the data.

To change the database password or use real Azure Blob Storage, copy `.env.example` to `.env` and edit it.

## How it fits together

```
Browser  ->  RaceDay.Web (MVC)  ->  RaceDay.Api (REST)  ->  SQL Server
                                          |
                                          +-> Azure Blob Storage (banners, profile pictures)
```

- **RaceDay.Api** holds all the business rules and the only database access (Entity Framework Core + SQL Server). Images go to Azure Blob Storage through `IBlobService`. Weather forecasts for events come from Open-Meteo.
- **RaceDay.Web** is an ASP.NET Core MVC site with Bootstrap and the RaceDay theme. It never touches the database: every call goes through one typed `ApiClient`.
- **RaceDay.Tests** are integration tests that run the API in memory with an in-memory database.

### Login and session

Users register and log in through the API. The API issues a session cookie; the website keeps that cookie, plus the user's id, role and name, in its own server-side session and forwards the cookie on every API call. A `RequireRole` filter sends anyone who is not logged in to the login page and anyone with the wrong role to "access denied". Logging out clears both sessions.

## Features

**Organisers**
- Dashboard with upcoming and past events and entry counts
- Create, edit and delete events, with a banner image
- Manage distances (name, kilometres, entry fee, entry limit)
- See who has entered each distance
- Capture and correct results (Finished with time and position, DNF, DNS, DSQ)

**Participants**
- Browse upcoming events and filter by keyword, location and dates
- Event page with distances, route and weather forecast
- Enter a distance and cancel an entry
- "My Enrolments" with colour-coded status badges
- "My Results" with position and total finishers (for example 47th of 312)
- Profile with picture upload

## Run it from Visual Studio instead

1. Install SQL Server Express (the connection string in `RaceDay.Api/appsettings.json` expects `.\SQLEXPRESS`) and, for image uploads, [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) or an Azure Storage account.
2. In the Package Manager Console, with `RaceDay.Api` as the default project, run `Update-Database`.
3. Start both projects (a multi-project launch profile that starts `RaceDay.Api` and `RaceDay.Web` together).

## Tests

```bash
dotnet test RaceDay.sln
```

## Project layout

```
RaceDay.sln
RaceDay.Api/        REST API, EF Core models and migrations, Swagger
RaceDay.Web/        MVC website: controllers, views, ApiClient
RaceDay. Tests/     API integration tests
docker-compose.yml  SQL Server + Azurite + API + Web
.github/workflows/  CI: build, test, build Docker images
```
