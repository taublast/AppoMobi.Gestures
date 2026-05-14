using Microsoft.Extensions.DependencyInjection;

namespace AppoMobi.Gestures;

public static class ServiceExtensions
{
    /// <summary>
    /// Registers BlazorGestureEffect as a transient service.
    /// Create one instance per gesture target element; dispose when the element unmounts.
    /// </summary>
    public static IServiceCollection AddBlazorGestures(this IServiceCollection services)
    {
        services.AddTransient<TouchEffect>();
        return services;
    }
}
