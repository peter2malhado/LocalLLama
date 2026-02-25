using System.Net;
using chatbot.Models;

namespace chatbot.Services;

public class ModelDownloadService
{
    private readonly HttpClient _httpClient;

    public ModelDownloadService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> DownloadAsync(
        AIModel model,
        IProgress<double> progress,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(model.Url))
            throw new ArgumentException("URL inválida.");

        var response = await _httpClient.GetAsync(
            model.Url,
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new FileNotFoundException("Modelo não encontrado no servidor.");

        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1;
        var canReport = total > 0;

        var modelsDir = Path.Combine(FileSystem.AppDataDirectory, "models");
        Directory.CreateDirectory(modelsDir);

        var filePath = Path.Combine(modelsDir, model.FileName);

        await using var input = await response.Content.ReadAsStreamAsync(ct);
        await using var output = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[1024 * 64];
        long readTotal = 0;
        int read;

        while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
            readTotal += read;

            if (canReport) progress.Report((double)readTotal / total);
        }

        progress.Report(1.0);
        return filePath;
    }
}