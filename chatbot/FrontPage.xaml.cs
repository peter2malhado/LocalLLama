using System;
using System.Collections.ObjectModel;
using System.Linq;
using chatbot.Models;
using chatbot.Services;

namespace chatbot
{
    public partial class FrontPage : ContentPage
    {
        public ObservableCollection<ChatSession> Conversations { get; set; } = new();

        public FrontPage()
        {
            InitializeComponent();
            BindingContext = this;

            LoadChats();
        }

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

        // 🔄 Botão para recarregar chats da base de dados
        private async void OnLoadChatsClicked(object sender, EventArgs e)
        {
            LoadChats();
            await DisplayAlert("Sucesso", $"Carregados {Conversations.Count} chat(s) da base de dados.", "OK");
        }

        // 👉 Botão "Nova Conversa"
        private async void OnStartChatClicked(object sender, EventArgs e)
        {
            var newChat = await ChatStorage.CreateNewChatAsync("Nova Conversa");
            Conversations.Add(newChat);

            // Abre a página do novo chat
            await Navigation.PushAsync(new chatpage(newChat.Id));
        }

        // 👉 Quando o utilizador seleciona uma conversa existente
        private async void OnChatSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is ChatSession selectedChat)
            {
                await Navigation.PushAsync(new chatpage(selectedChat.Id));
            }

            ((CollectionView)sender).SelectedItem = null;
        }

        // ✏️ Editar nome da conversa
        private async void OnEditChatClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is ChatSession chat)
            {
                string newTitle = await DisplayPromptAsync(
                    "Editar Conversa",
                    "Digite o novo nome para esta conversa:",
                    "OK",
                    "Cancelar",
                    chat.Title,
                    maxLength: 50,
                    keyboard: Keyboard.Default);

                if (!string.IsNullOrWhiteSpace(newTitle) && newTitle != chat.Title)
                {
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
        }

        // 🗑️ Deletar conversa
        private async void OnDeleteChatClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is ChatSession chat)
            {
                bool confirm = await DisplayAlert(
                    "Confirmar Exclusão",
                    $"Tem certeza que deseja deletar a conversa \"{chat.Title}\"?\n\nEsta ação não pode ser desfeita.",
                    "Deletar",
                    "Cancelar");

                if (confirm)
                {
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
        }
    }
}
