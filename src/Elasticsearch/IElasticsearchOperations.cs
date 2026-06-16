using System.Text.Json;

namespace Elk.Mcp.Elasticsearch;

public interface IElasticsearchOperations
{
    Task<JsonDocument> ResolveIndicesAsync(string indexPattern, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetAliasesAsync(string indexPattern, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetCatIndicesAsync(string indexPattern, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetMappingsAsync(string index, CancellationToken cancellationToken = default);
    Task<JsonDocument> SearchAsync(string index, JsonElement queryBody, CancellationToken cancellationToken = default);
    Task<JsonDocument> EsqlAsync(string query, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetShardsAsync(string? index, CancellationToken cancellationToken = default);
}
