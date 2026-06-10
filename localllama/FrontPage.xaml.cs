using System.Collections.ObjectModel;
using localllama.Models;
using localllama.Services;

namespace localllama;

public partial class FrontPage : ContentPage

{
    private readonly List<ChatSession> allConversations = new();
    private bool _isImportingModel;
    private double _importProgress;
    private string _importStatusText = "A importar modelo...";

    public FrontPage()
    {
        InitializeComponent();
        BindingContext = this;

        LoadChats();
    }

    public ObservableCollection<ChatSession> Conversations { get; set; } = new();
    public string SearchText { get; set; } = string.Empty;
    public bool IsImportingModel
    {
        get => _isImportingModel;
        set
        {
            if (_isImportingModel == value)
                return;

            _isImportingModel = value;
            OnPropertyChanged(nameof(IsImportingModel));
        }
    }

    public double ImportProgress
    {
        get => _importProgress;
        set
        {
            var clamped = Math.Clamp(value, 0, 1);
            if (Math.Abs(_importProgress - clamped) < 0.0001)
                return;

            _importProgress = clamped;
            OnPropertyChanged(nameof(ImportProgress));
            OnPropertyChanged(nameof(ImportProgressText));
        }
    }

    public string ImportStatusText
    {
        get => _importStatusText;
        set
        {
            if (_importStatusText == value)
                return;

            _importStatusText = value;
            OnPropertyChanged(nameof(ImportStatusText));
        }
    }

    public string ImportProgressText => $"{ImportProgress:P0}";

    public string ConversationSummary =>
        Conversations.Count switch
        {
            0 => "Nenhuma conversa ativa ainda.",
            1 => "1 conversa pronta para continuar.",
            _ => $"{Conversations.Count} conversas prontas para continuar."
        };

    // Atualizar lista quando a página aparecer (quando voltar de outra página)
    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadChats();
    }

    private async void LoadChats()
    {
        try
        {
            var chats = await ChatStorage.LoadChatsAsync();

            // Ordenar chats: os com mais mensagens primeiro (mais recentes/ativos)
            var sortedChats = chats.OrderByDescending(c => c.MessageCount).ToList();

            allConversations.Clear();
            allConversations.AddRange(sortedChats);
            ApplyConversationFilter();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", $"Erro ao carregar chats: {ex.Message}", "OK");
        }
    }

    // 🗄️ Abrir ferramentas da base de dados
    private async void OnOpenDatabaseToolsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new DatabaseManagerPage());
    }

    private void OnOpenDatabaseToolsTapped(object sender, TappedEventArgs e)
    {
        OnOpenDatabaseToolsClicked(sender, EventArgs.Empty);
    }

    // 👉 Botão "Nova Conversa"
    private async void OnStartChatClicked(object sender, EventArgs e)
    {
        var personality = PersonalitySelectionService.Selected;
        var newChat = await ChatStorage.CreateNewChatAsync(
            personalityName: personality.Name,
            personalityPrompt: personality.Prompt);
        Conversations.Add(newChat);
        OnPropertyChanged(nameof(ConversationSummary));

        // Abre a página do novo chat
        await Navigation.PushAsync(new chatpage(newChat.Id));
    }

    private async void OnOpenPersonalitiesClicked(object sender, EventArgs e)
    {
        try
        {
            var page = new PersonalitiesPage();
            await Navigation.PushAsync(page);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", $"Não foi possível abrir Personalidades: {ex.Message}", "OK");
        }
    }

    private void OnOpenPersonalitiesTapped(object sender, TappedEventArgs e)
    {
        OnOpenPersonalitiesClicked(sender, EventArgs.Empty);
    }

    // 📥 Abrir Gerenciador de Modelos
    private async void OnOpenModelManagerClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new ModelManagerPage());
    }

    private void OnOpenModelManagerTapped(object sender, TappedEventArgs e)
    {
        OnOpenModelManagerClicked(sender, EventArgs.Empty);
    }

    // 📁 Abrir Modelos Locais
    private async void OnOpenLocalModelsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new LocalModelsPage());
    }

    private void OnOpenLocalModelsTapped(object sender, TappedEventArgs e)
    {
        OnOpenLocalModelsClicked(sender, EventArgs.Empty);
    }

    private async void OnOpenSettingsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new InferenceSettingsPage());
    }

    private async void OnOpenDocumentManagerClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new DocumentManagerPage());
    }

    private void OnOpenDocumentManagerTapped(object sender, TappedEventArgs e)
    {
        OnOpenDocumentManagerClicked(sender, EventArgs.Empty);
    }

    // 👉 Quando o utilizador seleciona uma conversa existente
    private async void OnChatSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is ChatSession selectedChat)
            await Navigation.PushAsync(new chatpage(selectedChat.Id));

        ((CollectionView)sender).SelectedItem = null;
    }

    private async void OnChatSelectedButtonClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is ChatSession chat)
            await Navigation.PushAsync(new chatpage(chat.Id));
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        SearchText = e.NewTextValue ?? string.Empty;
        ApplyConversationFilter();
    }

    private void ApplyConversationFilter()
    {
        var normalizedSearch = SearchText.Trim();

        IEnumerable<ChatSession> filteredChats = allConversations;
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
            filteredChats = allConversations.Where(chat =>
                ContainsIgnoreCase(chat.Title, normalizedSearch) ||
                ContainsIgnoreCase(chat.LastMessagePreview, normalizedSearch) ||
                ContainsIgnoreCase(chat.PersonalityName, normalizedSearch));

        Conversations.Clear();
        foreach (var chat in filteredChats)
            Conversations.Add(chat);

        OnPropertyChanged(nameof(ConversationSummary));
    }

    private static bool ContainsIgnoreCase(string? source, string value)
    {
        return !string.IsNullOrWhiteSpace(source) &&
               source.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    // ✏️ Editar nome da conversa
    private async void OnEditChatClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is ChatSession chat)
        {
            var newTitle = await DisplayPromptAsync(
                "Editar Conversa",
                "Digite o novo nome para esta conversa:",
                "OK",
                "Cancelar",
                chat.Title,
                50,
                Keyboard.Default);

            if (!string.IsNullOrWhiteSpace(newTitle) && newTitle != chat.Title)
                try
                {
                    await ChatStorage.UpdateChatTitleAsync(chat.Id, newTitle);
                    chat.Title = newTitle;

                    // Atualizar a lista
                    LoadChats();

                    await DisplayAlert("Sucesso", "Nome da conversa atualizado!", "OK");
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Erro", $"Erro ao atualizar: {ex.Message}", "OK");
                }
        }
    }

    // 🗑️ Deletar conversa
    private async void OnDeleteChatClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is ChatSession chat)
        {
            var confirm = await DisplayAlert(
                "Confirmar Exclusão",
                $"Tem certeza que deseja deletar a conversa \"{chat.Title}\"?\n\nEsta ação não pode ser desfeita.",
                "Deletar",
                "Cancelar");

            if (confirm)
                try
                {
                    await ChatStorage.DeleteChatAsync(chat.Id);

                    // Remover da lista local
                    Conversations.Remove(chat);
                    OnPropertyChanged(nameof(ConversationSummary));

                    await DisplayAlert("Sucesso", "Conversa deletada com sucesso!", "OK");
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Erro", $"Erro ao deletar: {ex.Message}", "OK");
                }
        }
    }

    // 📂 Selecionar modelo .gguf
    private async void OnSelectModelClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Selecionar modelo .gguf",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.MacCatalyst, new[] { "gguf" } },
                    { DevicePlatform.iOS, new[] { "gguf" } },
                    { DevicePlatform.Android, new[] { "application/octet-stream" } },
                    { DevicePlatform.WinUI, new[] { ".gguf" } }
                })
            });

            if (result == null) return;

            if (!result.FileName.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
            {
                await DisplayAlert("Formato inválido", "Escolhe um ficheiro .gguf.", "OK");
                return;
            }

            IsImportingModel = true;
            ImportStatusText = $"A importar {result.FileName}";
            ImportProgress = 0;

            await Task.Yield();

            var localPath = await SaveModelToAppDataAsync(result, new Progress<double>(p => ImportProgress = p));
            ModelConfig.SelectedModelPath = localPath;
            await DisplayAlert("Modelo selecionado", Path.GetFileName(localPath), "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", $"Não foi possível selecionar o modelo: {ex.Message}", "OK");
        }
        finally
        {
            IsImportingModel = false;
            ImportStatusText = "A importar modelo...";
            ImportProgress = 0;
        }
    }

    private static async Task<string> SaveModelToAppDataAsync(FileResult result, IProgress<double>? progress = null)
    {
        var appDataDir = FileSystem.AppDataDirectory;
        var modelsDir = Path.Combine(appDataDir, "models");
        Directory.CreateDirectory(modelsDir);

        var destPath = Path.Combine(modelsDir, result.FileName);

        await using var src = await result.OpenReadAsync();
        await using var dest = File.Open(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[1024 * 1024];
        var totalRead = 0L;
        var totalBytes = src.CanSeek ? src.Length : 0L;
        int read;

        while ((read = await src.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
        {
            await dest.WriteAsync(buffer.AsMemory(0, read));
            totalRead += read;

            if (progress != null && totalBytes > 0)
                progress.Report((double)totalRead / totalBytes);
        }

        progress?.Report(1.0);

        return destPath;
    }

    private void OnLogoutClicked(object sender, EventArgs e)
    {
        UserContext.Clear();
        DatabaseConfig.SelectedDatabaseName = "chats.db";
        Application.Current!.MainPage = new NavigationPage(new LoginPage());
    }
}
