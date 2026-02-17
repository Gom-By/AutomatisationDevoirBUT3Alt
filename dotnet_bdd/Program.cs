using System.Text.Json;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
});

// DB configuration
var client = new MongoClient("mongodb://logs-mongo:27017");
builder.Services.AddSingleton<IMongoClient>(_ => client);

builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
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

// ---- Models ----
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
