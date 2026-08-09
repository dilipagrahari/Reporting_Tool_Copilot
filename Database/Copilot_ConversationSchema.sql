-- ============================================================
-- WareConnect Copilot – Conversation Persistence Schema
-- Run this script once against Report_SportsmanHotel database.
-- ============================================================

-- ----------------------------------------------------------
-- Table: Copilot_Conversations
-- One row per conversation session.
-- ----------------------------------------------------------

USE Report_SportsmanHotel;

CREATE TABLE [dbo].[Data_2026](
	[RowID] [int] IDENTITY(1,1) NOT NULL,
	[Year] [int] NULL,
	[MYOBAccount] [nvarchar](50) NULL,
	[AccountName] [nvarchar](255) NULL,
	[AccountType] [nvarchar](50) NULL,
	[Amount] [decimal](18, 2) NULL,
	[StartDate] [date] NULL,
	[EndDate] [date] NULL,
	[MonthName] [nvarchar](50) NULL,
	[WeekInMonth] [nvarchar](50) NULL,
	[MonthAmount] [decimal](18, 2) NULL,
	[GroupName] [nvarchar](255) NULL,
	[ItemType] [nvarchar](255) NULL,
	[Sales] [decimal](18, 2) NULL,
	[OtherExp] [decimal](18, 2) NULL,
	[GP2] [decimal](18, 2) NULL,
	[DistinctGP2] [decimal](18, 2) NULL,
	[BudgetAmount] [decimal](18, 2) NULL,
	[LYRBudgetAmount] [decimal](18, 2) NULL,
	[MonthBudgetAmount] [decimal](18, 2) NULL,
	[MonthLYRBudgetAmount] [decimal](18, 2) NULL
) ON [PRIMARY]
GO


IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Copilot_Conversations'
)
BEGIN
    CREATE TABLE [dbo].[Copilot_Conversations] (
        [ConversationId]     NVARCHAR(64)   NOT NULL,
        [UserId]             NVARCHAR(256)  NOT NULL DEFAULT '1',
        [Title]              NVARCHAR(512)  NULL,
        [CurrentPage]        NVARCHAR(256)  NULL,
        [CurrentCompany]     NVARCHAR(256)  NULL,
        [CurrentVendor]      NVARCHAR(256)  NULL,
        [CurrentInvoiceId]   NVARCHAR(128)  NULL,
        [CurrentModule]      NVARCHAR(128)  NULL,
        [Language]           NVARCHAR(16)   NOT NULL DEFAULT 'en',
        [TimeZone]           NVARCHAR(64)   NOT NULL DEFAULT 'UTC',
        [IsActive]           BIT            NOT NULL DEFAULT 1,
        [CreatedAt]          DATETIME2(7)   NOT NULL DEFAULT SYSUTCDATETIME(),
        [LastActivityAt]     DATETIME2(7)   NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_Copilot_Conversations] PRIMARY KEY CLUSTERED ([ConversationId] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_Copilot_Conversations_UserId]
        ON [dbo].[Copilot_Conversations] ([UserId] ASC, [IsActive] ASC)
        INCLUDE ([LastActivityAt]);
END;
GO

-- ----------------------------------------------------------
-- Table: Copilot_Messages
-- One row per message (user / assistant / tool) per conversation.
-- ----------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Copilot_Messages'
)
BEGIN
    CREATE TABLE [dbo].[Copilot_Messages] (
        [MessageId]          BIGINT         NOT NULL IDENTITY(1,1),
        [ConversationId]     NVARCHAR(64)   NOT NULL,
        [Role]               NVARCHAR(32)   NOT NULL,        -- user | assistant | tool | system
        [Content]            NVARCHAR(MAX)  NOT NULL,
        [ToolCallId]         NVARCHAR(128)  NULL,
        [ToolName]           NVARCHAR(256)  NULL,
        [PromptTokens]       INT            NULL,
        [CompletionTokens]   INT            NULL,
        [TotalTokens]        INT            NULL,
        [Model]              NVARCHAR(128)  NULL,
        [LatencyMs]          INT            NULL,
        [CreatedAt]          DATETIME2(7)   NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_Copilot_Messages] PRIMARY KEY CLUSTERED ([MessageId] ASC),
        CONSTRAINT [FK_Copilot_Messages_ConversationId]
            FOREIGN KEY ([ConversationId])
            REFERENCES [dbo].[Copilot_Conversations] ([ConversationId])
            ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_Copilot_Messages_ConversationId]
        ON [dbo].[Copilot_Messages] ([ConversationId] ASC, [CreatedAt] ASC)
        INCLUDE ([Role], [Content]);
END;
GO

-- ----------------------------------------------------------
-- Table: Copilot_UsageLog
-- Detailed token / cost audit trail per conversation turn.
-- ----------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Copilot_UsageLog'
)
BEGIN
    CREATE TABLE [dbo].[Copilot_UsageLog] (
        [LogId]              BIGINT         NOT NULL IDENTITY(1,1),
        [ConversationId]     NVARCHAR(64)   NOT NULL,
        [UserId]             NVARCHAR(256)  NOT NULL DEFAULT '1',
        [Model]              NVARCHAR(128)  NOT NULL,
        [PromptTokens]       INT            NOT NULL DEFAULT 0,
        [CompletionTokens]   INT            NOT NULL DEFAULT 0,
        [TotalTokens]        INT            NOT NULL DEFAULT 0,
        [ToolInvoked]        NVARCHAR(256)  NULL,
        [LatencyMs]          INT            NULL,
        [CreatedAt]          DATETIME2(7)   NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_Copilot_UsageLog] PRIMARY KEY CLUSTERED ([LogId] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_Copilot_UsageLog_ConversationId]
        ON [dbo].[Copilot_UsageLog] ([ConversationId] ASC, [CreatedAt] ASC);
END;
GO
