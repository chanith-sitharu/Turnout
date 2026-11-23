using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TurnoutStore.Services;

namespace TurnoutStore.Data
{
    public static class DbInitializer
    {
        // In-memory sample products with fixed IDs (for unit testing or display fallbacks)
        public static List<Product> GetSampleProducts()
        {
            return new List<Product>
            {
                new Product
                {
                    ProductId = 1,
                    Sku = "MINI-LOCO-001",
                    Name = "Class 4-6-2 Pacific Steam Locomotive",
                    Description = "Handcrafted HO-scale diecast steam locomotive with authentic brass details, directional LED lighting, and realistic tender.",
                    Price = 189.99m,
                    StockQuantity = 12,
                    IsUnique = false,
                    Scale = "1:87 (HO)",
                    DimensionsMm = "240x35x50",
                    WeightGrams = 420.00m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Product
                {
                    ProductId = 2,
                    Sku = "MINI-BLD-002",
                    Name = "Victorian Brick Station & Passenger Platform",
                    Description = "Pre-painted miniature passenger depot featuring glazed acrylic windows, exterior lamp posts, and ticket office details.",
                    Price = 64.50m,
                    StockQuantity = 18,
                    IsUnique = false,
                    Scale = "1:87 (HO)",
                    DimensionsMm = "180x120x95",
                    WeightGrams = 210.00m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Product
                {
                    ProductId = 3,
                    Sku = "MINI-SCN-003",
                    Name = "Alpine Mountain Scenery & Evergreen Trees (25 pcs)",
                    Description = "Ultra-realistic flocked pine trees with stone rock molds and static grass tufts for miniature railway and diorama layouts.",
                    Price = 34.95m,
                    StockQuantity = 40,
                    IsUnique = false,
                    Scale = "Universal",
                    DimensionsMm = "300x200x80",
                    WeightGrams = 150.00m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Product
                {
                    ProductId = 4,
                    Sku = "MINI-STR-004",
                    Name = "Handcrafted Weathered Railway Signal Tower",
                    Description = "One-of-a-kind scratch-built miniature interlocking signal tower with interior detailing and rustic weathered timber finish.",
                    Price = 129.00m,
                    StockQuantity = 1,
                    IsUnique = true,
                    Scale = "1:87 (HO)",
                    DimensionsMm = "90x75x135",
                    WeightGrams = 95.00m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Product
                {
                    ProductId = 5,
                    Sku = "MINI-ROL-005",
                    Name = "Vintage B&O Wooden Rolling Boxcar",
                    Description = "Precision-molded rolling stock with functional sliding side doors, metal knuckle couplers, and smooth-roll brass wheelsets.",
                    Price = 28.50m,
                    StockQuantity = 25,
                    IsUnique = false,
                    Scale = "1:87 (HO)",
                    DimensionsMm = "155x38x48",
                    WeightGrams = 85.00m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            };
        }

        // Database seed products WITHOUT explicit ProductId values (Identity column generates IDs)
        public static List<Product> GetSeedProducts()
        {
            return new List<Product>
            {
                new Product
                {
                    Sku = "MINI-LOCO-001",
                    Name = "Class 4-6-2 Pacific Steam Locomotive",
                    Description = "Handcrafted HO-scale diecast steam locomotive with authentic brass details, directional LED lighting, and realistic tender.",
                    Price = 189.99m,
                    StockQuantity = 12,
                    IsUnique = false,
                    Scale = "1:87 (HO)",
                    DimensionsMm = "240x35x50",
                    WeightGrams = 420.00m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Sku = "MINI-BLD-002",
                    Name = "Victorian Brick Station & Passenger Platform",
                    Description = "Pre-painted miniature passenger depot featuring glazed acrylic windows, exterior lamp posts, and ticket office details.",
                    Price = 64.50m,
                    StockQuantity = 18,
                    IsUnique = false,
                    Scale = "1:87 (HO)",
                    DimensionsMm = "180x120x95",
                    WeightGrams = 210.00m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Sku = "MINI-SCN-003",
                    Name = "Alpine Mountain Scenery & Evergreen Trees (25 pcs)",
                    Description = "Ultra-realistic flocked pine trees with stone rock molds and static grass tufts for miniature railway and diorama layouts.",
                    Price = 34.95m,
                    StockQuantity = 40,
                    IsUnique = false,
                    Scale = "Universal",
                    DimensionsMm = "300x200x80",
                    WeightGrams = 150.00m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Sku = "MINI-STR-004",
                    Name = "Handcrafted Weathered Railway Signal Tower",
                    Description = "One-of-a-kind scratch-built miniature interlocking signal tower with interior detailing and rustic weathered timber finish.",
                    Price = 129.00m,
                    StockQuantity = 1,
                    IsUnique = true,
                    Scale = "1:87 (HO)",
                    DimensionsMm = "90x75x135",
                    WeightGrams = 95.00m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Sku = "MINI-ROL-005",
                    Name = "Vintage B&O Wooden Rolling Boxcar",
                    Description = "Precision-molded rolling stock with functional sliding side doors, metal knuckle couplers, and smooth-roll brass wheelsets.",
                    Price = 28.50m,
                    StockQuantity = 25,
                    IsUnique = false,
                    Scale = "1:87 (HO)",
                    DimensionsMm = "155x38x48",
                    WeightGrams = 85.00m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            };
        }

        public static void Initialize(AppDbContext context, IConfiguration config, ILogger logger, IPasswordService passwordService)
        {
            if (context.Database.IsNpgsql())
            {
                logger.LogInformation("PostgreSQL detected (Render / Cloud). Initializing database schema...");
                try
                {
                    context.Database.EnsureCreated();
                    logger.LogInformation("PostgreSQL schema created/verified successfully.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to initialize PostgreSQL database schema.");
                    return;
                }
            }
            else
            {
                var connStr = context.Database.GetConnectionString();
                if (string.IsNullOrWhiteSpace(connStr))
                {
                    logger.LogWarning("No connection string found. Skipping database migration.");
                    return;
                }

                // LocalDB is Windows-only. In Linux/Docker, skip LocalDB without attempting connection to prevent named-pipe/GSSAPI native crashes
                if (!OperatingSystem.IsWindows() && connStr.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("Running in Linux/container environment with a Windows LocalDB connection string. LocalDB is Windows-only. Skipping database initialization until a production database is configured.");
                    return;
                }

                try
                {
                    if (!context.Database.CanConnect())
                    {
                        logger.LogWarning("Cannot reach the configured SQL database. Skipping migration and seeding.");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to connect to SQL database during startup check. Skipping migration and seeding.");
                    return;
                }

                try
                {
                    // Ensure migration history is baselined if transitioning from a pre-migration database
                    EnsureDatabaseBaselined(context, logger);

                    // Execute real EF Core database migrations
                    context.Database.Migrate();
                    logger.LogInformation("Database migration applied successfully.");
                }
                catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number == 2714)
                {
                    logger.LogWarning(sqlEx, "Migration detected existing database objects (SQL Error 2714). Forcing baseline registration in __EFMigrationsHistory...");
                    try
                    {
                        context.Database.ExecuteSqlRaw(@"
                            IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
                            BEGIN
                                CREATE TABLE [__EFMigrationsHistory] (
                                    [MigrationId] nvarchar(150) NOT NULL,
                                    [ProductVersion] nvarchar(32) NOT NULL,
                                    CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
                                );
                            END;
                            IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20261116000000_InitialCreate')
                            BEGIN
                                INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                                VALUES ('20261116000000_InitialCreate', '8.0.8');
                            END;
                        ");
                        logger.LogInformation("Successfully registered InitialCreate baseline following error 2714.");
                    }
                    catch (Exception innerEx)
                    {
                        logger.LogError(innerEx, "Failed to register baseline after error 2714.");
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while applying EF Core database migrations.");
                    throw;
                }
            }

            // Seed Admin User from Configuration / User-Secrets / Env
            try
            {
                var adminEmail = config["AdminSettings:Email"] ?? Environment.GetEnvironmentVariable("ADMIN_EMAIL") ?? "admin@turnout.com";
                var adminPassword = config["AdminSettings:Password"] ?? Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "AdminTurnout2026!Secure";
                var adminFirst = config["AdminSettings:FirstName"] ?? "Admin";
                var adminLast = config["AdminSettings:LastName"] ?? "Staff";

                var normalizedEmail = adminEmail.Trim().ToLowerInvariant();

                var adminRole = context.Roles.FirstOrDefault(r => r.RoleName == "Admin");
                if (adminRole == null)
                {
                    adminRole = new Role { RoleName = "Admin" };
                    context.Roles.Add(adminRole);
                    context.SaveChanges();
                }

                var existingAdmin = context.Users.FirstOrDefault(u => u.Email == normalizedEmail);
                if (existingAdmin == null)
                {
                    var hashedAdminPassword = passwordService.HashPassword(adminPassword);
                    var newAdmin = new User
                    {
                        FirstName = adminFirst,
                        LastName = adminLast,
                        Email = normalizedEmail,
                        PhoneNumber = "+94 70 385 6824",
                        RoleId = adminRole.RoleId,
                        PasswordHash = hashedAdminPassword,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    context.Users.Add(newAdmin);
                    context.SaveChanges();
                    logger.LogInformation("Admin user {Email} seeded successfully.", normalizedEmail);
                }
                else
                {
                    // Ensure role is Admin, account is active, and password matches config
                    bool modified = false;
                    if (existingAdmin.RoleId != adminRole.RoleId)
                    {
                        existingAdmin.RoleId = adminRole.RoleId;
                        modified = true;
                    }
                    if (!existingAdmin.IsActive)
                    {
                        existingAdmin.IsActive = true;
                        modified = true;
                    }
                    if (!passwordService.VerifyPassword(adminPassword, existingAdmin.PasswordHash))
                    {
                        existingAdmin.PasswordHash = passwordService.HashPassword(adminPassword);
                        modified = true;
                    }
                    if (modified)
                    {
                        context.SaveChanges();
                        logger.LogInformation("Updated role/status/credentials for admin user {Email}.", normalizedEmail);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to seed admin user.");
                throw;
            }

            // Seed Products if none exist
            try
            {
                if (!context.Products.Any())
                {
                    context.Products.AddRange(GetSeedProducts());
                    context.SaveChanges();
                    logger.LogInformation("Initial product catalog seeded successfully.");
                }

                // Seed Default Bank Accounts if none exist
                if (!context.BankAccounts.Any())
                {
                    context.BankAccounts.AddRange(new List<BankAccount>
                    {
                        new BankAccount
                        {
                            BankName = "Commercial Bank of Ceylon",
                            AccountName = "The Turnout Model Railway",
                            AccountNumber = "8009234512",
                            Branch = "Negombo Branch",
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        },
                        new BankAccount
                        {
                            BankName = "Sampath Bank PLC",
                            AccountName = "The Turnout Model Railway",
                            AccountNumber = "014210008892",
                            Branch = "Katunayake Airport Road Branch",
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        }
                    });
                    context.SaveChanges();
                    logger.LogInformation("Default store receiving bank accounts seeded successfully.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to seed initial catalog or bank accounts.");
                throw;
            }
        }

        private static void EnsureDatabaseBaselined(AppDbContext context, ILogger logger)
        {
            try
            {
                if (!context.Database.CanConnect())
                {
                    return;
                }

                logger.LogInformation("Verifying database schema and migration baseline...");

                var baselineSql = @"
                    -- 1. Ensure __EFMigrationsHistory table exists
                    IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
                    BEGIN
                        CREATE TABLE [__EFMigrationsHistory] (
                            [MigrationId] nvarchar(150) NOT NULL,
                            [ProductVersion] nvarchar(32) NOT NULL,
                            CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
                        );
                    END;

                    -- 2. If legacy database exists (Roles table present), ensure columns and baseline InitialCreate
                    IF OBJECT_ID(N'dbo.Roles', N'U') IS NOT NULL
                    BEGIN
                        -- Add multiple image columns ImageUrl1 to ImageUrl5
                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'ImageUrl1')
                        BEGIN
                            ALTER TABLE Products ADD ImageUrl1 NVARCHAR(500) NULL;
                        END;

                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'ImageUrl2')
                        BEGIN
                            ALTER TABLE Products ADD ImageUrl2 NVARCHAR(500) NULL;
                        END;

                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'ImageUrl3')
                        BEGIN
                            ALTER TABLE Products ADD ImageUrl3 NVARCHAR(500) NULL;
                        END;

                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'ImageUrl4')
                        BEGIN
                            ALTER TABLE Products ADD ImageUrl4 NVARCHAR(500) NULL;
                        END;

                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'ImageUrl5')
                        BEGIN
                            ALTER TABLE Products ADD ImageUrl5 NVARCHAR(500) NULL;
                        END;

                        -- Backfill ImageUrl1 with legacy ImageUrl if legacy column exists
                        IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'ImageUrl')
                        BEGIN
                            EXEC sp_executesql N'UPDATE Products SET ImageUrl1 = ImageUrl WHERE ImageUrl1 IS NULL AND ImageUrl IS NOT NULL';
                        END;

                        -- Add missing RowVersion column to Products
                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'RowVersion')
                        BEGIN
                            ALTER TABLE Products ADD RowVersion ROWVERSION NOT NULL;
                        END;

                        -- Add missing AccessFailedCount column to Users
                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'AccessFailedCount')
                        BEGIN
                            ALTER TABLE Users ADD AccessFailedCount INT NOT NULL DEFAULT 0;
                        END;

                        -- Add missing LockoutEnd column to Users
                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'LockoutEnd')
                        BEGIN
                            ALTER TABLE Users ADD LockoutEnd DATETIMEOFFSET NULL;
                        END;

                        -- Ensure TicketBookings table exists
                        IF OBJECT_ID(N'[TicketBookings]') IS NULL
                        BEGIN
                            CREATE TABLE [TicketBookings] (
                                [BookingId] int NOT NULL IDENTITY,
                                [CustomerName] nvarchar(100) NOT NULL,
                                [PhoneNumber] nvarchar(20) NOT NULL,
                                [VisitDate] datetime2 NOT NULL,
                                [TimeSlot] nvarchar(50) NOT NULL,
                                [AdultsCount] int NOT NULL,
                                [KidsCount] int NOT NULL,
                                [TicketType] nvarchar(50) NOT NULL,
                                [Notes] nvarchar(1000) NULL,
                                [TotalPrice] decimal(10,2) NOT NULL,
                                [BookingStatus] nvarchar(30) NOT NULL,
                                [PaymentMethod] nvarchar(50) NOT NULL DEFAULT 'Bank Transfer',
                                [PaymentSlipUrl] nvarchar(500) NULL,
                                [PaymentStatus] nvarchar(50) NOT NULL DEFAULT 'Pending Verification',
                                [CreatedAt] datetime2 NOT NULL,
                                CONSTRAINT [PK_TicketBookings] PRIMARY KEY ([BookingId])
                            );
                        END;

                        -- Ensure BankAccounts table exists
                        IF OBJECT_ID(N'[BankAccounts]') IS NULL
                        BEGIN
                            CREATE TABLE [BankAccounts] (
                                [Id] int NOT NULL IDENTITY,
                                [BankName] nvarchar(100) NOT NULL,
                                [AccountName] nvarchar(150) NOT NULL,
                                [AccountNumber] nvarchar(50) NOT NULL,
                                [Branch] nvarchar(100) NOT NULL,
                                [IsActive] bit NOT NULL DEFAULT 1,
                                [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                CONSTRAINT [PK_BankAccounts] PRIMARY KEY ([Id])
                            );
                        END;

                        -- Ensure PaymentMethod and PaymentSlipUrl in Orders
                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'PaymentMethod')
                        BEGIN
                            ALTER TABLE Orders ADD PaymentMethod NVARCHAR(50) NOT NULL DEFAULT 'Bank Transfer';
                        END;

                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'PaymentSlipUrl')
                        BEGIN
                            ALTER TABLE Orders ADD PaymentSlipUrl NVARCHAR(500) NULL;
                        END;

                        -- Ensure PaymentMethod, PaymentSlipUrl and PaymentStatus in TicketBookings
                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TicketBookings') AND name = 'PaymentMethod')
                        BEGIN
                            ALTER TABLE TicketBookings ADD PaymentMethod NVARCHAR(50) NOT NULL DEFAULT 'Bank Transfer';
                        END;

                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TicketBookings') AND name = 'PaymentSlipUrl')
                        BEGIN
                            ALTER TABLE TicketBookings ADD PaymentSlipUrl NVARCHAR(500) NULL;
                        END;

                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TicketBookings') AND name = 'PaymentStatus')
                        BEGIN
                            ALTER TABLE TicketBookings ADD PaymentStatus NVARCHAR(50) NOT NULL DEFAULT 'Pending Verification';
                        END;

                        -- Mark InitialCreate as applied in __EFMigrationsHistory
                        IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20261116000000_InitialCreate')
                        BEGIN
                            INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                            VALUES ('20261116000000_InitialCreate', '8.0.8');
                        END;
                    END;";

                context.Database.ExecuteSqlRaw(baselineSql);
                logger.LogInformation("Database baseline verification completed.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Database baseline check encountered an issue; proceeding with standard EF Core migration.");
            }
        }
    }
}
