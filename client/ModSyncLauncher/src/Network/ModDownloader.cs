using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ModSyncLauncher.Network;

public class ModDownloader : IDisposable
{
    private readonly HttpClient _httpClient;

    public ModDownloader(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> DownloadFileAsync(string url, string destinationPath, Action<double> onProgress, CancellationToken cancellationToken = default)
    {
        var maxRetries = 3;
        var delayMs = 1000;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var tempFile = destinationPath + ".tmp";
                
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = totalBytes != -1;

                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                var buffer = new byte[81920];
                var isMoreToRead = true;
                var totalRead = 0L;

                while (isMoreToRead)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (read == 0)
                    {
                        isMoreToRead = false;
                        continue;
                    }

                    await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                    totalRead += read;

                    if (canReportProgress)
                    {
                        onProgress?.Invoke((double)totalRead / totalBytes * 100);
                    }
                }
                
                fileStream.Close();
                
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
                File.Move(tempFile, destinationPath);
                
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (i == maxRetries - 1)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\nFailed to download {url} after {maxRetries} attempts: {ex.Message}");
                    Console.ResetColor();
                    return false;
                }
                await Task.Delay(delayMs, cancellationToken);
                delayMs *= 2; // Exponential backoff
            }
        }
        return false;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
