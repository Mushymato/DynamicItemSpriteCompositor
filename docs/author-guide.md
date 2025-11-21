# Author Guide

This document describes how to make a content pack for DISCO.

## Dependencies

Although DISCO content packs are simply content patcher mods, they must DISCO as a direct dependency, i.e. put `{ "UniqueID": "mushymato.DISCO" }` in the `"Dependencies"` section.

Conversely, avoid putting DISCO as a dependency unless you intend to use it directly in the mod.

This is used by DISCO to initialize the base asset instance for the content pack to edit. If a content pack does not do this, then DISCO ignores it completely.

## Model

To add sprites, edit `mushymato.DISCO/Data/{{ModId}}` where `{{ModId}}` is the content patcher mod id token.

This asset is a dictionary of **ItemSpriteRuleAtlas** objects.

Since the asset name is already your mod id, there's no real need to make the keys unique.

### ItemSpriteRuleAtlas

| Field | Type | Default | Notes |
| ----- | ---- | ------- | ----- |
| `TypeIdentifier` | string | `"(O)"` | This is the type identifier part of a qualified item id (e.g. `(O)0`). Currently, only `(O)` and `(BC)` are supported. |
| `LocalItemId` | string | `"0"` | This is the item id part of a qualified item id (e.g. `(O)0`). |
| `SourceTextures` | List<string> | _null_ | Source texture asset, where your sprites are on. You can give multiple source texture options here, and DISCO will allow players to choose which option they want to use via in-game configuration menu. Comma separated strings are accepted, which also mean you can put content patcher tokens here too. |
| `SourceSpritePerIndex` | uint | _null_ | This is used to set your expected sprite per index for the given source texture, must be 1 or greater. See [this section](#sprite-per-index) for more details. |
| `Rules` | List<**SpriteIndexRule**> | List of rules used to pick dynamic sprite index. |

#### SpriteIndexReqs

| Field | Type | Default | Notes |
| ----- | ---- | ------- | ----- |
| `RequiredContextTags` | List<string> | _null_ | List of context tags needed, item must have every tag in the list. Inverse tags that start with `!` is also allowed. You can use comma separated list of context tags. |
| `RequiredColor` | Color | _null_ | This is used for colored objects such as wine/jelly/pickle which have a color. You can pass colors in as hex (`#123123`), RGBA (`30 60 90`), named monogame colors, and context tags listed on [this page](./colortags.md). Not all flavored items are colored items, for example flavored honey is not colored in vanilla and needs to be checked by `RequiredContextTags` on `Preserve` instead. |
| `RequiredCondition` | string ([Game State Query](https://stardewvalleywiki.com/Modding:Game_state_queries)) | _null_ | Game state query that the item must fulfill. Item is passed in as the `Target` item. |

### SpriteIndexRule

| Field | Type | Default | Notes |
| ----- | ---- | ------- | ----- |
| All fields of **SpriteIndexReqs** | ... | ... | **SpriteIndexRule** inherits all fields of **SpriteIndexReqs**. |
| `HeldObject` | **SpriteIndexReqs** | _null_ | This is an extra set of requirements to check on the held object, most commonly machine outputs. |
| `Preserve` | **SpriteIndexReqs** | _null_ | This is an extra set of requirements to apply on the preserve item that grants the flavor. Most flavored items are also colored objects so simply checking `RequiredColor` can be enough, but this covers edge cases such as honey. If a rule has both `HeldObject` and `Preserve`, the preserve item being checked will be the held object's preserve item instead. |
| `SpriteIndexList` | List<int> | _empty_ | List of sprite indicies on your source texture that will be assigned when this rule matches. You can use comma separated list of numbers, or just a single number. |
| `IncludeDefaultSpriteIndex` | bool | false | If true, include the default sprite index in the list when picking. |
| `Precedence` | int | *varies* | This adjusts whether this rule applies before other rules, lower is more prioritized. The default value depends on whether you have set other requirements. |

## How does it pick index?

DISCO does not patch draw logic and instead operate directly on the data.

When an item is created, DISCO will combine the **ItemSpriteRuleAtlas** entries provided by every content pack and find the ones that apply to the given item.
Then, DISCO creates a special composite texture that combines the original sprite plus every content pack sprite based on the **ItemSpriteRuleAtlas** entries, and calculates a sprite index offset for every sprite involved.
This special composite texture becomes the item's new texture (shared across all instances of this item), and the final sprite index is picked based on the **SpriteIndexRule** list given.

There is a limitation here. If a particular place in question doesn't use `ParsedItemData` to get texture, then the changed texture won't propagate over. This mainly impacts mods that don't use `Item.draw` or `Item.drawInMenu` to display items.

When an item pass all requirements on a single **SpriteIndexRule**, it will get a random sprite index from the `SpriteIndexList`.
When an item pass all requirements on multiple **SpriteIndexRule** entries, only the **SpriteIndexRule** with lowest `Precedence` out of the matching rules are considered. The sprite index is picked with equal chance from all matching rules combined.

The sprite indexes are force rechecked in 2 situations:
- New day started
- Relevant assets invalidated

To debug any unexpected behavior, use console command `disco-export` to save the combined **ItemSpriteRuleAtlas** data and the special composite texture to DISCO's mod folder.

## Sprite Per Index

Sometimes, an item may have more than 1 sprite per index. A common example is colored objects, which can have 2 sprites per index (base and color mask). 

In this case, your sprite sheet should match this format and also have 2 sprites, a base and a color mask, then use the index of the first sprite in the `SpriteIndexList`.
If you don't care about the color mask sprite, then instead you can give a `SourceSpritePerIndex` value of 1, which will make DISCO automatically put in a transparent second sprite for you.
When you are using `SourceSpritePerIndex`, you cannot have 2 index whose difference is less than `SourceSpritePerIndex`. Intuitively, this is checking for no overlapping among the sprites given.

Regardless of whether you are using `SourceSpritePerIndex` or not, the sprite indicies you give in `SpriteIndexList` should always be relative to the sprite's position on the source texture asset.

DISCO tries to determine most common cases of sprite per index. Most hardcoded cases are added to `Data/Objects`/`Data/BigCraftables` as the custom field `"mushymato.DISCO/SpritePerIndex"`. You may edit this field yourself as needed, but for vanilla objects it's best to make a report.

