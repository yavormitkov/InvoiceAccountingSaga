-- Ensure Invoice database exists before applying schema objects
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'CubConnectDb')
BEGIN
    CREATE DATABASE CubConnectDb;
END;
GO

USE CubConnectDb;
GO

SET ANSI_NULLS ON; 
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;

-- Create Bailiffs table with existence validation
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Bailiffs' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.Bailiffs (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        LicenseNumber VARCHAR(50) NOT NULL,
        Name NVARCHAR(100) NOT NULL
    );

    CREATE UNIQUE NONCLUSTERED INDEX IX_Bailiffs_LicenseNumber ON dbo.Bailiffs (LicenseNumber);
END;

-- Create Invoices table with existence validation
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Invoices' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.Invoices (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        InvoiceNumber VARCHAR(50) NOT NULL,
        BailiffId UNIQUEIDENTIFIER NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        RegisteredAt DATETIME2 NOT NULL,
        CONSTRAINT FK_Invoices_Bailiffs FOREIGN KEY (BailiffId) REFERENCES dbo.Bailiffs (Id)
    );

    CREATE UNIQUE NONCLUSTERED INDEX IX_Invoices_InvoiceNumber_BailiffId ON dbo.Invoices (InvoiceNumber, BailiffId);
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OutboxEvents' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.OutboxEvents (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        EventType VARCHAR(100) NOT NULL,
        Payload NVARCHAR(MAX) NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        ProcessedAt DATETIME2 NULL
    );

    CREATE NONCLUSTERED INDEX IX_OutboxEvents_Unprocessed
        ON dbo.OutboxEvents (CreatedAt ASC)
        WHERE ProcessedAt IS NULL;
END;

-- Strict existence checks before inserting seed data
IF NOT EXISTS (SELECT 1 FROM dbo.Bailiffs WHERE Id = 'a3b1c2d3-e4f5-6789-abcd-0123456789ab')
BEGIN
    INSERT INTO dbo.Bailiffs (Id, LicenseNumber, Name) VALUES
    ('a3b1c2d3-e4f5-6789-abcd-0123456789ab', 'B-001', N'Bailiff Sofia');
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Bailiffs WHERE Id = 'b4c5d6e7-f890-1234-bcde-0987654321ff')
BEGIN
    INSERT INTO dbo.Bailiffs (Id, LicenseNumber, Name) VALUES
    ('b4c5d6e7-f890-1234-bcde-0987654321ff', 'B-002', N'Bailiff Plovdiv');
END;

COMMIT TRANSACTION;
