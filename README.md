# DISCO

Full name: **D**ynamic **I**tem **S**prite **C**omp**O**sitor

Mod that allow content packs to give items and big craftables dynamic sprites.
This mod is sort of an (E)BAGI successor with more features such as the ability to work on any flavored items.

See [author guide](docs/author-guide.md) for how to make a content pack.

## Configuration

For each content pack you have, you can enable/disable the textures as desired from DISCO's configuration page. Some content packs may offer a variety of options for each entry which can also be set here.

There is also a configuration for displaying the mini-icon representing the preserve item, which comes in 3 modes:
* Hidden: The mini-icon is never shown
* Pack Defined: The mini-icon is shown only if a content pack defines how to show the mini-icon.
* Always: The mini-icon is shown for all flavored items, even if they don't have any DISCO rules.

## Multiplayer

It's ok to not match the DISCO packs exactly.

In particular you can...
* have DISCO installed and play with someone without DISCO
* have completely different DISCO packs
* have the same DISCO packs, but choose different settings

## Using DISCO with (E)BAGI Mods

Aside from content packs made directly for DISCO, you can also have (E)BAGI packs load via DISCO by editing their manifest.json slightly:

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

Doing this has the following benefits:
* You can mix and match DISCO/EBAGI packs this way without compatibility concerns.
* You can use DISCO's config menu to enable/disable packs easily.
* You get to use DISCO's mini-icons, which works on all items.

## Installation

1. Install SMAPI
2. Download and extract this mod to the Mods folder
