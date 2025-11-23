using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley.Objects;

namespace DynamicItemSpriteCompositor.Framework;

internal static class Patches
{
    internal static bool ItemMetadata_SetTypeDefinition_Postfix_Enabled { get; set; } = true;

    internal static void Register()
    {
        Harmony harmony = new(ModEntry.ModId);
        try
        {
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

    private static readonly HashSet<SObject> ObjectsCheckedThisDraw = [];

    private static void SObject_draw_Prefix(SObject __instance, ref List<(SObject, ItemSpriteIndexHolder?)> __state)
    {
        __state = [];
        for (SObject curr = __instance; curr != null; curr = curr.heldObject.Value)
        {
            if (ObjectsCheckedThisDraw.Contains(curr))
            {
                continue;
            }
            ObjectsCheckedThisDraw.Add(curr);
            if (!ModEntry.manager.EnsureSpriteIndexForThisDraw(curr, out ItemSpriteIndexHolder? holder))
            {
                __state.Add(new(curr, null));
                continue;
            }
            __state.Add(new(curr, holder));
            holder.SetDrawParsedItemData(curr);
        }
    }

    private static void SObject_draw_Postfix(ref List<(SObject, ItemSpriteIndexHolder?)> __state)
    {
        foreach ((SObject curr, ItemSpriteIndexHolder? holder) in __state)
        {
            holder?.UnsetDrawParsedItemData(curr);
            ObjectsCheckedThisDraw.Remove(curr);
        }
    }
}
