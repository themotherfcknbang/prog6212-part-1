# prog6212-part-1
RaceDay — /docs README

This note documents two deliberate differences between the SQL script (`raceday-schema.sql`) and a literal reading of the ERD (`Prog.pdf`). Both are additions made for data integrity, not contradictions of the ERD's structure or cardinality.

1. ON DELETE NO ACTION on Enrolments.CategoryID (instead of CASCADE)

What the ERD shows: Categories (1) → (M) Enrolments. Deleting a category conceptually implies its enrolments should go too.

What the script does instead: The foreign key `FK_Enrolments_Categories` uses `ON DELETE NO ACTION`, while `FK_Enrolments_Participants` uses `ON DELETE CASCADE`.

Why: SQL Server rejects a schema if a table can be reached by more than one cascade path from the same ancestor. Here, `Results` is reachable from `Enrolments` two ways:

directly: `Enrolments → Results`
indirectly: `Enrolments → Categories → Events → Organisers` (all cascading) `→` back down to `Results`

Because both `Enrolments.ParticipantID` and `Enrolments.CategoryID` can't cascade at the same time without creating this conflict, one had to be `NO ACTION`. `CategoryID` was chosen because deleting a category with active enrolments is expected to be blocked at the application layer anyway — the API plan already returns `409 Conflict` for `DELETE /api/categories/{id}` when enrolments exist, so the database constraint reinforces a rule the API already enforces, rather than silently deleting participant enrolment history.

Net effect on data model: the cardinality (1 Category → M Enrolments) is unchanged. Only the delete-propagation behavior differs from a naive "cascade everything" reading of the diagram.

2. `UQ_Enrolments_Participant_Category` unique constraint (not shown on the ERD)

What the ERD shows: Enrolments as a standard associative entity between Participants and Categories, with no constraint drawn beyond the two foreign keys.

What the script does instead: Adds `UNIQUE (ParticipantID, CategoryID)` on the `Enrolments` table.

Why: Without it, nothing stops the same participant from being inserted into the same category twice (e.g. two duplicate sign-ups for the Soweto 21km). That's a real-world business rule (you can't enter the same race category more than once) that the entity/relationship diagram doesn't capture on its own — ERDs typically show structural relationships and cardinality, not business-rule-level uniqueness constraints. This was added at the SQL layer since it's cheap insurance against duplicate rows regardless of what validation the API layer does.

Net effect on data model: No entities, attributes, or relationships changed. This only restricts which *rows* are valid within the existing Enrolments structure.
# part 2
## Part 2: RESTful API

An ASP.NET Core Web API (C#) using Entity Framework Core (Code-First) on SQL Server. All Part 2 source lives in the `Part2/` folder.

```
Part2/
├── RaceDay.sln
├── RaceDay.Api/      # Controllers, Models, Data (DbContext), Dtos, Services, Swagger, Migrations
└── RaceDay.Tests/    # xUnit tests
```

### Running it locally

1. Install the .NET 8 SDK and SQL Server (Express is fine).
2. In `Part2/RaceDay.Api/appsettings.json`, set `ConnectionStrings:DefaultConnection` to your own SQL Server instance. It uses Windows authentication, so no password is stored in the repo.
3. Create the database from the migration: run `Update-Database` in the Visual Studio Package Manager Console (default project `RaceDay.Api`), or `dotnet ef database update` from `Part2/RaceDay.Api`.
4. Run `RaceDay.Api` (F5) and open `/swagger`, for example `https://localhost:7196/swagger` (your port may differ).
5. In Swagger, register or log in first. The session cookie is then sent automatically with every later request.

### Running the tests

```
dotnet test Part2/RaceDay.sln
```

The tests use the real ASP.NET Core pipeline (authentication, authorisation, validation) against an in-memory database, so no SQL Server is needed. They cover registration and login, event management, role-based rejection (401 when logged out, 403 for the wrong role or a non-owner), enrolments, results, and the weather endpoint with a fake weather service.

### Authentication and roles

- Organisers and Participants register through separate routes, so the role is fixed by the route used and stored in the session, not in the request body.
- Passwords are hashed with BCrypt. The original password is never stored.
- Sessions use ASP.NET Core cookie authentication (`RaceDay.Session`, HttpOnly, 2-hour sliding expiry). Protected routes return 401 when logged out and 403 for the wrong role or for someone else's data.
- An email address is unique across both roles, so login is never ambiguous.

### Differences from the Part 1 plan

| Area | Part 1 plan | Implementation | Why |
|---|---|---|---|
| Session handling | Register and login return a JWT token | A session cookie is issued, and the response returns the user's id, name, email and role | The Part 2 brief asks for session management |
| Cancelling an enrolment | `DELETE /api/enrolments/{id}` cancels it | The row is kept with status `Cancelled` instead of being deleted | Keeps history; the unique (participant, category) rule means a re-enrolment re-activates the row |
| Past events | Not specified | Creating or moving an event into the past, enrolling in a past event, and recording a result before the event date return 400 | Protects data integrity |
| Extra 409 cases | Not listed | Cancelling twice, lowering a category's capacity below its active enrolments, and recording a result for a cancelled enrolment | Protects data integrity |
| Weather | `{ temperature, conditions, windSpeed, forecastDate }` | Same fields plus `forecastType`, `temperatureMin`, `temperatureMax` and `note`; outside the 16-day forecast window it returns current conditions with an explanatory note; an unresolvable location returns 404 | The forecast service only covers 16 days |
| Optional filters | Not specified | `?includeCancelled=` on the roster and `?categoryId=` on event results | The plan describes a per-category view |
| Status values | Free-text `VARCHAR` columns | The API validates `Finished`, `DNF`, `DNS`, `DSQ` for results | Keeps the data consistent |

Schema notes from Part 1 still apply (`ON DELETE NO ACTION` on `Enrolments.CategoryID`, and a unique rule on `(ParticipantID, CategoryID)`). EF Core also creates its own `__EFMigrationsHistory` table, and it creates unique indexes where the Part 1 script used unique constraints. They enforce the same rules.

### Known limitations

- Anyone can register as an Organiser, as the Part 1 plan makes that route public. A production system would add an approval step.
- The "category is full" check counts first and saves afterwards, so two people taking the last place at the same instant could both succeed.
- Weather data comes from the free Open-Meteo service, which is licensed for non-commercial use.

Weather data by [Open-Meteo.com](https://open-meteo.com/) (CC BY 4.0).

### CI/CD

`.github/workflows/dotnet-ci.yml` restores, builds and runs every unit test on each push and pull request to `main`. `.github/workflows/validate-docs.yml` still checks the Part 1 `/docs` folder.

![Part 2 CI passing](docs/part2-ci-success.png)

Summary for markers

Both differences are constraint-level refinements made during implementation, not departures from the ERD's entities, attributes, primary keys, foreign keys, or cardinality — all of which match `erd.png` exactly. They exist to make the schema enforce rules that a diagram can't express (SQL Server's cascade-path limitation) or wouldn't typically express (a uniqueness business rule).
