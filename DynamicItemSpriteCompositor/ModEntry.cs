global using SObject = StardewValley.Object;
using System.Diagnostics;
using DynamicItemSpriteCompositor.Framework;
using StardewModdingAPI;

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
        picker = new(helper, config, manager.modDataAssets, manager.UpdateCompTxForQId);
        DynamicMethods.Make();
        Patches.Register();
    }

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
