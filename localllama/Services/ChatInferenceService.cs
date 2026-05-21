using System.Diagnostics;
using LLama;
using LLama.Common;
using localllama;
using localllama.Models;

namespace localllama.Services;

public class ChatInferenceService
{
    private readonly List<string> _modelResolutionDiagnostics = new();
    private readonly List<string> _modelLoadDiagnostics = new();
    private InteractiveExecutor? _executor;
    private InferenceParams? _inferenceParams;
    private LLama.ChatSession? _session;

    public EffectiveInferenceSettings? EffectiveSettings { get; private set; }

    public void Initialize()
    {
#if MACCATALYST || IOS
        NativeLibraryHelper.ConfigureNativeLibrary();
#endif

        var selectedModel = ModelConfig.SelectedModelPath;
        var modelFile = "llama-3.2-1b-instruct-q8_0.gguf";
        var modelPath = ResolveModelPath(selectedModel, modelFile, _modelResolutionDiagnostics);
        _modelLoadDiagnostics.Add($"ModelPath={modelPath}");
        _modelLoadDiagnostics.Add($"SelectedModel={selectedModel ?? "<null>"}");

        if (!File.Exists(modelPath))
        {
            var detail = _modelResolutionDiagnostics.Count == 0
                ? string.Empty
                : $" Caminhos verificados: {string.Join(" | ", _modelResolutionDiagnostics)}";
            throw new FileNotFoundException($"Modelo .gguf não encontrado. Seleciona ou importa um modelo nas definições.{detail}");
        }

        var modelInfo = new FileInfo(modelPath);
        _modelLoadDiagnostics.Add("Exists=true");
        _modelLoadDiagnostics.Add($"FileSize={modelInfo.Length}");
        _modelLoadDiagnostics.Add($"LastWriteUtc={modelInfo.LastWriteTimeUtc:O}");

        EffectiveSettings = InferenceSettingsService.GetEffectiveSettings(modelPath);
        _modelLoadDiagnostics.Add($"Profile={EffectiveSettings.ProfileName}");
        _modelLoadDiagnostics.Add($"AutoMode={EffectiveSettings.IsAutomatic}");

        var parameters = new ModelParams(modelPath)
        {
            ContextSize = EffectiveSettings.ContextSize,
            GpuLayerCount = EffectiveSettings.GpuLayerCount
        };
        _modelLoadDiagnostics.Add($"ContextSize={parameters.ContextSize}");
        _modelLoadDiagnostics.Add($"GpuLayerCount={parameters.GpuLayerCount}");

        LLamaWeights model;
        try
        {
            model = LLamaWeights.LoadFromFile(parameters);
            _modelLoadDiagnostics.Add("LoadFromFile=ok");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Falha em LLamaWeights.LoadFromFile. Diagnóstico: {string.Join(" | ", _modelLoadDiagnostics)}",
                ex);
        }

        LLamaContext context;
        try
        {
            context = model.CreateContext(parameters);
            _modelLoadDiagnostics.Add("CreateContext=ok");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Falha em model.CreateContext. Diagnóstico: {string.Join(" | ", _modelLoadDiagnostics)}",
                ex);
        }

        _executor = new InteractiveExecutor(context);
        _inferenceParams = new InferenceParams
        {
            MaxTokens = EffectiveSettings.MaxTokens,
            AntiPrompts = new List<string> { "User:", "Query:" },
            OverflowStrategy = ContextOverflowStrategy.TruncateAndReprefill
        };
    }

    public void RebuildSession(IEnumerable<ChatMessage> messages)
    {
        if (_executor == null)
            return;

        var chatHistory = new ChatHistory();
        chatHistory.AddMessage(AuthorRole.System, ChatPromptCatalog.SystemPrompt);

        foreach (var message in messages)
        {
            var role = string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase)
                ? AuthorRole.User
                : AuthorRole.Assistant;
            chatHistory.AddMessage(role, message.Text);
        }

        _session = new LLama.ChatSession(_executor, chatHistory);
    }

    public async Task<ChatInferenceResult> GenerateReplyAsync(string prompt, Action<string> onPartial)
    {
        if (_session == null || _inferenceParams == null)
            throw new InvalidOperationException("A conversa ainda está a ser preparada. Tenta novamente dentro de um instante.");

        var timer = Stopwatch.StartNew();
        var reply = string.Empty;
        var updateCount = 0;

        await foreach (var text in _session.ChatAsync(new ChatHistory.Message(AuthorRole.User, prompt), _inferenceParams))
        {
            reply += text;
            updateCount++;

            var cleaned = CleanReply(reply);
            onPartial(cleaned);

            if (updateCount % 3 == 0)
                await Task.Delay(1);
        }

        timer.Stop();
        var finalText = CleanReply(reply);
        onPartial(finalText);

        return new ChatInferenceResult
        {
            FinalText = finalText,
            Elapsed = timer.Elapsed
        };
    }

    public static string BuildDetailedError(Exception ex)
    {
        var parts = new List<string>();
        Exception? current = ex;
        while (current != null)
        {
            parts.Add($"{current.GetType().Name}: {current.Message}");
            current = current.InnerException;
        }

        return string.Join(" | ", parts);
    }

    private static string CleanReply(string reply)
    {
        return reply.Replace("bob:", "", StringComparison.OrdinalIgnoreCase)
            .Replace("User:", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static string ResolveModelPath(string? selectedModelPath, string defaultModelFile, List<string>? diagnostics = null)
    {
        if (!string.IsNullOrWhiteSpace(selectedModelPath))
        {
            diagnostics?.Add($"Selected={selectedModelPath}");
            if (File.Exists(selectedModelPath))
                return selectedModelPath;
        }

        var appDataModelsDir = Path.Combine(FileSystem.AppDataDirectory, "models");
        Directory.CreateDirectory(appDataModelsDir);
        var appDataModelPath = Path.Combine(appDataModelsDir, defaultModelFile);
        diagnostics?.Add($"AppData={appDataModelPath}");
        if (File.Exists(appDataModelPath))
            return appDataModelPath;

        if (TryCopyFromAppPackage($"AI model/{defaultModelFile}", appDataModelPath) ||
            TryCopyFromAppPackage(defaultModelFile, appDataModelPath))
        {
            return appDataModelPath;
        }

        var baseDir = AppContext.BaseDirectory;
        var candidates = new List<string>
        {
            Path.Combine(baseDir, "AI model", defaultModelFile),
            Path.Combine(baseDir, "gguf models", defaultModelFile),
            Path.Combine(baseDir, "modelos de ai", defaultModelFile),
            Path.Combine(baseDir, defaultModelFile),
            Path.GetFullPath(Path.Combine(baseDir, "..", "Resources", "AI model", defaultModelFile)),
            Path.GetFullPath(Path.Combine(baseDir, "..", "Resources", "gguf models", defaultModelFile)),
            Path.GetFullPath(Path.Combine(baseDir, "..", "Resources", "modelos de ai", defaultModelFile)),
            Path.GetFullPath(Path.Combine(baseDir, "..", "Resources", defaultModelFile))
        };

        var ancestorMatch = FindInAncestorFolders(baseDir, "AI model", defaultModelFile);
        if (!string.IsNullOrWhiteSpace(ancestorMatch))
            candidates.Insert(0, ancestorMatch);

        foreach (var path in candidates.Distinct())
        {
            diagnostics?.Add($"Candidate={path}");
            if (File.Exists(path))
                return TryCopyToAppData(path, appDataModelPath) ?? path;
        }

        diagnostics?.Add($"Fallback={defaultModelFile}");
        return defaultModelFile;
    }

    private static string? TryCopyToAppData(string sourcePath, string destPath)
    {
        try
        {
            File.Copy(sourcePath, destPath, overwrite: true);
            return destPath;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryCopyFromAppPackage(string assetPath, string destPath)
    {
        try
        {
            using var src = FileSystem.OpenAppPackageFileAsync(assetPath).GetAwaiter().GetResult();
            using var dest = File.Open(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
            src.CopyTo(dest);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindInAncestorFolders(string startDir, string folderName, string fileName)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, folderName, fileName);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        return null;
    }
}
