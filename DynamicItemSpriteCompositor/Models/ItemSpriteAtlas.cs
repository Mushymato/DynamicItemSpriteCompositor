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
    public int LocalMinIndex { get; set; } = 0;
    public int LocalMaxIndex { get; set; } = 0;
    public int BaseIndex { get; set; } = 0;
}
