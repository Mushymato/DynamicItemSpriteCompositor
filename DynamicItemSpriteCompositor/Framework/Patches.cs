using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;

namespace DynamicItemSpriteCompositor.Framework;

internal static class Patches
{
    internal static void Register()
    {
        Harmony harmony = new(ModEntry.ModId);
        try
        {
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(Item), nameof(Item.ResetParentSheetIndex)),
                postfix: new HarmonyMethod(typeof(Patches), nameof(Item_ResetParentSheetIndex_Postfix))
            );
            HarmonyMethod drawPrefix = new(typeof(Patches), nameof(SObject_draw_Prefix)) { priority = Priority.First };
            HarmonyMethod drawPostfix = new(typeof(Patches), nameof(SObject_draw_Postfix)) { priority = Priority.Last };
            // SObject
            harmony.Patch(
                original: AccessTools.DeclaredMethod(
                    typeof(SObject),
                    nameof(SObject.draw),
                    [typeof(SpriteBatch), typeof(int), typeof(int), typeof(float)]
                ),
                prefix: drawPrefix,
                postfix: drawPostfix
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(SObject), nameof(SObject.drawInMenu)),
                prefix: drawPrefix,
                postfix: drawPostfix
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(SObject), nameof(SObject.drawWhenHeld)),
                prefix: drawPrefix,
                postfix: drawPostfix
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(SObject), nameof(SObject.drawAsProp)),
                prefix: drawPrefix,
                postfix: drawPostfix
            );
            // ColoredObject
            harmony.Patch(
                original: AccessTools.DeclaredMethod(
                    typeof(ColoredObject),
                    nameof(ColoredObject.draw),
                    [typeof(SpriteBatch), typeof(int), typeof(int), typeof(float)]
                ),
                prefix: drawPrefix,
                postfix: drawPostfix
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(ColoredObject), nameof(ColoredObject.drawInMenu)),
                prefix: drawPrefix,
                postfix: drawPostfix
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(ColoredObject), nameof(ColoredObject.drawWhenHeld)),
                prefix: drawPrefix,
                postfix: drawPostfix
            );
            // Furniture
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.draw)),
                prefix: drawPrefix,
                postfix: drawPostfix
            );
        }
        catch (Exception ex)
        {
            ModEntry.Log($"Failed to patch sprite index manipulation:\n{ex}", LogLevel.Error);
            return;
        }
    }

    private static void Item_ResetParentSheetIndex_Postfix(Item __instance)
    {
        ModEntry.manager.ReapplyWatchedSpriteIndex(__instance);
    }

    private static void SObject_draw_Prefix(
        SObject __instance,
        ref (ItemSpriteIndexHolder?, ItemSpriteIndexHolder?)? __state
    )
    {
        __state = null;

        ItemSpriteIndexHolder? holder = null;
        if (__instance is not Furniture)
        {
            holder = GetAndApplyHolder(__instance);
        }

        ItemSpriteIndexHolder? heldHolder = null;
        if (__instance.heldObject.Value is SObject heldObj && heldObj is not ColoredObject)
        {
            heldHolder = GetAndApplyHolder(heldObj);
        }

        if (holder != null || heldHolder != null)
        {
            __state = new(holder, heldHolder);
        }
    }

    private static ItemSpriteIndexHolder? GetAndApplyHolder(SObject obj)
    {
        if (ModEntry.manager.EnsureSpriteIndexForThisDraw(obj, out ItemSpriteIndexHolder? holder))
        {
            holder.SetDrawParsedItemData(obj);
        }
        else
        {
            holder = null;
        }
        return holder;
    }

    private static void SObject_draw_Postfix(
        SObject __instance,
        ref (ItemSpriteIndexHolder?, ItemSpriteIndexHolder?)? __state
    )
    {
        if (__state != null)
        {
            __state.Value.Item1?.UnsetDrawParsedItemData(__instance);
            __state.Value.Item2?.UnsetDrawParsedItemData(__instance.heldObject.Value);
        }
    }
}
