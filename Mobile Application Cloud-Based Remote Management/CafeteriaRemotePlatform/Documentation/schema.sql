CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE TABLE "AuditLogs" (
        "Id" uuid NOT NULL,
        "TenantId" uuid,
        "UserId" uuid,
        "InstallationId" uuid,
        "Action" character varying(120) NOT NULL,
        "EntityType" character varying(80) NOT NULL,
        "EntityId" character varying(100),
        "DetailsJson" text,
        "IpAddress" character varying(64),
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_AuditLogs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE TABLE "Tenants" (
        "Id" uuid NOT NULL,
        "Name" character varying(160) NOT NULL,
        "Slug" character varying(80) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Tenants" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE TABLE "Users" (
        "Id" uuid NOT NULL,
        "Email" character varying(320) NOT NULL,
        "NormalizedEmail" character varying(320) NOT NULL,
        "DisplayName" character varying(120) NOT NULL,
        "PasswordHash" character varying(512) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "LastLoginAt" timestamp with time zone,
        CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE TABLE "InstallationClaimCodes" (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "CodeHash" character varying(128) NOT NULL,
        "CreatedByUserId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "UsedAt" timestamp with time zone,
        "CreatedInstallationId" uuid,
        CONSTRAINT "PK_InstallationClaimCodes" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_InstallationClaimCodes_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE TABLE "Installations" (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "Name" character varying(160) NOT NULL,
        "MachineName" character varying(160) NOT NULL,
        "AppVersion" character varying(64) NOT NULL,
        "SecretHash" character varying(128) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "LastSeenAt" timestamp with time zone,
        "IsActive" boolean NOT NULL,
        CONSTRAINT "PK_Installations" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Installations_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE TABLE "Invitations" (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "Email" character varying(320) NOT NULL,
        "NormalizedEmail" character varying(320) NOT NULL,
        "Role" character varying(20) NOT NULL,
        "TokenHash" character varying(128) NOT NULL,
        "InvitedByUserId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "AcceptedAt" timestamp with time zone,
        CONSTRAINT "PK_Invitations" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Invitations_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE TABLE "DeviceTokens" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "ExpoPushToken" character varying(300) NOT NULL,
        "Platform" character varying(40) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "LastSeenAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_DeviceTokens" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_DeviceTokens_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE TABLE "Notifications" (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "InstallationId" uuid,
        "Title" character varying(200) NOT NULL,
        "Body" character varying(1000) NOT NULL,
        "Type" character varying(40) NOT NULL,
        "IsRead" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Notifications" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Notifications_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_Notifications_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE TABLE "RefreshTokens" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "TokenHash" character varying(128) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "RevokedAt" timestamp with time zone,
        "ReplacedByTokenHash" text,
        CONSTRAINT "PK_RefreshTokens" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_RefreshTokens_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE TABLE "TenantMemberships" (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Role" character varying(20) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_TenantMemberships" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TenantMemberships_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_TenantMemberships_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE TABLE "BusinessEvents" (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "InstallationId" uuid NOT NULL,
        "EventKey" character varying(100) NOT NULL,
        "Type" character varying(100) NOT NULL,
        "PayloadJson" text NOT NULL,
        "OccurredAt" timestamp with time zone NOT NULL,
        "ReceivedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_BusinessEvents" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_BusinessEvents_Installations_InstallationId" FOREIGN KEY ("InstallationId") REFERENCES "Installations" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_BusinessEvents_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE TABLE "InstallationAccess" (
        "Id" uuid NOT NULL,
        "InstallationId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "AccessLevel" character varying(20) NOT NULL,
        "CanViewReports" boolean NOT NULL,
        "CanManageUsers" boolean NOT NULL,
        "CanAdjustInventory" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_InstallationAccess" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_InstallationAccess_Installations_InstallationId" FOREIGN KEY ("InstallationId") REFERENCES "Installations" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_InstallationAccess_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE TABLE "RemoteCommands" (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "InstallationId" uuid NOT NULL,
        "RequestedByUserId" uuid NOT NULL,
        "Type" character varying(80) NOT NULL,
        "PayloadJson" text NOT NULL,
        "RequiredPermission" character varying(80) NOT NULL,
        "Status" character varying(20) NOT NULL,
        "IdempotencyKey" character varying(100) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "DispatchedAt" timestamp with time zone,
        "StartedAt" timestamp with time zone,
        "CompletedAt" timestamp with time zone,
        "ResultJson" text,
        "Error" text,
        CONSTRAINT "PK_RemoteCommands" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_RemoteCommands_Installations_InstallationId" FOREIGN KEY ("InstallationId") REFERENCES "Installations" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_RemoteCommands_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_RemoteCommands_Users_RequestedByUserId" FOREIGN KEY ("RequestedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE INDEX "IX_AuditLogs_TenantId_CreatedAt" ON "AuditLogs" ("TenantId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_BusinessEvents_InstallationId_EventKey" ON "BusinessEvents" ("InstallationId", "EventKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE INDEX "IX_BusinessEvents_TenantId_OccurredAt" ON "BusinessEvents" ("TenantId", "OccurredAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_DeviceTokens_ExpoPushToken" ON "DeviceTokens" ("ExpoPushToken");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE INDEX "IX_DeviceTokens_UserId" ON "DeviceTokens" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_InstallationAccess_InstallationId_UserId" ON "InstallationAccess" ("InstallationId", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE INDEX "IX_InstallationAccess_UserId" ON "InstallationAccess" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_InstallationClaimCodes_CodeHash" ON "InstallationClaimCodes" ("CodeHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE INDEX "IX_InstallationClaimCodes_TenantId" ON "InstallationClaimCodes" ("TenantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE INDEX "IX_Installations_TenantId_Name" ON "Installations" ("TenantId", "Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE INDEX "IX_Invitations_TenantId_NormalizedEmail" ON "Invitations" ("TenantId", "NormalizedEmail");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_Invitations_TokenHash" ON "Invitations" ("TokenHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE INDEX "IX_Notifications_TenantId" ON "Notifications" ("TenantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE INDEX "IX_Notifications_UserId_TenantId_IsRead_CreatedAt" ON "Notifications" ("UserId", "TenantId", "IsRead", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_RefreshTokens_TokenHash" ON "RefreshTokens" ("TokenHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE INDEX "IX_RefreshTokens_UserId" ON "RefreshTokens" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE INDEX "IX_RemoteCommands_InstallationId_Status_ExpiresAt" ON "RemoteCommands" ("InstallationId", "Status", "ExpiresAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE INDEX "IX_RemoteCommands_RequestedByUserId" ON "RemoteCommands" ("RequestedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_RemoteCommands_TenantId_InstallationId_RequestedByUserId_Id~" ON "RemoteCommands" ("TenantId", "InstallationId", "RequestedByUserId", "IdempotencyKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_TenantMemberships_TenantId_UserId" ON "TenantMemberships" ("TenantId", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE INDEX "IX_TenantMemberships_UserId" ON "TenantMemberships" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_Tenants_Slug" ON "Tenants" ("Slug");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_Users_NormalizedEmail" ON "Users" ("NormalizedEmail");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115094130_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261115094130_InitialCreate', '10.0.15');
    END IF;
END $EF$;
COMMIT;

