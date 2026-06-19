using System;
using Npgsql;

class FixDb
{
    static void Run()
    {
        string connStr = "Host=localhost;Port=5432;Database=scm_db;Username=postgres;Password=password";
        using var conn = new NpgsqlConnection(connStr);
        conn.Open();

        string[] queries = {
            "ALTER TABLE \"PurchaseOrders\" ADD COLUMN IF NOT EXISTS \"InspectedBy\" text;",
            "ALTER TABLE \"PurchaseOrders\" ADD COLUMN IF NOT EXISTS \"QaInspectedDate\" timestamp with time zone;",
            "ALTER TABLE \"PurchaseOrders\" ADD COLUMN IF NOT EXISTS \"QaNotes\" text;",
            "ALTER TABLE \"PurchaseOrders\" ADD COLUMN IF NOT EXISTS \"QaStatus\" text;"
        };

        foreach (var q in queries)
        {
            try {
                using var cmd = new NpgsqlCommand(q, conn);
                cmd.ExecuteNonQuery();
                Console.WriteLine("Executed: " + q);
            } catch (Exception ex) {
                Console.WriteLine("Error on " + q + ": " + ex.Message);
            }
        }
        Console.WriteLine("Done!");
    }
}
