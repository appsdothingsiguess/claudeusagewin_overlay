using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace ClaudeUsage.Services;

public static class UpdateService
{
    private static readonly HttpClient _httpClient = new();
    private const string ReleasesApiUrl = "https://api.github.com/repos/sr-kai/claudeusagewin/releases/latest";

    public static string? LatestVersion { get; private set; }
    public static string? LatestReleaseUrl { get; private set; }
    public static bool UpdateAvailable { get; private set; }

    public static async Task CheckForUpdateAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesApiUrl);
            request.Headers.Add("User-Agent", "ClaudeUsage");
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString(); // e.g., "v1.5.1"
            var htmlUrl = root.GetProperty("html_url").GetString();

            if (tagName == null || htmlUrl == null) return;

            // Strip "v" prefix for comparison
            var remoteVersion = tagName.TrimStart('v');
            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

            LatestVersion = remoteVersion;
            LatestReleaseUrl = htmlUrl;

            // Compare versions
            if (Version.TryParse(remoteVersion, out var remote) &&
                Version.TryParse(currentVersion, out var current))
            {
                UpdateAvailable = remote > current;
            }

            System.Diagnostics.Debug.WriteLine(
                $"Update check: current={currentVersion}, latest={remoteVersion}, available={UpdateAvailable}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
        }
    }
}
