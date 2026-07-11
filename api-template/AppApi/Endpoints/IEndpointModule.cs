namespace AppApi.Endpoints;

/// <summary>
/// The plug-n-play registration point (EXTENDING_THE_TEMPLATE.md §5): one
/// module per aggregate, registered in the module list in Program.cs.
/// Group-level filters give every module the full security pipeline for
/// free.
/// </summary>
public interface IEndpointModule
{
    void Map(IEndpointRouteBuilder v1);
}
