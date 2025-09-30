-- =====================================================
-- AWS RDS SQL Server Database Setup Script
-- For InboxOutboxPattern Demo
-- =====================================================

-- Create database (if not using existing one)
-- Note: This might need to be run by an admin user
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'InboxOutboxDemo')
BEGIN
    CREATE DATABASE InboxOutboxDemo;
END
GO

USE InboxOutboxDemo;
GO

-- =====================================================
-- Orders Table
-- =====================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Orders' AND xtype='U')
BEGIN
    CREATE TABLE Orders (
        Id int IDENTITY(1,1) PRIMARY KEY,
        CustomerId int NOT NULL,
        ProductName nvarchar(200) NOT NULL,
        Quantity int NOT NULL,
        Price decimal(18,2) NOT NULL,
        CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),
        Status nvarchar(50) NOT NULL DEFAULT 'Created'
    );
    
    -- Index for customer queries
    CREATE INDEX IX_Orders_CustomerId ON Orders(CustomerId);
    CREATE INDEX IX_Orders_CreatedAt ON Orders(CreatedAt);
    
    PRINT 'Orders table created successfully';
END
ELSE
BEGIN
    PRINT 'Orders table already exists';
END
GO

-- =====================================================
-- Outbox Messages Table
-- =====================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='OutboxMessages' AND xtype='U')
BEGIN
    CREATE TABLE OutboxMessages (
        Id int IDENTITY(1,1) PRIMARY KEY,
        EventType nvarchar(100) NOT NULL,
        AggregateId nvarchar(100) NOT NULL,
        Payload nvarchar(max) NOT NULL,
        CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),
        IsProcessed bit NOT NULL DEFAULT 0,
        ProcessedAt datetime2 NULL,
        RetryCount int NOT NULL DEFAULT 0,
        ErrorMessage nvarchar(1000) NULL,
        Exchange nvarchar(100) NOT NULL DEFAULT '',
        RoutingKey nvarchar(100) NOT NULL DEFAULT '',
        CorrelationId nvarchar(100) NULL
    );
    
    -- Critical index for outbox processing performance
    CREATE INDEX IX_OutboxMessages_Processing ON OutboxMessages(IsProcessed, CreatedAt);
    CREATE INDEX IX_OutboxMessages_EventType ON OutboxMessages(EventType);
    CREATE INDEX IX_OutboxMessages_AggregateId ON OutboxMessages(AggregateId);
    CREATE INDEX IX_OutboxMessages_CorrelationId ON OutboxMessages(CorrelationId);
    
    PRINT 'OutboxMessages table created successfully';
END
ELSE
BEGIN
    PRINT 'OutboxMessages table already exists';
END
GO

-- =====================================================
-- Inbox Messages Table
-- =====================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='InboxMessages' AND xtype='U')
BEGIN
    CREATE TABLE InboxMessages (
        Id int IDENTITY(1,1) PRIMARY KEY,
        MessageId nvarchar(100) NOT NULL,
        EventType nvarchar(100) NOT NULL,
        Payload nvarchar(max) NOT NULL,
        ReceivedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),
        IsProcessed bit NOT NULL DEFAULT 0,
        ProcessedAt datetime2 NULL,
        RetryCount int NOT NULL DEFAULT 0,
        ErrorMessage nvarchar(1000) NULL,
        CorrelationId nvarchar(100) NULL,
        SourceExchange nvarchar(100) NULL,
        SourceRoutingKey nvarchar(100) NULL
    );
    
    -- Unique constraint for idempotency
    CREATE UNIQUE INDEX IX_InboxMessages_MessageId ON InboxMessages(MessageId);
    -- Index for efficient processing queries
    CREATE INDEX IX_InboxMessages_Processing ON InboxMessages(IsProcessed, ReceivedAt);
    CREATE INDEX IX_InboxMessages_EventType ON InboxMessages(EventType);
    CREATE INDEX IX_InboxMessages_CorrelationId ON InboxMessages(CorrelationId);
    
    PRINT 'InboxMessages table created successfully';
END
ELSE
BEGIN
    PRINT 'InboxMessages table already exists';
END
GO

-- =====================================================
-- Sample Data (Optional)
-- =====================================================
-- Insert some sample orders for testing
IF NOT EXISTS (SELECT * FROM Orders WHERE CustomerId = 9999)
BEGIN
    INSERT INTO Orders (CustomerId, ProductName, Quantity, Price, Status) VALUES
    (9999, 'Sample Laptop', 1, 999.99, 'Created'),
    (9998, 'Sample Mouse', 2, 25.99, 'Created'),
    (9997, 'Sample Keyboard', 1, 75.50, 'Processing');
    
    PRINT 'Sample orders inserted';
END
GO

-- =====================================================
-- Utility Views for Monitoring
-- =====================================================

-- View for outbox processing status
IF EXISTS (SELECT * FROM sys.views WHERE name = 'v_OutboxProcessingStatus')
    DROP VIEW v_OutboxProcessingStatus;
GO

CREATE VIEW v_OutboxProcessingStatus AS
SELECT 
    EventType,
    IsProcessed,
    COUNT(*) as MessageCount,
    AVG(CAST(RetryCount as float)) as AvgRetryCount,
    MIN(CreatedAt) as OldestMessage,
    MAX(CreatedAt) as NewestMessage,
    SUM(CASE WHEN RetryCount >= 3 AND IsProcessed = 0 THEN 1 ELSE 0 END) as FailedMessages
FROM OutboxMessages
GROUP BY EventType, IsProcessed;
GO

-- View for inbox processing status  
IF EXISTS (SELECT * FROM sys.views WHERE name = 'v_InboxProcessingStatus')
    DROP VIEW v_InboxProcessingStatus;
GO

CREATE VIEW v_InboxProcessingStatus AS
SELECT 
    EventType,
    IsProcessed,
    COUNT(*) as MessageCount,
    AVG(CAST(RetryCount as float)) as AvgRetryCount,
    MIN(ReceivedAt) as OldestMessage,
    MAX(ReceivedAt) as NewestMessage,
    SUM(CASE WHEN RetryCount >= 3 AND IsProcessed = 0 THEN 1 ELSE 0 END) as FailedMessages
FROM InboxMessages
GROUP BY EventType, IsProcessed;
GO

-- =====================================================
-- Stored Procedures for Maintenance
-- =====================================================

-- Procedure to clean up old processed messages
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_CleanupProcessedMessages')
    DROP PROCEDURE sp_CleanupProcessedMessages;
GO

CREATE PROCEDURE sp_CleanupProcessedMessages
    @DaysToKeep int = 30
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @CutoffDate datetime2 = DATEADD(day, -@DaysToKeep, GETUTCDATE());
    DECLARE @DeletedOutbox int, @DeletedInbox int;
    
    -- Clean up old outbox messages
    DELETE FROM OutboxMessages 
    WHERE IsProcessed = 1 AND ProcessedAt < @CutoffDate;
    SET @DeletedOutbox = @@ROWCOUNT;
    
    -- Clean up old inbox messages
    DELETE FROM InboxMessages 
    WHERE IsProcessed = 1 AND ProcessedAt < @CutoffDate;
    SET @DeletedInbox = @@ROWCOUNT;
    
    SELECT 
        @DeletedOutbox as DeletedOutboxMessages,
        @DeletedInbox as DeletedInboxMessages,
        @CutoffDate as CutoffDate;
END
GO

-- Procedure to reset failed messages for retry
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_ResetFailedMessages')
    DROP PROCEDURE sp_ResetFailedMessages;
GO

CREATE PROCEDURE sp_ResetFailedMessages
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @ResetOutbox int, @ResetInbox int;
    
    -- Reset failed outbox messages
    UPDATE OutboxMessages 
    SET RetryCount = 0, ErrorMessage = NULL 
    WHERE IsProcessed = 0 AND RetryCount >= 3;
    SET @ResetOutbox = @@ROWCOUNT;
    
    -- Reset failed inbox messages
    UPDATE InboxMessages 
    SET RetryCount = 0, ErrorMessage = NULL 
    WHERE IsProcessed = 0 AND RetryCount >= 3;
    SET @ResetInbox = @@ROWCOUNT;
    
    SELECT 
        @ResetOutbox as ResetOutboxMessages,
        @ResetInbox as ResetInboxMessages;
END
GO

-- =====================================================
-- Security and Permissions
-- =====================================================

-- Create application user (adjust as needed for your environment)
IF NOT EXISTS (SELECT name FROM sys.sql_logins WHERE name = 'InboxOutboxApp')
BEGIN
    -- Note: In AWS RDS, you might need to use the master user to create this
    -- CREATE LOGIN InboxOutboxApp WITH PASSWORD = 'YourSecurePassword123!';
    PRINT 'Remember to create login InboxOutboxApp with secure password';
END

IF NOT EXISTS (SELECT name FROM sys.database_principals WHERE name = 'InboxOutboxApp')
BEGIN
    -- CREATE USER InboxOutboxApp FOR LOGIN InboxOutboxApp;
    PRINT 'Remember to create user InboxOutboxApp';
END

-- Grant necessary permissions
-- GRANT SELECT, INSERT, UPDATE, DELETE ON Orders TO InboxOutboxApp;
-- GRANT SELECT, INSERT, UPDATE, DELETE ON OutboxMessages TO InboxOutboxApp;
-- GRANT SELECT, INSERT, UPDATE, DELETE ON InboxMessages TO InboxOutboxApp;
-- GRANT SELECT ON v_OutboxProcessingStatus TO InboxOutboxApp;
-- GRANT SELECT ON v_InboxProcessingStatus TO InboxOutboxApp;
-- GRANT EXECUTE ON sp_CleanupProcessedMessages TO InboxOutboxApp;
-- GRANT EXECUTE ON sp_ResetFailedMessages TO InboxOutboxApp;

PRINT '==============================================';
PRINT 'Database setup completed successfully!';
PRINT '==============================================';
PRINT 'Next steps:';
PRINT '1. Create application login and user';
PRINT '2. Grant appropriate permissions';
PRINT '3. Update connection string in App.config';
PRINT '4. Test connection from application';
PRINT '==============================================';

-- Query to check setup
SELECT 
    'Tables Created' as Status,
    COUNT(*) as TableCount
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_NAME IN ('Orders', 'OutboxMessages', 'InboxMessages');