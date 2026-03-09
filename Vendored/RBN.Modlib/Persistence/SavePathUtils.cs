using System;
using System.Linq;
using System.Reflection;

namespace RBN.Modlib.Persistence;

public static class SavePathUtils
{
    public static string SanitizeFolderName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (invalid.Contains(chars[i])) chars[i] = '_';
        }

        return new string(chars).Trim();
    }

    public static string? TryGetActiveSaveFolderName()
    {
        var type = Game.GameReflection.FindType("SavingLoadingManager");
        if (type == null) return null;

        var instance = Game.GameReflection.TryGetSingletonInstance(type);
        if (instance == null) return null;

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var field = type.GetField("ActiveSaveFileName", flags);
        var value = field?.GetValue(instance) as string;

        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return null;
        if (string.Equals(trimmed, "Editor Quick Save", StringComparison.OrdinalIgnoreCase)) return null;

        return trimmed;
    }
}
