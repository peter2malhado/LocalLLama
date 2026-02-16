using System.Collections.ObjectModel;
using chatbot.Models;
using chatbot.Services;

namespace chatbot;

public partial class FrontPage
{
    public FrontPage()
    {
        InitializeComponent();
        BindingContext = this;

        LoadChats();
    }

    public ObservableCollection<ChatSession> Conversations { get; set; } = new();

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

            Conversations.Clear();
            foreach (var chat in sortedChats)
                Conversations.Add(chat);
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

    // 👉 Botão "Nova Conversa"
    private async void OnStartChatClicked(object sender, EventArgs e)
    {
        var newChat = await ChatStorage.CreateNewChatAsync();
        Conversations.Add(newChat);

        // Abre a página do novo chat
        await Navigation.PushAsync(new chatpage(newChat.Id));
    }

    // 📥 Abrir Gerenciador de Modelos
    private async void OnOpenModelManagerClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new ModelManagerPage());
    }

    // 📁 Abrir Modelos Locais
    private async void OnOpenLocalModelsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new LocalModelsPage());
    }

    // 👉 Quando o utilizador seleciona uma conversa existente
    private async void OnChatSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is ChatSession selectedChat)
            await Navigation.PushAsync(new chatpage(selectedChat.Id));

        ((CollectionView)sender).SelectedItem = null;
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

            var localPath = await SaveModelToAppDataAsync(result);
            ModelConfig.SelectedModelPath = localPath;
            await DisplayAlert("Modelo selecionado", Path.GetFileName(localPath), "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", $"Não foi possível selecionar o modelo: {ex.Message}", "OK");
        }
    }

    private static async Task<string> SaveModelToAppDataAsync(FileResult result)
    {
        var appDataDir = FileSystem.AppDataDirectory;
        var modelsDir = Path.Combine(appDataDir, "models");
        Directory.CreateDirectory(modelsDir);

        var destPath = Path.Combine(modelsDir, result.FileName);

        await using var src = await result.OpenReadAsync();
        await using var dest = File.Open(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await src.CopyToAsync(dest);

        return destPath;
    }
}