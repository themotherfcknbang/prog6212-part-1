# prog6212-part-1
RaceDay — /docs README

This note documents two deliberate differences between the SQL script (`raceday-schema.sql`) and a literal reading of the ERD (`erd.png`). Both are additions made for data integrity, not contradictions of the ERD's structure or cardinality.

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

Summary for markers

Both differences are constraint-level refinements made during implementation, not departures from the ERD's entities, attributes, primary keys, foreign keys, or cardinality — all of which match `erd.png` exactly. They exist to make the schema enforce rules that a diagram can't express (SQL Server's cascade-path limitation) or wouldn't typically express (a uniqueness business rule).
