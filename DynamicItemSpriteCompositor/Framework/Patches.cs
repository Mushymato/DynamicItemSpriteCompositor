using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
                prefix: new HarmonyMethod(typeof(Patches), nameof(Item_ResetParentSheetIndex_Prefix))
                {
                    priority = Priority.Last,
                }
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(Item), nameof(Item.MarkContextTagsDirty)),
                prefix: new HarmonyMethod(typeof(Patches), nameof(Item_MarkContextTagsDirty_Prefix))
                {
                    priority = Priority.Last,
                }
            );
            harmony.Patch(
                original: AccessTools.PropertySetter(typeof(Item), nameof(Item.ParentSheetIndex)),
                prefix: new HarmonyMethod(typeof(Patches), nameof(Item_set_ParentSheetIndex_Prefix))
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
            ModEntry.Log($"Failed to patch sprite index manipulation:\n{ex}", LogLevel.Error);
        }

        try
        {
            harmony.Patch(
                original: AccessTools.DeclaredMethod(
                    typeof(SObject),
                    nameof(SObject.draw),
                    [typeof(SpriteBatch), typeof(int), typeof(int), typeof(float)]
                ),
                transpiler: new HarmonyMethod(typeof(Patches), nameof(SObject_draw_Transpiler))
                {
                    priority = Priority.Last,
                }
            );
        }
        catch (Exception ex)
        {
            ModEntry.Log($"Failed to patch Object.draw, some visual bugs exist:\n{ex}", LogLevel.Warn);
        }
    }

    private static void Item_ResetParentSheetIndex_Prefix(Item __instance)
    {
        ModEntry.manager.AddToNeedApplyDynamicSpriteIndex(__instance);
    }

    private static void Item_MarkContextTagsDirty_Prefix(Item __instance)
    {
        ModEntry.manager.AddToNeedApplyDynamicSpriteIndexIfWatched(__instance);
    }

    private static bool Item_set_ParentSheetIndex_Prefix(Item __instance, int value)
    {
        return ModEntry.manager.SetSpriteIndex(__instance, value);
    }

    private static void Item_get_ParentSheetIndex_Postfix(Item __instance, ref int __result)
    {
        __result = ModEntry.manager.GetSpriteIndex(__instance, __result);
    }

    private static void ItemMetadata_SetTypeDefinition_Postfix(ref ItemMetadata __instance)
    {
        if (ItemMetadata_SetTypeDefinition_Postfix_Enabled)
            ModEntry.manager.FixAdditionalMetadata(__instance);
    }

    public static IEnumerable<CodeInstruction> SObject_draw_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatch[] ParsedItemData_GetSourceRect =
            [
                new(),
                new(OpCodes.Initobj),
                new(),
                new(
                    OpCodes.Callvirt,
                    AccessTools.DeclaredMethod(typeof(ParsedItemData), nameof(ParsedItemData.GetSourceRect))
                ),
            ];
            MethodInfo Fix_ParsedItemData_GetSourceRect_Method = AccessTools.DeclaredMethod(
                typeof(Patches),
                nameof(Fix_ParsedItemData_GetSourceRect)
            );
            CodeMatcher matcher = new(instructions, generator);
            matcher
                .End()
                .MatchEndBackwards(ParsedItemData_GetSourceRect)
                .Repeat(match =>
                {
                    match.Opcode = OpCodes.Call;
                    match.Operand = Fix_ParsedItemData_GetSourceRect_Method;
                    match.InsertAndAdvance([new(OpCodes.Ldarg_0)]);
                });
            return matcher.Instructions();
        }
        catch (Exception ex)
        {
            ModEntry.Log($"Failed in SObject_draw_Transpiler:\n{ex}", LogLevel.Warn);
            return instructions;
        }
    }

    private static Rectangle Fix_ParsedItemData_GetSourceRect(
        ParsedItemData parsedItemData,
        int offset,
        int? spriteIndex,
        SObject obj
    )
    {
        if (spriteIndex == null)
        {
            if (parsedItemData.QualifiedItemId == obj.heldObject.Value?.QualifiedItemId)
            {
                spriteIndex = obj.heldObject.Value.ParentSheetIndex;
            }
            else
            {
                spriteIndex = obj.ParentSheetIndex;
            }
        }
        return parsedItemData.GetSourceRect(offset, spriteIndex);
    }
}
