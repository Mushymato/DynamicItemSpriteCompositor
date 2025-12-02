using DynamicItemSpriteCompositor.Integration;
using DynamicItemSpriteCompositor.Models;
using StardewModdingAPI;

namespace DynamicItemSpriteCompositor.Framework;

public sealed record TextureOption(bool Enabled, string Texture);

public class ModConfigData
{
    // ModId -> Key -> Texture
    public Dictionary<string, Dictionary<string, TextureOption>> ContentPackTextureOptions { get; set; } = [];
    public bool DisplayPreserveItemIcon { get; set; } = true;
}

public sealed class ModConfigHelper(IModHelper helper, IManifest mod)
{
    public ModConfigData Data
    {
        get => field ??= helper.ReadConfig<ModConfigData>();
        set => field = value;
    } = null;

    internal void SetupGMCM(ModSpritePicker picker)
    {
        IGenericModConfigMenuApi? gmcm = null;
        try
        {
            gmcm = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        }
        catch { }
        if (gmcm == null)
        {
            ModEntry.Log(
                $"Failed to get 'spacechase0.GenericModConfigMenu' API\nYou can still open the config menu for this mod via console command 'disco-pick'",
                LogLevel.Warn
            );
            return;
        }
        gmcm.Register(mod, () => { }, () => { }, titleScreenOnly: false);
        gmcm.AddComplexOption(
            mod,
            () => string.Empty,
            (b, origin) => { },
            beforeMenuOpened: picker.GMCMPickUI,
            beforeMenuClosed: picker.Cleanup
        );
    }

    internal void WriteConfig()
    {
        helper.WriteConfig(Data);
    }

    internal void SaveContentPackTextureOptions(IEnumerable<ModProidedDataHolder> modDataHolders)
    {
        Dictionary<string, Dictionary<string, TextureOption>> cpto = Data.ContentPackTextureOptions;
        foreach (ModProidedDataHolder holder in modDataHolders)
        {
            if (
                !holder.TryGetModRuleAtlas(
                    helper.GameContent,
                    out Dictionary<string, ItemSpriteRuleAtlas>? modRuleAtlas
                )
            )
            {
                continue;
            }
            if (!cpto.TryGetValue(holder.Mod.UniqueID, out Dictionary<string, TextureOption>? innerDict))
            {
                innerDict = [];
                cpto[holder.Mod.UniqueID] = innerDict;
            }
            foreach ((string key, ItemSpriteRuleAtlas ruleAtlas) in modRuleAtlas)
            {
                innerDict[key] = new(ruleAtlas.Enabled, ruleAtlas.ChosenSourceTexture.Texture);
            }
        }
        helper.WriteConfig(Data);
    }
}
