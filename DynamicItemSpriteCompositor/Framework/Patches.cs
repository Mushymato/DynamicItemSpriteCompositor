using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.ItemTypeDefinitions;

namespace DynamicItemSpriteCompositor.Framework;

internal static class Patches
{
    internal static bool ItemMetadata_SetTypeDefinition_Postfix_Enabled { get; set; } = true;
    internal static bool Item_get_ParentSheetIndex_Postfix_Enabled { get; set; } = false;

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
                original: AccessTools.PropertyGetter(typeof(Item), nameof(Item.ParentSheetIndex)),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Item_get_ParentSheetIndex_Postfix))
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
        if (ModEntry.manager.EnsureItemSpriteCompForQualifiedItemId(__instance.QualifiedItemId))
        {
            ModEntry.manager.AddToNeedApplyDynamicSpriteIndex(__instance);
        }
    }

    private static void Item_get_ParentSheetIndex_Postfix(Item __instance, ref int __result)
    {
        if (!Item_get_ParentSheetIndex_Postfix_Enabled)
            return;
        __result = ModEntry.manager.GetSpriteIndex(__instance) ?? __result;
    }

    private static void ItemMetadata_SetTypeDefinition_Postfix(ref ItemMetadata __instance)
    {
        if (!ItemMetadata_SetTypeDefinition_Postfix_Enabled)
            return;
        ModEntry.manager.FixAdditionalMetadata(__instance);
    }
}
