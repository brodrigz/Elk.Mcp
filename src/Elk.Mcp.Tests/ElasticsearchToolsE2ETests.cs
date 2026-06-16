using System.Text.Json;
using Elk.Mcp.Elasticsearch;
using Elk.Mcp.Tools;
using Xunit;

namespace Elk.Mcp.Tests;

public sealed class ElasticsearchToolsE2ETests
{
    private readonly IElasticsearchOperations _operations =
        NestElasticsearchOperations.Create(E2EEnvironment.CreateOptions());

    private ElasticsearchTools Tools => new(_operations);

    [E2EFact]
    [Trait("Category", "E2E")]
    public async Task List_indices_executes_against_configured_cluster()
    {
        var result = await Tools.ListIndicesAsync(E2EEnvironment.IndexPattern);
        Assert.NotEmpty(result);
    }

    [E2EFact]
    [Trait("Category", "E2E")]
    public async Task Resolve_indices_executes_against_configured_cluster()
    {
        using var result = await _operations.ResolveIndicesAsync(E2EEnvironment.IndexPattern);
        Assert.True(result.RootElement.TryGetProperty("indices", out _));
    }

    [E2EFact]
    [Trait("Category", "E2E")]
    public async Task Get_aliases_executes_against_configured_cluster()
    {
        using var result = await _operations.GetAliasesAsync(E2EEnvironment.IndexPattern);
        Assert.Equal(JsonValueKind.Object, result.RootElement.ValueKind);
    }

    [E2EFact]
    [Trait("Category", "E2E")]
    public async Task Get_cat_indices_executes_against_configured_cluster()
    {
        using var result = await _operations.GetCatIndicesAsync(E2EEnvironment.IndexPattern);
        Assert.Equal(JsonValueKind.Array, result.RootElement.ValueKind);
    }

    [E2EFact]
    [Trait("Category", "E2E")]
    public async Task Get_mappings_executes_against_configured_cluster()
    {
        var result = await Tools.GetMappingsAsync(E2EEnvironment.IndexPattern);
        Assert.NotEmpty(result);
    }

    [E2EFact]
    [Trait("Category", "E2E")]
    public async Task Search_executes_zero_size_query_against_configured_cluster()
    {
        using var query = JsonDocument.Parse("""{"size":0,"query":{"match_all":{}}}""");
        var result = await Tools.SearchAsync(E2EEnvironment.IndexPattern, query.RootElement);
        Assert.NotEmpty(result);
    }

    [E2EFact]
    [Trait("Category", "E2E")]
    public async Task Esql_executes_show_info_against_configured_cluster()
    {
        var result = await Tools.EsqlAsync("SHOW INFO");
        Assert.NotEmpty(result);
    }

    [E2EFact]
    [Trait("Category", "E2E")]
    public async Task Get_shards_executes_against_configured_cluster()
    {
        var result = await Tools.GetShardsAsync(E2EEnvironment.IndexPattern);
        Assert.NotEmpty(result);
    }

    public static class E2EEnvironment
    {
        public static string IndexPattern =>
            Environment.GetEnvironmentVariable("ELK_MCP_E2E_INDEX") ?? "*";

        public static ElasticsearchOptions CreateOptions()
        {
            var url = Environment.GetEnvironmentVariable("ES_URL");
            if (string.IsNullOrWhiteSpace(url))
            {
                url = "http://localhost:9200";
            }

            return new ElasticsearchOptions
            {
                Url = url,
                Username = Environment.GetEnvironmentVariable("ES_USERNAME"),
                Password = Environment.GetEnvironmentVariable("ES_PASSWORD"),
                SkipCertificateValidation = true
            };
        }
    }
}

public sealed class E2EFactAttribute : FactAttribute
{
    public E2EFactAttribute()
    {
        //if (!string.Equals(
        //    Environment.GetEnvironmentVariable("ELK_MCP_RUN_E2E"),
        //    "true",
        //    StringComparison.OrdinalIgnoreCase))
        //{
        //    Skip = "Set ELK_MCP_RUN_E2E=true to run tests against a real Elasticsearch cluster.";
        //}
    }
}