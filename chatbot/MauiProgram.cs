using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui;

namespace chatbot
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
#if MACCATALYST || IOS
            // Configure native library BEFORE any LLamaSharp types are used
            NativeLibraryHelper.ConfigureNativeLibrary();
#endif
            var builder = MauiApp.CreateBuilder();
            builder.UseMauiApp<App>().ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            }).UseMauiCommunityToolkit();
#if DEBUG
            builder.Logging.AddDebug();
#endif
            return builder.Build();
        }
    }
}