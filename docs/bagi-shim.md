# Better Artisian Goods Shim

> [!WARNING]
> If you are **creating a new mod**, please follow instructions on [author guide](./author-guide.md) instead!

DISCO has the ability to utilize content packs created for (Even) Better Artisian Goods Icons.

Packs loaded this way will:
1. Apply rules using `"RequiredContextTag": "preserve_sheet_index_<itemid>"` rule to match how (E)BAGI behaves
2. Have the preserve sub icon enabled by default with scale 0.5 and offset 0,0

To do this, you need to edit the manifest such that the content pack is for `mushymato.DISCO` instead of `cat.betterartisangoodicons` (BAGI) or `haze1nuts.evenbetterartisangoodicons` (EBAGI).

You can use DISCO and EBAGI together if you wish and EBAGI packs should take priority, but having DISCO load packs means you can enable/disable the textures with DISCO's configuration menu and won't get duplicate subicons.

Example manifest.json from [Fancy Artisan Goods EBAGI](https://www.nexusmods.com/stardewvalley/mods/23373)

```json
{
  // No change needed:
  "Name": "Fancy Artisan Goods Icons fix for EBAGI",
  "Author": "Aimon111",
  "Version": "2.0.0",
  "Description": "Fix for Better Artisan Goods icons mod (jelly, honey and pickles)",
  "UniqueID": "Aimon111.FancyArtisanGoodsIconsEBAGI",
  "UpdateKeys": [
    "Nexus:23373"
  ],
  "MinimumApiVersion": "4.0.0",

  // Part to change:
  "ContentPackFor": {
    "UniqueID": "mushymato.DISCO"
  },
}
```

## Converting Packs

If you wish to tweak more settings, you should convert it to Content Patcher/DISCO format first.

An easy way to do so is to `patch export mushymato.DISCO/Data/<BAGI pack mod ID>`, which will give you the generated asset. This is essentially what you'd need to put in the EditData of a DISCO pack. Remember to load the textures pngs to the targets specified in `SourceTextures` as well.
