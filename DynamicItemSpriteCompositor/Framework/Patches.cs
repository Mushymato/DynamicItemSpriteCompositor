using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.ItemTypeDefinitions;

namespace DynamicItemSpriteCompositor.Framework;

internal static class Patches
{
    internal static bool ItemMetadata_SetTypeDefinition_Postfix_Enabled { get; set; } = true;

    internal static void Register()
    {
        Harmony harmony = new(ModEntry.ModId);
        try
        {
            // TODO check mac inlining ugh
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(Item), nameof(Item.ResetParentSheetIndex)),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Item_ResetParentSheetIndex_Postfix))
                {
                    priority = Priority.Last,
                }
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(ItemMetadata), "SetTypeDefinition"),
                postfix: new HarmonyMethod(typeof(Patches), nameof(ItemMetadata_SetTypeDefinition_Postfix))
                {
                    priority = Priority.Last,
                }
            );
        }
        catch (Exception ex)
        {
            ModEntry.Log($"Failed to patch:\n{ex}", LogLevel.Error);
        }
    }

    private static void Item_ResetParentSheetIndex_Postfix(Item __instance)
    {
        ModEntry.manager.ApplyDynamicSpriteIndex(__instance);
    }

    private static void ItemMetadata_SetTypeDefinition_Postfix(ref ItemMetadata __instance)
    {
        if (!ItemMetadata_SetTypeDefinition_Postfix_Enabled)
            return;
        ModEntry.manager.FixAdditionalMetadata(__instance);
    }
}
