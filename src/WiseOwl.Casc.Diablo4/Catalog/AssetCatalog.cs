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
/// <param name="Sno">The SNO id (for singletons, the source scene id).
/// <b>Identity/stability (FR-C20 Q4):</b> the <c>(Group, Sno)</c> pair is the
/// canonical key and is stable <i>within a build</i> — ids are re-issued per
/// build, so persisted bakes should diff against the install's
/// <c>.build.info</c>. For the most patch-durable key across game updates,
/// prefer <see cref="Name"/> (re-resolve to an id via
/// <see cref="Catalog.TryResolve"/> on a new build).</param>
/// <param name="Name">The <c>CoreTOC</c> name (or a synthetic name for a
/// singleton). The most patch-durable identity (see <paramref name="Sno"/>).</param>
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
    /// <summary>FR-C20 Q2 — yield only assets that actually decode, dropping
    /// malformed/sentinel blobs (e.g. the "Bad Data" board). <b>Cost:</b> a
    /// decode per asset — for typed retrieval prefer <see cref="Catalog.Find{T}"/>,
    /// which is decodable-only by construction.</summary>
    public bool DecodableOnly { get; init; }
    /// <summary>FR-C20 Q2 — order results by (<see cref="AssetRef.Kind"/>, then
    /// ordinal <see cref="AssetRef.Name"/>). Buffers the results, so it is not
    /// lazy.</summary>
    public bool OrderByName { get; init; }
    /// <summary>An arbitrary predicate escape hatch.</summary>
    public Func<AssetRef, bool>? Where { get; init; }
}

/// <summary>
/// FR-C20 P2 — cheap, decode-free facets of an asset, for filtering large kinds
/// without fully decoding each. Fields are nullable: only those a kind can
/// supply without a full decode are populated (today: texture-atlas dimensions /
/// frame count / codec). Item/power/glyph categorical facets ride on
/// <see cref="AssetRef.Tags"/> as they are derived.
/// </summary>
/// <param name="Width">Atlas pixel width, if applicable.</param>
/// <param name="Height">Atlas pixel height, if applicable.</param>
/// <param name="FrameCount">Number of atlas frames, if applicable.</param>
/// <param name="Codec">Texture codec, if applicable.</param>
public readonly record struct AssetFacets(
    int? Width, int? Height, int? FrameCount, TextureCodec? Codec);

/// <summary>FR-C20 P2b — where a categorical <see cref="Facet"/>'s value came
/// from, so the consumer knows how much to trust it (mirrors
/// <c>NodeActivationSource</c>). Discovery/filtering may use any source;
/// authored rendering must not lean on <see cref="NameConvention"/>.</summary>
public enum FacetSource
{
    /// <summary>Parsed from the SNO's authored name convention (a convenience;
    /// not blob-verified).</summary>
    NameConvention,
    /// <summary>Read from the decoded definition (blob-verified).</summary>
    Decoded,
    /// <summary>Read from an authored scene/binding field.</summary>
    SceneField,
}

/// <summary>FR-C20 P2b — one categorical facet of an asset (e.g.
/// <c>class=Barbarian</c>), with its <see cref="Source"/> provenance. Use for
/// discovery/filtering (see <see cref="Catalog.Facets"/> /
/// <see cref="Catalog.FindByFacet"/>); an asset may carry several of the same
/// <see cref="Key"/> (e.g. a glyph usable by multiple classes).</summary>
/// <param name="Key">The facet name (e.g. <c>class</c>, <c>rarity</c>, <c>type</c>, <c>codec</c>).</param>
/// <param name="Value">The facet value (e.g. <c>Barbarian</c>).</param>
/// <param name="Source">Where the value came from.</param>
public readonly record struct Facet(string Key, string Value, FacetSource Source);

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
    private readonly Diablo4Storage _d4;
    private readonly IReadOnlyList<IAssetProvider> _providers;

    internal Catalog(Diablo4Storage d4)
    {
        _d4 = d4;
        _providers = AssetProviders.For(d4);
    }

    /// <summary>Discover assets matching <paramref name="query"/> (lazy, unless
    /// <see cref="AssetQuery.OrderByName"/> buffers to sort). A
    /// <see langword="null"/> query yields every catalogued asset.</summary>
    public IEnumerable<AssetRef> Find(AssetQuery? query = null) =>
        query is { OrderByName: true }
            ? FindCore(query)
                .OrderBy(r => r.Kind)
                .ThenBy(r => r.Name, StringComparer.Ordinal)
            : FindCore(query);

    private IEnumerable<AssetRef> FindCore(AssetQuery? query)
    {
        foreach (var p in _providers)
        {
            if (query?.Kind is { } k && p.Kind != k) continue;
            if (query?.Kinds is { } ks && !ks.Contains(p.Kind)) continue;
            if (query is { RenderRecipesOnly: true } && !p.IsRenderRecipe) continue;

            foreach (var r in p.Enumerate())
            {
                if (query is not null)
                {
                    if (query.NameContains is { } nc &&
                        !r.Name.Contains(nc, StringComparison.OrdinalIgnoreCase)) continue;
                    if (query.Tag is { } tg && !r.Tags.Contains(tg)) continue;
                    if (query.TagsAll is { } ta && !ta.All(r.Tags.Contains)) continue;
                    if (query.Where is { } w && !w(r)) continue;
                    if (query.DecodableOnly && !TryGet(r, out _)) continue;
                }
                yield return r;
            }
        }
    }

    /// <summary>Every asset of a kind (lazy).</summary>
    public IEnumerable<AssetRef> OfKind(AssetKind kind) =>
        Find(new AssetQuery { Kind = kind });

    /// <summary>FR-C20 P4 — discover <b>and decode</b> in one lazy pass:
    /// enumerate <paramref name="query"/> and yield each asset already decoded
    /// to <typeparamref name="T"/>, silently skipping non-matching kinds and
    /// undecodable blobs. The ergonomic "give me every <see cref="TiledStyleDefinition"/>"
    /// shortcut.</summary>
    public IEnumerable<T> Find<T>(AssetQuery? query = null)
    {
        foreach (var r in Find(query))
            if (TryGet<T>(r, out var v))
                yield return v;
    }

    /// <summary>FR-C20 P1 — reverse-lookup: resolve a raw texture
    /// <paramref name="handle"/> to the <see cref="AssetKind.TextureAtlas"/> that
    /// contains it and the handle's <paramref name="frameIndex"/> within that
    /// atlas (use <c>TextureDefinition.Frames[frameIndex]</c> for its UVs/rect).
    /// Returns <see langword="false"/> for a handle that resolves to no atlas
    /// frame (e.g. an engine-internal/non-texture handle).</summary>
    public bool TryResolveHandle(uint handle, out AssetRef atlas, out int frameIndex)
    {
        if (handle is not 0u and not 0xFFFFFFFFu &&
            _d4.TryGetIconFrame(handle, out var atlasSno, out _) &&
            _d4.TextureMeta.TryGet(atlasSno, out var td))
        {
            frameIndex = -1;
            for (int i = 0; i < td.Frames.Count; i++)
                if (td.Frames[i].ImageHandle == handle) { frameIndex = i; break; }
            atlas = AssetProviders.AtlasRef(_d4, atlasSno);
            return true;
        }
        atlas = default;
        frameIndex = -1;
        return false;
    }

    /// <summary>FR-C20 P1 — like <see cref="TryResolveHandle"/> but also yields
    /// the handle's <see cref="TexFrame"/> (UV rect) directly, saving the
    /// follow-up <c>TryGet(atlas)</c> + <c>Frames[i]</c> step. Use
    /// <c>frame.PixelRect(facets.Width, facets.Height)</c> (facets via
    /// <see cref="TryPeek"/>) for the pixel rect.</summary>
    public bool TryResolveFrame(uint handle, out AssetRef atlas, out TexFrame frame)
    {
        if (handle is not 0u and not 0xFFFFFFFFu &&
            _d4.TryGetIconFrame(handle, out var atlasSno, out frame))
        {
            atlas = AssetProviders.AtlasRef(_d4, atlasSno);
            return true;
        }
        atlas = default;
        frame = default;
        return false;
    }

    /// <summary>FR-C20 P2 — decode-free metadata peek: cheap facets for filtering
    /// big kinds without a full <see cref="TryGet(AssetRef, out object)"/>.
    /// Populated for <see cref="AssetKind.TextureAtlas"/> (dimensions, frame
    /// count, codec) from the preloaded combined-meta. Returns
    /// <see langword="false"/> when no cheap facet is available for the kind.</summary>
    public bool TryPeek(AssetRef asset, out AssetFacets facets)
    {
        if (asset.Kind == AssetKind.TextureAtlas &&
            _d4.TextureMeta.TryGet(asset.Sno, out var td))
        {
            facets = new AssetFacets(td.Width, td.Height, td.Frames.Count, td.Codec);
            return true;
        }
        facets = default;
        return false;
    }

    /// <summary>FR-C20 P2b — the asset's categorical <see cref="Facet"/>s with
    /// provenance, for discovery/filtering. Populated today:
    /// <list type="bullet">
    /// <item><see cref="AssetKind.ParagonGlyph"/> → <c>class</c> (one per usable
    /// class), <see cref="FacetSource.Decoded"/> from
    /// <c>ParagonGlyphDefinition.UsableByClassSnoIds</c>.</item>
    /// <item><see cref="AssetKind.TextureAtlas"/> → <c>codec</c>,
    /// <see cref="FacetSource.Decoded"/> (decode-free meta).</item>
    /// </list>
    /// Item type/rarity/class (<see cref="FacetSource.NameConvention"/>) and
    /// power class are not yet surfaced — no cheap authored source for power
    /// class (neither <c>PowerDefinition</c> nor <c>PlayerClass</c> carries the
    /// linkage). Glyph facets decode the glyph (cheap; 562 in the group).</summary>
    public IReadOnlyList<Facet> Facets(AssetRef asset)
    {
        var list = new List<Facet>();
        switch (asset.Kind)
        {
            case AssetKind.ParagonGlyph when TryGet<ParagonGlyphDefinition>(asset, out var g):
                foreach (var classSno in g.UsableByClassSnoIds)
                    if (_d4.CoreToc.GetName(SnoGroup.PlayerClass, classSno) is { Length: > 0 } cn)
                        list.Add(new Facet("class", cn, FacetSource.Decoded));
                break;
            case AssetKind.TextureAtlas when TryPeek(asset, out var f) && f.Codec is { } codec:
                list.Add(new Facet("codec", codec.ToString().ToLowerInvariant(), FacetSource.Decoded));
                break;
        }
        return list;
    }

    /// <summary>FR-C20 P2b — discover assets of a kind carrying a categorical
    /// facet (e.g. every <see cref="AssetKind.ParagonGlyph"/> with
    /// <c>class=Barbarian</c>). Computes <see cref="Facets"/> per asset (decodes
    /// where the facet requires it), so scope with <paramref name="kind"/>.</summary>
    public IEnumerable<AssetRef> FindByFacet(AssetKind kind, string key, string value) =>
        OfKind(kind).Where(r => Facets(r).Any(f =>
            f.Key.Equals(key, StringComparison.OrdinalIgnoreCase) &&
            f.Value.Equals(value, StringComparison.OrdinalIgnoreCase)));

    /// <summary>FR-C20 P3 — decode a whole <see cref="AssetKind.TextureAtlas"/>
    /// mip0 to RGBA pixels (the atlas-browser path: discover → peek → retrieve).
    /// Returns <see langword="false"/> (no throw) for a non-atlas ref, an
    /// unsupported codec (only BC1/BC3 decode today — check
    /// <see cref="AssetFacets.Codec"/> via <see cref="TryPeek"/>), or an absent
    /// payload.</summary>
    public bool TryGetAtlasImage(AssetRef atlas, out DecodedImage image)
    {
        if (atlas.Kind == AssetKind.TextureAtlas)
            return TryDecodeAtlas(atlas.Sno, out image, out _);
        image = default;
        return false;
    }

    /// <summary>FR-C20 P3 — decode the single frame a texture <paramref name="handle"/>
    /// names, cropped from its owning atlas mip0. Pairs with
    /// <see cref="TryResolveHandle"/> for "what does this handle look like?".
    /// Returns <see langword="false"/> for an unresolved/sentinel handle, an
    /// unsupported codec, or an absent payload. (Decodes the owning atlas per
    /// call — to slice many frames of one atlas, call
    /// <see cref="TryGetAtlasImage"/> once and crop with
    /// <c>TextureDefinition.Frames[i].PixelRect</c>.)</summary>
    public bool TryGetFrameImage(uint handle, out DecodedImage image)
    {
        if (TryResolveHandle(handle, out var atlas, out var frameIndex) && frameIndex >= 0 &&
            TryDecodeAtlas(atlas.Sno, out var full, out var td))
        {
            var (x, y, w, h) = td!.Frames[frameIndex].PixelRect(td.Width, td.Height);
            image = new DecodedImage(w, h, full.Crop(x, y, w, h));
            return true;
        }
        image = default;
        return false;
    }

    private bool TryDecodeAtlas(int sno, out DecodedImage image, out TextureDefinition? td)
    {
        td = null;
        try
        {
            if (_d4.TextureMeta.TryGet(sno, out var meta) &&
                _d4.TryReadSno(SnoGroup.Texture, sno, SnoFolder.Payload, out var payload))
            {
                td = meta;
                image = meta.DecodeMip0(payload);
                return true;
            }
        }
        catch (Exception ex) when (ex is NotSupportedException or CascException or FormatException
            or ArgumentException or System.IO.IOException or IndexOutOfRangeException or OverflowException)
        {
            // unsupported codec / malformed / absent payload → not decodable.
        }
        image = default;
        return false;
    }

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
