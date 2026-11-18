using Dapper;
using SmartCafeteria.Data.Repositories;

namespace SmartCafeteria.Data.Database;
public class DatabaseInitializer
{
    private readonly DbContext _dbContext;

    public DatabaseInitializer(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task InitializeAsync()
    {
        using var conn = await _dbContext.GetConnectionAsync();

        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS Products (
                Id TEXT PRIMARY KEY,
                NameAr TEXT NOT NULL,
                NameEn TEXT NOT NULL,
                DescriptionAr TEXT DEFAULT '',
                DescriptionEn TEXT DEFAULT '',
                Price REAL NOT NULL,
                Category TEXT DEFAULT '',
                ImageUrl TEXT DEFAULT '',
                Emoji TEXT DEFAULT '',
                Inventory INTEGER DEFAULT 0,
                MaxInventory INTEGER DEFAULT 100,
                Barcode TEXT DEFAULT ''
            )");

        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS ProductCustomizations (
                Id TEXT PRIMARY KEY,
                ProductId TEXT NOT NULL,
                NameAr TEXT NOT NULL,
                NameEn TEXT NOT NULL,
                Price REAL NOT NULL,
                FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE
            )");

        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS Orders (
                Id TEXT PRIMARY KEY,
                Code TEXT NOT NULL,
                Subtotal REAL NOT NULL,
                CustomizationsTotal REAL NOT NULL,
                Total REAL NOT NULL,
                PaymentMethod INTEGER NOT NULL,
                PaymentStatus TEXT DEFAULT 'paid',
                Status INTEGER NOT NULL,
                Timestamp TEXT NOT NULL,
                StationId TEXT DEFAULT '',
                CashierName TEXT DEFAULT '',
                IsPreOrder INTEGER DEFAULT 0,
                PickupTime TEXT,
                OrderType INTEGER NOT NULL DEFAULT 0,
                Notes TEXT NOT NULL DEFAULT ''
            )");

        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS OrderItems (
                Id TEXT PRIMARY KEY,
                OrderId TEXT NOT NULL,
                ProductId TEXT NOT NULL,
                ProductNameAr TEXT NOT NULL,
                ProductNameEn TEXT NOT NULL,
                ProductPrice REAL NOT NULL,
                ProductCategory TEXT DEFAULT '',
                Quantity INTEGER NOT NULL,
                CustomizationsTotal REAL DEFAULT 0,
                Notes TEXT DEFAULT '',
                SelectedCustomizationsJson TEXT DEFAULT '[]',
                FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE
            )");

        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS SystemLogs (
                Id TEXT PRIMARY KEY,
                Timestamp TEXT NOT NULL,
                Message TEXT NOT NULL,
                Level TEXT DEFAULT 'info'
            )");

        // One row per business day. Receipt numbers are allocated by an atomic
        // upsert against this table instead of COUNT(*) + 1, so two concurrent
        // checkouts can never receive the same number and deleting an order
        // never makes the next receipt reuse an issued number.
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS OrderCounters (
                Date TEXT PRIMARY KEY,
                LastNumber INTEGER NOT NULL DEFAULT 0
            )");

        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS Users (
                Id TEXT PRIMARY KEY,
                Username TEXT UNIQUE NOT NULL,
                PasswordHash TEXT NOT NULL,
                FullName TEXT NOT NULL,
                Role INTEGER NOT NULL,
                IsActive INTEGER DEFAULT 1,
                CreatedAt TEXT NOT NULL,
                MustChangePassword INTEGER NOT NULL DEFAULT 0,
                FailedLoginCount INTEGER NOT NULL DEFAULT 0,
                LockoutUntil TEXT
            )");

        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS AppSettings (
                Id INTEGER PRIMARY KEY DEFAULT 1,
                CurrencyAr TEXT DEFAULT 'ج.س',
                CurrencyEn TEXT DEFAULT 'SDG',
                LowStockThreshold INTEGER DEFAULT 5,
                CashierName TEXT DEFAULT 'أحمد السوداني',
                StationId TEXT DEFAULT 'TERMINAL-01',
                PrinterName TEXT DEFAULT '',
                PrintMethod INTEGER DEFAULT 0,
                CafeteriaNameAr TEXT DEFAULT 'كافتيريا التميز',
                CafeteriaNameEn TEXT DEFAULT 'AL-TAMAYUZ CAFETERIA',
                CafeteriaAddress TEXT DEFAULT 'شارع النيل الخرطوم',
                CafeteriaPhone TEXT DEFAULT '+249 123 456 789'
            )");

        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS AccountEntries (
                Id TEXT PRIMARY KEY,
                Type INTEGER NOT NULL,
                Category INTEGER DEFAULT 0,
                Description TEXT DEFAULT '',
                Amount REAL NOT NULL,
                Date TEXT NOT NULL,
                PaymentMethod INTEGER DEFAULT 0,
                ReferenceCode TEXT DEFAULT '',
                UserName TEXT DEFAULT ''
            )");

        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS Suppliers (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Phone TEXT DEFAULT '',
                Email TEXT DEFAULT '',
                Address TEXT DEFAULT '',
                Notes TEXT DEFAULT '',
                IsActive INTEGER DEFAULT 1,
                CreatedAt TEXT NOT NULL
            )");

        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS PurchaseOrders (
                Id TEXT PRIMARY KEY,
                Code TEXT NOT NULL,
                SupplierId TEXT NOT NULL,
                SupplierName TEXT DEFAULT '',
                Date TEXT NOT NULL,
                TotalAmount REAL DEFAULT 0,
                Status INTEGER DEFAULT 0,
                Notes TEXT DEFAULT ''
            )");

        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS PurchaseOrderItems (
                Id TEXT PRIMARY KEY,
                PurchaseOrderId TEXT NOT NULL,
                ProductId TEXT NOT NULL,
                ProductName TEXT DEFAULT '',
                Quantity INTEGER DEFAULT 0,
                UnitCost REAL DEFAULT 0,
                FOREIGN KEY (PurchaseOrderId) REFERENCES PurchaseOrders(Id) ON DELETE CASCADE
            )");

        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS SupplierPayments (
                Id TEXT PRIMARY KEY,
                SupplierId TEXT NOT NULL,
                Date TEXT NOT NULL,
                Amount REAL NOT NULL,
                PaymentMethod INTEGER DEFAULT 0,
                Notes TEXT DEFAULT ''
            )");

        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS Employees (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Phone TEXT DEFAULT '',
                Position INTEGER DEFAULT 0,
                MonthlySalary REAL DEFAULT 0,
                HireDate TEXT NOT NULL,
                IsActive INTEGER DEFAULT 1,
                Notes TEXT DEFAULT ''
            )");

        await EnsureColumnAsync(conn, "Products", "Barcode", "TEXT DEFAULT ''");

        // Account security state. Existing users keep working; the counters are
        // only populated by failed logins and the flag is only set for the
        // well-known legacy default password (migration below).
        await EnsureColumnAsync(conn, "Users", "MustChangePassword", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync(conn, "Users", "FailedLoginCount", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync(conn, "Users", "LockoutUntil", "TEXT");

        // Order type (0 = محلي) and the customer's note on the order. Existing
        // rows keep the default محلي and an empty note, which is exactly how they
        // were placed. Idempotent: EnsureColumnAsync only adds a missing column.
        await EnsureColumnAsync(conn, "Orders", "OrderType", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync(conn, "Orders", "Notes", "TEXT NOT NULL DEFAULT ''");

        // Migrate legacy 5-state order statuses (1=Preparing, 2=Ready, 3=Completed, 4=Cancelled)
        // to the 2-state model (0=Received, 1=Cancelled). Idempotent.
        await conn.ExecuteAsync("UPDATE Orders SET Status = 0 WHERE Status IN (1, 2, 3)");
        await conn.ExecuteAsync("UPDATE Orders SET Status = 1 WHERE Status = 4");

        // Migrate legacy payment methods (1=Card, 2=ApplePay, 3=Mada) to Bankak (1). Idempotent.
        await conn.ExecuteAsync("UPDATE Orders SET PaymentMethod = 1 WHERE PaymentMethod IN (2, 3)");
        await conn.ExecuteAsync("UPDATE AccountEntries SET PaymentMethod = 1 WHERE PaymentMethod IN (2, 3)");
        await conn.ExecuteAsync("UPDATE SupplierPayments SET PaymentMethod = 1 WHERE PaymentMethod IN (2, 3)");

        // Fix the misspelled address of the cafeteria. Only the exact old value is
        // rewritten, an address the user changed himself is never touched.
        // Idempotent: the new value does not match the condition any more.
        await conn.ExecuteAsync(
            "UPDATE AppSettings SET CafeteriaAddress = 'شارع النيل الخرطوم' " +
            "WHERE CafeteriaAddress = 'شارع النيل، الخرتوم'");

        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS Categories (
                Id TEXT PRIMARY KEY,
                NameAr TEXT NOT NULL,
                NameEn TEXT DEFAULT '',
                SortOrder INTEGER DEFAULT 0
            )");

        // Tombstones for categories the user deleted. Built-in seeding below
        // skips these so a deleted category stays deleted after restart.
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS DeletedCategoryIds (
                Id TEXT PRIMARY KEY
            )");

        // Seed the built-in categories once (stable ids double as legacy enum names).
        var deletedIds = (await conn.QueryAsync<string>("SELECT Id FROM DeletedCategoryIds"))
            .ToHashSet();
        var builtIns = new (string Id, string NameAr, string NameEn, int Order)[]
        {
            ("HotDrinks", "مشروبات ساخنة", "Hot Drinks", 0),
            ("ColdDrinks", "مشروبات باردة", "Cold Drinks", 1),
            ("Breakfast", "إفطار", "Breakfast", 2),
            ("Sandwiches", "ساندويتشات", "Sandwiches", 3),
            ("SweetPies", "حلويات", "Sweet Pies", 4),
            ("SavoryPies", "فطائر مالحة", "Savory Pies", 5),
            ("Desserts", "حلوى", "Desserts", 6),
            ("Juices", "عصائر", "Juices", 7),
            ("Grills", "مشويات", "Grills", 8),
        };
        foreach (var (id, nameAr, nameEn, order) in builtIns)
        {
            if (deletedIds.Contains(id))
                continue;
            await conn.ExecuteAsync(
                "INSERT OR IGNORE INTO Categories (Id, NameAr, NameEn, SortOrder) VALUES (@Id, @NameAr, @NameEn, @SortOrder)",
                new { Id = id, NameAr = nameAr, NameEn = nameEn, SortOrder = order });
        }

        // Migrate Products.Category from legacy enum integers (0-6) to category keys. Idempotent.
        await conn.ExecuteAsync(@"
            UPDATE Products SET Category = CASE CAST(Category AS INTEGER)
                WHEN 0 THEN 'HotDrinks'
                WHEN 1 THEN 'ColdDrinks'
                WHEN 2 THEN 'Breakfast'
                WHEN 3 THEN 'Sandwiches'
                WHEN 4 THEN 'SweetPies'
                WHEN 5 THEN 'SavoryPies'
                WHEN 6 THEN 'Desserts'
                ELSE Category END
            WHERE typeof(Category) = 'integer'");

        // Accounts still using the publicly documented legacy password must
        // change it before continuing. The check is hash-based, so an account
        // whose password was already changed (or provisioned with a random
        // first-run password) is never touched. Legacy hashes only, which keeps
        // startup cheap; v2 hashes are created by the current hasher.
        var legacyAdminHashes = await conn.QueryAsync<(string Id, string PasswordHash)>(
            "SELECT Id, PasswordHash FROM Users WHERE Username = 'admin'");
        foreach (var (id, hash) in legacyAdminHashes)
        {
            if (hash.StartsWith("v2$", StringComparison.Ordinal))
                continue;
            if (PasswordHasher.VerifyPassword("admin123", hash))
            {
                await conn.ExecuteAsync(
                    "UPDATE Users SET MustChangePassword = 1 WHERE Id = @Id", new { Id = id });
            }
        }
    }

    private static async Task EnsureColumnAsync(System.Data.IDbConnection conn, string table, string column, string definition)
    {
        var existing = await conn.QueryFirstOrDefaultAsync<string>(
            $"SELECT name FROM pragma_table_info('{table}') WHERE name = '{column}'");
        if (existing == null)
        {
            await conn.ExecuteAsync($"ALTER TABLE {table} ADD COLUMN {column} {definition}");
        }
    }
}
