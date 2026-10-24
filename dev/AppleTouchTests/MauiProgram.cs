using AppoMobi.Gestures;

namespace AppleTouchTests;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseGestures()
            .ConfigureMauiHandlers(handlers =>
            {
#if IOS || MACCATALYST
                handlers.AddHandler(typeof(ProbeControl), typeof(ProbeControlHandler));
#endif
            });

        return builder.Build();
    }
}
