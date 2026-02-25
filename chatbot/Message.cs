using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace chatbot.Models;

public class Message : INotifyPropertyChanged
{
    private bool _isUser;
    private string _text;

    public string Text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                _text = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsUser
    {
        get => _isUser;
        set
        {
            if (_isUser != value)
            {
                _isUser = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}