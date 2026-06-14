using Elk.Mcp.Elasticsearch;
using Elk.Mcp.Server;
using Elk.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

var elasticsearchOptions = new ElasticsearchOptions
{
    Url = Environment.GetEnvironmentVariable("ES_URL") ?? string.Empty,
    ApiKey = Environment.GetEnvironmentVariable("ES_API_KEY"),
    Username = Environment.GetEnvironmentVariable("ES_USERNAME"),
    Password = Environment.GetEnvironmentVariable("ES_PASSWORD"),
    SkipCertificateValidation = bool.TryParse(
        Environment.GetEnvironmentVariable("ES_SSL_SKIP_VERIFY"),
        out var skipCertificateValidation) &&
        skipCertificateValidation
};

var operations = NestElasticsearchOperations.Create(elasticsearchOptions);
var tools = new ElasticsearchTools(operations);
var catalog = new ElasticsearchToolCatalog(tools);

var mcpTools = ElasticsearchMcpToolFactory.Create(catalog);

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation
        {
            Name = "elk-mcp",
            Version = "0.1.0"
        };
        options.ServerInstructions = "Provides access to Elasticsearch";
    })
    .WithStdioServerTransport()
    .WithTools(mcpTools);

await builder.Build().RunAsync();
