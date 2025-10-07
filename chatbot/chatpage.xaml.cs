namespace chatbot
{
    public partial class chatpage : ContentPage
    {
        private readonly string _chatId;

        // Construtor padrão (por exemplo, quando abres a app)
        public chatpage() : this("default") // Usa "default" como ID padrão
        {
        }

        // Construtor quando é criada uma nova conversa
        public chatpage(string chatId)
        {
            InitializeComponent();
            _chatId = chatId;
            BindingContext = new ChatViewModel(_chatId);
        }

        private void MessageEntry_Completed(object sender, EventArgs e)
        {
            if (BindingContext is ChatViewModel vm && vm.SendMessageCommand.CanExecute(null))
            {
                vm.SendMessageCommand.Execute(null);
            }
        }
    }
}
