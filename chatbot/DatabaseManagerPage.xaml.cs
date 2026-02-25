using chatbot.ViewModels;

namespace chatbot;

public partial class DatabaseManagerPage : ContentPage
{
    public DatabaseManagerPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is DatabaseManagerViewModel vm) vm.LoadCommand.Execute(null);
    }
}