namespace Elk.Mcp.Elasticsearch;

public sealed class ElasticsearchOptions
{
    public string Url { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool SkipCertificateValidation { get; set; }
}
