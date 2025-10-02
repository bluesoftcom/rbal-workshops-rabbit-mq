using System;

using System.Threading.Tasks;

using Microsoft.Data.SqlClient;

using System.Configuration;



public class DatabaseConnectionTest
{

    public static async Task Main()

    {    

        Console.WriteLine("🗄️ SQL Server Database Connection Test");
        Console.WriteLine("=====================================");
        Console.WriteLine("Reading connection details from App.config...");
        Console.WriteLine();

        await TestDatabaseConnection();
    }



    private static async Task TestDatabaseConnection()

    { 

        try     
    {
            // Get connection string from App.config         

            string connectionString = ConfigurationManager.ConnectionStrings["SqlServerConnection"]?.ConnectionString;

            if (string.IsNullOrEmpty(connectionString)) 
            {          
                Console.WriteLine("❌ Connection string 'SqlServerConnection' not found in App.config");
                Console.WriteLine("💡 Make sure App.config contains the SqlServerConnection connection string");
                return;
            }

            // Parse connection string to display details

            var builder = new SqlConnectionStringBuilder(connectionString);
            Console.WriteLine($"Server: {builder.DataSource}");

            Console.WriteLine($"Database: {builder.InitialCatalog}");
            Console.WriteLine($"Username: {builder.UserID}");
            Console.WriteLine($"Password: {new string('*', builder.Password?.Length ?? 0)}");
            Console.WriteLine($"Encrypt: {builder.Encrypt}");
            Console.WriteLine($"Trust Server Certificate: {builder.TrustServerCertificate}");
            Console.WriteLine();

            Console.WriteLine("📡 Attempting to connect to SQL Server...");  

            using var connection = new SqlConnection(connectionString);

            // Test connection
            await connection.OpenAsync();

            Console.WriteLine("✅ SUCCESS: Connected to SQL Server!");
            Console.WriteLine($"Connection State: {connection.State}");
            Console.WriteLine($"Server Version: {connection.ServerVersion}");
            Console.WriteLine($"Database: {connection.Database}");
            Console.WriteLine();

            // Create InboxOutbox database and tables
            Console.WriteLine("🗂️ Creating InboxOutbox database and tables...");
            await CreateInboxOutboxDatabase(connection, ConfigurationManager.AppSettings["userName"]);

           } catch (Exception ex)
           {
               Console.WriteLine($"❌ ERROR: {ex.Message}");
           }
       }

    private static async Task CreateInboxOutboxDatabase(SqlConnection connection, string username)
    {
        try
        {
            string databaseName = $"db_{username}_inboxoutbox";
            Console.WriteLine($"📊 Creating database: {databaseName}");

            // Create database
            string createDbQuery = $@"
                IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'{databaseName}')
                BEGIN
                    CREATE DATABASE [{databaseName}];
                    PRINT 'Database {databaseName} created successfully';
                END
                ELSE
                BEGIN
                    PRINT 'Database {databaseName} already exists';
                END";

            using var createDbCommand = new SqlCommand(createDbQuery, connection);
            await createDbCommand.ExecuteNonQueryAsync();
            Console.WriteLine($"✅ Database {databaseName} ready");

            // Switch to the new database
            Console.WriteLine($"🔄 Switching to database: {databaseName}");
            await connection.ChangeDatabaseAsync(databaseName);
            Console.WriteLine($"✅ Connected to database: {databaseName}");

            // Create tb_inbox table
            Console.WriteLine("📥 Creating tb_inbox table...");
            string createInboxTableQuery = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='tb_inbox' AND xtype='U')
                BEGIN
                    CREATE TABLE tb_inbox (
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
                    CREATE UNIQUE INDEX IX_tb_inbox_MessageId ON tb_inbox(MessageId);
                    -- Index for efficient processing queries
                    CREATE INDEX IX_tb_inbox_Processing ON tb_inbox(IsProcessed, ReceivedAt);
                    CREATE INDEX IX_tb_inbox_EventType ON tb_inbox(EventType);
                    CREATE INDEX IX_tb_inbox_CorrelationId ON tb_inbox(CorrelationId);
                    
                    PRINT 'tb_inbox table created successfully';
                END
                ELSE
                BEGIN
                    PRINT 'tb_inbox table already exists';
                END";

            using var createInboxCommand = new SqlCommand(createInboxTableQuery, connection);
            await createInboxCommand.ExecuteNonQueryAsync();
            Console.WriteLine("✅ tb_inbox table ready");

            // Create tb_outbox table
            Console.WriteLine("📤 Creating tb_outbox table...");
            string createOutboxTableQuery = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='tb_outbox' AND xtype='U')
                BEGIN
                    CREATE TABLE tb_outbox (
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
                    CREATE INDEX IX_tb_outbox_Processing ON tb_outbox(IsProcessed, CreatedAt);
                    CREATE INDEX IX_tb_outbox_EventType ON tb_outbox(EventType);
                    CREATE INDEX IX_tb_outbox_AggregateId ON tb_outbox(AggregateId);
                    CREATE INDEX IX_tb_outbox_CorrelationId ON tb_outbox(CorrelationId);
                    
                    PRINT 'tb_outbox table created successfully';
                END
                ELSE
                BEGIN
                    PRINT 'tb_outbox table already exists';
                END";

            using var createOutboxCommand = new SqlCommand(createOutboxTableQuery, connection);
            await createOutboxCommand.ExecuteNonQueryAsync();
            Console.WriteLine("✅ tb_outbox table ready");

            // Verify tables were created
            Console.WriteLine("🔍 Verifying table creation...");
            string verifyTablesQuery = @"
                SELECT TABLE_NAME, TABLE_TYPE
                FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_NAME IN ('tb_inbox', 'tb_outbox')
                ORDER BY TABLE_NAME";

            using var verifyCommand = new SqlCommand(verifyTablesQuery, connection);
            using var reader = await verifyCommand.ExecuteReaderAsync();

            Console.WriteLine("📋 Created tables:");
            while (await reader.ReadAsync())
            {
                Console.WriteLine($"  ✓ {reader["TABLE_NAME"]} ({reader["TABLE_TYPE"]})");
            }

            Console.WriteLine($"\n🎉 InboxOutbox database setup completed successfully!");
            Console.WriteLine($"📍 Database: {databaseName}");
            Console.WriteLine($"📥 Inbox table: tb_inbox");
            Console.WriteLine($"📤 Outbox table: tb_outbox");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to create InboxOutbox database: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
        }
   }
   }