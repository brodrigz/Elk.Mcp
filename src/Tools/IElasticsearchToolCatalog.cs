using Microsoft.Extensions.AI;

namespace Elk.Mcp.Tools;

public interface IElasticsearchToolCatalog
{
    IReadOnlyList<AIFunction> Tools { get; }
}
