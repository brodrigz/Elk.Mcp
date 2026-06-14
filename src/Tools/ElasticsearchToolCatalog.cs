using Microsoft.Extensions.AI;

namespace Elk.Mcp.Tools;

public sealed class ElasticsearchToolCatalog : IElasticsearchToolCatalog
{
    public ElasticsearchToolCatalog(ElasticsearchTools tools)
    {
        if (tools is null)
        {
            throw new ArgumentNullException(nameof(tools));
        }

        Tools =
        [
            AIFunctionFactory.Create(
                tools.ListIndicesAsync,
                name: "list_indices",
                description: "List all available Elasticsearch indices"),
            AIFunctionFactory.Create(
                tools.GetMappingsAsync,
                name: "get_mappings",
                description: "Get field mappings for a specific Elasticsearch index"),
            AIFunctionFactory.Create(
                tools.SearchAsync,
                name: "search",
                description: "Perform an Elasticsearch search with the provided query DSL."),
            AIFunctionFactory.Create(
                tools.EsqlAsync,
                name: "esql",
                description: "Perform an Elasticsearch ES|QL query."),
            AIFunctionFactory.Create(
                tools.GetShardsAsync,
                name: "get_shards",
                description: "Get shard information for all or specific indices.")
        ];
    }

    public IReadOnlyList<AIFunction> Tools { get; }
}
