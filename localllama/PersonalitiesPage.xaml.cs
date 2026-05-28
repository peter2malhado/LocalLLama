using System.Collections.ObjectModel;
using localllama.Models;
using localllama.Services;

namespace localllama;

public partial class PersonalitiesPage : ContentPage
{
    private AiPersonalityOption? _selectedPersonality;
    private string? _editingOriginalName;
    private string _currentSelectionName = PersonalitySelectionService.Selected.Name;
    private string _customName = string.Empty;
    private string _customPrompt = string.Empty;
    private string _editorTitle = "Criar personalidade";
    private string _saveButtonText = "Usar personalidade criada";
    private bool _isEditingCustom;

    public PersonalitiesPage()
    {
        InitializeComponent();
        Personalities = new ObservableCollection<AiPersonalityOption>();
        BindingContext = this;
        LoadPersonalities();
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

    public string EditorTitle
    {
        get => _editorTitle;
        set
        {
            if (_editorTitle == value)
                return;

            _editorTitle = value;
            OnPropertyChanged();
        }
    }

    public string SaveButtonText
    {
        get => _saveButtonText;
        set
        {
            if (_saveButtonText == value)
                return;

            _saveButtonText = value;
            OnPropertyChanged();
        }
    }

    public bool IsEditingCustom
    {
        get => _isEditingCustom;
        set
        {
            if (_isEditingCustom == value)
                return;

            _isEditingCustom = value;
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

    private void LoadPersonalities()
    {
        Personalities.Clear();
        foreach (var personality in PersonalitySelectionService.GetAllPersonalities())
            Personalities.Add(personality);

        _selectedPersonality = PersonalitySelectionService.Selected;
        CurrentSelectionName = _selectedPersonality.Name;
    }

    private void OnPersonalitySelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not AiPersonalityOption selected)
            return;

        ApplySelection(selected);
    }

    private async void OnSaveCustomClicked(object sender, EventArgs e)
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
        var wasEditing = IsEditingCustom;

        if (wasEditing && !string.IsNullOrWhiteSpace(_editingOriginalName) &&
            !string.Equals(_editingOriginalName, customPersonality.Name, StringComparison.OrdinalIgnoreCase))
        {
            PersonalitySelectionService.DeleteCustomPersonality(_editingOriginalName);
            RemovePersonalityFromList(_editingOriginalName);
        }

        PersonalitySelectionService.SaveCustomPersonality(customPersonality);
        UpsertPersonalityInList(customPersonality);
        ApplySelection(customPersonality);
        ResetEditor();

        var message = wasEditing
            ? $"\"{customPersonality.Name}\" foi atualizada."
            : $"\"{customPersonality.Name}\" está selecionada.";

        await DisplayAlert("Personalidade pronta", message, "OK");
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

    private void OnEditCustomClicked(object sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not AiPersonalityOption personality || !personality.IsCustom)
            return;

        _editingOriginalName = personality.Name;
        CustomName = personality.Name;
        CustomPrompt = personality.Prompt;
        EditorTitle = "Editar personalidade";
        SaveButtonText = "Guardar alterações";
        IsEditingCustom = true;
    }

    private async void OnDeleteCustomClicked(object sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not AiPersonalityOption personality || !personality.IsCustom)
            return;

        var confirm = await DisplayAlert(
            "Apagar personalidade",
            $"Queres mesmo apagar \"{personality.Name}\"?",
            "Apagar",
            "Cancelar");

        if (!confirm)
            return;

        PersonalitySelectionService.DeleteCustomPersonality(personality.Name);
        RemovePersonalityFromList(personality.Name);

        if (string.Equals(_editingOriginalName, personality.Name, StringComparison.OrdinalIgnoreCase))
            ResetEditor();

        if (string.Equals(CurrentSelectionName, personality.Name, StringComparison.OrdinalIgnoreCase))
        {
            var fallback = PersonalitySelectionService.Selected;
            ApplySelection(fallback);
        }
    }

    private void OnCancelEditClicked(object sender, EventArgs e)
    {
        ResetEditor();
    }

    private void UpsertPersonalityInList(AiPersonalityOption personality)
    {
        var existing = Personalities.FirstOrDefault(p =>
            string.Equals(p.Name, personality.Name, StringComparison.OrdinalIgnoreCase));

        if (existing == null)
        {
            Personalities.Add(personality);
            return;
        }

        var index = Personalities.IndexOf(existing);
        Personalities[index] = personality;
    }

    private void RemovePersonalityFromList(string personalityName)
    {
        var existing = Personalities.FirstOrDefault(p =>
            string.Equals(p.Name, personalityName, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
            Personalities.Remove(existing);
    }

    private void ResetEditor()
    {
        _editingOriginalName = null;
        CustomName = string.Empty;
        CustomPrompt = string.Empty;
        EditorTitle = "Criar personalidade";
        SaveButtonText = "Usar personalidade criada";
        IsEditingCustom = false;
    }
}
