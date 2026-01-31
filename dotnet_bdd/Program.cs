using Microsoft.EntityFrameworkCore;
// using Microsoft.OpenApi.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Use SQLite as a simple default DB file "logs.db"
builder.Services.AddDbContext<LogsDb>(opt =>
{
    var dbPath = Path.Combine(AppContext.BaseDirectory, "logs.db");
    opt.UseSqlite($"Data Source={dbPath}");
});

// Ensure JSON binding is case-insensitive and accepts snake_case from Python
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    // Allow incoming snake_case to bind to PascalCase C# properties by using a custom converter during model binding:
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    // Keep default behavior otherwise
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Apply migrations / create DB
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LogsDb>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

// GET all
app.MapGet("/logs", async (LogsDb db) =>
    await db.LogItems.AsNoTracking().ToListAsync());

// GET by id
app.MapGet("/logs/{id:int}", async (int id, LogsDb db) =>
    await db.LogItems.FindAsync(id) is LogItem item ? Results.Ok(item) : Results.NotFound());

// Bulk insert: accepts list of objects (snake_case or PascalCase)
app.MapPost("/logs/bulk", async (HttpRequest request, LogsDb db) =>
{
    // Read raw JSON and normalize property names to match C# model
    using var doc = await JsonDocument.ParseAsync(request.Body);
    Console.WriteLine("ok");
    if (doc.RootElement.ValueKind != JsonValueKind.Array) return Results.BadRequest("Expected JSON array");

    var list = new List<LogItem>();
    foreach (var el in doc.RootElement.EnumerateArray())
    {
        // Map snake_case keys (from Python) to PascalCase properties
        var mapped = new LogItem
        {
            MigrationStartTime = ReadDateTime(el, "migration_start_time", "MigrationStartTime"),
            SubJobId = ReadString(el, "sub_job_id", "SubJobId"),
            Title = ReadString(el, "title", "Title"),
            Type = ReadString(el, "type", "Type"),
            SourceID = ReadString(el, "source_ID", "SourceID", "sourceId"),
            Source = ReadString(el, "source", "Source"),
            DestinationId = ReadString(el, "destination_id", "DestinationId", "destinationId"),
            Destination = ReadString(el, "destination", "Destination"),
            Size = ReadString(el, "size", "Size"),
            Status = ReadString(el, "status", "Status"),
            MigrationAction = ReadString(el, "migration_action", "MigrationAction"),
            Comment = ReadString(el, "comment", "Comment"),
            ErrorCode = ReadString(el, "error_code", "ErrorCode")
        };

        list.Add(mapped);
    }

    if (list.Count == 0) return Results.BadRequest("No items supplied");

    await db.LogItems.AddRangeAsync(list);
    await db.SaveChangesAsync();

    return Results.Created("/logs", new { inserted = list.Count });
});

app.Run();

static string ReadString(JsonElement el, params string[] keys)
{
    foreach (var k in keys)
    {
        if (el.TryGetProperty(k, out var v) && v.ValueKind != JsonValueKind.Null)
            return v.GetString() ?? "";
    }
    return "";
}

static DateTime ReadDateTime(JsonElement el, params string[] keys)
{
    foreach (var k in keys)
    {
        if (el.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String)
        {
            var s = v.GetString();
            if (DateTime.TryParse(s, out var dt)) return dt;
        }
        else if (el.TryGetProperty(k, out v) && v.ValueKind == JsonValueKind.Number)
        {
            if (v.TryGetInt64(out var unix)) return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
        }
    }
    return DateTime.MinValue;
}

// ---- Models & DbContext ----

public class LogItem
{
    public int Id { get; set; }
    public DateTime MigrationStartTime { get; set; } = DateTime.MinValue;
    public string SubJobId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Type { get; set; } = "";
    public string SourceID { get; set; } = "";
    public string Source { get; set; } = "";
    public string DestinationId { get; set; } = "";
    public string Destination { get; set; } = "";
    public string Size { get; set; } = "";
    public string Status { get; set; } = "";
    public string MigrationAction { get; set; } = "";
    public string? Comment { get; set; }
    public string? ErrorCode { get; set; }
}

public class LogsDb : DbContext
{
    public LogsDb(DbContextOptions<LogsDb> options) : base(options) { }
    public DbSet<LogItem> LogItems => Set<LogItem>();
}
