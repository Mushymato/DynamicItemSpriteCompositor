using StardewModdingAPI;

namespace DynamicItemSpriteCompositor.Models;

public sealed class ItemSpriteRuleAtlas
{
    public string TypeIdentifier { get; set; } = "(O)";
    public string LocalItemId { get; set; } = "0";
    public string SourceTexture { get; set; } = "";
    public List<SpriteIndexRule> Rules = [];

    internal IAssetName? SourceTextureAsset = null;

    internal IAssetName GetAssetName(IGameContentHelper gameContent)
    {
        SourceTextureAsset = gameContent.ParseAssetName(SourceTexture);
        return SourceTextureAsset;
    }

    internal IAssetName? SourceModAsset { get; set; } = null;
    internal int LocalMinIndex { get; set; } = 0;
    internal int LocalMaxIndex { get; set; } = 0;
    internal int BaseIndex { get; set; } = 0;
}
