using localllama.ViewModels;

namespace localllama;

public partial class LocalModelsPage : ContentPage
{
    public LocalModelsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is LocalModelsViewModel vm) vm.LoadLocalModelsCommand.Execute(null);
    }
}