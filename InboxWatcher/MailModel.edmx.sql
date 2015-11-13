
-- --------------------------------------------------
-- Entity Designer DDL Script for SQL Server 2005, 2008, 2012 and Azure
-- --------------------------------------------------
-- Date Created: 11/12/2015 09:04:21
-- Generated from EDMX file: C:\Users\pfuller\documents\visual studio 2015\Projects\InboxWatcher\InboxWatcher\MailModel.edmx
-- --------------------------------------------------

SET QUOTED_IDENTIFIER OFF;
GO
USE [InboxWatcher];
GO
IF SCHEMA_ID(N'dbo') IS NULL EXECUTE(N'CREATE SCHEMA [dbo]');
GO

-- --------------------------------------------------
-- Dropping existing FOREIGN KEY constraints
-- --------------------------------------------------

IF OBJECT_ID(N'[dbo].[FK_EmailLogEntry]', 'F') IS NOT NULL
    ALTER TABLE [dbo].[EmailLogs] DROP CONSTRAINT [FK_EmailLogEntry];
GO

-- --------------------------------------------------
-- Dropping existing tables
-- --------------------------------------------------

IF OBJECT_ID(N'[dbo].[Emails]', 'U') IS NOT NULL
    DROP TABLE [dbo].[Emails];
GO
IF OBJECT_ID(N'[dbo].[EmailLogs]', 'U') IS NOT NULL
    DROP TABLE [dbo].[EmailLogs];
GO
IF OBJECT_ID(N'[dbo].[EmailFilters]', 'U') IS NOT NULL
    DROP TABLE [dbo].[EmailFilters];
GO
IF OBJECT_ID(N'[dbo].[ImapMailBoxConfigurations]', 'U') IS NOT NULL
    DROP TABLE [dbo].[ImapMailBoxConfigurations];
GO

-- --------------------------------------------------
-- Creating all tables
-- --------------------------------------------------

-- Creating table 'Emails'
CREATE TABLE [dbo].[Emails] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [InQueue] bit  NOT NULL,
    [Minutes] int  NOT NULL,
    [Sender] nvarchar(max)  NOT NULL,
    [TimeReceived] datetime  NOT NULL,
    [Subject] nvarchar(max)  NOT NULL,
    [MarkedAsRead] bit  NOT NULL,
    [BodyText] nvarchar(max)  NOT NULL,
    [EnvelopeID] nvarchar(max)  NOT NULL
);
GO

-- Creating table 'EmailLogs'
CREATE TABLE [dbo].[EmailLogs] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [TakenBy] nvarchar(max)  NOT NULL,
    [Action] nvarchar(max)  NOT NULL,
    [TimeActionTaken] datetime  NOT NULL,
    [EmailId] int  NOT NULL
);
GO

-- Creating table 'EmailFilters'
CREATE TABLE [dbo].[EmailFilters] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [FilterName] nvarchar(max)  NOT NULL,
    [SubjectContains] nvarchar(max)  NOT NULL,
    [SentFromContains] nvarchar(max)  NOT NULL,
    [ForwardToAddress] nvarchar(max)  NOT NULL,
    [ForwardThis] bit  NOT NULL,
    [MoveToFolder] nvarchar(max)  NOT NULL
);
GO

-- Creating table 'ImapMailBoxConfigurations'
CREATE TABLE [dbo].[ImapMailBoxConfigurations] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [UserName] nvarchar(max)  NOT NULL,
    [Password] nvarchar(max)  NOT NULL,
    [HostName] nvarchar(max)  NOT NULL,
    [UseSecure] bit  NOT NULL,
    [Port] int  NOT NULL,
    [MailBoxName] nvarchar(max)  NOT NULL
);
GO

-- --------------------------------------------------
-- Creating all PRIMARY KEY constraints
-- --------------------------------------------------

-- Creating primary key on [Id] in table 'Emails'
ALTER TABLE [dbo].[Emails]
ADD CONSTRAINT [PK_Emails]
    PRIMARY KEY CLUSTERED ([Id] ASC);
GO

-- Creating primary key on [Id] in table 'EmailLogs'
ALTER TABLE [dbo].[EmailLogs]
ADD CONSTRAINT [PK_EmailLogs]
    PRIMARY KEY CLUSTERED ([Id] ASC);
GO

-- Creating primary key on [Id] in table 'EmailFilters'
ALTER TABLE [dbo].[EmailFilters]
ADD CONSTRAINT [PK_EmailFilters]
    PRIMARY KEY CLUSTERED ([Id] ASC);
GO

-- Creating primary key on [Id] in table 'ImapMailBoxConfigurations'
ALTER TABLE [dbo].[ImapMailBoxConfigurations]
ADD CONSTRAINT [PK_ImapMailBoxConfigurations]
    PRIMARY KEY CLUSTERED ([Id] ASC);
GO

-- --------------------------------------------------
-- Creating all FOREIGN KEY constraints
-- --------------------------------------------------

-- Creating foreign key on [EmailId] in table 'EmailLogs'
ALTER TABLE [dbo].[EmailLogs]
ADD CONSTRAINT [FK_EmailLogEntry]
    FOREIGN KEY ([EmailId])
    REFERENCES [dbo].[Emails]
        ([Id])
    ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

-- Creating non-clustered index for FOREIGN KEY 'FK_EmailLogEntry'
CREATE INDEX [IX_FK_EmailLogEntry]
ON [dbo].[EmailLogs]
    ([EmailId]);
GO

-- --------------------------------------------------
-- Script has ended
-- --------------------------------------------------