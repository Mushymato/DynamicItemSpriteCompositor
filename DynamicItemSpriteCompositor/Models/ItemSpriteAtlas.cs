using Newtonsoft.Json;
using StardewModdingAPI;

namespace DynamicItemSpriteCompositor.Models;

public sealed class SourceTextureOption
{
    public string Id => Texture;
    public string Texture { get; set; } = "";
    public string? ConfigName { get; set; } = null;

    internal IAssetName? SourceTextureAsset = null;

    internal IAssetName GetAssetName(IGameContentHelper gameContent)
    {
        SourceTextureAsset = gameContent.ParseAssetName(Texture);
        return SourceTextureAsset;
    }
}

public sealed class ItemSpriteRuleAtlas
{
    public string TypeIdentifier { get; set; } = "(O)";
    public string LocalItemId { get; set; } = "0";
    public string? ConfigName { get; set; } = null;
    internal string QualifiedItemId => string.Concat(TypeIdentifier, LocalItemId);

    [JsonConverter(typeof(SourceTextureOptionListConverter))]
    public List<SourceTextureOption> SourceTextureList { get; set; } = [];
    public int? SourceSpritePerIndex { get; set; } = null;
    public List<SpriteIndexRule> Rules { get; set; } = [];

    internal IAssetName? SourceModAsset { get; set; } = null;
    internal int ChosenIdx { get; set; } = 0;
    internal SourceTextureOption ChosenSourceTexture => SourceTextureList[ChosenIdx];
}
