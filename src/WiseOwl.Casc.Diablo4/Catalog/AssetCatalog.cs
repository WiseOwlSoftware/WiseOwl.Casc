using System;
using System.Collections.Generic;
using System.Linq;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// FR-C20 — the family an <see cref="AssetRef"/> belongs to. Each kind is
/// backed by one provider that knows how to enumerate its <see cref="AssetRef"/>s
/// from <c>CoreTOC</c> and decode one into its strongly-typed definition. Kinds
/// flagged as <i>render recipes</i> (the draw/composition surface) are also
/// reachable via <see cref="AssetQuery.RenderRecipesOnly"/>.
/// <br/><br/>
/// Adding a new family is one new provider + one value here — no change to the
/// <see cref="Catalog"/> facade. Sub-classification within a kind (e.g. a weapon
/// vs armor <see cref="Item"/>) rides on <see cref="AssetRef.Tags"/>, not on new
/// kinds.
/// </summary>
public enum AssetKind
{
    // ── render / composition recipes ────────────────────────────────────
    /// <summary>The paragon node render recipe (<see cref="ParagonNodeRecipe"/>);
    /// a singleton projected from the board UI scene.</summary>
    ParagonNodeRender,
    /// <summary>A UI 9-/3-slice tile recipe (<see cref="TiledStyleDefinition"/>).</summary>
    TiledStyle,
    /// <summary>A mouse-over selection-highlight recipe — a
    /// <see cref="TiledStyleDefinition"/> over a selection atlas, tagged with its
    /// <see cref="SelectionShape"/>.</summary>
    SelectionHighlight,
    /// <summary>The paragon board grid-layout metric (<see cref="ParagonBoardGrid"/>);
    /// a singleton.</summary>
    ParagonBoardGrid,
    /// <summary>A UI texture atlas (<see cref="TextureDefinition"/>).</summary>
    TextureAtlas,

    // ── paragon domain ──────────────────────────────────────────────────
    /// <summary>A paragon board definition (<see cref="ParagonBoardDefinition"/>).</summary>
    ParagonBoard,
    /// <summary>A paragon node definition (<see cref="ParagonNodeDefinition"/>).</summary>
    ParagonNode,
    /// <summary>A paragon glyph definition (<see cref="ParagonGlyphDefinition"/>).</summary>
    ParagonGlyph,
    /// <summary>A paragon glyph-affix definition (<see cref="ParagonGlyphAffixDefinition"/>).</summary>
    ParagonGlyphAffix,
    /// <summary>A power definition (<see cref="PowerDefinition"/>).</summary>
    Power,
    /// <summary>A player-class definition (<see cref="PlayerClassDefinition"/>).</summary>
    PlayerClass,
    /// <summary>The attribute-formula table (<see cref="AttributeFormulaTable"/>);
    /// a singleton.</summary>
    AttributeFormulas,

    // ── broader domain (extensible) ─────────────────────────────────────
    /// <summary>An item definition (<see cref="ItemDefinition"/>) — weapons,
    /// armor, jewelry, … distinguished by <see cref="AssetRef.Tags"/>.</summary>
    Item,
    /// <summary>An affix definition (<see cref="AffixDefinition"/>).</summary>
    Affix,
}

/// <summary>
/// FR-C20 — a lightweight, decode-free handle to one catalogued asset: its
/// kind, SNO identity, name, and classification tags. Returned by
/// <see cref="Catalog.Find"/>; pass to <see cref="Catalog.TryGet(AssetRef, out object)"/>
/// (or <see cref="Catalog.TryGet{T}(AssetRef, out T)"/>) to decode it.
/// </summary>
/// <param name="Kind">The asset family.</param>
/// <param name="Group">The SNO group the asset lives in (for singletons, the
/// source scene's group).</param>
/// <param name="Sno">The SNO id (for singletons, the source scene id).</param>
/// <param name="Name">The <c>CoreTOC</c> name (or a synthetic name for a
/// singleton).</param>
/// <param name="Tags">Authored-data-derived classification tags (e.g. a
/// selection shape, an atlas family, an item slot) for filtering. Never a guess
/// from geometry or art.</param>
public readonly record struct AssetRef(
    AssetKind Kind, SnoGroup Group, int Sno, string Name, IReadOnlyList<string> Tags);

/// <summary>
/// FR-C20 — a discovery filter for <see cref="Catalog.Find"/>. All set members
/// are combined with <c>AND</c>; an unset (null/false) member does not filter.
/// </summary>
public sealed record AssetQuery
{
    /// <summary>Restrict to a single kind.</summary>
    public AssetKind? Kind { get; init; }
    /// <summary>Restrict to any of these kinds.</summary>
    public IReadOnlySet<AssetKind>? Kinds { get; init; }
    /// <summary>Case-insensitive substring the name must contain.</summary>
    public string? NameContains { get; init; }
    /// <summary>A tag the asset must carry.</summary>
    public string? Tag { get; init; }
    /// <summary>Tags the asset must all carry.</summary>
    public IReadOnlyCollection<string>? TagsAll { get; init; }
    /// <summary>Restrict to render-recipe kinds (the draw/composition surface).</summary>
    public bool RenderRecipesOnly { get; init; }
    /// <summary>An arbitrary predicate escape hatch.</summary>
    public Func<AssetRef, bool>? Where { get; init; }
}

/// <summary>
/// FR-C20 — a provider for one <see cref="AssetKind"/>: it enumerates the kind's
/// <see cref="AssetRef"/>s from <c>CoreTOC</c> and decodes one into its
/// strongly-typed definition. Internal — the extension point for new families.
/// </summary>
internal interface IAssetProvider
{
    /// <summary>The kind this provider serves.</summary>
    AssetKind Kind { get; }
    /// <summary>Whether this kind is part of the render/composition surface.</summary>
    bool IsRenderRecipe { get; }
    /// <summary>Enumerate every asset of this kind (lazy; decode-free where it
    /// can be).</summary>
    IEnumerable<AssetRef> Enumerate();
    /// <summary>Decode the asset (uses <see cref="AssetRef.Sno"/>). Must return
    /// <see langword="false"/> rather than throw on a malformed/absent blob.</summary>
    bool TryDecode(AssetRef r, out object value);
}

/// <summary>
/// FR-C20 — the asset discovery/retrieval facade (<see cref="Diablo4Storage.Catalog"/>).
/// Lets a consumer <b>discover</b> what the game contains and <b>retrieve</b> the
/// decoded definition without hardcoding SNO ids/names or knowing which typed
/// reader to call: <c>Find</c> a filtered set of <see cref="AssetRef"/>s, then
/// <c>TryGet</c> the decoded value (pattern-match the real type, or ask for it
/// via <see cref="TryGet{T}(AssetRef, out T)"/>). The strongly-typed accessors
/// on <see cref="Diablo4Storage"/> remain as ergonomic shortcuts over the same
/// providers.
/// </summary>
public sealed class Catalog
{
    private readonly IReadOnlyList<IAssetProvider> _providers;

    internal Catalog(Diablo4Storage d4) =>
        _providers = AssetProviders.For(d4);

    /// <summary>Discover assets matching <paramref name="query"/> (lazy). A
    /// <see langword="null"/> query yields every catalogued asset.</summary>
    public IEnumerable<AssetRef> Find(AssetQuery? query = null)
    {
        foreach (var p in _providers)
        {
            if (query?.Kind is { } k && p.Kind != k) continue;
            if (query?.Kinds is { } ks && !ks.Contains(p.Kind)) continue;
            if (query is { RenderRecipesOnly: true } && !p.IsRenderRecipe) continue;

            foreach (var r in p.Enumerate())
            {
                if (query is null) { yield return r; continue; }
                if (query.NameContains is { } nc &&
                    !r.Name.Contains(nc, StringComparison.OrdinalIgnoreCase)) continue;
                if (query.Tag is { } tg && !r.Tags.Contains(tg)) continue;
                if (query.TagsAll is { } ta && !ta.All(r.Tags.Contains)) continue;
                if (query.Where is { } w && !w(r)) continue;
                yield return r;
            }
        }
    }

    /// <summary>Every asset of a kind (lazy).</summary>
    public IEnumerable<AssetRef> OfKind(AssetKind kind) =>
        Find(new AssetQuery { Kind = kind });

    /// <summary>Resolve a kind's asset by its <c>CoreTOC</c> name
    /// (case-insensitive).</summary>
    public bool TryResolve(AssetKind kind, string name, out AssetRef asset)
    {
        foreach (var r in OfKind(kind))
            if (string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                asset = r;
                return true;
            }
        asset = default;
        return false;
    }

    /// <summary>Decode an asset into its strongly-typed definition (the real
    /// type — e.g. <see cref="TiledStyleDefinition"/>, <see cref="ItemDefinition"/>).
    /// Returns <see langword="false"/> (no throw) on a malformed/absent blob or
    /// an unknown kind.</summary>
    public bool TryGet(AssetRef asset, out object value)
    {
        foreach (var p in _providers)
            if (p.Kind == asset.Kind)
                return p.TryDecode(asset, out value);
        value = null!;
        return false;
    }

    /// <summary>Decode a kind's asset by SNO id (when the ref isn't in hand).</summary>
    public bool TryGet(AssetKind kind, int sno, out object value) =>
        TryGet(new AssetRef(kind, default, sno, string.Empty, Array.Empty<string>()), out value);

    /// <summary>Decode an asset, narrowing to the expected type
    /// <typeparamref name="T"/> (the decoded definition type).</summary>
    public bool TryGet<T>(AssetRef asset, out T value)
    {
        if (TryGet(asset, out var v) && v is T t) { value = t; return true; }
        value = default!;
        return false;
    }
}
