namespace chatbot;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Usar NavigationPage para permitir navegação
        MainPage = new NavigationPage(new FrontPage());
    }
}