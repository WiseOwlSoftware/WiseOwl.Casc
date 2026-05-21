# NodeFact enumeration

FR-C16 R11 — the closed vocabulary of runtime node facts a consumer computes and feeds to a layer's [`NodeActivation`](./NodeActivation.md). Each value is a single boolean condition the engine's UI controller keys a widget's visibility off. The consumer owns ONLY the computation of these facts (genuine app state — which node is selected, purchased, reachable, …); the mapping fact→layer is the engine-sourced [`NodeActivation`](./NodeActivation.md), never consumer-authored.

```csharp
public enum NodeFact
```

## Values

| name | value | description |
| --- | --- | --- |
| Always | `0` | The layer always draws (no gating fact). |
| Purchased | `1` | The node has been purchased/allocated (engine `Node_Purchased` / `ParagonNodeIsPurchased`). The base disc swaps to the purchased variant (red ring, brighter) and the purchased add-ons (cardinal arrows + connectors) draw. |
| Unpurchased | `2` | The node has not been purchased — the resting/default base disc (no red ring). The positive complement of Purchased (the engine's default state; the consumer sets this when the node is not purchased). |
| Purchasable | `3` | The node can be purchased now (engine `Node_Purchasable`). Distinct from Available — both are separate engine widgets/states. |
| Available | `4` | The node is available/reachable (engine `Node_Available` / the `NodeAvailableGlow` widget). The engine keeps this and Purchasable as separate states/widgets, so CASC surfaces both; the precise distinction is not yet known (likely reachable-vs-affordable). A consumer MAY treat the two identically until a behavioural difference is established. |
| Disabled | `5` | The node is interaction-disabled (engine `Node_Disabled` / `hImageFrameDisable`). |
| Revealed | `6` | The node has been revealed (engine `Common_Node_Revealed` / `Board_Attach_Reveal`). |
| Selected | `7` | The node is the currently selected node (engine `Node_Selected` / `IsSelected`). |
| Unselected | `8` | The node is not the selected node — the positive complement of Selected (the engine's default/"normal" state; the consumer sets this when the node is not selected). |
| Located | `9` | The node is the located/targeted ("you are here") node (engine `Node_Located`). |
| Socketed | `10` | The node is a glyph socket with a glyph socketed (engine `ui_paragon_glyphNode_socketed_ring`). |
| Persistent | `11` | The node is a persistent (always-active) legendary node (engine `ui_paragon_legendaryNode_persistent`). |
| Equipped | `12` | The node carries the equipped-item glow (engine `Node_EquipGlow` / `ItemIsEquipped`). |
| SearchMatch | `13` | The node matches the active search query (engine `Node_SearchResultHighlight`). |
| Tutorial | `14` | The node is the tutorial-highlighted node. |
| NeighbourPurchasableTop | `15` | The cardinal-north neighbour is purchasable. |
| NeighbourPurchasableRight | `16` | The cardinal-east neighbour is purchasable. |
| NeighbourPurchasableBottom | `17` | The cardinal-south neighbour is purchasable. |
| NeighbourPurchasableLeft | `18` | The cardinal-west neighbour is purchasable. |
| NeighbourPurchasedTop | `19` | The cardinal-north neighbour is already purchased (connector target — distinct from a purchasable neighbour, which an arrow points to). |
| NeighbourPurchasedRight | `20` | The cardinal-east neighbour is already purchased. |
| NeighbourPurchasedBottom | `21` | The cardinal-south neighbour is already purchased. |
| NeighbourPurchasedLeft | `22` | The cardinal-west neighbour is already purchased. |
| KindCommon | `23` | Node kind Common (the grey base disc; engine `eRarity` 0). |
| KindMagic | `24` | Node kind Magic (`eRarity` 2). |
| KindRare | `25` | Node kind Rare (`eRarity` 3). |
| KindLegendary | `26` | Node kind Legendary (`eRarity` 4). |
| KindSocket | `27` | Node kind glyph-socket (`Template_Node_Socketable`). |
| KindGate | `28` | Node kind board exit/gate (`Template_Node_Quest`). |
| KindStart | `29` | Node kind board start/entry (`Template_Node_Starter`). |
| Locked | `30` | The node is locked (not yet reachable) — engine texture state `ParagonNode_Texture_Locked`. |
| Unlocked | `31` | The node is unlocked (reachable but not purchased) — engine state `ParagonNode_Legendary_Unlocked`. |
| Pressed | `32` | Engine widget interaction state: pressed (`hImageFramePressed`). |
| MouseOver | `33` | Engine widget interaction state: cursor over (`hImageFrameMouseOver`). |
| Never | `34` | The layer never draws under any computed fact (an authored-inactive widget with no recovered predicate). |

## Remarks

Provenance (see [`NodeActivationSource`](./NodeActivationSource.md)): the paragon UI scene does not store an activation expression per widget — there is no condition/visibility/predicate field, no binding expression in the value records, and no condition-SNO reference (FR-C16 R10, scene 657304, exhaustively verified). The engine binds a widget's `bActive` to a runtime state by the widget's name in its C++ UI controller; the data-side representation of the association is the naming convention (per-state field suffixes such as `hImageFramePressed`/`MouseOver`/`Disable`, and per-state widget/asset names such as `Node_Purchased`/`Node_Purchasable`/ `Template_Node_Magic`). CASC decodes that convention into the typed activations below so the consumer evaluates rather than invents. EXE-validated (FR-C16 R12). This vocabulary is corroborated by the named data-source / predicate symbols in `Diablo IV.exe` — the engine uses a `DataBinding`/`SetObjectBinding` system whose boolean sources include `ParagonNodeIsPurchased` (= Purchased), `IsSelected` (= Selected), `IsLocked`/`ParagonNode_Texture_Locked` (= Locked), `IsEquipped` (= Equipped), and `ParagonGlyphAffixIsActive`. The per-widget wiring lives in the `ParagonBoardUI` controller (compiled code, not a SNO field); the source names are EXE-recoverable, the wiring needs disassembly.

## See Also

* namespace [WiseOwl.Casc.Diablo4](../WiseOwl.Casc.Diablo4.md)
* [NodeFact.cs](https://github.com/WiseOwlSoftware/WiseOwl.Casc/blob/main/NodeFact.cs)

<!-- DO NOT EDIT: generated by xmldocmd for WiseOwl.Casc.Diablo4.dll -->
