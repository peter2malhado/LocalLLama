using Microsoft.Maui.Storage;

namespace chatbot.Services
{
    public static class ModelConfig
    {
        private const string ModelPathKey = "selected_model_path";

        public static string? SelectedModelPath
        {
            get => Preferences.Get(ModelPathKey, null);
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    Preferences.Remove(ModelPathKey);
                    return;
                }

                Preferences.Set(ModelPathKey, value);
            }
        }
    }
}
