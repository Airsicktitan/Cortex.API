/*
Development-only learning seed data for Cortex.

Creates focused patterns for validating learning signals around:
- Ticket assignment outcomes (TicketOutcomes)
- Comments as complexity signal
- Reassignment / override / SLA risk indicators

Safe to re-run:
- Deletes prior seed rows where Ticket.Id starts with 'LRN-'
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

    DECLARE @NowUtc DATETIME2 = SYSUTCDATETIME();

    /* Resolve real users from current DB state (never hard-code FK ids). */
    DECLARE @SarahUserId INT =
    (
        SELECT TOP (1) u.Id
        FROM Users u
        WHERE u.IsActive = 1
          AND (u.IsSynitiOwnerEligible = 1 OR u.IsBusinessOwnerEligible = 1)
          AND u.DisplayName LIKE '%Sarah%'
        ORDER BY u.Id
    );

    DECLARE @JohnUserId INT =
    (
        SELECT TOP (1) u.Id
        FROM Users u
        WHERE u.IsActive = 1
          AND (u.IsSynitiOwnerEligible = 1 OR u.IsBusinessOwnerEligible = 1)
          AND u.DisplayName LIKE '%John%'
        ORDER BY u.Id
    );

    IF @SarahUserId IS NULL
    BEGIN
        SELECT TOP (1) @SarahUserId = u.Id
        FROM Users u
        WHERE u.IsActive = 1
          AND (u.IsSynitiOwnerEligible = 1 OR u.IsBusinessOwnerEligible = 1)
        ORDER BY u.Id;
    END;

    IF @JohnUserId IS NULL
    BEGIN
        SELECT TOP (1) @JohnUserId = u.Id
        FROM Users u
        WHERE u.IsActive = 1
          AND (u.IsSynitiOwnerEligible = 1 OR u.IsBusinessOwnerEligible = 1)
          AND u.Id <> @SarahUserId
        ORDER BY u.Id;
    END;

    DECLARE @FallbackActorId INT =
    (
        SELECT TOP (1) u.Id
        FROM Users u
        WHERE u.IsActive = 1
        ORDER BY u.Id
    );

    IF @FallbackActorId IS NULL
    BEGIN
        RAISERROR('seed-learning-data.sql requires at least one active user in [Users].', 16, 1);
    END;

    IF @SarahUserId IS NULL SET @SarahUserId = @FallbackActorId;
    IF @JohnUserId IS NULL SET @JohnUserId = @SarahUserId;

    DECLARE @SarahName NVARCHAR(100) =
    (
        SELECT TOP (1) COALESCE(NULLIF(u.DisplayName, ''), u.Email)
        FROM Users u
        WHERE u.Id = @SarahUserId
    );

    DECLARE @JohnName NVARCHAR(100) =
    (
        SELECT TOP (1) COALESCE(NULLIF(u.DisplayName, ''), u.Email)
        FROM Users u
        WHERE u.Id = @JohnUserId
    );

    DECLARE @BoardId INT =
    (
        SELECT TOP (1) b.Id
        FROM TicketBoardDefinitions b
        WHERE b.IsEnabled = 1
        ORDER BY CASE WHEN b.Name = 'Ticket' THEN 0 ELSE 1 END, b.Id
    );

    IF @BoardId IS NULL
    BEGIN
    RAISERROR('No enabled board found in [TicketBoardDefinitions].', 16, 1);
    END;

    DECLARE @StatusNew NVARCHAR(100) =
    (
        SELECT TOP (1) s.Name
        FROM TicketStatusDefinitions s
        WHERE s.IsEnabled = 1 AND s.Name = 'New'
    );
    IF @StatusNew IS NULL
    BEGIN
        SELECT TOP (1) @StatusNew = s.Name
        FROM TicketStatusDefinitions s
        WHERE s.IsEnabled = 1
        ORDER BY s.Id;
    END;

    DECLARE @StatusInProgress NVARCHAR(100) =
    (
        SELECT TOP (1) s.Name
        FROM TicketStatusDefinitions s
        WHERE s.IsEnabled = 1 AND s.Name = 'In Progress'
    );
    IF @StatusInProgress IS NULL SET @StatusInProgress = @StatusNew;

    DECLARE @StatusResolved NVARCHAR(100) =
    (
        SELECT TOP (1) s.Name
        FROM TicketStatusDefinitions s
        WHERE s.IsEnabled = 1 AND s.Name = 'Resolved'
    );
    IF @StatusResolved IS NULL SET @StatusResolved = @StatusInProgress;

    DECLARE @StatusClosed NVARCHAR(100) =
    (
        SELECT TOP (1) s.Name
        FROM TicketStatusDefinitions s
        WHERE s.IsEnabled = 1 AND s.Name = 'Closed'
    );
    IF @StatusClosed IS NULL SET @StatusClosed = @StatusResolved;

    /* Idempotent cleanup of previous dev seed rows. */
    DELETE c
    FROM Comments c
    INNER JOIN Tickets t ON t.Id = c.TicketId
    WHERE t.Id LIKE 'LRN-%';

    DELETE o
    FROM TicketOutcomes o
    INNER JOIN Tickets t ON t.Id = o.TicketId
    WHERE t.Id LIKE 'LRN-%';

    DELETE FROM TicketOutcomes
    WHERE TicketId LIKE 'LRN-%';

    DELETE FROM Tickets
    WHERE Id LIKE 'LRN-%';

    DECLARE @SeedTickets TABLE
    (
        TicketId NVARCHAR(450) NOT NULL,
        PatternCode CHAR(1) NOT NULL,
        SeqNo INT NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(MAX) NOT NULL,
        InitialOwner NVARCHAR(200) NULL,
        FinalOwner NVARCHAR(200) NULL,
        TicketStatus NVARCHAR(100) NOT NULL,
        CommentCount INT NOT NULL,
        WasOverridden BIT NOT NULL,
        WasReassigned BIT NOT NULL,
        WasReopened BIT NOT NULL,
        SlaBreached BIT NOT NULL,
        ReachedTerminalStatus BIT NOT NULL,
        CreatedOffsetDays INT NOT NULL,
        CreatedByUserId INT NOT NULL,
        LastModifiedByUserId INT NOT NULL,
        CompletedOffsetDays INT NULL
    );

    /* Pattern A: Strong success signal (Sarah, no override, low comments, SLA met). */
    INSERT INTO @SeedTickets
    SELECT
        CONCAT('LRN-A-', RIGHT(CONCAT('00', n), 2)) AS TicketId,
        'A',
        n,
        CONCAT('SAP FI posting validation failure - company code 1000 (batch ', n, ')'),
        CONCAT(
            'SAP finance issue: FI document posting fails for company code 1000 due to validation exit mismatch. ',
            'Replicated after month-end job in ECC and requires routing to SAP finance support.'
        ),
        @SarahName,
        @SarahName,
        CASE WHEN n % 3 = 0 THEN @StatusClosed ELSE @StatusResolved END,
        CASE WHEN n % 2 = 0 THEN 2 ELSE 1 END,
        0,
        0,
        0,
        0,
        1,
        29 - n,               -- spread over ~last 30 days
        @SarahUserId,
        @SarahUserId,
        CASE WHEN 29 - n - 2 < 0 THEN 0 ELSE 29 - n - 2 END
    FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10)) v(n);

    /* Pattern B: Bad initial owner -> override to Sarah, more comments, some SLA risk. */
    INSERT INTO @SeedTickets
    SELECT
        CONCAT('LRN-B-', RIGHT(CONCAT('00', n), 2)) AS TicketId,
        'B',
        n,
        CONCAT('SAP FI posting blocked by substitution rule conflict (case ', n, ')'),
        CONCAT(
            'Similar SAP finance posting issue as pattern A, but initially triaged to wrong owner. ',
            'Requires owner override after initial diagnosis and finance validation walkthrough.'
        ),
        @JohnName,
        @SarahName,
        CASE WHEN n IN (3, 7) THEN @StatusClosed ELSE @StatusResolved END,
        3 + (n % 3),          -- 3-5 comments
        1,
        1,
        0,
        CASE WHEN n IN (2, 6) THEN 1 ELSE 0 END,  -- near breach represented as occasional breach
        1,
        24 - n,
        @JohnUserId,
        @SarahUserId,
        CASE WHEN 24 - n - 1 < 0 THEN 0 ELSE 24 - n - 1 END
    FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8)) v(n);

    /* Pattern C: Ambiguous tickets, messy handling, high comments, reopen/breach noise. */
    INSERT INTO @SeedTickets
    SELECT
        CONCAT('LRN-C-', RIGHT(CONCAT('00', n), 2)) AS TicketId,
        'C',
        n,
        CONCAT('Need help with finance thing - urgent ', n),
        CONCAT(
            'User reports "SAP is broken" with incomplete steps, unclear module context, and mixed ownership signals. ',
            'Ticket bounced between teams while clarifying business impact and technical logs.'
        ),
        CASE WHEN n % 2 = 0 THEN @JohnName ELSE @SarahName END,
        CASE WHEN n IN (1, 4) THEN @SarahName ELSE @JohnName END,
        CASE WHEN n IN (2, 5) THEN @StatusInProgress ELSE @StatusResolved END,
        5 + (n % 3),          -- 5-7 comments
        CASE WHEN n IN (1, 3, 6) THEN 1 ELSE 0 END,
        1,
        CASE WHEN n IN (2, 4, 5) THEN 1 ELSE 0 END,
        CASE WHEN n IN (3, 5, 6) THEN 1 ELSE 0 END,
        CASE WHEN n IN (2, 5) THEN 0 ELSE 1 END,  -- some still not terminal
        18 - n,
        CASE WHEN n % 2 = 0 THEN @JohnUserId ELSE @SarahUserId END,
        CASE WHEN n IN (1, 4) THEN @SarahUserId ELSE @JohnUserId END,
        CASE
            WHEN n IN (2, 5) THEN NULL
            WHEN 18 - n - 1 < 0 THEN 0
            ELSE 18 - n - 1
        END
    FROM (VALUES (1),(2),(3),(4),(5),(6)) v(n);

    INSERT INTO Tickets
    (
        Id,
        Title,
        Description,
        Status,
        ApprovalStatus,
        Priority,
        BoardId,
        SynitiOwner,
        BusinessOwner,
        CreatedBy,
        CreatedDate,
        LastModifiedBy,
        LastModifiedDate
    )
    SELECT
        s.TicketId,
        s.Title,
        s.Description,
        s.TicketStatus,
        'Approved',
        CASE WHEN s.PatternCode = 'C' THEN 'High' ELSE 'Medium' END,
        @BoardId,
        s.FinalOwner,
        NULL,
        s.CreatedByUserId,
        DATEADD(DAY, -s.CreatedOffsetDays, @NowUtc),
        s.LastModifiedByUserId,
        DATEADD(HOUR, 8, DATEADD(DAY, -s.CreatedOffsetDays, @NowUtc))
    FROM @SeedTickets s;

    /* Insert comments with pattern-aligned narrative and counts. */
    ;WITH TicketNumbers AS
    (
        SELECT TOP (10) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
        FROM sys.all_objects
    )
    INSERT INTO Comments
    (
        TicketId,
        Body,
        CreatedBy,
        CreatedDate,
        LastModifiedDate
    )
    SELECT
        s.TicketId,
        CASE
            WHEN s.PatternCode = 'A' THEN
                CASE t.n
                    WHEN 1 THEN 'Initial triage confirms recurring SAP FI posting validation issue; routed directly to finance support owner.'
                    ELSE 'Fix validated quickly; posting succeeds in test company code after mapping correction.'
                END
            WHEN s.PatternCode = 'B' THEN
                CASE t.n
                    WHEN 1 THEN 'Initial assignment landed with non-finance owner; issue context does not match owner specialty.'
                    WHEN 2 THEN 'Override requested to finance specialist after reviewing SAP substitution exit logs.'
                    WHEN 3 THEN 'Sarah confirmed prior incident match and started correction script.'
                    WHEN 4 THEN 'Business confirms output posting now completes; monitoring next scheduled run.'
                    ELSE 'Close-out note: response time approached SLA threshold because of initial misassignment.'
                END
            ELSE
                CASE t.n
                    WHEN 1 THEN 'Reporter provided vague details; no exact transaction code or company code included.'
                    WHEN 2 THEN 'Support requested reproduction steps and relevant SAP dump/log attachments.'
                    WHEN 3 THEN 'Ownership changed after discovering mixed business + technical blockers.'
                    WHEN 4 THEN 'Ticket reopened when downstream posting still failed in UAT.'
                    WHEN 5 THEN 'Additional clarification added after cross-team sync with finance operations.'
                    ELSE 'Extended troubleshooting continued; ticket remained noisy and ambiguous.'
                END
        END,
        CASE
            WHEN s.PatternCode = 'A' THEN @SarahUserId
            WHEN s.PatternCode = 'B' AND t.n = 1 THEN @JohnUserId
            ELSE @SarahUserId
        END,
        DATEADD(HOUR, t.n * 4, DATEADD(DAY, -s.CreatedOffsetDays, @NowUtc)),
        DATEADD(HOUR, t.n * 4 + 1, DATEADD(DAY, -s.CreatedOffsetDays, @NowUtc))
    FROM @SeedTickets s
    INNER JOIN TicketNumbers t
        ON t.n <= s.CommentCount;

    INSERT INTO TicketOutcomes
    (
        TicketId,
        BoardId,
        AssignedSynitiOwner,
        AssignedBusinessOwner,
        FinalSynitiOwner,
        FinalBusinessOwner,
        WasOverridden,
        SlaBreached,
        WasReassigned,
        WasReopened,
        CommentCount,
        ReachedTerminalStatus,
        MatchedRuleId,
        CreatedAtUtc,
        LastUpdatedAtUtc,
        CompletedAtUtc
    )
    SELECT
        s.TicketId,
        @BoardId,
        s.InitialOwner,
        NULL,
        s.FinalOwner,
        NULL,
        s.WasOverridden,
        s.SlaBreached,
        s.WasReassigned,
        s.WasReopened,
        s.CommentCount,
        s.ReachedTerminalStatus,
        NULL,
        DATEADD(MINUTE, 30, DATEADD(DAY, -s.CreatedOffsetDays, @NowUtc)),
        DATEADD(HOUR, 12, DATEADD(DAY, -s.CreatedOffsetDays, @NowUtc)),
        CASE
            WHEN s.CompletedOffsetDays IS NULL THEN NULL
            ELSE DATEADD(DAY, -s.CompletedOffsetDays, @NowUtc)
        END
    FROM @SeedTickets s;

    BEGIN TRY

    COMMIT TRANSACTION;

    SELECT
        'Seed complete' AS Result,
        COUNT(*) AS TicketCount
    FROM Tickets
    WHERE Id LIKE 'LRN-%';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
