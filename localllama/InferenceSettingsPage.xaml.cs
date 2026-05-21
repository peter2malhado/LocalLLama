using localllama.Services;

namespace localllama;

public partial class InferenceSettingsPage : ContentPage
{
    private static readonly int[] ContextOptions = { 1024, 2048, 4096, 8192 };
    private static readonly int[] MaxTokenOptions = { 128, 256, 512, 1024, 2048 };
    private static readonly int[] GpuLayerOptions = { 0, 2, 3, 5, 8, 12, 16, 24, 32 };

    public InferenceSettingsPage()
    {
        InitializeComponent();
        SetupPickers();
        LoadSettings();
    }

    private void SetupPickers()
    {
        foreach (var option in ContextOptions)
            ContextSizePicker.Items.Add(option.ToString());

        foreach (var option in MaxTokenOptions)
            MaxTokensPicker.Items.Add(option.ToString());

        foreach (var option in GpuLayerOptions)
            GpuLayersPicker.Items.Add(option.ToString());
    }

    private void LoadSettings()
    {
        AutoModeSwitch.IsToggled = InferenceSettingsService.IsAutomaticMode;
        DeveloperModeSwitch.IsToggled = InferenceSettingsService.IsDeveloperStatsEnabled;
        WebSearchSwitch.IsToggled = InferenceSettingsService.IsWebSearchEnabled;
        WebSearchApiKeyEntry.Text = InferenceSettingsService.WebSearchApiKey;

        ContextSizePicker.SelectedIndex = FindIndex(ContextOptions, InferenceSettingsService.ManualContextSize);
        MaxTokensPicker.SelectedIndex = FindIndex(MaxTokenOptions, InferenceSettingsService.ManualMaxTokens);
        GpuLayersPicker.SelectedIndex = FindIndex(GpuLayerOptions, InferenceSettingsService.ManualGpuLayers);

        UpdateAppliedSettingsPreview();
        UpdateManualVisibility();
        UpdateWebSearchVisibility();
    }

    private void OnAutoModeToggled(object? sender, ToggledEventArgs e)
    {
        UpdateManualVisibility();
        UpdateAppliedSettingsPreview();
    }

    private void OnWebSearchToggled(object? sender, ToggledEventArgs e)
    {
        UpdateWebSearchVisibility();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        InferenceSettingsService.IsAutomaticMode = AutoModeSwitch.IsToggled;
        InferenceSettingsService.IsDeveloperStatsEnabled = DeveloperModeSwitch.IsToggled;
        InferenceSettingsService.IsWebSearchEnabled = WebSearchSwitch.IsToggled;
        InferenceSettingsService.WebSearchApiKey = WebSearchApiKeyEntry.Text?.Trim() ?? string.Empty;
        
        InferenceSettingsService.ManualContextSize = GetSelectedValue(ContextOptions, ContextSizePicker.SelectedIndex, 2048);
        InferenceSettingsService.ManualMaxTokens = GetSelectedValue(MaxTokenOptions, MaxTokensPicker.SelectedIndex, 512);
        InferenceSettingsService.ManualGpuLayers = GetSelectedValue(GpuLayerOptions, GpuLayersPicker.SelectedIndex, 5);

        UpdateAppliedSettingsPreview();
        await DisplayAlert("Settings", "As preferências foram guardadas.", "OK");
    }

    private void UpdateManualVisibility()
    {
        ManualSettingsFrame.IsVisible = !AutoModeSwitch.IsToggled;
    }

    private void UpdateWebSearchVisibility()
    {
        WebSearchKeyContainer.IsVisible = WebSearchSwitch.IsToggled;
    }

    private void UpdateAppliedSettingsPreview()
    {
        var effective = InferenceSettingsService.GetEffectiveSettings(ModelConfig.SelectedModelPath);

        ProfileNameLabel.Text = effective.ProfileName + (effective.IsAutomatic ? " automático" : "");
        AppliedContextLabel.Text = effective.ContextSize.ToString();
        AppliedTokensLabel.Text = effective.MaxTokens.ToString();
        AppliedGpuLabel.Text = effective.GpuLayerCount.ToString();
    }

    private static int FindIndex(IReadOnlyList<int> options, int value)
    {
        for (var i = 0; i < options.Count; i++)
            if (options[i] == value)
                return i;

        return 0;
    }

    private static int GetSelectedValue(IReadOnlyList<int> options, int selectedIndex, int fallback)
    {
        if (selectedIndex < 0 || selectedIndex >= options.Count)
            return fallback;

        return options[selectedIndex];
    }
}
