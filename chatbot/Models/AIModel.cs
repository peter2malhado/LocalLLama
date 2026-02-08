using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace chatbot.Models
{
    // Modelo de dados para a lista de modelos GGUF
    public class AIModel : INotifyPropertyChanged
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // Progresso do download (0..1)
        private double _progress;
        public double Progress
        {
            get => _progress;
            set
            {
                if (Math.Abs(_progress - value) < 0.0001) return;
                _progress = value;
                OnPropertyChanged();
            }
        }

        private bool _isDownloading;
        public bool IsDownloading
        {
            get => _isDownloading;
            set
            {
                if (_isDownloading == value) return;
                _isDownloading = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
