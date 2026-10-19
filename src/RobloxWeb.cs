using System.Text.Json;

namespace MultiRoblox;

internal sealed record RobloxUser(long Id, string Name, string DisplayName);

internal static class RobloxWeb
{
    public const string LoginUrl = "https://www.roblox.com/login";
    public const string HomeUrl = "https://www.roblox.com/home";

    private static readonly HttpClient Http = new(new HttpClientHandler { UseCookies = false })
    {
        Timeout = TimeSpan.FromSeconds(15),
    };

    /// <summary>Asks roblox.com who a freshly signed-in session belongs to. The session is only ever sent to roblox.com.</summary>
    public static async Task<RobloxUser?> GetSignedInUserAsync(string sessionCookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated");
        request.Headers.TryAddWithoutValidation("Cookie", ".ROBLOSECURITY=" + sessionCookie);

        using var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        try
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            return new RobloxUser(
                root.GetProperty("id").GetInt64(),
                root.GetProperty("name").GetString() ?? "",
                root.GetProperty("displayName").GetString() ?? "");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Turns what the user pasted into a roblox.com page that starts the game when opened.
    /// Accepts a place ID, a game link, a private-server link, or a share link. Only roblox.com
    /// addresses are allowed, so an account window never opens some other site.
    /// </summary>
    public static bool TryMakeLaunchUrl(string input, out string url, out string error)
    {
        url = "";
        error = "";
        string text = input.Trim();

        if (text.Length == 0)
        {
            error = "Paste a Roblox game link or a place ID first.";
            return false;
        }

        if (text.All(char.IsAsciiDigit))
        {
            url = $"https://www.roblox.com/games/start?placeId={text}";
            return true;
        }

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !(uri.Host.Equals("roblox.com", StringComparison.OrdinalIgnoreCase)
                 || uri.Host.EndsWith(".roblox.com", StringComparison.OrdinalIgnoreCase)))
        {
            error = "That doesn't look like a roblox.com link. Paste a game link (roblox.com/games/…) or a place ID.";
            return false;
        }

        // A plain game page (roblox.com/games/123/Name) only shows the page; the /games/start
        // form starts the game straight away. Private-server, share and start links already do.
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        bool isPlainGamePage = segments.Length >= 2
            && segments[0].Equals("games", StringComparison.OrdinalIgnoreCase)
            && segments[1].All(char.IsAsciiDigit)
            && !uri.Query.Contains("privateServerLinkCode", StringComparison.OrdinalIgnoreCase);

        url = isPlainGamePage
            ? $"https://www.roblox.com/games/start?placeId={segments[1]}"
            : uri.AbsoluteUri;
        return true;
    }
}
