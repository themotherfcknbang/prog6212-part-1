/*
================================================================================
 RaceDay Database Schema
 Target: SQL Server (SSMS)
 Matches: /docs/erd.png and /docs/api-endpoint-plan.md exactly (6 entities)
================================================================================
*/

-- ============================================================
-- CLEAN SLATE: drop tables in reverse dependency order if re-running
-- ============================================================
IF OBJECT_ID('dbo.Results', 'U') IS NOT NULL DROP TABLE dbo.Results;
IF OBJECT_ID('dbo.Enrolments', 'U') IS NOT NULL DROP TABLE dbo.Enrolments;
IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL DROP TABLE dbo.Categories;
IF OBJECT_ID('dbo.Events', 'U') IS NOT NULL DROP TABLE dbo.Events;
IF OBJECT_ID('dbo.Participants', 'U') IS NOT NULL DROP TABLE dbo.Participants;
IF OBJECT_ID('dbo.Organisers', 'U') IS NOT NULL DROP TABLE dbo.Organisers;
GO

-- ============================================================
-- TABLE: Organisers
-- ============================================================
CREATE TABLE dbo.Organisers (
    OrganiserID         INT IDENTITY(1,1) PRIMARY KEY,
    FullName             NVARCHAR(100)   NOT NULL,
    Email                NVARCHAR(150)   NOT NULL UNIQUE,
    PasswordHash         NVARCHAR(255)   NOT NULL,
    OrganisationName     NVARCHAR(150)   NOT NULL,
    PhoneNumber          NVARCHAR(20)    NULL,
    CreatedAt            DATETIME2       NOT NULL DEFAULT (SYSDATETIME())
);
GO

-- ============================================================
-- TABLE: Participants
-- ============================================================
CREATE TABLE dbo.Participants (
    ParticipantID         INT IDENTITY(1,1) PRIMARY KEY,
    FullName              NVARCHAR(100)  NOT NULL,
    Email                 NVARCHAR(150)  NOT NULL UNIQUE,
    PasswordHash          NVARCHAR(255)  NOT NULL,
    DateOfBirth           DATE           NOT NULL,
    Gender                NVARCHAR(20)   NULL,
    EmergencyContactName  NVARCHAR(100)  NULL,
    EmergencyContactPhone NVARCHAR(20)   NULL,
    CreatedAt             DATETIME2      NOT NULL DEFAULT (SYSDATETIME())
);
GO

-- ============================================================
-- TABLE: Events
-- ============================================================
CREATE TABLE dbo.Events (
    EventID           INT IDENTITY(1,1) PRIMARY KEY,
    OrganiserID       INT             NOT NULL,
    EventName         NVARCHAR(150)   NOT NULL,
    EventDate         DATE            NOT NULL,
    Location          NVARCHAR(150)   NOT NULL,
    Description       NVARCHAR(MAX)   NULL,
    RouteDescription  NVARCHAR(MAX)   NULL,
    StartTime         TIME            NULL,
    CreatedAt         DATETIME2       NOT NULL DEFAULT (SYSDATETIME()),
    CONSTRAINT FK_Events_Organisers FOREIGN KEY (OrganiserID)
        REFERENCES dbo.Organisers(OrganiserID)
        ON DELETE CASCADE
);
GO

-- ============================================================
-- TABLE: Categories
-- ============================================================
CREATE TABLE dbo.Categories (
    CategoryID       INT IDENTITY(1,1) PRIMARY KEY,
    EventID          INT             NOT NULL,
    CategoryName     NVARCHAR(50)    NOT NULL,
    DistanceKM       DECIMAL(5,2)    NOT NULL,
    EntryFee         DECIMAL(8,2)    NOT NULL DEFAULT (0),
    MaxParticipants  INT             NULL,
    CONSTRAINT FK_Categories_Events FOREIGN KEY (EventID)
        REFERENCES dbo.Events(EventID)
        ON DELETE CASCADE
);
GO

-- ============================================================
-- TABLE: Enrolments
-- ============================================================
CREATE TABLE dbo.Enrolments (
    EnrolmentID     INT IDENTITY(1,1) PRIMARY KEY,
    ParticipantID   INT             NOT NULL,
    CategoryID      INT             NOT NULL,
    EnrolmentDate   DATETIME2       NOT NULL DEFAULT (SYSDATETIME()),
    Status          NVARCHAR(20)    NOT NULL DEFAULT ('Pending'),
    BibNumber       NVARCHAR(10)    NULL,
    CONSTRAINT FK_Enrolments_Participants FOREIGN KEY (ParticipantID)
        REFERENCES dbo.Participants(ParticipantID)
        ON DELETE CASCADE,
    CONSTRAINT FK_Enrolments_Categories FOREIGN KEY (CategoryID)
        REFERENCES dbo.Categories(CategoryID)
        ON DELETE NO ACTION,
    -- A participant may only enrol in the same category once
    CONSTRAINT UQ_Enrolments_Participant_Category UNIQUE (ParticipantID, CategoryID)
);
GO

-- ============================================================
-- TABLE: Results
-- ============================================================
CREATE TABLE dbo.Results (
    ResultID       INT IDENTITY(1,1) PRIMARY KEY,
    EnrolmentID    INT             NOT NULL UNIQUE,  -- enforces 1-to-(0..1) with Enrolments
    FinishTime     TIME            NULL,
    Position       INT             NULL,
    Status         NVARCHAR(20)    NOT NULL DEFAULT ('Finished'),
    RecordedAt     DATETIME2       NOT NULL DEFAULT (SYSDATETIME()),
    CONSTRAINT FK_Results_Enrolments FOREIGN KEY (EnrolmentID)
        REFERENCES dbo.Enrolments(EnrolmentID)
        ON DELETE CASCADE
);
GO


/*
================================================================================
 SEED DATA
================================================================================
*/

-- ---------- Organisers (2) ----------
INSERT INTO dbo.Organisers (FullName, Email, PasswordHash, OrganisationName, PhoneNumber)
VALUES
('Thandiwe Mokoena', 'thandiwe@parkrunsa.co.za', 'hashed_pw_001', 'Parkrun South Africa', '0821234567'),
('Johan van der Merwe', 'johan@twooceans.co.za', 'hashed_pw_002', 'Two Oceans Marathon NPC', '0837654321');
GO

-- ---------- Participants (2) ----------
INSERT INTO dbo.Participants (FullName, Email, PasswordHash, DateOfBirth, Gender, EmergencyContactName, EmergencyContactPhone)
VALUES
('Lindiwe Dlamini', 'lindiwe.d@gmail.com', 'hashed_pw_101', '1994-03-12', 'Female', 'Sipho Dlamini', '0711112222'),
('Kevin Naidoo', 'kevin.naidoo@gmail.com', 'hashed_pw_102', '1988-11-05', 'Male', 'Priya Naidoo', '0733334444');
GO

-- ---------- Events (3) ----------
INSERT INTO dbo.Events (OrganiserID, EventName, EventDate, Location, Description, RouteDescription, StartTime)
VALUES
(1, 'Soweto Marathon', '2026-11-01', 'Soweto, Johannesburg',
   'Annual community marathon through the streets of Soweto.',
   'Starts at FNB Stadium, loops through Orlando and Vilakazi Street.', '06:00:00'),
(2, 'Two Oceans Cycle Tour', '2026-10-18', 'Cape Town',
   'Scenic cycling event around the Cape Peninsula.',
   'Starts in Newlands, along Chapmans Peak Drive, finishes at UCT.', '07:00:00'),
(1, 'Comrades Marathon', '2027-06-13', 'Pietermaritzburg to Durban',
   'Iconic ultramarathon between Pietermaritzburg and Durban.',
   'Traditional "down run" route via Drummond and Cato Ridge.', '05:30:00');
GO

-- ---------- Categories (2-3 per event) ----------
INSERT INTO dbo.Categories (EventID, CategoryName, DistanceKM, EntryFee, MaxParticipants)
VALUES
(1, '10km Fun Run', 10.0, 150.00, 2000),
(1, '21km Half Marathon', 21.1, 250.00, 1500),
(1, '42km Full Marathon', 42.2, 350.00, 1000),
(2, '50km Cycle Challenge', 50.0, 400.00, 3000),
(2, '109km Cycle Tour', 109.0, 650.00, 5000),
(3, '90km Ultramarathon', 90.0, 500.00, 20000);
GO

-- ---------- Sample Enrolments ----------
INSERT INTO dbo.Enrolments (ParticipantID, CategoryID, Status, BibNumber)
VALUES
(1, 2, 'Confirmed', 'A1023'),   -- Lindiwe -> Soweto 21km Half Marathon
(1, 4, 'Confirmed', 'B2044'),   -- Lindiwe -> Two Oceans 50km Cycle Challenge
(2, 3, 'Confirmed', 'A1101'),   -- Kevin -> Soweto 42km Full Marathon
(2, 6, 'Pending',   NULL);      -- Kevin -> Comrades 90km Ultramarathon (pending payment)
GO

-- ---------- Sample Results (for completed enrolments) ----------
INSERT INTO dbo.Results (EnrolmentID, FinishTime, Position, Status)
VALUES
(1, '01:52:30', 145, 'Finished'),
(3, '04:15:07', 302, 'Finished');
GO
