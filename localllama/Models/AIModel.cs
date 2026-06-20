using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace localllama.Models;

// Modelo de dados para a lista de modelos GGUF
public class AIModel : INotifyPropertyChanged
{
    private bool _isDownloading;
    private bool _hasMmproj;

    // Progresso do download (0..1)
    private double _progress;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SizeText { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
    public string MmprojFileName { get; set; } = string.Empty;
    public bool IsSelected { get; set; }

    public bool HasMmproj
    {
        get => _hasMmproj;
        set
        {
            if (_hasMmproj == value) return;
            _hasMmproj = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MmprojText));
        }
    }

    public string MmprojText => HasMmproj
        ? $"mmproj associado{(string.IsNullOrWhiteSpace(MmprojFileName) ? string.Empty : $" ({MmprojFileName})")}"
        : "Sem mmproj";

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
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
