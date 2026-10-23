namespace Situ.Api.Endpoints;

public interface IEndpoint
{
    static abstract void Map(RouteGroupBuilder group);
}
