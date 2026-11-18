namespace Invoicing.Api.Commands;

public static class SqlStatements
{
    public const string RegisterInvoiceSql = @"
        SET XACT_ABORT ON;
        SET NOCOUNT ON;

        DECLARE @BailiffId UNIQUEIDENTIFIER = NULL;
        DECLARE @ExistingInvoiceId UNIQUEIDENTIFIER = NULL;
        DECLARE @Now DATETIME2 = SYSUTCDATETIME();

        BEGIN TRANSACTION;

        BEGIN TRY
            -- Bailiffs reference table is seed-managed; read Id by natural key
            SELECT @BailiffId = Id
            FROM dbo.Bailiffs
            WHERE LicenseNumber = @LicenseNumber;

            IF @BailiffId IS NULL
            BEGIN
                ROLLBACK TRANSACTION;
                SELECT
                    CAST('00000000-0000-0000-0000-000000000000' AS UNIQUEIDENTIFIER) AS InvoiceId,
                    CAST('00000000-0000-0000-0000-000000000000' AS UNIQUEIDENTIFIER) AS BailiffId,
                    1 AS ErrorCode;
                RETURN;
            END;

            SELECT @ExistingInvoiceId = Id 
            FROM dbo.Invoices WITH (UPDLOCK, HOLDLOCK)
            WHERE InvoiceNumber = @InvoiceNumber AND BailiffId = @BailiffId;

            IF @ExistingInvoiceId IS NOT NULL
            BEGIN
                ROLLBACK TRANSACTION;
                SELECT 
                    @ExistingInvoiceId AS InvoiceId, 
                    @BailiffId AS BailiffId, 
                    2 AS ErrorCode;
                RETURN;
            END;

            INSERT INTO dbo.Invoices (Id, InvoiceNumber, BailiffId, Amount, RegisteredAt)
            VALUES (@InvoiceId, @InvoiceNumber, @BailiffId, @Amount, @Now);

            DECLARE @Payload NVARCHAR(MAX) = (
                SELECT
                    @InvoiceId AS InvoiceId,
                    @BailiffId AS BailiffId,
                    @Amount AS Amount
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
            );

            INSERT INTO dbo.OutboxEvents (Id, EventType, Payload, CreatedAt, ProcessedAt)
            VALUES (@OutboxEventId, 'InvoiceRegisteredEvent', @Payload, @Now, NULL);

            COMMIT TRANSACTION;
    
            SELECT 
                @InvoiceId AS InvoiceId, 
                @BailiffId AS BailiffId, 
                0 AS ErrorCode;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0
            BEGIN
                ROLLBACK TRANSACTION;
            END;

            IF ERROR_NUMBER() IN (2601, 2627) AND @BailiffId IS NOT NULL
            BEGIN
                SELECT @ExistingInvoiceId = Id
                FROM dbo.Invoices WITH (UPDLOCK, HOLDLOCK)
                WHERE InvoiceNumber = @InvoiceNumber AND BailiffId = @BailiffId;

                IF @ExistingInvoiceId IS NOT NULL
                BEGIN
                    SELECT
                        @ExistingInvoiceId AS InvoiceId,
                        @BailiffId AS BailiffId,
                        2 AS ErrorCode;
                    RETURN;
                END;
            END;
    
            SELECT 
                CAST('00000000-0000-0000-0000-000000000000' AS UNIQUEIDENTIFIER) AS InvoiceId, 
                ISNULL(@BailiffId, CAST('00000000-0000-0000-0000-000000000000' AS UNIQUEIDENTIFIER)) AS BailiffId, 
                3 AS ErrorCode;
        END CATCH;
    ";

    public const string FetchPendingOutboxEventsSql = @"
        SELECT TOP (50) Id, EventType, Payload
        FROM dbo.OutboxEvents WITH (UPDLOCK, READPAST, ROWLOCK)
        WHERE ProcessedAt IS NULL
        ORDER BY CreatedAt ASC;";

    public const string MarkOutboxEventProcessedSql = @"
        UPDATE dbo.OutboxEvents
        SET ProcessedAt = SYSUTCDATETIME()
        WHERE Id = @Id;";
}
