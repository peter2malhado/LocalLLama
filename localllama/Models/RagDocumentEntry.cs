using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace localllama.Models;

public class RagDocumentEntry : INotifyPropertyChanged
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string SizeText { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
    public int ChunkCount { get; set; }
    private bool _isSelected = true;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public string DisplayName => string.IsNullOrWhiteSpace(Extension) ? Name : $"{Name}{Extension}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
