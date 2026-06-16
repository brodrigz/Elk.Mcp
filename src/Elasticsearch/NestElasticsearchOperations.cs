using System.Collections.Specialized;
using System.Text.Json;
using Elasticsearch.Net;
using Elasticsearch.Net.Specification.CatApi;
using Nest;
using ElasticsearchHttpMethod = Elasticsearch.Net.HttpMethod;

namespace Elk.Mcp.Elasticsearch;

public sealed class NestElasticsearchOperations : IElasticsearchOperations
{
    private readonly IElasticLowLevelClient _client;

    public NestElasticsearchOperations(IElasticLowLevelClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public static NestElasticsearchOperations Create(ElasticsearchOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (!Uri.TryCreate(options.Url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("A valid Elasticsearch URL is required.", nameof(options));
        }

        var settings = new ConnectionSettings(uri)
            .EnableApiVersioningHeader()
            .UserAgent("elk-mcp/0.1.0");

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            settings = settings.GlobalHeaders(new NameValueCollection
            {
                ["Authorization"] = $"ApiKey {options.ApiKey}"
            });
        }
        else if (!string.IsNullOrWhiteSpace(options.Username))
        {
            if (options.Password is null)
            {
                throw new ArgumentException(
                    "A password is required when an Elasticsearch username is configured.",
                    nameof(options));
            }

            settings = settings.BasicAuthentication(options.Username, options.Password);
        }

        if (options.SkipCertificateValidation)
        {
            settings = settings.ServerCertificateValidationCallback((_, _, _, _) => true);
        }

        return new NestElasticsearchOperations(new ElasticClient(settings).LowLevel);
    }

    public Task<JsonDocument> ResolveIndicesAsync(
        string indexPattern,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            ElasticsearchHttpMethod.GET,
            $"/_resolve/index/{EscapePathSegment(indexPattern)}",
            null,
            null,
            cancellationToken);

    public Task<JsonDocument> GetAliasesAsync(
        string indexPattern,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            ElasticsearchHttpMethod.GET,
            $"/{EscapePathSegment(indexPattern)}/_alias",
            null,
            null,
            cancellationToken);

    public Task<JsonDocument> GetCatIndicesAsync(
        string indexPattern,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            ElasticsearchHttpMethod.GET,
            $"/_cat/indices/{EscapePathSegment(indexPattern)}",
            null,
            new CatIndicesRequestParameters
            {
                Headers = ["index", "status", "docs.count"],
                Format = "json"
            },
            cancellationToken);

    public Task<JsonDocument> GetMappingsAsync(
        string index,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            ElasticsearchHttpMethod.GET,
            $"/{EscapePathSegment(index)}/_mapping",
            null,
            null,
            cancellationToken);

    public Task<JsonDocument> SearchAsync(
        string index,
        JsonElement queryBody,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            ElasticsearchHttpMethod.POST,
            $"/{EscapePathSegment(index)}/_search",
            PostData.String(queryBody.GetRawText()),
            null,
            cancellationToken);

    public Task<JsonDocument> EsqlAsync(
        string query,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            ElasticsearchHttpMethod.POST,
            "/_query",
            PostData.String(JsonSerializer.Serialize(new { query })),
            null,
            cancellationToken);

    public Task<JsonDocument> GetShardsAsync(
        string? index,
        CancellationToken cancellationToken = default)
    {
        var indexPath = string.IsNullOrWhiteSpace(index)
            ? string.Empty
            : $"/{EscapePathSegment(index)}";

        return SendAsync(
            ElasticsearchHttpMethod.GET,
            $"/_cat/shards{indexPath}",
            null,
            new CatShardsRequestParameters
            {
                Headers = ["index", "shard", "prirep", "state", "docs", "store", "node"],
                Format = "json"
            },
            cancellationToken);
    }

    private async Task<JsonDocument> SendAsync(
        ElasticsearchHttpMethod method,
        string path,
        PostData? postData,
        IRequestParameters? requestParameters,
        CancellationToken cancellationToken)
    {
        StringResponse response;

        try
        {
            response = await _client.DoRequestAsync<StringResponse>(
                method,
                path,
                cancellationToken,
                postData,
                requestParameters).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw new ElasticsearchOperationException(
                $"The Elasticsearch request could not be completed: {exception.Message}",
                exception);
        }

        if (!response.Success)
        {
            var responseDetail = FormatErrorDetail(response.Body);
            throw new ElasticsearchOperationException(
                $"Elasticsearch returned HTTP status " +
                $"{response.HttpStatusCode?.ToString() ?? "unknown"}{responseDetail}.");
        }

        try
        {
            return JsonDocument.Parse(response.Body);
        }
        catch (JsonException exception)
        {
            throw new ElasticsearchOperationException(
                "Elasticsearch returned an invalid JSON response.",
                exception);
        }
    }

    private static string EscapePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", nameof(value));
        }

        return Uri.EscapeDataString(value)
            .Replace("%2A", "*", StringComparison.OrdinalIgnoreCase)
            .Replace("%2C", ",", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatErrorDetail(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return string.Empty;
        }

        const int maxLength = 2000;
        var detail = responseBody.Trim();

        if (detail.Length > maxLength)
        {
            detail = $"{detail[..maxLength]}...";
        }

        return $": {detail}";
    }
}
