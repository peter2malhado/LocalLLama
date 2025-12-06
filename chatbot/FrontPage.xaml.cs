using System.Collections.ObjectModel;
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
                Conversations.Clear();
                foreach (var chat in chats)
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
            await LoadChats();
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
    }
}
