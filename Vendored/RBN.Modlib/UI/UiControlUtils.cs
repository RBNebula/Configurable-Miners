using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RBN.Modlib.UI;

public static class UiControlUtils
{
    private static readonly BindingFlags AnyInstance =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly Type? UiManagerType = Game.GameReflection.FindType("UIManager");
    private static readonly MethodInfo? UiManagerIsInAnyMenuMethod = UiManagerType?.GetMethod("IsInAnyMenu", AnyInstance);
    private static readonly MethodInfo? UiManagerIsInAnyMenuExceptInventoryMethod = UiManagerType?.GetMethod("IsInAnyMenuExceptInventory", AnyInstance);
    private static readonly MethodInfo? UiManagerIsInPauseMenuMethod = UiManagerType?.GetMethod("IsInPauseMenu", AnyInstance);
    private static readonly MethodInfo? UiManagerIsInInventoryMethod = UiManagerType?.GetMethod("IsInInventory", AnyInstance);
    private static readonly MethodInfo? UiManagerIsInQuestTreeMethod = UiManagerType?.GetMethod("IsInQuestTree", AnyInstance);
    private static readonly MethodInfo? UiManagerIsInEditTextPopupMethod = UiManagerType?.GetMethod("IsInEditTextPopup", AnyInstance);

    public static bool ShouldHidePopupUi()
    {
        try
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.IsValid() && string.Equals(scene.name, "MainMenu", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var uiManager = Game.GameReflection.TryGetSingletonInstance(UiManagerType ?? Game.GameReflection.FindType("UIManager"));
            if (uiManager == null) return false;

            if (TryInvokeBool(uiManager, UiManagerIsInAnyMenuMethod)) return true;
            if (TryInvokeBool(uiManager, UiManagerIsInAnyMenuExceptInventoryMethod)) return true;
            if (TryInvokeBool(uiManager, UiManagerIsInPauseMenuMethod)) return true;
            if (TryInvokeBool(uiManager, UiManagerIsInInventoryMethod)) return true;
            if (TryInvokeBool(uiManager, UiManagerIsInQuestTreeMethod)) return true;
            if (TryInvokeBool(uiManager, UiManagerIsInEditTextPopupMethod)) return true;

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryInvokeBool(object instance, MethodInfo? method)
    {
        if (method == null) return false;
        try
        {
            return method.Invoke(instance, null) is bool value && value;
        }
        catch
        {
            return false;
        }
    }

    public static void EnsureCursorUnlocked()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public static void TogglePlayerControls(bool enable, List<MonoBehaviour> disabledScripts)
    {
        GameObject? playerRoot = GameObject.FindWithTag("Player");
        if (playerRoot == null && Camera.main != null) playerRoot = Camera.main.transform.root.gameObject;
        if (playerRoot == null) return;

        foreach (var mono in playerRoot.GetComponents<MonoBehaviour>())
        {
            var typeName = mono.GetType().Name;
            if (typeName != "PlayerController" && typeName != "PlayerInventory") continue;

            if (!enable)
            {
                if (!mono.enabled) continue;
                mono.enabled = false;
                disabledScripts.Add(mono);
                continue;
            }

            if (disabledScripts.Contains(mono)) mono.enabled = true;
        }

        if (enable) disabledScripts.Clear();
    }

    public static void BeginUiManagerSuppression(ref MonoBehaviour? suppressedUiManagerBehaviour, ref bool suppressedUiManagerWasEnabled)
    {
        if (suppressedUiManagerBehaviour != null) return;

        var instance = Game.GameReflection.TryGetSingletonInstance("UIManager");
        if (instance is not MonoBehaviour uiManager) return;

        suppressedUiManagerBehaviour = uiManager;
        suppressedUiManagerWasEnabled = uiManager.enabled;
        uiManager.enabled = false;
    }

    public static void EndUiManagerSuppression(ref MonoBehaviour? suppressedUiManagerBehaviour, ref bool suppressedUiManagerWasEnabled)
    {
        if (suppressedUiManagerBehaviour == null) return;

        suppressedUiManagerBehaviour.enabled = suppressedUiManagerWasEnabled;
        suppressedUiManagerBehaviour = null;
        suppressedUiManagerWasEnabled = false;
    }
}
