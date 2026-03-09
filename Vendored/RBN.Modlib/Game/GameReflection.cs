using System;
using System.Linq;
using System.Reflection;

namespace RBN.Modlib.Game;

public static class GameReflection
{
    private static readonly BindingFlags AnyStatic =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;

    public static Type? FindType(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return null;

        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(a => a.GetType(fullName, throwOnError: false))
            .FirstOrDefault(t => t != null);
    }

    public static object? TryGetSingletonInstance(string typeName)
    {
        return string.IsNullOrWhiteSpace(typeName)
            ? null
            : TryGetSingletonInstance(FindType(typeName));
    }

    public static object? TryGetSingletonInstance(Type? type)
    {
        if (type == null) return null;

        string[] singletonNames = { "Instance", "instance", "Singleton", "Current", "current" };

        for (var i = 0; i < singletonNames.Length; i++)
        {
            var property = type.GetProperty(singletonNames[i], AnyStatic);
            if (property != null && property.CanRead)
            {
                try
                {
                    return property.GetValue(null, null);
                }
                catch
                {
                    // Ignore and continue.
                }
            }

            var field = type.GetField(singletonNames[i], AnyStatic);
            if (field != null)
            {
                try
                {
                    return field.GetValue(null);
                }
                catch
                {
                    // Ignore and continue.
                }
            }
        }

#pragma warning disable CS0618
        return UnityEngine.Object.FindObjectOfType(type);
#pragma warning restore CS0618
    }
}
