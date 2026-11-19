using System.Net.Http;
using System.Text.RegularExpressions;

namespace Typesetterer3;

internal static partial class GoogleDocsService
{
    private static readonly HttpClient HttpClient = new HttpClient();

    static GoogleDocsService()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        HttpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public static string? TryGetExportUrl(string inputUrl, out bool isHtml)
    {
        isHtml = true;
        if (string.IsNullOrWhiteSpace(inputUrl)) return null;

        var url = inputUrl.Trim();

        // Published web document format: https://docs.google.com/document/d/e/2PACX-1v.../pub
        if (url.Contains("/document/d/e/", StringComparison.OrdinalIgnoreCase))
        {
            if (!url.EndsWith("/pub", StringComparison.OrdinalIgnoreCase) && !url.Contains("/pub?", StringComparison.OrdinalIgnoreCase))
            {
                int pubIdx = url.IndexOf("/pub", StringComparison.OrdinalIgnoreCase);
                if (pubIdx > 0)
                {
                    url = url.Substring(0, pubIdx + 4);
                }
                else
                {
                    url = url.TrimEnd('/') + "/pub";
                }
            }
            return url;
        }

        // Standard Google Doc: https://docs.google.com/document/d/{DOC_ID}/...
        var match = StandardDocRegex().Match(url);
        if (match.Success)
        {
            var docId = match.Groups[1].Value;
            return $"https://docs.google.com/document/d/{docId}/export?format=html";
        }

        return null;
    }

    public static async Task<(string? content, bool isHtml, string? error)> FetchDocumentContentAsync(string inputUrl)
    {
        var exportUrl = TryGetExportUrl(inputUrl, out bool isHtml);
        if (string.IsNullOrEmpty(exportUrl))
        {
            return (null, false, "Invalid Google Docs URL format.");
        }

        try
        {
            var response = await HttpClient.GetAsync(exportUrl);
            if (!response.IsSuccessStatusCode)
            {
                // Fallback to format=txt if HTML export failed
                if (exportUrl.Contains("format=html", StringComparison.OrdinalIgnoreCase))
                {
                    var txtUrl = exportUrl.Replace("format=html", "format=txt", StringComparison.OrdinalIgnoreCase);
                    var txtResponse = await HttpClient.GetAsync(txtUrl);
                    if (txtResponse.IsSuccessStatusCode)
                    {
                        var txtContent = await txtResponse.Content.ReadAsStringAsync();
                        return (txtContent, false, null);
                    }
                }

                return (null, false, $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase}). Ensure document is viewable by anyone with link.");
            }

            var content = await response.Content.ReadAsStringAsync();
            return (content, isHtml, null);
        }
        catch (Exception ex)
        {
            return (null, false, $"Failed to connect: {ex.Message}");
        }
    }

    [GeneratedRegex(@"/document/d/([a-zA-Z0-9_-]+)")]
    private static partial Regex StandardDocRegex();
}
