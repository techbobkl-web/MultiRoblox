using System.Text.Json;
using System.Text.Json.Serialization;

namespace MultiRoblox;

/// <summary>
/// One saved Roblox account. The sign-in itself is not stored here: it lives in that account's own
/// browser profile folder (Edge/WebView2 keeps its cookies encrypted for the Windows user).
/// </summary>
internal sealed class SavedAccount
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public long UserId { get; set; }
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DateTime AddedUtc { get; set; }

    [JsonIgnore]
    public string ProfileFolder => Path.Combine(AccountStore.ProfilesFolder, Id);

    public override string ToString()
    {
        if (Username.Length == 0)
        {
            return "New account";
        }

        return DisplayName.Length > 0 && !DisplayName.Equals(Username, StringComparison.OrdinalIgnoreCase)
            ? $"{DisplayName} (@{Username})"
            : $"@{Username}";
    }
}

internal sealed class AppData
{
    public List<SavedAccount> Accounts { get; set; } = new();
    public string LastGame { get; set; } = "";
}

/// <summary>%LOCALAPPDATA%\MultiRoblox\accounts.json plus one browser profile folder per account.</summary>
internal static class AccountStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string Root =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MultiRoblox");

    public static string ProfilesFolder => Path.Combine(Root, "Profiles");

    private static string FilePath => Path.Combine(Root, "accounts.json");

    /// <returns>The data, and whether it was read cleanly (false if the file was unreadable or corrupt).</returns>
    public static (AppData Data, bool ReadCleanly) Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return (new AppData(), true);
            }

            var data = JsonSerializer.Deserialize<AppData>(File.ReadAllText(FilePath), JsonOptions);
            return data is null ? (new AppData(), false) : (data, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return (new AppData(), false);
        }
    }

    public static void Save(AppData data)
    {
        Directory.CreateDirectory(Root);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(data, JsonOptions));
        File.Move(temp, FilePath, overwrite: true);
    }

    /// <summary>Best effort: the folder can stay locked for a moment after its window closes.</summary>
    public static void TryDeleteProfile(string accountId)
    {
        try
        {
            string folder = Path.Combine(ProfilesFolder, accountId);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Cleaned up on a later start by DeleteOrphanProfiles.
        }
    }

    /// <summary>Removes profile folders left behind by removed accounts or abandoned sign-ins.</summary>
    public static void DeleteOrphanProfiles(AppData data)
    {
        if (!Directory.Exists(ProfilesFolder))
        {
            return;
        }

        foreach (string folder in Directory.GetDirectories(ProfilesFolder))
        {
            string id = Path.GetFileName(folder);
            if (!data.Accounts.Any(a => a.Id == id))
            {
                TryDeleteProfile(id);
            }
        }
    }
}
