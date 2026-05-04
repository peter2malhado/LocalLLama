namespace localllama.Services;

public sealed class EffectiveInferenceSettings
{
    public uint ContextSize { get; init; }
    public int MaxTokens { get; init; }
    public int GpuLayerCount { get; init; }
    public string ProfileName { get; init; } = "Equilibrado";
    public bool IsAutomatic { get; init; }
}

public static class InferenceSettingsService
{
    private const string AutoModeKey = "inference_auto_mode";
    private const string ManualContextSizeKey = "inference_manual_context_size";
    private const string ManualMaxTokensKey = "inference_manual_max_tokens";
    private const string ManualGpuLayersKey = "inference_manual_gpu_layers";
    private const string DeveloperStatsKey = "inference_developer_stats";

    public static bool IsAutomaticMode
    {
        get => Preferences.Get(AutoModeKey, true);
        set => Preferences.Set(AutoModeKey, value);
    }

    public static int ManualContextSize
    {
        get => Preferences.Get(ManualContextSizeKey, 2048);
        set => Preferences.Set(ManualContextSizeKey, value);
    }

    public static int ManualMaxTokens
    {
        get => Preferences.Get(ManualMaxTokensKey, 512);
        set => Preferences.Set(ManualMaxTokensKey, value);
    }

    public static int ManualGpuLayers
    {
        get => Preferences.Get(ManualGpuLayersKey, 5);
        set => Preferences.Set(ManualGpuLayersKey, value);
    }

    public static bool IsDeveloperStatsEnabled
    {
        get => Preferences.Get(DeveloperStatsKey, false);
        set => Preferences.Set(DeveloperStatsKey, value);
    }

    public static EffectiveInferenceSettings GetEffectiveSettings(string? modelPath)
    {
        if (!IsAutomaticMode)
        {
            return new EffectiveInferenceSettings
            {
                ContextSize = (uint)Math.Max(512, ManualContextSize),
                MaxTokens = Math.Max(64, ManualMaxTokens),
                GpuLayerCount = GetSafeGpuLayers(ManualGpuLayers),
                ProfileName = "Manual",
                IsAutomatic = false
            };
        }

        return GetAutomaticSettings(modelPath);
    }

    public static EffectiveInferenceSettings GetAutomaticSettings(string? modelPath)
    {
        var platform = DeviceInfo.Platform;
        var gpuLayers = platform == DevicePlatform.iOS ? 0 : 5;

        if (platform == DevicePlatform.Android || platform == DevicePlatform.iOS)
        {
            return new EffectiveInferenceSettings
            {
                ContextSize = 1024,
                MaxTokens = 256,
                GpuLayerCount = platform == DevicePlatform.iOS ? 0 : 2,
                ProfileName = "Mobile",
                IsAutomatic = true
            };
        }

        var fileSizeGb = GetModelSizeInGb(modelPath);

        if (fileSizeGb > 0 && fileSizeGb < 2)
        {
            return new EffectiveInferenceSettings
            {
                ContextSize = 4096,
                MaxTokens = 1024,
                GpuLayerCount = Math.Max(gpuLayers, 8),
                ProfileName = "Alto desempenho",
                IsAutomatic = true
            };
        }

        if (fileSizeGb > 0 && fileSizeGb < 5)
        {
            return new EffectiveInferenceSettings
            {
                ContextSize = 2048,
                MaxTokens = 512,
                GpuLayerCount = gpuLayers,
                ProfileName = "Equilibrado",
                IsAutomatic = true
            };
        }

        return new EffectiveInferenceSettings
        {
            ContextSize = 1024,
            MaxTokens = 256,
            GpuLayerCount = Math.Min(gpuLayers, 3),
            ProfileName = "Económico",
            IsAutomatic = true
        };
    }

    private static double GetModelSizeInGb(string? modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
            return 0;

        return new FileInfo(modelPath).Length / (1024d * 1024d * 1024d);
    }

    private static int GetSafeGpuLayers(int gpuLayers)
    {
        if (DeviceInfo.Platform == DevicePlatform.iOS)
            return 0;

        return Math.Max(0, gpuLayers);
    }
}
