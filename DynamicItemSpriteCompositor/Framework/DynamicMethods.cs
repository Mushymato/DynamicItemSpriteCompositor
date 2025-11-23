using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.ItemTypeDefinitions;

namespace DynamicItemSpriteCompositor.Framework;

internal static class DynamicMethods
{
    internal static Func<Item, bool> Item_get_contextTagsDirty = null!;
    internal static Func<ParsedItemData, Rectangle> ParsedItemData_get_DefaultSourceRect = null!;
    internal static Action<ParsedItemData, Texture2D?> ParsedItemData_set_Texture = null!;
    internal static Action<ParsedItemData, Rectangle> ParsedItemData_set_DefaultSourceRect = null!;
    internal static Action<ParsedItemData, int> ParsedItemData_set_SpriteIndex = null!;

    internal static bool Make()
    {
        Item_get_contextTagsDirty = MakeFieldGetter<Item, bool>(nameof(Item_get_contextTagsDirty), "_contextTagsDirty");
        ParsedItemData_get_DefaultSourceRect = MakeFieldGetter<ParsedItemData, Rectangle>(
            nameof(ParsedItemData_set_DefaultSourceRect),
            "DefaultSourceRect"
        );

        ParsedItemData_set_Texture = MakeFieldSetter<ParsedItemData, Texture2D?>(
            nameof(ParsedItemData_set_Texture),
            "Texture"
        );
        ParsedItemData_set_DefaultSourceRect = MakeFieldSetter<ParsedItemData, Rectangle>(
            nameof(ParsedItemData_set_DefaultSourceRect),
            "DefaultSourceRect"
        );
        ParsedItemData_set_SpriteIndex = MakeFieldSetter<ParsedItemData, int>(
            nameof(ParsedItemData_set_SpriteIndex),
            "SpriteIndex"
        );
        return true;
    }

    private static Func<TArg0, TRet> MakeFieldGetter<TArg0, TRet>(string name, string field)
    {
        DynamicMethod dm = new(name, typeof(TRet), [typeof(TArg0)]);
        ILGenerator gen = dm.GetILGenerator();
        gen.Emit(OpCodes.Ldarg_0);
        gen.Emit(OpCodes.Ldfld, AccessTools.DeclaredField(typeof(TArg0), field));
        gen.Emit(OpCodes.Ret);
        return dm.CreateDelegate<Func<TArg0, TRet>>();
    }

    private static Action<TArg0, TVal> MakeFieldSetter<TArg0, TVal>(string name, string field)
    {
        DynamicMethod dm = new(name, null, [typeof(TArg0), typeof(TVal)]);
        ILGenerator gen = dm.GetILGenerator();
        gen.Emit(OpCodes.Ldarg_0);
        gen.Emit(OpCodes.Ldarg_1);
        gen.Emit(OpCodes.Stfld, AccessTools.DeclaredField(typeof(TArg0), field));
        gen.Emit(OpCodes.Ret);
        return dm.CreateDelegate<Action<TArg0, TVal>>();
    }
}
