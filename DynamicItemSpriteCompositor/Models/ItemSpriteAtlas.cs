using Newtonsoft.Json;
using StardewModdingAPI;

namespace DynamicItemSpriteCompositor.Models;

public sealed record SourceTextureOption(string Texture, IAssetName SourceTextureAsset);

public sealed class ItemSpriteRuleAtlas
{
    public string TypeIdentifier { get; set; } = "(O)";
    public string LocalItemId { get; set; } = "0";
    public string? ConfigName { get; set; } = null;
    public int? ConfigIconSpriteIndex { get; set; } = null;

    [JsonConverter(typeof(SourceTexturesConverter))]
    public List<string> SourceTextures { get; set; } = [];
    public int? SourceSpritePerIndex { get; set; } = null;
    public List<SpriteIndexRule> Rules { get; set; } = [];

    private string? qId = null;
    internal string QualifiedItemId => qId ??= string.Concat(TypeIdentifier, LocalItemId);

    internal string Key { get; set; } = null!;
    internal IAssetName SourceModAsset { get; set; } = null!;

    internal List<SourceTextureOption> SourceTextureOptions { get; set; } = [];
    internal int ChosenIdx { get; set; } = 0;
    internal SourceTextureOption ChosenSourceTexture => SourceTextureOptions[ChosenIdx];
    internal bool Enabled { get; set; } = true;
}
