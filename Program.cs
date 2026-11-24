var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var scripts = Path.Combine(app.Environment.ContentRootPath, "data");
var database = new Database(Environment.GetEnvironmentVariable("TEMP_SHOP_DB") ?? Path.Combine(scripts, "shop.sqlite3"));
database.Initialize(scripts);

app.UseWhen(context => context.Request.Path.StartsWithSegments("/api"), api =>
    api.Use(async (context, next) =>
    {
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            await next();
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            app.Logger.LogError("Ошибка при обращении к SQLite");
            context.Response.StatusCode = 503;
            await context.Response.WriteAsJsonAsync(new { error = "База данных временно недоступна." });
        }
    }));

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/api/catalog", () => database.Catalog());
app.MapPost("/api/orders", async (HttpRequest request) =>
{
    if (!request.HasJsonContentType())
        return Results.Json(new { error = "Ожидается JSON." }, statusCode: 415);
    try
    {
        var order = await request.ReadFromJsonAsync<OrderRequest>();
        return order is null ? Results.BadRequest(new { error = "Пустой заказ." }) : Orders.Save(order, database);
    }
    catch (System.Text.Json.JsonException)
    {
        return Results.BadRequest(new { error = "Некорректный заказ." });
    }
});
app.MapFallback("/api/{**path}", () => Results.NotFound(new { error = "Адрес не найден." }));
app.Run();
