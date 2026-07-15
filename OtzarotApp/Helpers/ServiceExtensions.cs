namespace OtzarotApp.Helpers;

/// <summary>
/// Extension methods על IServiceProvider לנוחות.
/// </summary>
internal static class ServiceExtensions
{
    public static T GetRequired<T>(this IServiceProvider sp) where T : notnull
        => (T)(sp.GetService(typeof(T))
               ?? throw new InvalidOperationException($"Service {typeof(T).Name} not registered"));
}
