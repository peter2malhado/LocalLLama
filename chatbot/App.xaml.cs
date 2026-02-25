using chatbot.Services;

namespace chatbot;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        DatabaseHelper.InitializeAuthDatabase();
        

        // Usar NavigationPage para permitir navegação
        MainPage = new NavigationPage(new LoginPage());
    }
}
