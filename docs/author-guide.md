# Author Guide

This document describes how to make a content pack for DISCO (Dynamic Item Sprite Compositor).

## Dependencies

Although DISCO content packs are simply content patcher mods, they must DISCO as a direct dependency, i.e. put `{ "UniqueID": "mushymato.DISCO" }` in the `"Dependencies"` section.

Conversely, avoid putting DISCO as a dependency unless you intend to use it directly in the mod.

This is used by DISCO to initialize the base data asset instance for the content pack to edit. If a content pack does not do this, then DISCO ignores it completely.

## Model

To add sprites, edit `mushymato.DISCO/Data/{{ModId}}` where `{{ModId}}` is the content patcher mod id token.

This asset is a dictionary of **ItemSpriteRuleAtlas** objects.

Since the asset name is already your mod id, there's no real need to make the keys unique.

### ItemSpriteRuleAtlas

| Field | Type | Default | Notes |
| ----- | ---- | ------- | ----- |
| `TypeIdentifier` | string | `"(O)"` | This is the type identifier part of a qualified item id (e.g. `(O)0`). Currently, only `(O)` and `(BC)` are supported. |
| `LocalItemId` | string | `"0"` | This is the item id part of a qualified item id (e.g. `(O)0`). |
| `ConfigName` | string | _null_ | A user friendly name for this entry to display on the config menu. Defaults to the base item name when not given. |
| `ConfigIconSpriteIndex` | string | _null_ | The primary sprite to display in the configuration menu, When not given, the lowest index defined across the rules for this atlas will be used. |
| `ConfigSubIconItemId` | string | _null_ | When `SubIconScale` is set, this will be the item used for previewing in the config menu. |
| `SourceTextures` | List<string> | _null_ | Source textures where your sprites are on. You can give multiple source texture options here, and DISCO will allow players to choose which option they want to use via in-game configuration menu. Comma separated strings are accepted, which also mean you can put content patcher tokens here too. |
| `SourceSpritePerIndex` | uint | _null_ | This is used to set your expected sprite per index for the given source texture, must be 1 or greater. See [this section](#sprite-per-index) for more details. |
| `SubIconScale` | float | 0 | A draw scale for a mini icon on top of the main item icon. When this is greater than zero, an icon of the item used to flavor this item is drawn on top. |
| `SubIconOffset` | Vector2 | 0,0 | A draw offset for the sub icon. This is relative to the top left pixel of the icon. |
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
| `Precedence` | int | *varies* | This adjusts whether this rule applies before other rules, lower is more prioritized. The default value depends on the requirements you have set. |

## How does it pick index?

Whenever an item is created and attempted to be drawn for the first time, DISCO will look at whether it has any DISCO data and whether it matches any rules:

When an item pass all requirements on a single **SpriteIndexRule**, it will get a random sprite index from the `SpriteIndexList`.
When an item pass all requirements on multiple **SpriteIndexRule** entries, only the **SpriteIndexRule** with lowest `Precedence` out of the matching rules are considered.
In case where there are multiple matching **SpriteIndexRule** entries with equal and lowest `Precedence`, one random rule is picked, followed by random sprite index from that rule.

The sprite indexes are force rechecked in 2 situations:
- Save loaded (all items in the world will be rechecked)
- Relevant assets invalidated (relevant watched items will be rechecked)

To debug any unexpected behavior, use console command `disco-export` to save the combined **ItemSpriteRuleAtlas** data and relevant textures to DISCO's mod folder.

### Default Precedences

The default precedence is decided like this:
- If `RequiredContextTags` is set: 1000
- Else if `RequiredColor` is set: 2000
- Else if `RequiredCondition` is set: 3000
- Else (no conditions at all): 9000

When using `Preserve`, a precedence bonus is added:
    - If `RequiredContextTags` is set: 100
    - Else if `RequiredColor` is set: 200
    - Else if `RequiredCondition` is set: 300
    - Else no condition: 900

When using `HeldObject`, a precedence bonus is added:
    - If `RequiredContextTags` is set: 10
    - Else if `RequiredColor` is set: 20
    - Else if `RequiredCondition` is set: 30
    - Else no condition: 90

If a precedence is set by content pack, then none of these apply and the content pack given value is used as is.
Given the formula, the default precedence will always be positive. Thus content packs should use negative value if manually setting the precedence.

## Sprite Per Index

Sometimes, an item may have more than 1 sprite per index.
A common example is colored objects, which can have 2 sprites per index (base and color mask). 
In this case, your sprite sheet should match this format and also have 2 sprites, a base and a color mask, then use the index of the first sprite in the `SpriteIndexList`.

If you don't care about the color mask sprite, then instead you can give a `SourceSpritePerIndex` value of 1, which will make DISCO automatically generate a new sprite where the 2nd sprite is transparent. You can see this generated texture by using `disco-export`. There is a minor performance cost to doing this recomposing at runtime, so feel free to take the exported PNG file and use it directly in your content pack.

When you are using `SourceSpritePerIndex`, you cannot have 2 index whose difference is less than `SourceSpritePerIndex`. Intuitively, this is checking for no overlapping among the sprites given.

Regardless of whether you are using `SourceSpritePerIndex` or not, the sprite indicies you give in `SpriteIndexList` should always be relative to the source texture asset.

DISCO tries to determine most common cases of sprite per index. Most hardcoded cases are added to `Data/Objects`/`Data/BigCraftables` as the custom field `"mushymato.DISCO/SpritePerIndex"`. You may edit this field yourself as needed, but for vanilla objects it's best to make a bug report so that it can be added for everyone.
