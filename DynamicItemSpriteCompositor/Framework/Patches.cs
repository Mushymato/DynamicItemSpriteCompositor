using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Objects;

namespace DynamicItemSpriteCompositor.Framework;

internal static class Patches
{
    private const float LAYER_DEPTH_OFFSET = 1 / 10000f;

    private static IGameContentHelper content = null!;

    internal static void Register(IModHelper helper)
    {
        content = helper.GameContent;
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
            HarmonyMethod drawPostfix_Menu = new(typeof(Patches), nameof(SObject_draw_Postfix_Menu))
            {
                priority = Priority.Last,
            };
            HarmonyMethod drawPostfix_Held = new(typeof(Patches), nameof(SObject_draw_Postfix_Held))
            {
                priority = Priority.Last,
            };
            HarmonyMethod drawPostfix_Furniture;
            if (helper.ModRegistry.IsLoaded("sophie.Calcifer"))
            {
                drawPostfix_Furniture = new(typeof(Patches), nameof(SObject_draw_Postfix_Furniture_Calcifer))
                {
                    priority = Priority.Last,
                };
            }
            else
            {
                drawPostfix_Furniture = new(typeof(Patches), nameof(SObject_draw_Postfix_Furniture))
                {
                    priority = Priority.Last,
                };
            }
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
                postfix: drawPostfix_Base
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
                postfix: drawPostfix_Furniture
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

        if (__instance.isTemporarilyInvisible)
            return;

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
        ItemSpriteIndexHolder? holder,
        SObject obj,
        SpriteBatch spriteBatch,
        float x,
        float y,
        float alpha,
        float baseScale,
        float layerDepth
    )
    {
        if (obj.bigCraftable.Value || obj.preservedParentSheetIndex.Value is not string preserveId)
            return;
        float scale;
        Vector2 offset;
        switch (ModEntry.config.Data.SubIconDisplay)
        {
            case SubIconDisplayMode.PackDefined:
                if (holder == null)
                    return;
                if (!holder.TryGetSubIconDraw(out scale, out offset))
                    return;
                break;
            case SubIconDisplayMode.Always:
                if (!(holder?.TryGetSubIconDraw(out scale, out offset) ?? false))
                {
                    scale = 0.5f;
                    offset = Vector2.Zero;
                }
                break;
            default:
                return;
        }
        if (ItemRegistry.GetData(preserveId) is ParsedItemData data)
        {
            // intentionally avoided applying sprite variations on preserve since the item doesn't truly exist
            spriteBatch.Draw(
                data.GetTexture(),
                new Vector2(x + offset.X, y + offset.Y),
                data.GetSourceRect(),
                Color.White * alpha,
                0f,
                Vector2.Zero,
                baseScale * scale,
                SpriteEffects.None,
                layerDepth
            );
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
        if (__state != null)
        {
            if (__state.Value.Item1 is ItemSpriteIndexHolder holder)
                holder.UnsetDrawParsedItemData(obj);
            if (__state.Value.Item2 is ItemSpriteIndexHolder heldHolder)
                heldHolder.UnsetDrawParsedItemData(obj.heldObject.Value);
        }
        if (ModEntry.config.Data.SubIconDisplay == SubIconDisplayMode.None)
            return;
        TryDrawPreserveIcon(__state?.Item1, obj, spriteBatch, x, y, alpha, baseScale, layerDepth);
        if (obj.heldObject.Value != null && (!obj.bigCraftable.Value || obj.readyForHarvest.Value))
            TryDrawPreserveIcon(__state?.Item2, obj.heldObject.Value, spriteBatch, x, y, alpha, baseScale, layerDepth);
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
        Vector2 local = Game1.GlobalToLocal(new(x * 64, y * 64));
        SObject_draw_Postfix_Shared(
            __instance,
            ref __state,
            spriteBatch,
            local.X,
            local.Y,
            alpha,
            4f,
            ((y + 1) * 64 + 1) / 10000f + x / 50000f
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
            4f,
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
            scaleSize * 4f,
            layerDepth + LAYER_DEPTH_OFFSET
        );
    }

    private static void SObject_draw_Postfix_Furniture(
        Furniture __instance,
        ref (ItemSpriteIndexHolder?, ItemSpriteIndexHolder?)? __state,
        SpriteBatch spriteBatch,
        float alpha
    )
    {
        if (__state?.Item2 is ItemSpriteIndexHolder heldHolder && __instance.heldObject.Value is SObject heldObj)
        {
            heldHolder.UnsetDrawParsedItemData(heldObj);
            if (!heldObj.bigCraftable.Value)
            {
                Vector2 local = Game1.GlobalToLocal(
                    new(
                        __instance.boundingBox.Center.X - 32,
                        __instance.boundingBox.Center.Y - (__instance.drawHeldObjectLow.Value ? 32 : 85)
                    )
                );
                TryDrawPreserveIcon(
                    heldHolder,
                    heldObj,
                    spriteBatch,
                    local.X,
                    local.Y,
                    alpha,
                    4f,
                    (__instance.boundingBox.Bottom + 2) / 10000f
                );
            }
        }
    }

    private const string CalciferCompat_FurnitureOffsets = "sophie.Calcifer/FurnitureOffsets";

    private static void SObject_draw_Postfix_Furniture_Calcifer(
        Furniture __instance,
        ref (ItemSpriteIndexHolder?, ItemSpriteIndexHolder?)? __state,
        SpriteBatch spriteBatch,
        float alpha
    )
    {
        if (__state?.Item2 is ItemSpriteIndexHolder heldHolder && __instance.heldObject.Value is SObject heldObj)
        {
            heldHolder.UnsetDrawParsedItemData(heldObj);
            if (!heldObj.bigCraftable.Value)
            {
                Vector2 local = Game1.GlobalToLocal(
                    new(
                        __instance.boundingBox.Center.X - 32,
                        __instance.boundingBox.Center.Y - (__instance.drawHeldObjectLow.Value ? 32 : 85)
                    )
                );
                if (
                    content.DoesAssetExist<Dictionary<string, Vector2>>(
                        content.ParseAssetName(CalciferCompat_FurnitureOffsets)
                    )
                )
                {
                    Dictionary<string, Vector2> calciferFurnitureOffsets = content.Load<Dictionary<string, Vector2>>(
                        CalciferCompat_FurnitureOffsets
                    );
                    if (calciferFurnitureOffsets.TryGetValue(__instance.QualifiedItemId, out Vector2 calciferOffset))
                    {
                        local.X += calciferOffset.X;
                        local.Y += calciferOffset.Y;
                    }
                }
                TryDrawPreserveIcon(
                    heldHolder,
                    heldObj,
                    spriteBatch,
                    local.X,
                    local.Y,
                    alpha,
                    4f,
                    (__instance.boundingBox.Bottom + 2) / 10000f
                );
            }
        }
    }
}
