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

        if (!string.IsNullOrEmpty(config.ServerAddress) && Uri.TryCreate(config.ServerAddress, UriKind.Absolute, out var baseUri))
        {
            _httpClient.BaseAddress = baseUri;
        }
        if (!string.IsNullOrEmpty(config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        }
    }

    public async Task<ModManifest?> GetManifestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_httpClient.BaseAddress == null)
            {
                return null;
            }

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var endpoints = new[] { "/api/manifest", "/manifest.json", "/manifest" };
            HttpResponseMessage? response = null;
            string lastError = string.Empty;

            foreach (var ep in endpoints)
            {
                try
                {
                    var res = await _httpClient.GetAsync(ep, linkedCts.Token);
                    if (res.IsSuccessStatusCode)
                    {
                        response = res;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                }
            }

            if (response == null)
            {
                if (!string.IsNullOrEmpty(lastError))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[NOTICE] Manifest status: {lastError}");
                    Console.ResetColor();
                }
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(linkedCts.Token);
            return JsonSerializer.Deserialize(json, ModManifestContext.Default.ModManifest);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[NOTICE] Manifest status: {ex.Message}");
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
