using System;
using Microsoft.Maui.Controls;
using localllama.ViewModels;

namespace localllama;

public partial class GenerateDocumentPage : ContentPage
{
    public GenerateDocumentPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is GenerateDocumentViewModel vm)
            await vm.InitializeAsync();
    }
}
