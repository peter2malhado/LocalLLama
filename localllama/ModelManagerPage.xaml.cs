using localllama.ViewModels;

namespace localllama;

public partial class ModelManagerPage : ContentPage
{
    public ModelManagerPage()
    {
        InitializeComponent();
        if (BindingContext is ModelManagerViewModel vm) vm.LoadModelsCommand.Execute(null);
    }

    private void OnLoadClicked(object sender, EventArgs e)
    {
        if (BindingContext is ModelManagerViewModel vm) vm.LoadModelsCommand.Execute(null);
    }
}