using System.Text.Json;
using Elk.Mcp.Elasticsearch;
using Elk.Mcp.Server;
using Elk.Mcp.Tools;
using Microsoft.Extensions.AI;
using Xunit;

namespace Elk.Mcp.Tests;

public sealed class ElasticsearchToolsTests
{
    [Fact]
    public void Catalog_exposes_original_tool_names_and_descriptions()
    {
        var catalog = new ElasticsearchToolCatalog(
            new ElasticsearchTools(new StubOperations()));

        Assert.Collection(
            catalog.Tools,
            tool => AssertTool(tool, "list_indices", "List all available Elasticsearch indices"),
            tool => AssertTool(tool, "get_mappings", "Get field mappings for a specific Elasticsearch index"),
            tool => AssertTool(tool, "search", "Perform an Elasticsearch search with the provided query DSL."),
            tool => AssertTool(tool, "esql", "Perform an Elasticsearch ES|QL query."),
            tool => AssertTool(tool, "get_shards", "Get shard information for all or specific indices."));

        AssertRequired(catalog.Tools[0], "index_pattern");
        AssertRequired(catalog.Tools[1], "index");
        AssertRequired(catalog.Tools[2], "index", "query_body");
        AssertRequired(catalog.Tools[3], "query");
        AssertRequired(catalog.Tools[4]);
    }

    [Fact]
    public async Task List_indices_returns_resolved_index_metadata()
    {
        var operations = new StubOperations
        {
            ResolveIndicesResponse =
                """
                {
                  "indices": [
                    {
                      "name": "sales",
                      "attributes": ["open"],
                      "aliases": ["sales-current"],
                      "data_stream": null
                    }
                  ],
                  "aliases": [],
                  "data_streams": []
                }
                """
        };

        var tools = new ElasticsearchTools(operations);
        var result = await tools.ListIndicesAsync("*");

        Assert.Collection(
            result,
            item => Assert.Equal("Found 1 indices:", Text(item)),
            item =>
            {
                Assert.Contains("\"index\":\"sales\"", Text(item));
                Assert.Contains("\"attributes\":[\"open\"]", Text(item));
                Assert.Contains("\"aliases\":[\"sales-current\"]", Text(item));
                Assert.Contains("\"data_stream\":null", Text(item));
            });
    }

    [Fact]
    public async Task Get_mappings_returns_only_the_first_mapping()
    {
        var operations = new StubOperations
        {
            MappingsResponse =
                """
                {
                  "first": { "mappings": { "properties": { "sales": { "type": "long" } } } },
                  "second": { "mappings": { "properties": { "region": { "type": "keyword" } } } }
                }
                """
        };

        var tools = new ElasticsearchTools(operations);
        var result = await tools.GetMappingsAsync("*");

        Assert.Collection(
            result,
            item => Assert.Equal("Mappings for index *:", Text(item)),
            item =>
            {
                Assert.Contains("\"sales\"", Text(item));
                Assert.DoesNotContain("\"region\"", Text(item));
            });
    }

    [Fact]
    public void Mcp_tools_preserve_original_titles_and_read_only_annotations()
    {
        var catalog = new ElasticsearchToolCatalog(
            new ElasticsearchTools(new StubOperations()));

        var tools = ElasticsearchMcpToolFactory.Create(catalog);

        Assert.Equal(
            new[]
            {
                "List ES indices",
                "Get ES index mappings",
                "Elasticsearch search DSL query",
                "Elasticsearch ES|QL query",
                "Get ES shard information"
            },
            tools.Select(tool => tool.ProtocolTool.Annotations?.Title));

        Assert.All(tools, tool =>
        {
            Assert.True(tool.ProtocolTool.Annotations?.ReadOnlyHint);
            Assert.Null(tool.ProtocolTool.OutputSchema);
        });
    }

    [Fact]
    public async Task Search_preserves_original_content_order_and_augments_source()
    {
        var operations = new StubOperations
        {
            SearchResponse =
                """
                {
                  "hits": {
                    "total": { "value": 2, "relation": "eq" },
                    "hits": [
                      { "_source": { "region": "north", "sales": 10 } },
                      { "_source": { "region": "south", "sales": 20 } }
                    ]
                  },
                  "aggregations": {
                    "sales_by_region": { "buckets": [] }
                  }
                }
                """
        };

        var tools = new ElasticsearchTools(operations);
        using var query = JsonDocument.Parse("""{"query":{"match_all":{}},"_source":["existing"]}""");

        var result = await tools.SearchAsync("sales", query.RootElement, ["region"]);

        Assert.Collection(
            result,
            item => Assert.Equal("Total results: 2, showing 2.", Text(item)),
            item => Assert.Contains("\"region\":\"north\"", Text(item)),
            item => Assert.Equal("Aggregations results:", Text(item)),
            item => Assert.Contains("\"sales_by_region\"", Text(item)));

        Assert.NotNull(operations.LastSearchBody);
        Assert.Equal(
            new[] { "existing", "region" },
            operations.LastSearchBody.Value.GetProperty("_source")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray());
    }

    [Fact]
    public async Task Pure_aggregation_search_omits_result_summary()
    {
        var operations = new StubOperations
        {
            SearchResponse =
                """
                {
                  "hits": {
                    "total": { "value": 0, "relation": "eq" },
                    "hits": []
                  },
                  "aggregations": {
                    "total_sales": { "value": 30 }
                  }
                }
                """
        };

        var tools = new ElasticsearchTools(operations);
        using var query = JsonDocument.Parse("""{"size":0}""");

        var result = await tools.SearchAsync("sales", query.RootElement);

        Assert.Collection(
            result,
            item => Assert.Equal("Aggregations results:", Text(item)),
            item => Assert.Contains("\"total_sales\"", Text(item)));
    }

    [Fact]
    public async Task Esql_converts_columns_and_values_to_objects()
    {
        var operations = new StubOperations
        {
            EsqlResponse =
                """
                {
                  "is_partial": false,
                  "columns": [
                    { "name": "region", "type": "keyword" },
                    { "name": "sales", "type": "long" }
                  ],
                  "values": [
                    ["north", 10],
                    ["south", 20]
                  ]
                }
                """
        };

        var tools = new ElasticsearchTools(operations);
        var result = await tools.EsqlAsync("FROM sales");

        Assert.Collection(
            result,
            item => Assert.Equal("Results", Text(item)),
            item =>
            {
                Assert.Contains("\"region\":\"north\"", Text(item));
                Assert.Contains("\"sales\":20", Text(item));
            });
    }

    private static void AssertTool(AIFunction tool, string name, string description)
    {
        Assert.Equal(name, tool.Name);
        Assert.Equal(description, tool.Description);
        Assert.Equal(JsonValueKind.Object, tool.JsonSchema.ValueKind);
    }

    private static void AssertRequired(AIFunction tool, params string[] expected)
    {
        var required = tool.JsonSchema.TryGetProperty("required", out var requiredElement)
            ? requiredElement.EnumerateArray().Select(item => item.GetString()).ToArray()
            : [];

        Assert.Equal(expected, required);
    }

    private static string Text(AIContent content) =>
        Assert.IsType<TextContent>(content).Text;

    private sealed class StubOperations : IElasticsearchOperations
    {
        public string SearchResponse { get; set; } =
            """{"hits":{"total":{"value":0},"hits":[]},"aggregations":{}}""";

        public string EsqlResponse { get; set; } =
            """{"columns":[],"values":[]}""";

        public string ResolveIndicesResponse { get; set; } =
            """{"indices":[],"aliases":[],"data_streams":[]}""";

        public string MappingsResponse { get; set; } =
            """{"index":{"mappings":{"properties":{}}}}""";

        public JsonElement? LastSearchBody { get; private set; }

        public Task<JsonDocument> ResolveIndicesAsync(
            string indexPattern,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(JsonDocument.Parse(ResolveIndicesResponse));

        public Task<JsonDocument> GetAliasesAsync(
            string indexPattern,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(JsonDocument.Parse("{}"));

        public Task<JsonDocument> GetCatIndicesAsync(
            string indexPattern,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(JsonDocument.Parse("[]"));

        public Task<JsonDocument> GetMappingsAsync(
            string index,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(JsonDocument.Parse(MappingsResponse));

        public Task<JsonDocument> SearchAsync(
            string index,
            JsonElement queryBody,
            CancellationToken cancellationToken = default)
        {
            LastSearchBody = queryBody.Clone();
            return Task.FromResult(JsonDocument.Parse(SearchResponse));
        }

        public Task<JsonDocument> EsqlAsync(
            string query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(JsonDocument.Parse(EsqlResponse));

        public Task<JsonDocument> GetShardsAsync(
            string? index,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(JsonDocument.Parse("[]"));
    }
}
