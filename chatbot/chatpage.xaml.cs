using chatbot.Models;

namespace chatbot;

public partial class chatpage : ContentPage
{
    private readonly string _chatId;
    private readonly ChatViewModel _viewModel;

    // Construtor padrão (por exemplo, quando abres a app)
    public chatpage() : this("default") // Usa "default" como ID padrão
    {
    }

    // Construtor quando é criada uma nova conversa
    public chatpage(string chatId)
    {
        InitializeComponent();
        _chatId = chatId;
        _viewModel = new ChatViewModel(_chatId);
        BindingContext = _viewModel;

        // Subscrever eventos para scroll automático quando novas mensagens são adicionadas
        _viewModel.Messages.CollectionChanged += (s, e) =>
        {
            if (e.NewItems != null && e.NewItems.Count > 0)
            {
                ScrollToLastMessage();

                // Também subscrever mudanças de propriedade na última mensagem (para streaming)
                if (e.NewItems[0] is Message newMessage)
                    newMessage.PropertyChanged += (sender, args) =>
                    {
                        if (args.PropertyName == "Text" && !newMessage.IsUser) ScrollToLastMessage();
                    };
            }
        };
    }

    private void MessageEntry_Completed(object sender, EventArgs e)
    {
        if (BindingContext is ChatViewModel vm && vm.SendMessageCommand.CanExecute(null))
            vm.SendMessageCommand.Execute(null);
    }

    private void ScrollToLastMessage()
    {
        if (MessagesView.ItemsSource != null && _viewModel.Messages.Count > 0)
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var lastMessage = _viewModel.Messages.LastOrDefault();
                if (lastMessage != null)
                    MessagesView.ScrollTo(lastMessage, position: ScrollToPosition.End, animate: false);
            });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Scroll para a última mensagem quando a página aparece
        ScrollToLastMessage();
    }
}