using StardewModdingAPI;

namespace DynamicItemSpriteCompositor.Models;

public sealed class ItemSpriteRuleAtlas
{
    public string TypeIdentifier { get; set; } = "(O)";
    public string LocalItemId { get; set; } = "0";
    public string SourceTexture { get; set; } = "";
    public int? SourceSpritePerIndex { get; set; } = null;
    public List<SpriteIndexRule> Rules { get; set; } = [];

    internal IAssetName? SourceTextureAsset = null;

    internal IAssetName GetAssetName(IGameContentHelper gameContent)
    {
        SourceTextureAsset = gameContent.ParseAssetName(SourceTexture);
        return SourceTextureAsset;
    }

    internal IAssetName? SourceModAsset { get; set; } = null;
}
