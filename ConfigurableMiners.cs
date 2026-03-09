using BepInEx;
using HarmonyLib;

namespace ConfigurableMiners;

[BepInPlugin(ModInfo.PLUGIN_GUID, ModInfo.PLUGIN_NAME, ModInfo.PLUGIN_VERSION)]
[BepInDependency("com.rebind", BepInDependency.DependencyFlags.SoftDependency)]
public sealed partial class ConfigurableMiners : BaseUnityPlugin
{
    private readonly Harmony _harmony = new(ModInfo.HARMONY_ID);

    private void Awake()
    {
        InitializePlugin();
    }

    private void OnDestroy()
    {
        ShutdownPlugin();
    }
}
