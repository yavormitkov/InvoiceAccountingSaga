-- Ensure Financial database exists before applying schema objects
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'FinancialDb')
BEGIN
    CREATE DATABASE FinancialDb;
END;
GO

USE FinancialDb;
GO

SET ANSI_NULLS ON; 
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;

-- Create mirrored Bailiffs table with strict verification
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Bailiffs' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.Bailiffs (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        LicenseNumber VARCHAR(50) NOT NULL,
        Name NVARCHAR(100) NOT NULL
    );

    CREATE UNIQUE NONCLUSTERED INDEX IX_Bailiffs_LicenseNumber ON dbo.Bailiffs (LicenseNumber);
END;

-- Create UserBalances table with matched types (UNIQUEIDENTIFIER)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserBalances' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.UserBalances (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        UserIdentifier UNIQUEIDENTIFIER NOT NULL, -- Matched to Bailiffs.Id type exactly
        InvoiceCount INT NOT NULL CONSTRAINT DF_UserBalances_InvoiceCount DEFAULT (0),
        Balance DECIMAL(18, 4) NOT NULL CONSTRAINT DF_UserBalances_Balance DEFAULT (0.0000),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT UK_UserBalances_UserIdentifier UNIQUE (UserIdentifier),
		CONSTRAINT FK_UserBalances_Bailiffs FOREIGN KEY (UserIdentifier) REFERENCES dbo.Bailiffs (Id)
    );
END;

-- Create Consumer Idempotency table for at-least-once RabbitMQ message deduplication
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProcessedInvoices' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.ProcessedInvoices (
        InvoiceId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        ProcessedAt DATETIME2 NOT NULL
    );
END;

-- Populate mirrored Bailiffs with strict existence check parameters
IF NOT EXISTS (SELECT 1 FROM dbo.Bailiffs WHERE Id = 'a3b1c2d3-e4f5-6789-abcd-0123456789ab')
BEGIN
    INSERT INTO dbo.Bailiffs (Id, LicenseNumber, Name) 
    VALUES ('a3b1c2d3-e4f5-6789-abcd-0123456789ab', 'B-001', N'Bailiff Sofia');
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Bailiffs WHERE Id = 'b4c5d6e7-f890-1234-bcde-0987654321ff')
BEGIN
    INSERT INTO dbo.Bailiffs (Id, LicenseNumber, Name) 
    VALUES ('b4c5d6e7-f890-1234-bcde-0987654321ff', 'B-002', N'Bailiff Plovdiv');
END;

-- Seed initial zero balances
IF NOT EXISTS (SELECT 1 FROM dbo.UserBalances WHERE UserIdentifier = 'a3b1c2d3-e4f5-6789-abcd-0123456789ab')
BEGIN
    INSERT INTO dbo.UserBalances (Id, UserIdentifier, InvoiceCount, Balance)
    VALUES (NEWID(), 'a3b1c2d3-e4f5-6789-abcd-0123456789ab', 0, 0.0000);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.UserBalances WHERE UserIdentifier = 'b4c5d6e7-f890-1234-bcde-0987654321ff')
BEGIN
    INSERT INTO dbo.UserBalances (Id, UserIdentifier, InvoiceCount, Balance)
    VALUES (NEWID(), 'b4c5d6e7-f890-1234-bcde-0987654321ff', 0, 0.0000);
END;


COMMIT TRANSACTION;
