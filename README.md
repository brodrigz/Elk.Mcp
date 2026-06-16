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

Tool names, descriptions, parameter descriptions, titles, and read-only annotations
follow the Rust implementation. `list_indices` uses the resolve-index API so it only
requires index-level `view_index_metadata`; the CAT indices operation remains available
internally for deployments that grant the broader monitoring privileges.

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

## End-to-end tests

The test project includes opt-in tests for all five tools. Configure the normal
`ES_*` variables, optionally set `ELK_MCP_E2E_INDEX` to a safe index or pattern,
and enable them explicitly:

```powershell
$env:ELK_MCP_RUN_E2E = "true"
$env:ELK_MCP_E2E_INDEX = "my-read-only-index"
dotnet test src\Elk.Mcp.Tests\Elk.Mcp.Tests.csproj --filter Category=E2E
```

These tests make real read-only requests to the configured Elasticsearch cluster.
They also cover the resolve-index, get-aliases, and retained CAT indices operations.

## Design

- `IElasticsearchOperations` isolates Elasticsearch access.
- `NestElasticsearchOperations` is the initial Elasticsearch 8 implementation.
- Index discovery operations are exposed separately through resolve-index, get-aliases,
  and CAT indices methods.
- `ElasticsearchTools` owns the agent-visible transformations.
- `IElasticsearchToolCatalog` exposes the canonical `AIFunction` declarations.
- `ElasticsearchMcpToolFactory` adds MCP titles and read-only annotations.
