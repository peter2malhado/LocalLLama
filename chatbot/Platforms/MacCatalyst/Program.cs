using UIKit;

namespace chatbot
{
    public class Program
    {
        // This is the main entry point of the application.
        static void Main(string[] args)
        {
            // Configure native library BEFORE anything else runs
            NativeLibraryHelper.ConfigureNativeLibrary();

            // if you want to use a different Application Delegate class from "AppDelegate"
            // you can specify it here.
            UIApplication.Main(args, null, typeof(AppDelegate));
        }
    }
}