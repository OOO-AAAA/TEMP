using Microsoft.Data.Sqlite;

public class Database(string path)
{
    public SqliteConnection Open()
    {
        var options = new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true };
        var db = new SqliteConnection(options.ToString());
        db.Open();
        return db;
    }

    public void Initialize(string scripts)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var db = Open();
        using var transaction = db.BeginTransaction();
        using var cmd = db.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='products'";
        if ((long)cmd.ExecuteScalar()! == 0)
        {
            foreach (string file in new[] { "schema.sql", "seed.sql" })
            {
                cmd.CommandText = File.ReadAllText(Path.Combine(scripts, file));
                cmd.ExecuteNonQuery();
            }
        }
        transaction.Commit();
    }

    public object Catalog()
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT id, slug, name FROM categories ORDER BY id";
        var categories = new List<object>();
        using (var rows = cmd.ExecuteReader())
        {
            while (rows.Read())
                categories.Add(new { id = rows.GetInt32(0), slug = rows.GetString(1), name = rows.GetString(2) });
        }

        cmd.CommandText = "SELECT id, category_id, name, description, price, stock, photo FROM products ORDER BY id";
        var products = new List<object>();
        using (var rows = cmd.ExecuteReader())
        {
            while (rows.Read())
                products.Add(new { id = rows.GetInt32(0), category_id = rows.GetInt32(1),
                    name = rows.GetString(2), description = rows.GetString(3), price = rows.GetInt32(4),
                    stock = rows.GetInt32(5), photo = rows.GetString(6) });
        }
        return new { categories, products };
    }
}
