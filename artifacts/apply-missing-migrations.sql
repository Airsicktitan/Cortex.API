IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211031310_InitialCreate'
)
BEGIN
    CREATE TABLE [Tickets] (
        [Id] nvarchar(450) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [Status] nvarchar(450) NOT NULL,
        [Priority] nvarchar(450) NOT NULL,
        [SynitiOwner] nvarchar(max) NULL,
        [BusinessOwner] nvarchar(max) NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [LastModifiedBy] nvarchar(max) NULL,
        [LastModifiedDate] datetime2 NULL,
        CONSTRAINT [PK_Tickets] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211031310_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [DisplayName] nvarchar(100) NOT NULL,
        [Email] nvarchar(200) NOT NULL,
        [Role] nvarchar(max) NOT NULL,
        [Department] nvarchar(max) NULL,
        [CreatedDate] datetime2 NOT NULL,
        [LastLoginDate] datetime2 NULL,
        [ExpiryDate] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [Auth0Id] nvarchar(450) NULL,
        [LastModifiedDate] datetime2 NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211031310_InitialCreate'
)
BEGIN
    CREATE TABLE [Comments] (
        [Id] int NOT NULL IDENTITY,
        [TicketId] nvarchar(450) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [LastModifiedDate] datetime2 NOT NULL,
        CONSTRAINT [PK_Comments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Comments_Tickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [Tickets] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211031310_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Comments_TicketId] ON [Comments] ([TicketId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211031310_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_Priority] ON [Tickets] ([Priority]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211031310_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_Status] ON [Tickets] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211031310_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Users_Auth0Id] ON [Users] ([Auth0Id]) WHERE [Auth0Id] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211031310_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211031310_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260211031310_InitialCreate', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211031407_UpdateUserForAuth0'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260211031407_UpdateUserForAuth0', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214082156_UpdateModels'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Id] = 0)
    BEGIN
        SET IDENTITY_INSERT [Users] ON;
        INSERT INTO [Users]
        (
            [Id],
            [DisplayName],
            [Email],
            [Role],
            [Department],
            [CreatedDate],
            [LastLoginDate],
            [ExpiryDate],
            [IsActive],
            [Auth0Id],
            [LastModifiedDate]
        )
        VALUES
        (
            0,
            'Legacy User',
            'legacy-user@local.invalid',
            'User',
            NULL,
            SYSUTCDATETIME(),
            NULL,
            NULL,
            1,
            NULL,
            NULL
        );
        SET IDENTITY_INSERT [Users] OFF;
    END;
    UPDATE [Tickets]
    SET [CreatedBy] = '0'
    WHERE TRY_CONVERT(int, [CreatedBy]) IS NULL
       OR NOT EXISTS
       (
           SELECT 1
           FROM [Users]
           WHERE [Id] = TRY_CONVERT(int, [Tickets].[CreatedBy])
       );
    UPDATE [Tickets]
    SET [LastModifiedBy] = '0'
    WHERE [LastModifiedBy] IS NULL
       OR TRY_CONVERT(int, [LastModifiedBy]) IS NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214082156_UpdateModels'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tickets]') AND [c].[name] = N'LastModifiedBy');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Tickets] DROP CONSTRAINT [' + @var0 + '];');
    EXEC(N'UPDATE [Tickets] SET [LastModifiedBy] = 0 WHERE [LastModifiedBy] IS NULL');
    ALTER TABLE [Tickets] ALTER COLUMN [LastModifiedBy] int NOT NULL;
    ALTER TABLE [Tickets] ADD DEFAULT 0 FOR [LastModifiedBy];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214082156_UpdateModels'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tickets]') AND [c].[name] = N'CreatedBy');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Tickets] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Tickets] ALTER COLUMN [CreatedBy] int NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214082156_UpdateModels'
)
BEGIN
    UPDATE [Tickets]
    SET [CreatedBy] = 0
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM [Users]
        WHERE [Id] = [Tickets].[CreatedBy]
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214082156_UpdateModels'
)
BEGIN
    CREATE INDEX [IX_Tickets_CreatedBy] ON [Tickets] ([CreatedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214082156_UpdateModels'
)
BEGIN
    ALTER TABLE [Tickets] ADD CONSTRAINT [FK_Tickets_Users_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214082156_UpdateModels'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260214082156_UpdateModels', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093121_UpdateModels1'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Id] = 0)
    BEGIN
        SET IDENTITY_INSERT [Users] ON;
        INSERT INTO [Users]
        (
            [Id],
            [DisplayName],
            [Email],
            [Role],
            [Department],
            [CreatedDate],
            [LastLoginDate],
            [ExpiryDate],
            [IsActive],
            [Auth0Id],
            [LastModifiedDate]
        )
        VALUES
        (
            0,
            'Legacy User',
            'legacy-user@local.invalid',
            'User',
            NULL,
            SYSUTCDATETIME(),
            NULL,
            NULL,
            1,
            NULL,
            NULL
        );
        SET IDENTITY_INSERT [Users] OFF;
    END;
    UPDATE [Comments]
    SET [CreatedBy] = '0'
    WHERE TRY_CONVERT(int, [CreatedBy]) IS NULL
       OR NOT EXISTS
       (
           SELECT 1
           FROM [Users]
           WHERE [Id] = TRY_CONVERT(int, [Comments].[CreatedBy])
       );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093121_UpdateModels1'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Comments]') AND [c].[name] = N'CreatedBy');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Comments] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [Comments] ALTER COLUMN [CreatedBy] int NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093121_UpdateModels1'
)
BEGIN
    ALTER TABLE [Comments] ADD [UserId] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093121_UpdateModels1'
)
BEGIN
    UPDATE [Comments]
    SET [CreatedBy] = 0
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM [Users]
        WHERE [Id] = [Comments].[CreatedBy]
    );
    UPDATE [Comments]
    SET [UserId] = [CreatedBy];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093121_UpdateModels1'
)
BEGIN
    CREATE INDEX [IX_Comments_UserId] ON [Comments] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093121_UpdateModels1'
)
BEGIN
    ALTER TABLE [Comments] ADD CONSTRAINT [FK_Comments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093121_UpdateModels1'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260214093121_UpdateModels1', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095402_UpdateModels2'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Id] = 0)
    BEGIN
        SET IDENTITY_INSERT [Users] ON;
        INSERT INTO [Users]
        (
            [Id],
            [DisplayName],
            [Email],
            [Role],
            [Department],
            [CreatedDate],
            [LastLoginDate],
            [ExpiryDate],
            [IsActive],
            [Auth0Id],
            [LastModifiedDate]
        )
        VALUES
        (
            0,
            'Legacy User',
            'legacy-user@local.invalid',
            'User',
            NULL,
            SYSUTCDATETIME(),
            NULL,
            NULL,
            1,
            NULL,
            NULL
        );
        SET IDENTITY_INSERT [Users] OFF;
    END;
    UPDATE [Comments]
    SET [CreatedBy] = 0
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM [Users]
        WHERE [Id] = [Comments].[CreatedBy]
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095402_UpdateModels2'
)
BEGIN
    ALTER TABLE [Comments] DROP CONSTRAINT [FK_Comments_Users_UserId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095402_UpdateModels2'
)
BEGIN
    DROP INDEX [IX_Comments_UserId] ON [Comments];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095402_UpdateModels2'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Comments]') AND [c].[name] = N'UserId');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Comments] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [Comments] DROP COLUMN [UserId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095402_UpdateModels2'
)
BEGIN
    CREATE INDEX [IX_Comments_CreatedBy] ON [Comments] ([CreatedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095402_UpdateModels2'
)
BEGIN
    ALTER TABLE [Comments] ADD CONSTRAINT [FK_Comments_Users_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [Users] ([Id]) ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095402_UpdateModels2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260214095402_UpdateModels2', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406143000_AddSlaConfigurations'
)
BEGIN
    CREATE TABLE [SlaConfigurations] (
        [Priority] nvarchar(50) NOT NULL,
        [TargetHours] int NOT NULL,
        [WarningHours] int NOT NULL,
        CONSTRAINT [PK_SlaConfigurations] PRIMARY KEY ([Priority])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406143000_AddSlaConfigurations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260406143000_AddSlaConfigurations', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406235500_AddUserProfileFields'
)
BEGIN
    ALTER TABLE [Users] ADD [NickName] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406235500_AddUserProfileFields'
)
BEGIN
    ALTER TABLE [Users] ADD [PhoneNumber] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406235500_AddUserProfileFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260406235500_AddUserProfileFields', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408025428_AddTicketAttachments'
)
BEGIN
    CREATE TABLE [TicketAttachments] (
        [Id] int NOT NULL IDENTITY,
        [TicketId] nvarchar(450) NOT NULL,
        [FileName] nvarchar(260) NOT NULL,
        [ContentType] nvarchar(200) NOT NULL,
        [FileSize] bigint NOT NULL,
        [Content] varbinary(max) NOT NULL,
        [UploadedBy] int NOT NULL,
        [UploadedDate] datetime2 NOT NULL,
        CONSTRAINT [PK_TicketAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TicketAttachments_Tickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [Tickets] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TicketAttachments_Users_UploadedBy] FOREIGN KEY ([UploadedBy]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408025428_AddTicketAttachments'
)
BEGIN
    CREATE INDEX [IX_TicketAttachments_TicketId] ON [TicketAttachments] ([TicketId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408025428_AddTicketAttachments'
)
BEGIN
    CREATE INDEX [IX_TicketAttachments_UploadedBy] ON [TicketAttachments] ([UploadedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408025428_AddTicketAttachments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260408025428_AddTicketAttachments', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408033002_AddArchivedTickets'
)
BEGIN
    CREATE TABLE [ArchivedTickets] (
        [Id] nvarchar(450) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [Status] nvarchar(450) NOT NULL,
        [Priority] nvarchar(450) NOT NULL,
        [SynitiOwner] nvarchar(max) NULL,
        [BusinessOwner] nvarchar(max) NULL,
        [CreatedBy] int NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [LastModifiedBy] int NOT NULL,
        [LastModifiedDate] datetime2 NULL,
        [ArchivedBy] int NOT NULL,
        [ArchivedDate] datetime2 NOT NULL,
        [CommentCount] int NOT NULL,
        [AttachmentCount] int NOT NULL,
        CONSTRAINT [PK_ArchivedTickets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ArchivedTickets_Users_ArchivedBy] FOREIGN KEY ([ArchivedBy]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ArchivedTickets_Users_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408033002_AddArchivedTickets'
)
BEGIN
    CREATE INDEX [IX_ArchivedTickets_ArchivedBy] ON [ArchivedTickets] ([ArchivedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408033002_AddArchivedTickets'
)
BEGIN
    CREATE INDEX [IX_ArchivedTickets_ArchivedDate] ON [ArchivedTickets] ([ArchivedDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408033002_AddArchivedTickets'
)
BEGIN
    CREATE INDEX [IX_ArchivedTickets_CreatedBy] ON [ArchivedTickets] ([CreatedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408033002_AddArchivedTickets'
)
BEGIN
    CREATE INDEX [IX_ArchivedTickets_Priority] ON [ArchivedTickets] ([Priority]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408033002_AddArchivedTickets'
)
BEGIN
    CREATE INDEX [IX_ArchivedTickets_Status] ON [ArchivedTickets] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408033002_AddArchivedTickets'
)
BEGIN
    CREATE OR ALTER PROCEDURE dbo.ArchiveTicket
        @TicketId nvarchar(450),
        @ArchivedBy int
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;
        BEGIN TRY
            BEGIN TRANSACTION;
            IF EXISTS (SELECT 1 FROM dbo.ArchivedTickets WHERE Id = @TicketId)
            BEGIN
                THROW 50002, 'Ticket is already archived.', 1;
            END;
            INSERT INTO dbo.ArchivedTickets
            (
                Id,
                Title,
                Description,
                Status,
                Priority,
                SynitiOwner,
                BusinessOwner,
                CreatedBy,
                CreatedDate,
                LastModifiedBy,
                LastModifiedDate,
                ArchivedBy,
                ArchivedDate,
                CommentCount,
                AttachmentCount
            )
            SELECT
                t.Id,
                t.Title,
                t.Description,
                t.Status,
                t.Priority,
                t.SynitiOwner,
                t.BusinessOwner,
                t.CreatedBy,
                t.CreatedDate,
                t.LastModifiedBy,
                t.LastModifiedDate,
                @ArchivedBy,
                SYSUTCDATETIME(),
                (SELECT COUNT(1) FROM dbo.Comments c WHERE c.TicketId = t.Id),
                (SELECT COUNT(1) FROM dbo.TicketAttachments a WHERE a.TicketId = t.Id)
            FROM dbo.Tickets t
            WHERE t.Id = @TicketId;
            IF @@ROWCOUNT = 0
            BEGIN
                THROW 50001, 'Ticket was not found.', 1;
            END;
            DELETE FROM dbo.Tickets
            WHERE Id = @TicketId;
            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0
            BEGIN
                ROLLBACK TRANSACTION;
            END;
            THROW;
        END CATCH;
    END;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408033002_AddArchivedTickets'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260408033002_AddArchivedTickets', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408035528_AddArchiveConfiguration'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260408035528_AddArchiveConfiguration', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408040214_AddArchiveConfigurationTable'
)
BEGIN
    CREATE TABLE [ArchiveConfigurations] (
        [Id] int NOT NULL IDENTITY,
        [ArchiveAfterDays] int NOT NULL,
        [ArchiveResolvedTickets] bit NOT NULL,
        [ArchiveClosedTickets] bit NOT NULL,
        CONSTRAINT [PK_ArchiveConfigurations] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408040214_AddArchiveConfigurationTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260408040214_AddArchiveConfigurationTable', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409044732_AddScheduledJobsAndStoredProcedures'
)
BEGIN
    CREATE TABLE [StoredProcedureDefinitions] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [ProcedureName] nvarchar(256) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsEnabled] bit NOT NULL,
        [CreatedDateUtc] datetime2 NOT NULL,
        [LastModifiedDateUtc] datetime2 NULL,
        CONSTRAINT [PK_StoredProcedureDefinitions] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409044732_AddScheduledJobsAndStoredProcedures'
)
BEGIN
    CREATE TABLE [ScheduledJobs] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [JobType] nvarchar(50) NOT NULL,
        [IntervalMinutes] int NOT NULL,
        [IsEnabled] bit NOT NULL,
        [StoredProcedureDefinitionId] int NULL,
        [RunAsUserId] int NOT NULL,
        [CreatedDateUtc] datetime2 NOT NULL,
        [LastModifiedDateUtc] datetime2 NULL,
        [LastRunDateUtc] datetime2 NULL,
        [NextRunDateUtc] datetime2 NULL,
        [LastRunStatus] nvarchar(50) NULL,
        [LastRunMessage] nvarchar(1000) NULL,
        CONSTRAINT [PK_ScheduledJobs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ScheduledJobs_StoredProcedureDefinitions_StoredProcedureDefinitionId] FOREIGN KEY ([StoredProcedureDefinitionId]) REFERENCES [StoredProcedureDefinitions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ScheduledJobs_Users_RunAsUserId] FOREIGN KEY ([RunAsUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409044732_AddScheduledJobsAndStoredProcedures'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ScheduledJobs_Name] ON [ScheduledJobs] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409044732_AddScheduledJobsAndStoredProcedures'
)
BEGIN
    CREATE INDEX [IX_ScheduledJobs_NextRunDateUtc] ON [ScheduledJobs] ([NextRunDateUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409044732_AddScheduledJobsAndStoredProcedures'
)
BEGIN
    CREATE INDEX [IX_ScheduledJobs_RunAsUserId] ON [ScheduledJobs] ([RunAsUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409044732_AddScheduledJobsAndStoredProcedures'
)
BEGIN
    CREATE INDEX [IX_ScheduledJobs_StoredProcedureDefinitionId] ON [ScheduledJobs] ([StoredProcedureDefinitionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409044732_AddScheduledJobsAndStoredProcedures'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StoredProcedureDefinitions_Name] ON [StoredProcedureDefinitions] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409044732_AddScheduledJobsAndStoredProcedures'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StoredProcedureDefinitions_ProcedureName] ON [StoredProcedureDefinitions] ([ProcedureName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409044732_AddScheduledJobsAndStoredProcedures'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260409044732_AddScheduledJobsAndStoredProcedures', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409050950_AddTicketAuditHistory'
)
BEGIN
    CREATE TABLE [TicketAuditEntries] (
        [Id] int NOT NULL IDENTITY,
        [TicketId] nvarchar(450) NOT NULL,
        [Action] nvarchar(50) NOT NULL,
        [Summary] nvarchar(250) NOT NULL,
        [Reason] nvarchar(1000) NULL,
        [ChangedBy] int NOT NULL,
        [ChangedDateUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_TicketAuditEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TicketAuditEntries_Users_ChangedBy] FOREIGN KEY ([ChangedBy]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409050950_AddTicketAuditHistory'
)
BEGIN
    CREATE TABLE [TicketAuditFieldChanges] (
        [Id] int NOT NULL IDENTITY,
        [TicketAuditEntryId] int NOT NULL,
        [FieldName] nvarchar(100) NOT NULL,
        [OldValue] nvarchar(max) NULL,
        [NewValue] nvarchar(max) NULL,
        CONSTRAINT [PK_TicketAuditFieldChanges] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TicketAuditFieldChanges_TicketAuditEntries_TicketAuditEntryId] FOREIGN KEY ([TicketAuditEntryId]) REFERENCES [TicketAuditEntries] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409050950_AddTicketAuditHistory'
)
BEGIN
    CREATE INDEX [IX_TicketAuditEntries_ChangedBy] ON [TicketAuditEntries] ([ChangedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409050950_AddTicketAuditHistory'
)
BEGIN
    CREATE INDEX [IX_TicketAuditEntries_TicketId_ChangedDateUtc] ON [TicketAuditEntries] ([TicketId], [ChangedDateUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409050950_AddTicketAuditHistory'
)
BEGIN
    CREATE INDEX [IX_TicketAuditFieldChanges_TicketAuditEntryId] ON [TicketAuditFieldChanges] ([TicketAuditEntryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409050950_AddTicketAuditHistory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260409050950_AddTicketAuditHistory', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409052557_AddSessionConfiguration'
)
BEGIN
    CREATE TABLE [SessionConfigurations] (
        [Id] int NOT NULL IDENTITY,
        [InactivityTimeoutMinutes] int NOT NULL,
        [WarningMinutes] int NOT NULL,
        CONSTRAINT [PK_SessionConfigurations] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409052557_AddSessionConfiguration'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260409052557_AddSessionConfiguration', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409053225_AddUserPresenceTracking'
)
BEGIN
    ALTER TABLE [Users] ADD [LastSeenDateUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409053225_AddUserPresenceTracking'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260409053225_AddUserPresenceTracking', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409053838_AddReportDefinitions'
)
BEGIN
    CREATE TABLE [ReportDefinitions] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(120) NOT NULL,
        [Description] nvarchar(500) NULL,
        [SqlQuery] nvarchar(max) NOT NULL,
        [IsEnabled] bit NOT NULL,
        [CreatedDateUtc] datetime2 NOT NULL,
        [LastModifiedDateUtc] datetime2 NULL,
        CONSTRAINT [PK_ReportDefinitions] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409053838_AddReportDefinitions'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReportDefinitions_Name] ON [ReportDefinitions] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409053838_AddReportDefinitions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260409053838_AddReportDefinitions', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409061509_AddTicketStatusesAndArchiveStatusSelections'
)
BEGIN
    ALTER TABLE [ArchiveConfigurations] ADD [EligibleStatusesJson] nvarchar(max) NOT NULL DEFAULT N'[]';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409061509_AddTicketStatusesAndArchiveStatusSelections'
)
BEGIN
    UPDATE [ArchiveConfigurations]
    SET [EligibleStatusesJson] =
        CASE
            WHEN [ArchiveResolvedTickets] = 1 AND [ArchiveClosedTickets] = 1 THEN N'["Resolved","Closed"]'
            WHEN [ArchiveResolvedTickets] = 1 THEN N'["Resolved"]'
            WHEN [ArchiveClosedTickets] = 1 THEN N'["Closed"]'
            ELSE N'[]'
        END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409061509_AddTicketStatusesAndArchiveStatusSelections'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ArchiveConfigurations]') AND [c].[name] = N'ArchiveClosedTickets');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [ArchiveConfigurations] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [ArchiveConfigurations] DROP COLUMN [ArchiveClosedTickets];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409061509_AddTicketStatusesAndArchiveStatusSelections'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ArchiveConfigurations]') AND [c].[name] = N'ArchiveResolvedTickets');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [ArchiveConfigurations] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [ArchiveConfigurations] DROP COLUMN [ArchiveResolvedTickets];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409061509_AddTicketStatusesAndArchiveStatusSelections'
)
BEGIN
    CREATE TABLE [TicketStatusDefinitions] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsEnabled] bit NOT NULL,
        [CreatedDateUtc] datetime2 NOT NULL,
        [LastModifiedDateUtc] datetime2 NULL,
        CONSTRAINT [PK_TicketStatusDefinitions] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409061509_AddTicketStatusesAndArchiveStatusSelections'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TicketStatusDefinitions_Name] ON [TicketStatusDefinitions] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409061509_AddTicketStatusesAndArchiveStatusSelections'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'Description', N'IsEnabled', N'CreatedDateUtc', N'LastModifiedDateUtc') AND [object_id] = OBJECT_ID(N'[TicketStatusDefinitions]'))
        SET IDENTITY_INSERT [TicketStatusDefinitions] ON;
    EXEC(N'INSERT INTO [TicketStatusDefinitions] ([Id], [Name], [Description], [IsEnabled], [CreatedDateUtc], [LastModifiedDateUtc])
    VALUES (1, N''New'', N''Recently created work waiting to be picked up.'', CAST(1 AS bit), ''2026-04-09T06:15:09.0000000Z'', NULL),
    (2, N''In Progress'', N''Active work currently being handled.'', CAST(1 AS bit), ''2026-04-09T06:15:09.0000000Z'', NULL),
    (3, N''Pending Business Review'', N''Waiting for business validation or feedback.'', CAST(1 AS bit), ''2026-04-09T06:15:09.0000000Z'', NULL),
    (4, N''Resolved'', N''Technical work is complete and ready for closure or archive.'', CAST(1 AS bit), ''2026-04-09T06:15:09.0000000Z'', NULL),
    (5, N''Closed'', N''Ticket has been completed and fully closed out.'', CAST(1 AS bit), ''2026-04-09T06:15:09.0000000Z'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'Description', N'IsEnabled', N'CreatedDateUtc', N'LastModifiedDateUtc') AND [object_id] = OBJECT_ID(N'[TicketStatusDefinitions]'))
        SET IDENTITY_INSERT [TicketStatusDefinitions] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409061509_AddTicketStatusesAndArchiveStatusSelections'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260409061509_AddTicketStatusesAndArchiveStatusSelections', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409204207_AddDatabaseBackedReportsAndStoredProcedures'
)
BEGIN
    ALTER TABLE [StoredProcedureDefinitions] ADD [DefinitionSql] nvarchar(max) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409204207_AddDatabaseBackedReportsAndStoredProcedures'
)
BEGIN
    ALTER TABLE [ReportDefinitions] ADD [ViewName] nvarchar(256) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409204207_AddDatabaseBackedReportsAndStoredProcedures'
)
BEGIN
    UPDATE [ReportDefinitions]
    SET [ViewName] = CONCAT(N'dbo.vw_CortexReport_', CAST([Id] AS nvarchar(20)))
    WHERE [ViewName] IS NULL OR LTRIM(RTRIM([ViewName])) = N''
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409204207_AddDatabaseBackedReportsAndStoredProcedures'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReportDefinitions_ViewName] ON [ReportDefinitions] ([ViewName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409204207_AddDatabaseBackedReportsAndStoredProcedures'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260409204207_AddDatabaseBackedReportsAndStoredProcedures', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260410175953_AddArchivedTicketChildrenAndArchiveAutomation'
)
BEGIN
    CREATE TABLE [ArchivedComments] (
        [Id] int NOT NULL IDENTITY,
        [TicketId] nvarchar(450) NOT NULL,
        [OriginalCommentId] int NULL,
        [Body] nvarchar(max) NOT NULL,
        [CreatedBy] int NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [LastModifiedDate] datetime2 NOT NULL,
        CONSTRAINT [PK_ArchivedComments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ArchivedComments_ArchivedTickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [ArchivedTickets] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ArchivedComments_Users_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260410175953_AddArchivedTicketChildrenAndArchiveAutomation'
)
BEGIN
    CREATE TABLE [ArchivedTicketAttachments] (
        [Id] int NOT NULL IDENTITY,
        [TicketId] nvarchar(450) NOT NULL,
        [OriginalAttachmentId] int NULL,
        [FileName] nvarchar(260) NOT NULL,
        [ContentType] nvarchar(200) NOT NULL,
        [FileSize] bigint NOT NULL,
        [Content] varbinary(max) NOT NULL,
        [UploadedBy] int NOT NULL,
        [UploadedDate] datetime2 NOT NULL,
        CONSTRAINT [PK_ArchivedTicketAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ArchivedTicketAttachments_ArchivedTickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [ArchivedTickets] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ArchivedTicketAttachments_Users_UploadedBy] FOREIGN KEY ([UploadedBy]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260410175953_AddArchivedTicketChildrenAndArchiveAutomation'
)
BEGIN
    CREATE INDEX [IX_ArchivedComments_CreatedBy] ON [ArchivedComments] ([CreatedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260410175953_AddArchivedTicketChildrenAndArchiveAutomation'
)
BEGIN
    CREATE INDEX [IX_ArchivedComments_TicketId] ON [ArchivedComments] ([TicketId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260410175953_AddArchivedTicketChildrenAndArchiveAutomation'
)
BEGIN
    CREATE INDEX [IX_ArchivedTicketAttachments_TicketId] ON [ArchivedTicketAttachments] ([TicketId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260410175953_AddArchivedTicketChildrenAndArchiveAutomation'
)
BEGIN
    CREATE INDEX [IX_ArchivedTicketAttachments_UploadedBy] ON [ArchivedTicketAttachments] ([UploadedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260410175953_AddArchivedTicketChildrenAndArchiveAutomation'
)
BEGIN
    CREATE OR ALTER PROCEDURE dbo.ArchiveTicket
        @TicketId nvarchar(450),
        @ArchivedBy int
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;
        BEGIN TRY
            BEGIN TRANSACTION;
            IF EXISTS (SELECT 1 FROM dbo.ArchivedTickets WHERE Id = @TicketId)
            BEGIN
                THROW 50002, 'Ticket is already archived.', 1;
            END;
            INSERT INTO dbo.ArchivedTickets
            (
                Id,
                Title,
                Description,
                Status,
                Priority,
                SynitiOwner,
                BusinessOwner,
                CreatedBy,
                CreatedDate,
                LastModifiedBy,
                LastModifiedDate,
                ArchivedBy,
                ArchivedDate,
                CommentCount,
                AttachmentCount
            )
            SELECT
                t.Id,
                t.Title,
                t.Description,
                t.Status,
                t.Priority,
                t.SynitiOwner,
                t.BusinessOwner,
                t.CreatedBy,
                t.CreatedDate,
                t.LastModifiedBy,
                t.LastModifiedDate,
                @ArchivedBy,
                SYSUTCDATETIME(),
                (SELECT COUNT(1) FROM dbo.Comments c WHERE c.TicketId = t.Id),
                (SELECT COUNT(1) FROM dbo.TicketAttachments a WHERE a.TicketId = t.Id)
            FROM dbo.Tickets t
            WHERE t.Id = @TicketId;
            IF @@ROWCOUNT = 0
            BEGIN
                THROW 50001, 'Ticket was not found.', 1;
            END;
            INSERT INTO dbo.ArchivedComments
            (
                TicketId,
                OriginalCommentId,
                Body,
                CreatedBy,
                CreatedDate,
                LastModifiedDate
            )
            SELECT
                c.TicketId,
                c.Id,
                c.Body,
                c.CreatedBy,
                c.CreatedDate,
                c.LastModifiedDate
            FROM dbo.Comments c
            WHERE c.TicketId = @TicketId;
            INSERT INTO dbo.ArchivedTicketAttachments
            (
                TicketId,
                OriginalAttachmentId,
                FileName,
                ContentType,
                FileSize,
                Content,
                UploadedBy,
                UploadedDate
            )
            SELECT
                a.TicketId,
                a.Id,
                a.FileName,
                a.ContentType,
                a.FileSize,
                a.Content,
                a.UploadedBy,
                a.UploadedDate
            FROM dbo.TicketAttachments a
            WHERE a.TicketId = @TicketId;
            DELETE FROM dbo.Tickets
            WHERE Id = @TicketId;
            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0
            BEGIN
                ROLLBACK TRANSACTION;
            END;
            THROW;
        END CATCH;
    END;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260410175953_AddArchivedTicketChildrenAndArchiveAutomation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260410175953_AddArchivedTicketChildrenAndArchiveAutomation', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411144339_AddUserNotifications'
)
BEGIN
    CREATE TABLE [UserNotifications] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [TicketId] nvarchar(50) NULL,
        [TicketIsArchived] bit NOT NULL,
        [Category] nvarchar(50) NOT NULL,
        [EventType] nvarchar(50) NOT NULL,
        [Severity] nvarchar(20) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Message] nvarchar(1000) NOT NULL,
        [IsRead] bit NOT NULL,
        [CreatedDateUtc] datetime2 NOT NULL,
        [ReadDateUtc] datetime2 NULL,
        [DeduplicationKey] nvarchar(200) NULL,
        CONSTRAINT [PK_UserNotifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserNotifications_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411144339_AddUserNotifications'
)
BEGIN
    CREATE INDEX [IX_UserNotifications_UserId_CreatedDateUtc] ON [UserNotifications] ([UserId], [CreatedDateUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411144339_AddUserNotifications'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_UserNotifications_UserId_DeduplicationKey] ON [UserNotifications] ([UserId], [DeduplicationKey]) WHERE [DeduplicationKey] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411144339_AddUserNotifications'
)
BEGIN
    CREATE INDEX [IX_UserNotifications_UserId_IsRead_CreatedDateUtc] ON [UserNotifications] ([UserId], [IsRead], [CreatedDateUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411144339_AddUserNotifications'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260411144339_AddUserNotifications', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411224110_AddTicketRoutingRulesFix'
)
BEGIN
    CREATE TABLE [TicketRoutingRules] (
        [Id] int NOT NULL IDENTITY,
        [Department] nvarchar(120) NOT NULL,
        [SynitiOwner] nvarchar(200) NOT NULL,
        [IsEnabled] bit NOT NULL,
        [CreatedDateUtc] datetime2 NOT NULL,
        [LastModifiedDateUtc] datetime2 NULL,
        CONSTRAINT [PK_TicketRoutingRules] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411224110_AddTicketRoutingRulesFix'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TicketRoutingRules_Department] ON [TicketRoutingRules] ([Department]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411224110_AddTicketRoutingRulesFix'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260411224110_AddTicketRoutingRulesFix', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412044155_AddNotificationChannelConfiguration'
)
BEGIN
    CREATE TABLE [NotificationChannelConfigurations] (
        [Id] int NOT NULL IDENTITY,
        [AssignmentChannel] nvarchar(20) NOT NULL,
        [SlaRiskChannel] nvarchar(20) NOT NULL,
        CONSTRAINT [PK_NotificationChannelConfigurations] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412044155_AddNotificationChannelConfiguration'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260412044155_AddNotificationChannelConfiguration', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412045846_AddUserNotificationPreferences'
)
BEGIN
    ALTER TABLE [Users] ADD [AssignmentNotificationChannel] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412045846_AddUserNotificationPreferences'
)
BEGIN
    ALTER TABLE [Users] ADD [SlaRiskNotificationChannel] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412045846_AddUserNotificationPreferences'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260412045846_AddUserNotificationPreferences', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412053620_ExpandTicketRoutingRuleMatching'
)
BEGIN
    DROP INDEX [IX_TicketRoutingRules_Department] ON [TicketRoutingRules];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412053620_ExpandTicketRoutingRuleMatching'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TicketRoutingRules]') AND [c].[name] = N'SynitiOwner');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [TicketRoutingRules] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [TicketRoutingRules] ALTER COLUMN [SynitiOwner] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412053620_ExpandTicketRoutingRuleMatching'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TicketRoutingRules]') AND [c].[name] = N'Department');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [TicketRoutingRules] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [TicketRoutingRules] ALTER COLUMN [Department] nvarchar(120) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412053620_ExpandTicketRoutingRuleMatching'
)
BEGIN
    ALTER TABLE [TicketRoutingRules] ADD [BusinessOwner] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412053620_ExpandTicketRoutingRuleMatching'
)
BEGIN
    ALTER TABLE [TicketRoutingRules] ADD [TitleContains] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412053620_ExpandTicketRoutingRuleMatching'
)
BEGIN
    CREATE INDEX [IX_TicketRoutingRules_Department] ON [TicketRoutingRules] ([Department]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412053620_ExpandTicketRoutingRuleMatching'
)
BEGIN
    CREATE INDEX [IX_TicketRoutingRules_Department_TitleContains] ON [TicketRoutingRules] ([Department], [TitleContains]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412053620_ExpandTicketRoutingRuleMatching'
)
BEGIN
    CREATE INDEX [IX_TicketRoutingRules_TitleContains] ON [TicketRoutingRules] ([TitleContains]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412053620_ExpandTicketRoutingRuleMatching'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260412053620_ExpandTicketRoutingRuleMatching', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412210858_AddTicketBoardsAndStoryPoints'
)
BEGIN
    CREATE TABLE [TicketBoardDefinitions] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [RequiresStoryPoints] bit NOT NULL,
        [IsEnabled] bit NOT NULL,
        [CreatedDateUtc] datetime2 NOT NULL,
        [LastModifiedDateUtc] datetime2 NULL,
        CONSTRAINT [PK_TicketBoardDefinitions] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412210858_AddTicketBoardsAndStoryPoints'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'Description', N'RequiresStoryPoints', N'IsEnabled', N'CreatedDateUtc', N'LastModifiedDateUtc') AND [object_id] = OBJECT_ID(N'[TicketBoardDefinitions]'))
        SET IDENTITY_INSERT [TicketBoardDefinitions] ON;
    EXEC(N'INSERT INTO [TicketBoardDefinitions] ([Id], [Name], [Description], [RequiresStoryPoints], [IsEnabled], [CreatedDateUtc], [LastModifiedDateUtc])
    VALUES (1, N''Ticket'', N''Standard operational ticket board.'', CAST(0 AS bit), CAST(1 AS bit), ''2026-04-12T00:00:00.0000000Z'', NULL),
    (2, N''Hypercare'', N''High-touch stabilization and production support work.'', CAST(0 AS bit), CAST(1 AS bit), ''2026-04-12T00:00:00.0000000Z'', NULL),
    (3, N''Enhancement'', N''Planned improvements and backlog work.'', CAST(1 AS bit), CAST(1 AS bit), ''2026-04-12T00:00:00.0000000Z'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'Description', N'RequiresStoryPoints', N'IsEnabled', N'CreatedDateUtc', N'LastModifiedDateUtc') AND [object_id] = OBJECT_ID(N'[TicketBoardDefinitions]'))
        SET IDENTITY_INSERT [TicketBoardDefinitions] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412210858_AddTicketBoardsAndStoryPoints'
)
BEGIN
    ALTER TABLE [Tickets] ADD [BoardId] int NOT NULL DEFAULT 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412210858_AddTicketBoardsAndStoryPoints'
)
BEGIN
    ALTER TABLE [Tickets] ADD [StoryPoints] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412210858_AddTicketBoardsAndStoryPoints'
)
BEGIN
    ALTER TABLE [ArchivedTickets] ADD [BoardId] int NOT NULL DEFAULT 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412210858_AddTicketBoardsAndStoryPoints'
)
BEGIN
    ALTER TABLE [ArchivedTickets] ADD [StoryPoints] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412210858_AddTicketBoardsAndStoryPoints'
)
BEGIN
    UPDATE dbo.Tickets
    SET BoardId = 1
    WHERE BoardId IS NULL OR BoardId = 0;
    UPDATE dbo.ArchivedTickets
    SET BoardId = 1
    WHERE BoardId IS NULL OR BoardId = 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412210858_AddTicketBoardsAndStoryPoints'
)
BEGIN
    CREATE INDEX [IX_Tickets_BoardId] ON [Tickets] ([BoardId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412210858_AddTicketBoardsAndStoryPoints'
)
BEGIN
    CREATE INDEX [IX_ArchivedTickets_BoardId] ON [ArchivedTickets] ([BoardId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412210858_AddTicketBoardsAndStoryPoints'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TicketBoardDefinitions_Name] ON [TicketBoardDefinitions] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412210858_AddTicketBoardsAndStoryPoints'
)
BEGIN
    ALTER TABLE [ArchivedTickets] ADD CONSTRAINT [FK_ArchivedTickets_TicketBoardDefinitions_BoardId] FOREIGN KEY ([BoardId]) REFERENCES [TicketBoardDefinitions] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412210858_AddTicketBoardsAndStoryPoints'
)
BEGIN
    ALTER TABLE [Tickets] ADD CONSTRAINT [FK_Tickets_TicketBoardDefinitions_BoardId] FOREIGN KEY ([BoardId]) REFERENCES [TicketBoardDefinitions] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412210858_AddTicketBoardsAndStoryPoints'
)
BEGIN
    CREATE OR ALTER PROCEDURE dbo.ArchiveTicket
        @TicketId nvarchar(450),
        @ArchivedBy int
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;
        BEGIN TRY
            BEGIN TRANSACTION;
            IF EXISTS (SELECT 1 FROM dbo.ArchivedTickets WHERE Id = @TicketId)
            BEGIN
                THROW 50002, 'Ticket is already archived.', 1;
            END;
            INSERT INTO dbo.ArchivedTickets
            (
                Id,
                Title,
                Description,
                Status,
                Priority,
                BoardId,
                StoryPoints,
                SynitiOwner,
                BusinessOwner,
                CreatedBy,
                CreatedDate,
                LastModifiedBy,
                LastModifiedDate,
                ArchivedBy,
                ArchivedDate,
                CommentCount,
                AttachmentCount
            )
            SELECT
                t.Id,
                t.Title,
                t.Description,
                t.Status,
                t.Priority,
                ISNULL(NULLIF(t.BoardId, 0), 1),
                t.StoryPoints,
                t.SynitiOwner,
                t.BusinessOwner,
                t.CreatedBy,
                t.CreatedDate,
                t.LastModifiedBy,
                t.LastModifiedDate,
                @ArchivedBy,
                SYSUTCDATETIME(),
                (SELECT COUNT(1) FROM dbo.Comments c WHERE c.TicketId = t.Id),
                (SELECT COUNT(1) FROM dbo.TicketAttachments a WHERE a.TicketId = t.Id)
            FROM dbo.Tickets t
            WHERE t.Id = @TicketId;
            IF @@ROWCOUNT = 0
            BEGIN
                THROW 50001, 'Ticket was not found.', 1;
            END;
            INSERT INTO dbo.ArchivedComments
            (
                TicketId,
                OriginalCommentId,
                Body,
                CreatedBy,
                CreatedDate,
                LastModifiedDate
            )
            SELECT
                c.TicketId,
                c.Id,
                c.Body,
                c.CreatedBy,
                c.CreatedDate,
                c.LastModifiedDate
            FROM dbo.Comments c
            WHERE c.TicketId = @TicketId;
            INSERT INTO dbo.ArchivedTicketAttachments
            (
                TicketId,
                OriginalAttachmentId,
                FileName,
                ContentType,
                FileSize,
                Content,
                UploadedBy,
                UploadedDate
            )
            SELECT
                a.TicketId,
                a.Id,
                a.FileName,
                a.ContentType,
                a.FileSize,
                a.Content,
                a.UploadedBy,
                a.UploadedDate
            FROM dbo.TicketAttachments a
            WHERE a.TicketId = @TicketId;
            DELETE FROM dbo.Tickets
            WHERE Id = @TicketId;
            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0
            BEGIN
                ROLLBACK TRANSACTION;
            END;
            THROW;
        END CATCH;
    END;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260412210858_AddTicketBoardsAndStoryPoints'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260412210858_AddTicketBoardsAndStoryPoints', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414023420_AddHttpRequestLogEntries'
)
BEGIN
    CREATE TABLE [HttpRequestLogEntries] (
        [Id] bigint NOT NULL IDENTITY,
        [OccurredUtc] datetime2 NOT NULL,
        [Method] nvarchar(16) NOT NULL,
        [Path] nvarchar(2048) NOT NULL,
        [StatusCode] int NOT NULL,
        [DurationMs] float NOT NULL,
        [TraceId] nvarchar(128) NOT NULL,
        [IsAuthenticated] bit NOT NULL,
        CONSTRAINT [PK_HttpRequestLogEntries] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414023420_AddHttpRequestLogEntries'
)
BEGIN
    CREATE INDEX [IX_HttpRequestLogEntries_OccurredUtc] ON [HttpRequestLogEntries] ([OccurredUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414023420_AddHttpRequestLogEntries'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260414023420_AddHttpRequestLogEntries', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414043836_RoleAuthorizationUserAdmin'
)
BEGIN
    UPDATE Users SET Role = N'User' WHERE Role IN (N'Guest', N'Manager');
    UPDATE Users SET Role = N'User' WHERE Role NOT IN (N'User', N'Admin');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414043836_RoleAuthorizationUserAdmin'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'Role');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var8 + '];');
    ALTER TABLE [Users] ALTER COLUMN [Role] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414043836_RoleAuthorizationUserAdmin'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260414043836_RoleAuthorizationUserAdmin', N'8.0.1');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415031204_EnsureArchiveTicketPersistsBoardAndStoryPoints'
)
BEGIN
    CREATE OR ALTER PROCEDURE dbo.ArchiveTicket
        @TicketId nvarchar(450),
        @ArchivedBy int
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;
        BEGIN TRY
            BEGIN TRANSACTION;
            IF EXISTS (SELECT 1 FROM dbo.ArchivedTickets WHERE Id = @TicketId)
            BEGIN
                THROW 50002, 'Ticket is already archived.', 1;
            END;
            INSERT INTO dbo.ArchivedTickets
            (
                Id,
                Title,
                Description,
                Status,
                Priority,
                BoardId,
                StoryPoints,
                SynitiOwner,
                BusinessOwner,
                CreatedBy,
                CreatedDate,
                LastModifiedBy,
                LastModifiedDate,
                ArchivedBy,
                ArchivedDate,
                CommentCount,
                AttachmentCount
            )
            SELECT
                t.Id,
                t.Title,
                t.Description,
                t.Status,
                t.Priority,
                t.BoardId,
                t.StoryPoints,
                t.SynitiOwner,
                t.BusinessOwner,
                t.CreatedBy,
                t.CreatedDate,
                t.LastModifiedBy,
                t.LastModifiedDate,
                @ArchivedBy,
                SYSUTCDATETIME(),
                (SELECT COUNT(1) FROM dbo.Comments c WHERE c.TicketId = t.Id),
                (SELECT COUNT(1) FROM dbo.TicketAttachments a WHERE a.TicketId = t.Id)
            FROM dbo.Tickets t
            WHERE t.Id = @TicketId;
            IF @@ROWCOUNT = 0
            BEGIN
                THROW 50001, 'Ticket was not found.', 1;
            END;
            INSERT INTO dbo.ArchivedComments
            (
                TicketId,
                OriginalCommentId,
                Body,
                CreatedBy,
                CreatedDate,
                LastModifiedDate
            )
            SELECT
                c.TicketId,
                c.Id,
                c.Body,
                c.CreatedBy,
                c.CreatedDate,
                c.LastModifiedDate
            FROM dbo.Comments c
            WHERE c.TicketId = @TicketId;
            INSERT INTO dbo.ArchivedTicketAttachments
            (
                TicketId,
                OriginalAttachmentId,
                FileName,
                ContentType,
                FileSize,
                Content,
                UploadedBy,
                UploadedDate
            )
            SELECT
                a.TicketId,
                a.Id,
                a.FileName,
                a.ContentType,
                a.FileSize,
                a.Content,
                a.UploadedBy,
                a.UploadedDate
            FROM dbo.TicketAttachments a
            WHERE a.TicketId = @TicketId;
            DELETE FROM dbo.Tickets
            WHERE Id = @TicketId;
            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0
            BEGIN
                ROLLBACK TRANSACTION;
            END;
            THROW;
        END CATCH;
    END;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415031204_EnsureArchiveTicketPersistsBoardAndStoryPoints'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260415031204_EnsureArchiveTicketPersistsBoardAndStoryPoints', N'8.0.1');
END;
GO

COMMIT;
GO

