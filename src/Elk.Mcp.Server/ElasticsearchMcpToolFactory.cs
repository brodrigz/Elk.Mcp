using Elk.Mcp.Tools;
using ModelContextProtocol.Server;

namespace Elk.Mcp.Server;

public static class ElasticsearchMcpToolFactory
{
    private static readonly IReadOnlyDictionary<string, string> Titles =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["list_indices"] = "List ES indices",
            ["get_mappings"] = "Get ES index mappings",
            ["search"] = "Elasticsearch search DSL query",
            ["esql"] = "Elasticsearch ES|QL query",
            ["get_shards"] = "Get ES shard information"
        };

    public static IReadOnlyList<McpServerTool> Create(IElasticsearchToolCatalog catalog)
    {
        if (catalog is null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        return catalog.Tools
            .Select(function =>
                McpServerTool.Create(function, new McpServerToolCreateOptions
                {
                    Title = Titles[function.Name],
                    ReadOnly = true,
                    UseStructuredContent = false
                }))
            .ToArray();
    }
}
