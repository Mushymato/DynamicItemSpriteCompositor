global using SObject = StardewValley.Object;
using System.Diagnostics;
using DynamicItemSpriteCompositor.Framework;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;

namespace DynamicItemSpriteCompositor;

public sealed class ModEntry : Mod
{
#if DEBUG
    private const LogLevel DEFAULT_LOG_LEVEL = LogLevel.Debug;
#else
    private const LogLevel DEFAULT_LOG_LEVEL = LogLevel.Trace;
#endif

    public const string ModId = "mushymato.DISCO";
    private static IMonitor? mon;
    internal static ItemSpriteManager manager = null!;
    internal static ModConfigHelper config = null!;
    internal static ModSpritePicker picker = null!;

    public override void Entry(IModHelper helper)
    {
        mon = Monitor;

        if (ItemSpriteManager.Make(helper) is not ItemSpriteManager mngr)
        {
            return;
        }

        manager = mngr;
        config = new(helper, ModManifest);
        picker = new(
            helper,
            config,
            $"DISCO v{ModManifest.Version}",
            manager.modDataAssets,
            manager.UpdateCompTxForAtlas
        );
        DynamicMethods.Make();
        Patches.Register(helper);

#if DEBUG
        helper.ConsoleCommands.Add("jelly", "jelly", ConsoleJelly);
    }

    private void ConsoleJelly(string arg1, string[] arg2)
    {
        xTile.Layers.Layer layer = Game1.currentLocation.Map.RequireLayer("Back");
        for (int x = 0; x < layer.LayerWidth; x++)
        {
            for (int y = 0; y < layer.LayerHeight; y++)
            {
                Vector2 pos = new(x, y);
                if (layer.Tiles[x, y] is null)
                    continue;
                if (Utility.CreateFlavoredItem("Jelly", "(O)613") is SObject jelly)
                {
                    Game1.currentLocation.objects.Remove(pos);
                    Game1.currentLocation.objects.Add(pos, jelly);
                }
                // if (ItemRegistry.Create<SObject>("(BC)12", 1) is SObject keg)
                // {
                //     Game1.currentLocation.objects.Remove(pos);
                //     Game1.currentLocation.objects.Add(pos, keg);
                // }
            }
        }
    }
#else
    }
#endif

    /// <summary>SMAPI static monitor Log wrapper</summary>
    /// <param name="msg"></param>
    /// <param name="level"></param>
    internal static void Log(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon!.Log(msg, level);
    }

    /// <summary>SMAPI static monitor LogOnce wrapper</summary>
    /// <param name="msg"></param>
    /// <param name="level"></param>
    internal static void LogOnce(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon!.LogOnce(msg, level);
    }

    /// <summary>SMAPI static monitor Log wrapper, debug only</summary>
    /// <param name="msg"></param>
    /// <param name="level"></param>
    [Conditional("DEBUG_VERBOSE")]
    internal static void LogDebug(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon!.Log(msg, level);
    }
}
