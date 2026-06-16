using System.Text;
using Elasticsearch.Net;
using Elk.Mcp.Elasticsearch;
using Nest;
using Xunit;

namespace Elk.Mcp.Tests;

public sealed class NestElasticsearchOperationsTests
{
    [Fact]
    public async Task Index_discovery_requests_use_the_expected_endpoints()
    {
        var connection = new RecordingConnection(
            """{"indices":[],"aliases":[],"data_streams":[]}""");
        var pool = new SingleNodeConnectionPool(new Uri("http://localhost:9200"));
        var settings = new ConnectionSettings(pool, connection);
        var operations = new NestElasticsearchOperations(new ElasticClient(settings).LowLevel);

        using var resolved = await operations.ResolveIndicesAsync("logs-*");
        Assert.Equal("/_resolve/index/logs-*", connection.LastUri?.AbsolutePath);
        Assert.Empty(connection.LastUri?.Query ?? string.Empty);

        using var aliases = await operations.GetAliasesAsync("logs-*");
        Assert.Equal("/logs-*/_alias", connection.LastUri?.AbsolutePath);
        Assert.Empty(connection.LastUri?.Query ?? string.Empty);

        using var indices = await operations.GetCatIndicesAsync("*");

        Assert.Equal("/_cat/indices/*", connection.LastUri?.AbsolutePath);
        Assert.Contains("format=json", connection.LastUri?.Query);
        Assert.Contains("h=index%2Cstatus%2Cdocs.count", connection.LastUri?.Query);

        using var shards = await operations.GetShardsAsync("logs-*");

        Assert.Equal("/_cat/shards/logs-*", connection.LastUri?.AbsolutePath);
        Assert.Contains("format=json", connection.LastUri?.Query);
        Assert.Contains("h=index%2Cshard%2Cprirep%2Cstate%2Cdocs%2Cstore%2Cnode", connection.LastUri?.Query);
    }

    private sealed class RecordingConnection : InMemoryConnection
    {
        public RecordingConnection(string responseBody)
            : base(Encoding.UTF8.GetBytes(responseBody))
        {
        }

        public Uri? LastUri { get; private set; }

        public override TResponse Request<TResponse>(RequestData requestData)
        {
            LastUri = requestData.Uri;
            return base.Request<TResponse>(requestData);
        }

        public override Task<TResponse> RequestAsync<TResponse>(
            RequestData requestData,
            CancellationToken cancellationToken)
        {
            LastUri = requestData.Uri;
            return base.RequestAsync<TResponse>(requestData, cancellationToken);
        }
    }
}
