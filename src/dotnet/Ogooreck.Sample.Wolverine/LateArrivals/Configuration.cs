namespace Ogooreck.Sample.Wolverine.LateArrivals;

public static class Configuration
{
    public static IServiceCollection AddLateArrivals(this IServiceCollection services) =>
        services.AddSingleton<LateArrivalHolds>();

    public static IEndpointRouteBuilder MapLateArrivals(this IEndpointRouteBuilder app)
    {
        app.MapPost("/late-arrivals/{cartId:guid}", (Guid cartId, LateArrivalHolds lateArrivals) =>
        {
            lateArrivals.Hold(cartId);
            return Results.NoContent();
        });

        app.MapPost("/late-arrivals/{cartId:guid}/release", async (Guid cartId, LateArrivalHolds lateArrivals) =>
        {
            await lateArrivals.Release(cartId);
            return Results.NoContent();
        });

        return app;
    }
}
