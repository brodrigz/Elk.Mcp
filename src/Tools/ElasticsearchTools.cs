using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Elk.Mcp.Elasticsearch;
using Microsoft.Extensions.AI;

namespace Elk.Mcp.Tools;

public sealed class ElasticsearchTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IElasticsearchOperations _operations;

    public ElasticsearchTools(IElasticsearchOperations operations)
    {
        _operations = operations ?? throw new ArgumentNullException(nameof(operations));
    }

    public async Task<IReadOnlyList<AIContent>> ListIndicesAsync(
        [Description("Index pattern of Elasticsearch indices to list")]
        string index_pattern,
        CancellationToken cancellationToken = default)
    {
        using var response = await _operations
            .ResolveIndicesAsync(index_pattern, cancellationToken)
            .ConfigureAwait(false);

        if (!response.RootElement.TryGetProperty("indices", out var indicesElement) ||
            indicesElement.ValueKind is not JsonValueKind.Array)
        {
            throw new ElasticsearchOperationException(
                "Elasticsearch returned an invalid resolve-index response.");
        }

        var indices = indicesElement.Deserialize<List<ResolvedIndexResponse>>(JsonOptions)
            ?? throw new ElasticsearchOperationException(
                "Elasticsearch returned an invalid resolve-index response.");

        return Content(
            $"Found {indices.Count} indices:",
            JsonSerializer.Serialize(
                indices.Select(item => new
                {
                    index = item.Name,
                    attributes = item.Attributes,
                    aliases = item.Aliases,
                    data_stream = item.DataStream
                }),
                JsonOptions));
    }

    public async Task<IReadOnlyList<AIContent>> GetMappingsAsync(
        [Description("Name of the Elasticsearch index to get mappings for")]
        string index,
        CancellationToken cancellationToken = default)
    {
        using var response = await _operations
            .GetMappingsAsync(index, cancellationToken)
            .ConfigureAwait(false);

        if (response.RootElement.ValueKind is not JsonValueKind.Object)
        {
            throw new ElasticsearchOperationException("Elasticsearch returned an invalid mappings response.");
        }

        using var mappings = response.RootElement.EnumerateObject();
        if (!mappings.MoveNext())
        {
            throw new ElasticsearchOperationException("Elasticsearch returned no mappings.");
        }

        return Content(
            $"Mappings for index {index}:",
            mappings.Current.Value.GetRawText());
    }

    public async Task<IReadOnlyList<AIContent>> SearchAsync(
        [Description("Name of the Elasticsearch index to search")]
        string index,
        [Description("Complete Elasticsearch query DSL object that can include query, size, from, sort, etc.")]
        JsonElement query_body,
        [Description("Name of the fields that need to be returned (optional)")]
        IReadOnlyList<string>? fields = null,
        CancellationToken cancellationToken = default)
    {
        var queryBody = JsonNode.Parse(query_body.GetRawText()) as JsonObject
            ?? throw new ArgumentException("The query_body parameter must be a JSON object.", nameof(query_body));

        AddSourceFields(queryBody, fields);

        using var queryDocument = JsonDocument.Parse(queryBody.ToJsonString());
        using var response = await _operations
            .SearchAsync(index, queryDocument.RootElement, cancellationToken)
            .ConfigureAwait(false);

        return FormatSearchResponse(response.RootElement);
    }

    public async Task<IReadOnlyList<AIContent>> EsqlAsync(
        [Description("Complete Elasticsearch ES|QL query")]
        string query,
        CancellationToken cancellationToken = default)
    {
        using var response = await _operations
            .EsqlAsync(query, cancellationToken)
            .ConfigureAwait(false);

        if (!response.RootElement.TryGetProperty("columns", out var columns) ||
            columns.ValueKind is not JsonValueKind.Array ||
            !response.RootElement.TryGetProperty("values", out var values) ||
            values.ValueKind is not JsonValueKind.Array)
        {
            throw new ElasticsearchOperationException("Elasticsearch returned an invalid ES|QL response.");
        }

        var columnNames = columns
            .EnumerateArray()
            .Select(column => column.GetProperty("name").GetString()
                ?? throw new ElasticsearchOperationException("An ES|QL column has no name."))
            .ToArray();

        var objects = new JsonArray();
        foreach (var row in values.EnumerateArray())
        {
            var rowValues = row.EnumerateArray().ToArray();
            var result = new JsonObject();

            for (var index = 0; index < rowValues.Length; index++)
            {
                result[columnNames[index]] = JsonNode.Parse(rowValues[index].GetRawText());
            }

            objects.Add(result);
        }

        return Content("Results", objects.ToJsonString());
    }

    public async Task<IReadOnlyList<AIContent>> GetShardsAsync(
        [Description("Optional index name to get shard information for")]
        string? index = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await _operations
            .GetShardsAsync(index, cancellationToken)
            .ConfigureAwait(false);

        var shards = response.RootElement.Deserialize<List<CatShardResponse>>(JsonOptions)
            ?? throw new ElasticsearchOperationException("Elasticsearch returned an invalid shards response.");

        return Content(
            $"Found {shards.Count} shards:",
            JsonSerializer.Serialize(shards, JsonOptions));
    }

    private static IReadOnlyList<AIContent> FormatSearchResponse(JsonElement response)
    {
        if (!response.TryGetProperty("hits", out var hitsObject) ||
            !hitsObject.TryGetProperty("hits", out var hits) ||
            hits.ValueKind is not JsonValueKind.Array)
        {
            throw new ElasticsearchOperationException("Elasticsearch returned an invalid search response.");
        }

        var aggregations = response.TryGetProperty("aggregations", out var aggregationValue) &&
            aggregationValue.ValueKind is JsonValueKind.Object
            ? aggregationValue
            : default;

        var hasAggregations = aggregations.ValueKind is JsonValueKind.Object &&
            aggregations.EnumerateObject().Any();
        var hitItems = hits.EnumerateArray().ToArray();
        var result = new List<AIContent>();

        if (!hasAggregations || hitItems.Length > 0)
        {
            result.Add(new TextContent(
                $"Total results: {TryGetTotal(hitsObject)}, showing {hitItems.Length}."));
        }

        if (hitItems.Length > 0)
        {
            var sources = new JsonArray();
            foreach (var hit in hitItems)
            {
                sources.Add(hit.TryGetProperty("_source", out var source)
                    ? JsonNode.Parse(source.GetRawText())
                    : null);
            }

            result.Add(new TextContent(sources.ToJsonString()));
        }

        if (hasAggregations)
        {
            result.Add(new TextContent("Aggregations results:"));
            result.Add(new TextContent(aggregations.GetRawText()));
        }

        return result;
    }

    private static string TryGetTotal(JsonElement hits)
    {
        if (!hits.TryGetProperty("total", out var total))
        {
            return "unknown";
        }

        return total.ValueKind switch
        {
            JsonValueKind.Object when total.TryGetProperty("value", out var value) => value.ToString(),
            JsonValueKind.Number => total.ToString(),
            _ => "unknown"
        };
    }

    private static void AddSourceFields(JsonObject queryBody, IReadOnlyList<string>? fields)
    {
        if (fields is not { Count: > 0 })
        {
            return;
        }

        if (queryBody["_source"] is JsonArray source)
        {
            foreach (var field in fields)
            {
                source.Add(field);
            }
        }
        else
        {
            queryBody["_source"] = new JsonArray(
                fields.Select(field => JsonValue.Create(field)).ToArray());
        }
    }

    private static IReadOnlyList<AIContent> Content(string heading, string json) =>
    [
        new TextContent(heading),
        new TextContent(json)
    ];

    private sealed class ResolvedIndexResponse
    {
        public string Name { get; set; } = string.Empty;
        public string[] Attributes { get; set; } = [];
        public string[] Aliases { get; set; } = [];

        [JsonPropertyName("data_stream")]
        public string? DataStream { get; set; }
    }

    private sealed class CatShardResponse
    {
        public string Index { get; set; } = string.Empty;

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int Shard { get; set; }

        public string Prirep { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public ulong? Docs { get; set; }

        public string? Store { get; set; }
        public string? Node { get; set; }
    }
}
