using System.Text.RegularExpressions;

public record CartItem(int Id, int Quantity);
public record OrderRequest(string? Customer, string? Contact, List<CartItem?>? Items);

public static class Orders
{
    public static IResult Save(OrderRequest request, Database database)
    {
        string name = request.Customer?.Trim() ?? "";
        string phone = request.Contact?.Trim() ?? "";
        if (name.Length is < 2 or > 60)
            return Error("Введите имя от 2 до 60 символов.");
        if (!Regex.IsMatch(phone, @"^\+?[0-9 ()-]{7,22}$") || phone.Count(char.IsDigit) is < 7 or > 15)
            return Error("Проверьте номер телефона.");
        if (request.Items is not { Count: >= 1 and <= 30 } items)
            return Error("Проверьте состав корзины.");

        using var db = database.Open();
        using var transaction = db.BeginTransaction(deferred: false);
        using var cmd = db.CreateCommand();
        cmd.Transaction = transaction;
        var lines = new Dictionary<int, (int Quantity, long Price)>();
        long total = 0;

        foreach (var item in items)
        {
            if (item is null || item.Id < 1 || item.Quantity is < 1 or > 99 || lines.ContainsKey(item.Id))
                return Error("Проверьте позиции заказа.");
            cmd.Parameters.Clear();
            cmd.CommandText = "SELECT name, price, stock FROM products WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", item.Id);
            using var row = cmd.ExecuteReader();
            if (!row.Read()) return Error("Товар отсутствует в каталоге.");
            if (row.GetInt32(2) < item.Quantity)
                return Error($"Недостаточно товара «{row.GetString(0)}».");
            long price = row.GetInt64(1);
            total += price * item.Quantity;
            lines.Add(item.Id, (item.Quantity, price));
        }

        cmd.Parameters.Clear();
        cmd.CommandText = "INSERT INTO orders(customer, contact, total) VALUES ($name, $phone, $total) RETURNING id";
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$phone", phone);
        cmd.Parameters.AddWithValue("$total", total);
        long id = (long)cmd.ExecuteScalar()!;

        foreach (var (productId, line) in lines)
        {
            cmd.Parameters.Clear();
            cmd.CommandText = """
                INSERT INTO order_items(order_id, product_id, quantity, unit_price)
                VALUES ($order, $product, $quantity, $price);
                UPDATE products SET stock = stock - $quantity WHERE id = $product;
                """;
            cmd.Parameters.AddWithValue("$order", id);
            cmd.Parameters.AddWithValue("$product", productId);
            cmd.Parameters.AddWithValue("$quantity", line.Quantity);
            cmd.Parameters.AddWithValue("$price", line.Price);
            cmd.ExecuteNonQuery();
        }
        transaction.Commit();
        return Results.Json(new { id, total }, statusCode: 201);
    }

    private static IResult Error(string message) => Results.BadRequest(new { error = message });
}
