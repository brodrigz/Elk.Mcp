namespace Elk.Mcp.Elasticsearch;

public sealed class ElasticsearchOperationException : Exception
{
    public ElasticsearchOperationException(string message)
        : base(message)
    {
    }

    public ElasticsearchOperationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
