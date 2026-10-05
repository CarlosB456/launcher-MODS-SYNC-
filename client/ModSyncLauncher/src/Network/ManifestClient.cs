using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModSyncLauncher.Core;

namespace ModSyncLauncher.Network;

public class ManifestClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ModSyncConfig _config;

    public ManifestClient(HttpClient httpClient, ModSyncConfig config)
    {
        _httpClient = httpClient;
        _config = config;

        _httpClient.BaseAddress = new Uri(config.ServerAddress);
        if (!string.IsNullOrEmpty(config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        }
    }

    public async Task<ModManifest?> GetManifestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/manifest", cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize(json, ModManifestContext.Default.ModManifest);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error fetching manifest: {ex.Message}");
            Console.ResetColor();
            return null;
        }
    }

    public void Dispose()
    {
        // _httpClient might be managed by DI / factory if upgraded later.
        // For console app, we can dispose it.
        _httpClient.Dispose();
    }
}
