using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;

namespace DynamicItemSpriteCompositor.Framework;

internal static class Patches
{
    private const float LAYER_DEPTH_OFFSET = 1 / 10000f;

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
            HarmonyMethod drawPostfix_Base = new(typeof(Patches), nameof(SObject_draw_Postfix_Base))
            {
                priority = Priority.Last,
            };
            HarmonyMethod drawPostfix_Pos = new(typeof(Patches), nameof(SObject_draw_Postfix_Pos))
            {
                priority = Priority.Last,
            };
            HarmonyMethod drawPostfix_NonTile = new(typeof(Patches), nameof(SObject_draw_Postfix_NonTile))
            {
                priority = Priority.Last,
            };
            HarmonyMethod drawPostfix_Menu = new(typeof(Patches), nameof(SObject_draw_Postfix_Menu))
            {
                priority = Priority.Last,
            };
            HarmonyMethod drawPostfix_Held = new(typeof(Patches), nameof(SObject_draw_Postfix_Held))
            {
                priority = Priority.Last,
            };
            // SObject
            harmony.Patch(
                original: AccessTools.DeclaredMethod(
                    typeof(SObject),
                    nameof(SObject.draw),
                    [typeof(SpriteBatch), typeof(int), typeof(int), typeof(float)]
                ),
                prefix: drawPrefix,
                postfix: drawPostfix_Pos
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(
                    typeof(SObject),
                    nameof(SObject.draw),
                    [typeof(SpriteBatch), typeof(int), typeof(int), typeof(float), typeof(float)]
                ),
                prefix: drawPrefix,
                postfix: drawPostfix_NonTile
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(SObject), nameof(SObject.drawInMenu)),
                prefix: drawPrefix,
                postfix: drawPostfix_Menu
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(SObject), nameof(SObject.drawWhenHeld)),
                prefix: drawPrefix,
                postfix: drawPostfix_Held
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(SObject), nameof(SObject.drawAsProp)),
                prefix: drawPrefix,
                postfix: drawPostfix_Base
            );
            // ColoredObject
            harmony.Patch(
                original: AccessTools.DeclaredMethod(
                    typeof(ColoredObject),
                    nameof(ColoredObject.draw),
                    [typeof(SpriteBatch), typeof(int), typeof(int), typeof(float)]
                ),
                prefix: drawPrefix,
                postfix: drawPostfix_Pos
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(ColoredObject), nameof(ColoredObject.drawInMenu)),
                prefix: drawPrefix,
                postfix: drawPostfix_Menu
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(ColoredObject), nameof(ColoredObject.drawWhenHeld)),
                prefix: drawPrefix,
                postfix: drawPostfix_Held
            );
            // Furniture
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(Furniture), nameof(Furniture.draw)),
                prefix: drawPrefix,
                postfix: drawPostfix_Pos
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

    private static bool DrawingPreserveIcon = false;

    private static void SObject_draw_Prefix(
        SObject __instance,
        ref (ItemSpriteIndexHolder?, ItemSpriteIndexHolder?)? __state
    )
    {
        if (DrawingPreserveIcon)
            return;

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

    private static void SObject_draw_Postfix_Base(
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

    private static void TryDrawPreserveIcon(
        ItemSpriteIndexHolder holder,
        SObject obj,
        SpriteBatch spriteBatch,
        float x,
        float y,
        float alpha,
        float baseScale,
        float layerDepth
    )
    {
        if (holder == null || obj.preservedParentSheetIndex.Value == null)
            return;
        holder.UnsetDrawParsedItemData(obj);
        if (
            holder.TryGetPreserveIconDraw(out float scale, out Vector2 offset)
            && SampleObjectCache.GetObject(obj.preservedParentSheetIndex.Value) is SObject preserve
        )
        {
            DrawingPreserveIcon = true;
            preserve.drawInMenu(
                spriteBatch,
                new(x - offset.X, y - offset.Y),
                baseScale * scale,
                alpha,
                layerDepth,
                StackDrawType.Hide,
                Color.White,
                drawShadow: false
            );
            DrawingPreserveIcon = false;
        }
    }

    private static void SObject_draw_Postfix_Shared(
        SObject obj,
        ref (ItemSpriteIndexHolder?, ItemSpriteIndexHolder?)? __state,
        SpriteBatch spriteBatch,
        float x,
        float y,
        float alpha,
        float baseScale,
        float layerDepth
    )
    {
        if (DrawingPreserveIcon)
            return;

        if (__state == null)
        {
            return;
        }
        if (__state.Value.Item1 is ItemSpriteIndexHolder holder)
        {
            holder.UnsetDrawParsedItemData(obj);
            if (!obj.bigCraftable.Value)
                TryDrawPreserveIcon(holder, obj, spriteBatch, x, y, alpha, baseScale, layerDepth);
        }
        if (__state.Value.Item2 is ItemSpriteIndexHolder heldHolder)
        {
            heldHolder.UnsetDrawParsedItemData(obj.heldObject.Value);
            if (!obj.bigCraftable.Value)
                TryDrawPreserveIcon(heldHolder, obj.heldObject.Value, spriteBatch, x, y, alpha, baseScale, layerDepth);
        }
    }

    private static void SObject_draw_Postfix_Pos(
        SObject __instance,
        ref (ItemSpriteIndexHolder?, ItemSpriteIndexHolder?)? __state,
        SpriteBatch spriteBatch,
        int x,
        int y,
        float alpha
    )
    {
        SObject_draw_Postfix_Shared(
            __instance,
            ref __state,
            spriteBatch,
            x * 64,
            y * 64,
            alpha,
            4f,
            ((y + 1) * 64 + 1) / 10000f + x / 50000f
        );
    }

    private static void SObject_draw_Postfix_NonTile(
        SObject __instance,
        ref (ItemSpriteIndexHolder?, ItemSpriteIndexHolder?)? __state,
        SpriteBatch spriteBatch,
        int xNonTile,
        int yNonTile,
        float layerDepth,
        float alpha
    )
    {
        SObject_draw_Postfix_Shared(
            __instance,
            ref __state,
            spriteBatch,
            xNonTile,
            yNonTile,
            alpha,
            4f,
            layerDepth + LAYER_DEPTH_OFFSET
        );
    }

    private static void SObject_draw_Postfix_Held(
        SObject __instance,
        ref (ItemSpriteIndexHolder?, ItemSpriteIndexHolder?)? __state,
        SpriteBatch spriteBatch,
        Vector2 objectPosition,
        Farmer f
    )
    {
        SObject_draw_Postfix_Shared(
            __instance,
            ref __state,
            spriteBatch,
            objectPosition.X,
            objectPosition.Y,
            1f,
            1f,
            Math.Max(0f, (f.StandingPixel.Y + 4) / 10000f)
        );
    }

    private static void SObject_draw_Postfix_Menu(
        SObject __instance,
        ref (ItemSpriteIndexHolder?, ItemSpriteIndexHolder?)? __state,
        SpriteBatch spriteBatch,
        Vector2 location,
        float scaleSize,
        float transparency,
        float layerDepth
    )
    {
        SObject_draw_Postfix_Shared(
            __instance,
            ref __state,
            spriteBatch,
            location.X,
            location.Y,
            transparency,
            scaleSize,
            layerDepth + LAYER_DEPTH_OFFSET
        );
    }
}
