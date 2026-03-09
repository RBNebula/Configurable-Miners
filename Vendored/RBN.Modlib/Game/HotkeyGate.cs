using System;
using UnityEngine;

namespace RBN.Modlib.Game;

public static class HotkeyGate
{
    private static int _lastCheckedFrame = -1;
    private static bool _lastFrameResult;
    private static float _nextPlayerContextCheckTime;
    private static bool _cachedPlayerContext;

    public static bool IsHotkeyInputEnabled()
    {
        try
        {
            var frame = Time.frameCount;
            if (_lastCheckedFrame == frame)
            {
                return _lastFrameResult;
            }

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var result =
                scene.IsValid() &&
                scene.isLoaded &&
                !string.IsNullOrWhiteSpace(scene.name) &&
                !string.Equals(scene.name, "MainMenu", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(scene.name, "Bootstrap", StringComparison.OrdinalIgnoreCase) &&
                HasPlayerContext();

            _lastCheckedFrame = frame;
            _lastFrameResult = result;
            return result;
        }
        catch
        {
            _lastCheckedFrame = Time.frameCount;
            _lastFrameResult = false;
            return false;
        }
    }

    private static bool HasPlayerContext()
    {
        var now = Time.unscaledTime;
        if (now < _nextPlayerContextCheckTime)
        {
            return _cachedPlayerContext;
        }

        _nextPlayerContextCheckTime = now + 0.25f;
        _cachedPlayerContext = HasPlayerContextSlow();
        return _cachedPlayerContext;
    }

    private static bool HasPlayerContextSlow()
    {
        try
        {
            // Fast path in gameplay scenes.
            var playerTagged = GameObject.FindWithTag("Player");
            if (playerTagged != null) return true;
        }
        catch
        {
            // Ignore missing tag exceptions and continue fallback checks.
        }

        var playerController = GameReflection.TryGetSingletonInstance("PlayerController");
        if (playerController != null) return true;

        return false;
    }
}
