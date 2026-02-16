using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using MongoDB.Driver;

public static class LogsRoutes
{
    public static void MapLogsRoutes(this WebApplication app)
    {
        app.MapGet("/logs", async (IMongoDatabase db) =>
        {
            var collection = db.GetCollection<LogItem>("logs");
            var logs = await collection.Find(FilterDefinition<LogItem>.Empty)
                                        .ToListAsync();
            return logs;
        });

        app.MapGet("/logs/{id:int}", async (int id, IMongoDatabase db) =>
        {
            var collection = db.GetCollection<LogItem>("logs");
            var item = collection.Find(x => x.Id == id.ToString()).FirstOrDefaultAsync();
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        app.MapPost("/logs/bulk", async (HttpRequest request, IMongoDatabase db) =>
        {
            using var doc = await JsonDocument.ParseAsync(request.Body);

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return Results.BadRequest("Expected JSON array");

            var incomingItems = new List<LogItem>();
            var existingIds = new HashSet<string>();

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var logItem = new LogItem
                {
                    MigrationStartTime = ReadDateTime(el, "migration_start_time"),
                    SubJobId = ReadString(el, "sub_job_id"),
                    Title = ReadString(el, "title"),
                    Type = ReadString(el, "type"),
                    SourceID = ReadString(el, "source_id"),
                    Source = ReadString(el, "source"),
                    DestinationId = ReadString(el, "destination_id"),
                    Destination = ReadString(el, "destination"),
                    Size = ReadString(el, "size"),
                    Status = ReadString(el, "status"),
                    MigrationAction = ReadString(el, "migration_action"),
                    Comment = ReadString(el, "comment"),
                    ErrorCode = ReadString(el, "error_code")
                };

                incomingItems.Add(logItem);
            }

            if (incomingItems.Count == 0)
                return Results.BadRequest("No items supplied");

            var existingItems = await db.GetCollection<LogItem>("logs")
                                          .Find(Builders<LogItem>.Filter.In(item => item.SubJobId, incomingItems.Select(i => i.SubJobId)))
                                          .ToListAsync();

            foreach (var existingItem in existingItems)
            {
                existingIds.Add(existingItem.SubJobId);
            }

            var newItems = incomingItems
                               .Where(item => !existingIds.Contains(item.SubJobId))
                               .ToList();

            if (newItems.Count == 0)
                return Results.BadRequest("No new items to insert");
            await db.GetCollection<LogItem>("logs").InsertManyAsync(newItems);

            return Results.Created("/logs", new { inserted = newItems.Count });
        });
    }

    static string ReadString(JsonElement el, string key)
    {
        if (el.TryGetProperty(key, out var v) && v.ValueKind != JsonValueKind.Null)
        {
            return v.GetString() ?? "";
        }
        return "";
    }

    static DateTime ReadDateTime(JsonElement el, string key)
    {

        if (el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String)
        {
            var s = v.GetString();
            if (DateTime.TryParse(s, out var dt)) return dt;
        }
        else if (el.TryGetProperty(key, out v) && v.ValueKind == JsonValueKind.Number)
        {
            if (v.TryGetInt64(out var unix)) return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
        }
        return DateTime.MinValue;
    }
}
