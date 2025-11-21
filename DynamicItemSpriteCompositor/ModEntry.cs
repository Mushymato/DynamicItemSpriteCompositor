global using SObject = StardewValley.Object;
using System.Diagnostics;
using DynamicItemSpriteCompositor.Framework;
using DynamicItemSpriteCompositor.Integration;
using StardewModdingAPI;
using StardewModdingAPI.Events;

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

    internal static IExtraMachineConfigApi? EMC = null;

    public override void Entry(IModHelper helper)
    {
        mon = Monitor;
        config = new(helper);
        manager = new(helper);

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        try
        {
            EMC = Helper.ModRegistry.GetApi<IExtraMachineConfigApi>("selph.ExtraMachineConfig");
        }
        catch (Exception ex)
        {
            Log($"Failed to get 'selph.ExtraMachineConfig' API:\n{ex}", LogLevel.Warn);
            EMC = null;
        }
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
    [Conditional("DEBUG")]
    internal static void LogDebug(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon!.Log(msg, level);
    }
}
