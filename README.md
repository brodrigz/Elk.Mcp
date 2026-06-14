# Elk.Mcp

.NET implementation of the five base tools exposed by Elastic's Elasticsearch MCP server.
The first implementation stage preserves the original agent-visible tool contract while
using NEST 7.17 compatibility mode for Elasticsearch 8.

## Tools

- `list_indices`
- `get_mappings`
- `search`
- `esql`
- `get_shards`

Tool names, descriptions, parameter descriptions, titles, read-only annotations, and
result content ordering follow the Rust implementation.

## Configuration

Set:

- `ES_URL`
- `ES_API_KEY`, or `ES_USERNAME` and `ES_PASSWORD`
- `ES_SSL_SKIP_VERIFY=true` only for development environments

## Run

```powershell
dotnet run --project Elk.Mcp.Server
```

The server uses the MCP stdio transport. Logs are written to standard error.

## Design

- `IElasticsearchOperations` isolates Elasticsearch access.
- `NestElasticsearchOperations` is the initial Elasticsearch 8 implementation.
- `ElasticsearchTools` owns the agent-visible transformations.
- `IElasticsearchToolCatalog` exposes the canonical `AIFunction` declarations.
- `ElasticsearchMcpToolFactory` adds MCP titles and read-only annotations.
