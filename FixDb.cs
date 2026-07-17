using System;
using Npgsql;

class FixDb
{
    static void Run()
    {
        string connStr = "Host=localhost;Port=5432;Database=scm_db;Username=postgres;Password=password";
        using var conn = new NpgsqlConnection(connStr);
        conn.Open();

        string query = @"
            DELETE FROM ""RecipeIngredients"" ri
            USING ""Recipes"" r, ""FinishedProducts"" fp
            WHERE ri.""RecipeId"" = r.""RecipeId""
              AND r.""ProductId"" = fp.""ProductId""
              AND ri.""ItemId"" = fp.""ItemId"";
        ";

        try {
            using var cmd = new NpgsqlCommand(query, conn);
            int rowsAffected = cmd.ExecuteNonQuery();
            Console.WriteLine($"Executed cleanup. Rows deleted: {rowsAffected}");
        } catch (Exception ex) {
            Console.WriteLine("Error on cleanup: " + ex.Message);
        }
        Console.WriteLine("Done!");
    }
}
