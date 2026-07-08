using System.Text.Json;
using AppOpenerAndTimer.Models;

namespace AppOpenerAndTimer.Services;

/// <summary>
/// Persists the user's named app lists as JSON in <see cref="Preferences"/>.
/// </summary>
public static class AppListStore
{
    const string Key = "app_lists_v1";

    public static List<AppList> GetAll()
    {
        var raw = Preferences.Get(Key, string.Empty);
        if (string.IsNullOrEmpty(raw))
            return new List<AppList>();

        try
        {
            var lists = JsonSerializer.Deserialize<List<AppList>>(raw);
            return lists ?? new List<AppList>();
        }
        catch
        {
            return new List<AppList>();
        }
    }

    public static AppList? Get(string name)
        => GetAll().FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));

    public static bool Exists(string name)
        => GetAll().Any(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Insert or update a list, matched by name (case-insensitive).</summary>
    public static void Save(AppList list)
    {
        var all = GetAll();
        var idx = all.FindIndex(l => string.Equals(l.Name, list.Name, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0)
            all[idx] = list;
        else
            all.Add(list);
        SaveAll(all);
    }

    public static void Delete(string name)
    {
        var all = GetAll();
        all.RemoveAll(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));
        SaveAll(all);
    }

    static void SaveAll(List<AppList> lists)
        => Preferences.Set(Key, JsonSerializer.Serialize(lists));
}
