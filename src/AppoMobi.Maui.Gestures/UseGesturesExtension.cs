namespace AppoMobi.Gestures;

public static class UseGesturesExtension
{

    public static MauiAppBuilder UseGestures(this MauiAppBuilder builder)
    {


#if WINDOWS

        builder.ConfigureEffects(effects =>
        {
            effects.Add<TouchEffect, PlatformTouchEffect>();
        });

#elif ANDROID

             builder.ConfigureEffects(effects =>
            {
                effects.Add<TouchEffect, PlatformTouchEffect>();
            });

#elif IOS

             builder.ConfigureEffects(effects =>
            {
                effects.Add<TouchEffect, PlatformTouchEffect>();
            });

#elif MACCATALYST

            builder.ConfigureEffects(effects =>
            {
                effects.Add<TouchEffect, PlatformTouchEffect>();
            });

#endif

        return builder;
    }
}