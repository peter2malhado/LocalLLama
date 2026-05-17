using localllama.ViewModels;

namespace localllama;

public partial class DocumentManagerPage : ContentPage
{
    public DocumentManagerPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is DocumentManagerViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
