-- NamClient schema (SQL Server 2016 SP1+). Matches the EF Core migrations, which the app applies on startup.

IF DB_ID(N'NamClientDb') IS NULL
    CREATE DATABASE NamClientDb;
GO

USE NamClientDb;
GO

CREATE TABLE dbo.Clients
(
    ClientId   INT IDENTITY(1, 1) NOT NULL,
    Name       NVARCHAR(100)      NOT NULL,
    ClientCode CHAR(6)            NOT NULL,
    CreatedAt  DATETIME2(0)       NOT NULL CONSTRAINT DF_Clients_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt  DATETIME2(0)       NOT NULL CONSTRAINT DF_Clients_UpdatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_Clients PRIMARY KEY CLUSTERED (ClientId)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX UX_Clients_ClientCode ON dbo.Clients (ClientCode);

-- Covers the client list, ordered and keyset-paged on (Name, ClientId).
CREATE NONCLUSTERED INDEX IX_Clients_Name ON dbo.Clients (Name) INCLUDE (ClientCode);
GO

-- Words of each client name, so a search seeks Word LIKE 'bank%' instead of scanning Name LIKE '%bank%'.
CREATE TABLE dbo.ClientNameWords
(
    Word     NVARCHAR(100) NOT NULL,
    ClientId INT           NOT NULL,

    CONSTRAINT PK_ClientNameWords PRIMARY KEY CLUSTERED (Word, ClientId),
    CONSTRAINT FK_ClientNameWords_Clients_ClientId
        FOREIGN KEY (ClientId) REFERENCES dbo.Clients (ClientId) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX IX_ClientNameWords_ClientId ON dbo.ClientNameWords (ClientId);
GO

CREATE TABLE dbo.Contacts
(
    ContactId INT IDENTITY(1, 1) NOT NULL,
    Name      NVARCHAR(100)      NOT NULL,
    Surname   NVARCHAR(100)      NOT NULL,
    Email     NVARCHAR(256)      NOT NULL,
    CreatedAt DATETIME2(0)       NOT NULL CONSTRAINT DF_Contacts_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2(0)       NOT NULL CONSTRAINT DF_Contacts_UpdatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_Contacts PRIMARY KEY CLUSTERED (ContactId)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX UX_Contacts_Email ON dbo.Contacts (Email);

-- Contact lists are ordered by full name: {Surname} {Name}.
CREATE NONCLUSTERED INDEX IX_Contacts_Surname_Name ON dbo.Contacts (Surname, Name);

CREATE NONCLUSTERED INDEX IX_Contacts_Name ON dbo.Contacts (Name);
GO

-- Many-to-many link; the composite key prevents duplicate links.
CREATE TABLE dbo.ClientContacts
(
    ClientId  INT NOT NULL,
    ContactId INT NOT NULL,

    CONSTRAINT PK_ClientContacts PRIMARY KEY CLUSTERED (ClientId, ContactId),
    CONSTRAINT FK_ClientContacts_Clients_ClientId
        FOREIGN KEY (ClientId) REFERENCES dbo.Clients (ClientId) ON DELETE CASCADE,
    CONSTRAINT FK_ClientContacts_Contacts_ContactId
        FOREIGN KEY (ContactId) REFERENCES dbo.Contacts (ContactId) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX IX_ClientContacts_ContactId ON dbo.ClientContacts (ContactId);
GO
