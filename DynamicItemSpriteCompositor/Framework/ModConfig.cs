using DynamicItemSpriteCompositor.Models;
using StardewModdingAPI;

namespace DynamicItemSpriteCompositor.Framework;

public sealed record TextureOption(bool Enabled, string Texture);

public sealed class ModConfigData
{
    // ModId -> Key -> Texture
    public Dictionary<string, Dictionary<string, TextureOption>> ContentPackTextureOptions = [];
}

public sealed class ModConfigHelper(IModHelper helper)
{
    public ModConfigData Data = helper.ReadConfig<ModConfigData>();

    internal void LoadContentPackTextureOptions(IEnumerable<ModProidedDataHolder> modDataHolders)
    {
        bool shouldWrite = false;
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
                continue;
            }
            foreach ((string key, ItemSpriteRuleAtlas ruleAtlas) in modRuleAtlas)
            {
                if (innerDict.TryGetValue(key, out TextureOption? option))
                {
                    ruleAtlas.Enabled = option.Enabled;
                    ruleAtlas.ChosenIdx = ruleAtlas.SourceTextures.IndexOf(option.Texture);
                    if (ruleAtlas.ChosenIdx < 0)
                    {
                        ruleAtlas.ChosenIdx = 0;
                        shouldWrite = true;
                    }
                }
            }
        }
        if (shouldWrite)
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
