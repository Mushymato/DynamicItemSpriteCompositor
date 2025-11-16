# Author Guide

This document describes how to make a content pack for DISCO.

## Dependencies

Although DISCO packs are simply content patcher mods, they MUST have DISCO as a required dependency.

The manifest should have:
```js
{
  ...
  "ContentPackFor": {
    "UniqueID": "Pathoschild.ContentPatcher"
  },
  "Dependencies": [
    {
      "UniqueID": "mushymato.DISCO",
      "IsRequired": true
    }
  ]
}
```

This is used by DISCO to initialize the base asset instance for the content pack to edit.

## Model

To add sprites, edit `mushymato.DISCO/Data/{{ModId}}` where `{{ModId}}` is the content patcher mod id token.

This asset is a dictionary of *ItemSpriteRuleAtlas* objects.

Since the asset name is already your mod id, there's no real need to make the keys unique.

### ItemSpriteRuleAtlas

| Field | Type | Default | Notes |
| ----- | ---- | ------- | ----- |
| `TypeIdentifier` | string | `"(O)"` | This is the type identifier part of a qualified item id (e.g. `(O)0`). Currently, only `(O)` and `(BC)` are supported. |
| `LocalItemId` | string | `"0"` | This is the item id part of a qualified item id (e.g. `(O)0`). |
| `SourceTexture` | string | _null_ | Source texture asset, where your sprites are on. This must be loaded. |
| `Rules` | List<*SpriteIndexRule*> | List of rules used to pick dynamic sprite index. |

### SpriteIndexRule

| Field | Type | Default | Notes |
| ----- | ---- | ------- | ----- |
| `RequiredContextTags` | List<string> | _null_ | List of context tags needed, item must have every tag in the list. Inverse tags that start with `!` is also allowed. You can use comma separated list of context tags. |
| `RequiredColor` | Color | _null_ | This is used for flavored items (ColoredObject) which have a color. You can pass colors in as hex (`#123123`), RGBA (`30 60 90`). For non flavored items, use the color tags in `RequiredContextTags` instead. |
| `RequiredCondition` | string ([Game State Query](https://stardewvalleywiki.com/Modding:Game_state_queries)) | _null_ | Game state query that the item must fulfill. Item is passed in as the `Target` item. |
| `SpriteIndexList` | List<int> | _empty_ | List of sprite indicies on your source texture that will be assigned when this rule matches. You can use comma separated list of numbers, or just a single number. |
| `IncludeDefaultSpriteIndex` | bool | false | If true, include the default sprite index in the list when picking. |

## How does it pick index?

DISCO does not patch draw logic and instead operate directly on the data.

When an item is created, DISCO will combine the *ItemSpriteRuleAtlas* entries provided by every content pack and find the ones that apply to the given item.
Then, DISCO creates a special texture that combines the original sprite plus every content pack sprite based on the *ItemSpriteRuleAtlas* entries, and calculates a sprite index offset for every sprite involved.
This special texture becomes the item's new texture (shared across all instances of this item), and the final sprite index is picked based on the *SpriteIndexRule* list given.

When an item pass all requirements on a *SpriteIndexRule*, it will get a random sprite index from the `SpriteIndexList`.
When multiple *SpriteIndexRule* across the *ItemSpriteRuleAtlas* provided by the same content pack pass all requirements, their `SpriteIndexList` are combined and a random sprite index is picked from there.
When multiple *SpriteIndexRule* across the *ItemSpriteRuleAtlas* provided by the different content pack pass all requirements, the content pack that comes later in the dependency tree gets to apply their sprites.

The sprite indexes are rechecked in 2 situations:
- New day started
- Relevant assets invalidated
