using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Ensure JSON binding is case-insensitive and accepts snake_case from Python
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    // Allow incoming snake_case to bind to PascalCase C# properties by using a custom converter during model binding:
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
});

// DB configuration
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient("mongodb://localhost:27017"));

builder.Services.AddSingleton(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase("logsdb");
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapLogsRoutes();

app.Run();

// ---- Models & DbContext ----

public class LogItem
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("migration_start_time")]
    public DateTime MigrationStartTime { get; set; } = DateTime.MinValue;

    [BsonElement("sub_job_id")]
    public string SubJobId { get; set; } = "";

    [BsonElement("title")]
    public string Title { get; set; } = "";

    [BsonElement("type")]
    public string Type { get; set; } = "";

    [BsonElement("source_id")]
    public string SourceID { get; set; } = "";

    [BsonElement("source")]
    public string Source { get; set; } = "";

    [BsonElement("destination_id")]
    public string DestinationId { get; set; } = "";

    [BsonElement("destination")]
    public string Destination { get; set; } = "";

    [BsonElement("size")]
    public string Size { get; set; } = "";

    [BsonElement("status")]
    public string Status { get; set; } = "";

    [BsonElement("migration_action")]
    public string MigrationAction { get; set; } = "";

    [BsonElement("comment")]
    public string? Comment { get; set; }

    [BsonElement("error_code")]
    public string? ErrorCode { get; set; }
}
