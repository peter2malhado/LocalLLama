using Foundation;

namespace chatbot
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        static AppDelegate()
        {
            // Configure native library as early as possible, before any LLamaSharp types are loaded
            NativeLibraryHelper.ConfigureNativeLibrary();
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}