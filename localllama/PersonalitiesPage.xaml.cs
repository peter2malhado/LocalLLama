using System.Collections.ObjectModel;
using localllama.Models;
using localllama.Services;

namespace localllama;

public partial class PersonalitiesPage : ContentPage
{
    private AiPersonalityOption? _selectedPersonality;
    private string _currentSelectionName = PersonalitySelectionService.Selected.Name;
    private string _customName = string.Empty;
    private string _customPrompt = string.Empty;

    public PersonalitiesPage()
    {
        InitializeComponent();
        Personalities = new ObservableCollection<AiPersonalityOption>(ChatPromptCatalog.GetBuiltInPersonalities());
        _selectedPersonality = PersonalitySelectionService.Selected;
        BindingContext = this;
    }

    public ObservableCollection<AiPersonalityOption> Personalities { get; }

    public string CurrentSelectionName
    {
        get => _currentSelectionName;
        set
        {
            if (_currentSelectionName == value)
                return;

            _currentSelectionName = value;
            OnPropertyChanged();
        }
    }

    public string CustomName
    {
        get => _customName;
        set
        {
            if (_customName == value)
                return;

            _customName = value;
            OnPropertyChanged();
        }
    }

    public string CustomPrompt
    {
        get => _customPrompt;
        set
        {
            if (_customPrompt == value)
                return;

            _customPrompt = value;
            OnPropertyChanged();
        }
    }

    private void OnPersonalitySelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not AiPersonalityOption selected)
            return;

        ApplySelection(selected);
    }

    private async void OnCreateCustomClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CustomName) || string.IsNullOrWhiteSpace(CustomPrompt))
        {
            await DisplayAlert("Dados em falta", "Preenche o nome e o prompt da personalidade.", "OK");
            return;
        }

        var customPersonality = new AiPersonalityOption
        {
            Name = CustomName.Trim(),
            Description = "Personalidade criada pelo utilizador.",
            Prompt = CustomPrompt.Trim(),
            IsCustom = true
        };

        ApplySelection(customPersonality);
        await DisplayAlert("Personalidade pronta", $"\"{customPersonality.Name}\" está selecionada.", "OK");
    }

    private async void OnContinueToChatClicked(object sender, EventArgs e)
    {
        var selected = _selectedPersonality ?? PersonalitySelectionService.Selected;
        PersonalitySelectionService.Selected = selected;

        var newChat = await ChatStorage.CreateNewChatAsync(
            personalityName: selected.Name,
            personalityPrompt: selected.Prompt);

        await Navigation.PushAsync(new chatpage(newChat.Id));
    }

    private void ApplySelection(AiPersonalityOption selected)
    {
        _selectedPersonality = selected;
        PersonalitySelectionService.Selected = selected;
        CurrentSelectionName = selected.Name;
    }
}
