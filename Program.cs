var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var scripts = Path.Combine(app.Environment.ContentRootPath, "data");
var database = new Database(Environment.GetEnvironmentVariable("TEMP_SHOP_DB") ?? Path.Combine(scripts, "shop.sqlite3"));
database.Initialize(scripts);

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/api/catalog", () => database.Catalog());
app.Run();
