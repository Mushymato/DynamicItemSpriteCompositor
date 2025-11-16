using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace DynamicItemSpriteCompositor.Framework;

internal static class Patches
{
    internal static void Register()
    {
        Harmony harmony = new(ModEntry.ModId);
        try
        {
            // TODO check mac inlining ugh
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(Item), nameof(Item.ResetParentSheetIndex)),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Item_ResetParentSheetIndex_Postfix))
            );
        }
        catch (Exception ex)
        {
            ModEntry.Log($"Failed to patch:\n{ex}", LogLevel.Error);
        }
    }

    private static void Item_ResetParentSheetIndex_Postfix(Item __instance)
    {
        ModEntry.manager.ApplyDynamicSpriteIndex(__instance, watch: false);
        ModEntry.manager.PushItemToApplyDynamicSpriteIn1Tick(__instance);
    }
}
