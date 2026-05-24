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

/// <summary>FR-C20 P5 — one authored relationship edge from an asset to another
/// (e.g. a board to one of its nodes, a node to its passive power, a glyph to an
/// affix it grants). The <see cref="Target"/> is a full <see cref="AssetRef"/>,
/// so traversal chains: <see cref="Catalog.Related"/> the target again. Only
/// <b>authored</b> FK edges are surfaced — runtime relationships (e.g. which
/// glyph a player has socketed into a node) are <i>not</i> links.</summary>
/// <param name="Role">The edge label (e.g. <c>node</c>, <c>power</c>,
/// <c>affix</c>, <c>class</c>).</param>
/// <param name="Target">The related asset.</param>
public readonly record struct AssetLink(string Role, AssetRef Target);

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

    // FR-C21 caches — node defs and resolved infos are reused ~25× per
    // board and many boards repeat across a search. Decoded once on
    // first access, kept for the storage's lifetime. ConcurrentDictionary
    // tolerates the optimizer's parallel queries.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, ParagonNodeDefinition?>
        _nodeDefCache = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, ParagonNodeInfo?>
        _nodeInfoCache = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, ParagonBoardDefinition?>
        _boardDefCache = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, IReadOnlyList<(ParagonGridCell, ParagonNodeInfo)>?>
        _boardNodesCache = new();
    private AttributeFormulaTable? _attributeFormulas;
    private readonly object _formulasLock = new();
    // Player-class SnoName roster, cached on first use — drives the
    // Power → class facet's name-convention dispatch.
    private string[]? _playerClassSnoNames;
    private readonly object _playerClassesLock = new();
    // FR-C23 — the engine-authored tooltip-chrome inventory, cached
    // for the catalog's lifetime (CoreToc lookups only — no decode).
    private ParagonTooltipChrome? _paragonTooltipChrome;
    private readonly object _paragonTooltipChromeLock = new();

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
    /// <item><see cref="AssetKind.Power"/> → <c>class</c>,
    /// <see cref="FacetSource.NameConvention"/> from the
    /// <c>&lt;ClassSnoName&gt;_&lt;SkillName&gt;</c> first-party convention
    /// the engine uses for class-skill powers (CL-72). Honest partial
    /// surface: ~<c>1 700</c> of the ~<c>2 500</c> group-29 SNOs match
    /// the prefix (every active class-skill power on the live build);
    /// the rest (monster powers, generic <c>1HAxe_Unique_*</c> /
    /// <c>1HFocus_Unique_*</c> item-affix powers, unnamed
    /// debug stubs) carry no class facet and are surfaced unfaceted.</item>
    /// <item><see cref="AssetKind.Item"/> →
    /// <c>type</c>/<c>rarity</c>/<c>class</c>,
    /// <see cref="FacetSource.NameConvention"/> from the engine's
    /// first-party item-naming convention (CL-73). Three patterns:
    /// weapons/armor (<c>&lt;Type&gt;_&lt;Rarity&gt;_&lt;Class&gt;_&lt;NN&gt;</c>),
    /// cosmetics (<c>Cosmetic_&lt;Class&gt;_…</c>), and classless
    /// (<c>…_&lt;Rarity&gt;_Generic_…</c>). Class tokens normalized to
    /// the full PlayerClass SnoName so abbreviated and full-form
    /// authored names produce the same facet value.</item>
    /// </list>
    /// Glyph facets decode the glyph (cheap; 562 in the group);
    /// Power + Item facets are <b>decode-free</b> (the CoreTOC name is
    /// enough).</summary>
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
            case AssetKind.Power when TryGetPowerClassFromName(asset.Name) is { } className:
                list.Add(new Facet("class", className, FacetSource.NameConvention));
                break;
            case AssetKind.Item:
            {
                var conv = ParseItemConvention(asset.Name);
                if (conv.Type is not null)
                    list.Add(new Facet("type", conv.Type, FacetSource.NameConvention));
                if (conv.Rarity is not null)
                    list.Add(new Facet("rarity", conv.Rarity, FacetSource.NameConvention));
                if (conv.Class is not null)
                    list.Add(new Facet("class", conv.Class, FacetSource.NameConvention));
                break;
            }
        }
        return list;
    }

    /// <summary>Match a power's CoreTOC name against the §6.5 PlayerClass
    /// roster: a class-skill power is named
    /// <c>&lt;ClassSnoName&gt;_&lt;SkillName&gt;</c> by the engine's
    /// first-party convention (e.g. <c>Barbarian_Bash</c>,
    /// <c>Sorcerer_Fireball</c>, <c>Necromancer_BloodLance</c>). Returns
    /// the matched <c>ClassSnoName</c>, or <see langword="null"/> for
    /// non-matching names (monster powers like
    /// <c>MorluCaster_Fireball</c>, generic item-affix powers like
    /// <c>1HAxe_Unique_Druid_100</c>, debug stubs).</summary>
    internal string? TryGetPowerClassFromName(string powerName)
    {
        if (string.IsNullOrEmpty(powerName)) return null;
        var underscore = powerName.IndexOf('_');
        if (underscore <= 0) return null;
        var prefix = powerName.AsSpan(0, underscore);
        foreach (var cn in PlayerClassSnoNames())
            if (prefix.SequenceEqual(cn)) return cn;
        return null;
    }

    /// <summary>Parse the item's CoreTOC name into its
    /// <c>(Type, Rarity, Class)</c> facet triple via the engine's
    /// first-party item-naming convention (CL-73). Three patterns are
    /// recognized:
    /// <list type="bullet">
    /// <item><b>Weapons &amp; armor</b> —
    /// <c>&lt;Type&gt;_&lt;Rarity&gt;_&lt;Class&gt;_&lt;NN&gt;[_&lt;Variant&gt;]</c>
    /// (<c>1HAxe_Unique_Druid_100</c>, <c>Helm_Rare_Barb_Crafted_47</c>).</item>
    /// <item><b>Cosmetics</b> —
    /// <c>Cosmetic_&lt;Class&gt;_&lt;Name&gt;</c>
    /// (<c>Cosmetic_Barbarian_*</c>).</item>
    /// <item><b>Classless or generic</b> —
    /// <c>&lt;Type&gt;_&lt;Rarity&gt;_Generic_&lt;NN&gt;</c>
    /// (<c>1HAxe_Magic_Generic_001</c>) emits no class facet
    /// (<c>Generic</c> is the engine's "no class" sentinel).</item>
    /// </list>
    /// Item names that don't fit any pattern still get a <c>type</c>
    /// facet from the first token (honest partial). Class tokens are
    /// normalized to their full PlayerClass SnoName
    /// (<c>Barb</c> → <c>Barbarian</c>, <c>Sorc</c> → <c>Sorcerer</c>,
    /// <c>Necro</c> → <c>Necromancer</c>) so a single facet value
    /// matches both abbreviated and full-form authored names.</summary>
    internal static (string? Type, string? Rarity, string? Class) ParseItemConvention(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return default;
        var tokens = itemName.Split('_');
        if (tokens.Length < 1 || string.IsNullOrEmpty(tokens[0])) return default;
        var type = tokens[0];

        // Pattern: <Type>_<Rarity>_<Class>_<...>
        if (tokens.Length >= 3 && IsKnownItemRarity(tokens[1]))
        {
            var rarity = tokens[1];
            var cls = MapItemClassToken(tokens[2]);
            return (type, rarity, cls);
        }

        // Pattern: <Type>_<Class>_<...> (cosmetics + a few others)
        if (tokens.Length >= 2 && MapItemClassToken(tokens[1]) is { } cls2)
            return (type, null, cls2);

        // Fallback — only the type token survives.
        return (type, null, null);
    }

    /// <summary>The closed set of rarity tokens the item convention
    /// uses in the second slot. <c>Cosmetic</c>, <c>Charm</c>,
    /// <c>Journey</c>, <c>Template</c>, <c>MSWK</c>, etc. occupy that
    /// slot in non-weapon/armor items but they're <i>types</i>, not
    /// rarities — they're already surfaced by the type facet. Keeping
    /// this set tight (the canonical 6) prevents a type leaking into
    /// the rarity facet.</summary>
    private static bool IsKnownItemRarity(string token) =>
        token is "Normal" or "Magic" or "Rare" or "Legendary" or "Unique" or "Any";

    /// <summary>Map an item-name class token to the canonical
    /// PlayerClass SnoName, or <see langword="null"/> when the token
    /// isn't a class. Both the short (<c>Barb</c>, <c>Sorc</c>,
    /// <c>Necro</c>) and full (<c>Barbarian</c>, <c>Sorcerer</c>,
    /// <c>Necromancer</c>) forms are recognized — the engine uses both
    /// (cosmetics tend to use full form; weapon/armor uses short).
    /// <c>Generic</c> is the engine's "no class" sentinel; the
    /// function returns <see langword="null"/> for it on purpose so
    /// the consumer doesn't accidentally treat generic items as
    /// class-restricted.</summary>
    private static string? MapItemClassToken(string token) => token switch
    {
        "Barb" or "Barbarian" => "Barbarian",
        "Sorc" or "Sorcerer" => "Sorcerer",
        "Necro" or "Necromancer" => "Necromancer",
        "Druid" or "Rogue" or "Paladin" or "Warlock" or "Spiritborn" => token,
        _ => null,
    };

    /// <summary>FR-C23 (Option A, CL-77) — the engine's authored
    /// paragon-node tooltip chrome (per-rarity 9-slice
    /// <see cref="AssetKind.TiledStyle"/> panels). The consumer
    /// renders each via the existing
    /// <see cref="Diablo4Storage.ReadTiledStyle"/> /
    /// <see cref="TryGet{T}(AssetRef, out T)"/> path. See
    /// <see cref="ParagonTooltipChrome"/> for the surfaced fields.
    /// The wider layout / per-state recipe (slot rects, typography,
    /// state binding) is tracked on <c>casc-fr#38</c> (FR-C26) — the
    /// FR-C7-shaped multi-CL RE thread.</summary>
    /// <remarks>
    /// The eight <c>TooltipBackgroundRarity_*</c> SNOs are resolved
    /// once via <see cref="CoreToc.TryGetId"/> + the canonical
    /// CoreTOC name; missing SNOs are dropped (an honest partial —
    /// every paragon-relevant rarity is populated on the live
    /// install, but a future build dropping one wouldn't crash the
    /// surface, just shrink the dictionary).
    /// </remarks>
    public ParagonTooltipChrome GetParagonTooltipChrome()
    {
        if (_paragonTooltipChrome is not null) return _paragonTooltipChrome;
        lock (_paragonTooltipChromeLock)
        {
            return _paragonTooltipChrome ??= BuildParagonTooltipChrome();
        }
    }

    private ParagonTooltipChrome BuildParagonTooltipChrome()
    {
        // Paragon rarities map to the four TooltipBackgroundRarity_*
        // SNOs by name — Optimizer-confirmed coverage on the live
        // build (`casc-fr#35` consumer probe, 2026-05-23).
        var paragon = new SortedDictionary<ParagonRarity, AssetRef>();
        foreach (var (rarity, suffix) in ParagonRarityChromeSuffixes())
            if (TryGetTiledStyleRef($"TooltipBackgroundRarity_{suffix}", out var assetRef))
                paragon[rarity] = assetRef;

        // Item-side rarities — future-proofing handle. Keyed by the
        // engine's string rarity token (the suffix from the SNO
        // name), not by ParagonRarity (which has no Unique / Set /
        // Mythic / Season members).
        var item = new SortedDictionary<string, AssetRef>(StringComparer.Ordinal);
        foreach (var suffix in (string[])["Unique", "Set", "Mythic", "Season"])
            if (TryGetTiledStyleRef($"TooltipBackgroundRarity_{suffix}", out var assetRef))
                item[suffix] = assetRef;

        // CL-80 — the rest of the engine's multi-layer tooltip
        // composite. TooltipBaseBackground is the universal dark
        // backdrop; TooltipFrame / TooltipFrameLight are the ornate
        // spiky outer border (9-slice; centre from
        // 2DUI_BackgroundSquares); DefaultTooltip / TextTooltip are
        // the smaller simple-frame variants. All decode through the
        // existing TryGet<TiledStyleDefinition> path.
        _ = TryGetTiledStyleRef("TooltipBaseBackground", out var baseLayer);
        _ = TryGetTiledStyleRef("TooltipFrame", out var ornateFrame);
        _ = TryGetTiledStyleRef("TooltipFrameLight", out var ornateFrameLight);
        _ = TryGetTiledStyleRef("DefaultTooltip", out var defaultFrame);
        _ = TryGetTiledStyleRef("TextTooltip", out var textFrame);

        // Banner variants (Map / Town) — future-proofing for non-
        // tooltip placements. Keyed by the placement token.
        var banners = new SortedDictionary<string, AssetRef>(StringComparer.Ordinal);
        foreach (var placement in (string[])["Map", "Town"])
            if (TryGetTiledStyleRef($"TooltipBanner_{placement}", out var bannerRef))
                banners[placement] = bannerRef;

        // CL-81 — the inline skill-tag icon atlas (2DUI_Tooltip_Icons,
        // sno 2119840). 61 frames the engine composites into tooltip
        // body prose wherever a {c_important} keyword token appears
        // in a glyph-affix description (Demonology / Hellfire / Abyss
        // / Archfiend / Healthy / etc.). The consumer decodes via
        // TryGet<TextureDefinition> and uses the Frames list.
        _ = TryGetAtlasRef("2DUI_Tooltip_Icons", out var skillIconAtlas);

        // CL-82 — the horizontal divider line (Center_Divider_White,
        // sno 1559055). Optimizer-validated structural pick on #38
        // (the only white candidate of four divider TiledStyles —
        // the other three are dark-teal and would render invisible
        // against the tooltip's dark backdrop).
        _ = TryGetTiledStyleRef("Center_Divider_White", out var divider);

        return new ParagonTooltipChrome(
            BaseLayer: baseLayer,
            PanelByRarity: paragon,
            ItemSidePanelByRarityName: item,
            OrnateFrame: ornateFrame,
            OrnateFrameLight: ornateFrameLight,
            DefaultFrame: defaultFrame,
            TextFrame: textFrame,
            BannerByPlacement: banners,
            Divider: divider,
            SkillIconAtlas: skillIconAtlas);
    }

    private bool TryGetAtlasRef(string name, out AssetRef assetRef)
    {
        if (_d4.CoreToc.TryGetId(SnoGroup.Texture, name, out var id))
        {
            assetRef = AssetProviders.AtlasRef(_d4, id);
            return true;
        }
        assetRef = default;
        return false;
    }

    private static IEnumerable<(ParagonRarity Rarity, string Suffix)>
        ParagonRarityChromeSuffixes() =>
        [
            (ParagonRarity.Common, "Common"),
            (ParagonRarity.Magic, "Magic"),
            (ParagonRarity.Rare, "Rare"),
            (ParagonRarity.Legendary, "Legendary"),
        ];

    private bool TryGetTiledStyleRef(string name, out AssetRef assetRef)
    {
        if (_d4.CoreToc.TryGetId(SnoGroup.UiStyle, name, out var id))
        {
            assetRef = new AssetRef(
                AssetKind.TiledStyle, SnoGroup.UiStyle, id, name,
                Array.Empty<string>());
            return true;
        }
        assetRef = default;
        return false;
    }

    /// <summary>Lazy access to the §6.5 PlayerClass SnoName roster
    /// (cached for the catalog's lifetime — the eight class names
    /// drive every Power class-facet match).</summary>
    private string[] PlayerClassSnoNames()
    {
        if (_playerClassSnoNames is not null) return _playerClassSnoNames;
        lock (_playerClassesLock)
        {
            if (_playerClassSnoNames is not null) return _playerClassSnoNames;
            var names = new List<string>();
            foreach (var e in _d4.CoreToc.EntriesInGroup(SnoGroup.PlayerClass))
                if (!string.IsNullOrEmpty(e.Name) && e.Name != "Axe Bad Data")
                    names.Add(e.Name);
            return _playerClassSnoNames = names.ToArray();
        }
    }

    /// <summary>FR-C20 P2b — discover assets of a kind carrying a categorical
    /// facet (e.g. every <see cref="AssetKind.ParagonGlyph"/> with
    /// <c>class=Barbarian</c>). Computes <see cref="Facets"/> per asset (decodes
    /// where the facet requires it), so scope with <paramref name="kind"/>.</summary>
    public IEnumerable<AssetRef> FindByFacet(AssetKind kind, string key, string value) =>
        OfKind(kind).Where(r => Facets(r).Any(f =>
            f.Key.Equals(key, StringComparison.OrdinalIgnoreCase) &&
            f.Value.Equals(value, StringComparison.OrdinalIgnoreCase)));

    /// <summary>FR-C20 P5 — the asset's authored relationship edges, so
    /// <b>board → node → power</b> and <b>glyph → affix / class</b> traversal is
    /// first-class instead of hand-assembled. Each <see cref="AssetLink.Target"/>
    /// is a full <see cref="AssetRef"/> — call <see cref="Related"/> on it to
    /// chain. Authored FK edges only:
    /// <list type="bullet">
    /// <item><see cref="AssetKind.ParagonBoard"/> → <c>node</c> (distinct
    /// non-empty cells).</item>
    /// <item><see cref="AssetKind.ParagonNode"/> → <c>power</c> (its
    /// <c>SnoPassivePower</c>, when set — the legendary-node passive).</item>
    /// <item><see cref="AssetKind.ParagonGlyph"/> → <c>affix</c> (granted) +
    /// <c>class</c> (usable-by).</item>
    /// </list>
    /// A socket node ↔ glyph is a <b>runtime</b> slotting, not an authored FK, so
    /// it is deliberately not a link; find candidate glyphs for a class via
    /// <see cref="FindByFacet"/>.</summary>
    public IReadOnlyList<AssetLink> Related(AssetRef asset)
    {
        var links = new List<AssetLink>();
        switch (asset.Kind)
        {
            case AssetKind.ParagonBoard when TryGet<ParagonBoardDefinition>(asset, out var b):
                foreach (var nodeSno in b.Cells.Where(c => c is > 0).Select(c => c!.Value).Distinct())
                    links.Add(new AssetLink("node", Ref(AssetKind.ParagonNode, SnoGroup.ParagonNode, nodeSno)));
                break;
            case AssetKind.ParagonNode when TryGet<ParagonNodeDefinition>(asset, out var n):
                if (n.SnoPassivePower > 0)   // negative/zero = no passive power (sentinel)
                    links.Add(new AssetLink("power", Ref(AssetKind.Power, SnoGroup.Power, n.SnoPassivePower)));
                break;
            case AssetKind.ParagonGlyph when TryGet<ParagonGlyphDefinition>(asset, out var g):
                foreach (var affix in g.AffixSnoIds.Where(a => a > 0))
                    links.Add(new AssetLink("affix", Ref(AssetKind.ParagonGlyphAffix, SnoGroup.ParagonGlyphAffix, affix)));
                foreach (var cls in g.UsableByClassSnoIds.Where(c => c > 0))
                    links.Add(new AssetLink("class", Ref(AssetKind.PlayerClass, SnoGroup.PlayerClass, cls)));
                break;
        }
        return links;
    }

    private AssetRef Ref(AssetKind kind, SnoGroup group, int sno) =>
        new(kind, group, sno, _d4.CoreToc.GetName(group, sno) ?? string.Empty, Array.Empty<string>());

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

    /// <summary>FR-C21 — the display-ready projection of one paragon
    /// node, evaluated and resolved through the Appendix C carve-out
    /// (magnitudes computed via <see cref="ParagonMagnitudeFormula.Evaluate"/>;
    /// the <see cref="AssetKind.AttributeFormulas"/> table consulted
    /// once and re-used; stat names resolved from the
    /// <c>Generic_&lt;Rarity&gt;_&lt;Token&gt;</c> node-name
    /// convention). Returns <see langword="null"/> when the SNO is
    /// missing, the record fails to decode, or has no corresponding
    /// <see cref="CoreToc"/> name.</summary>
    /// <remarks>
    /// <b>Caching.</b> Results are cached by SNO for the storage's
    /// lifetime — the optimizer hot path re-queries the same boards
    /// repeatedly, and each board carries ~17–21 distinct node defs
    /// across ~441 cells. Decode once, reuse. The cache also stores
    /// negative results (missing/undecodable SNOs) so a malformed
    /// repeat-query is just as cheap as a hit.
    /// </remarks>
    /// <param name="sno">The <see cref="SnoGroup.ParagonNode"/> id.</param>
    public ParagonNodeInfo? GetNodeInfo(int sno) =>
        _nodeInfoCache.GetOrAdd(sno, ComputeNodeInfo);

    private ParagonNodeInfo? ComputeNodeInfo(int sno)
    {
        var def = GetNodeDef(sno);
        if (def is null) return null;
        var name = _d4.CoreToc.GetName(SnoGroup.ParagonNode, sno);
        if (name is null) return null;
        var formulas = GetAttributeFormulas();
        return ParagonNodeInfoBuilder.Build(_d4, this, def, name, formulas);
    }

    /// <summary>Return the cached decoded
    /// <see cref="ParagonNodeDefinition"/> for an SNO (decodes on first
    /// miss). The cache key is the SNO id; missing/undecodable SNOs
    /// memoize as <see langword="null"/> so repeat lookups stay
    /// O(1).</summary>
    internal ParagonNodeDefinition? GetNodeDef(int sno) =>
        _nodeDefCache.GetOrAdd(sno, ComputeNodeDef);

    private ParagonNodeDefinition? ComputeNodeDef(int sno)
    {
        try { return _d4.ReadParagonNode(sno); }
        catch (CascException) { return null; }
    }

    /// <summary>Lazy access to the shared
    /// <see cref="AttributeFormulaTable"/> (sno
    /// <c>201912</c>) — read once on first use and held for the
    /// catalog's lifetime. The table is ~1MB and otherwise re-decoded
    /// once per node-info build.</summary>
    private AttributeFormulaTable GetAttributeFormulas()
    {
        if (_attributeFormulas is not null) return _attributeFormulas;
        lock (_formulasLock)
        {
            return _attributeFormulas ??= _d4.ReadAttributeFormulas();
        }
    }

    /// <summary>FR-C21 — the consumer hot path: enumerate every placed
    /// node on a paragon board paired with its grid cell coordinate
    /// (row, col), each <see cref="ParagonNodeInfo"/> fully resolved
    /// (magnitude / unit / stat name). Returns an empty list when the
    /// board SNO is missing or fails to decode.</summary>
    /// <remarks>
    /// <para>
    /// <b>Performance contract.</b> The board itself is cached (one
    /// decoded <see cref="ParagonBoardDefinition"/> per SNO), the per-
    /// cell <see cref="ParagonNodeInfo"/>s are cached per node SNO
    /// (~17–21 distinct per board, ~441 cells), and the projected
    /// <c>(cell, info)</c> list is cached per board SNO so repeat
    /// queries return the same instance with O(1) cost. Missing /
    /// undecodable board SNOs memoize as an empty list (the optimizer's
    /// search-tree pruning often probes malformed ids; the cache makes
    /// re-probes free).
    /// </para>
    /// <para>
    /// <b>Ordering.</b> Row-major (<c>row 0 = top</c>,
    /// <c>col 0 = left</c>), skipping empty cells.
    /// </para>
    /// </remarks>
    /// <param name="boardSno">The <see cref="SnoGroup.ParagonBoard"/> id.</param>
    public IReadOnlyList<(ParagonGridCell Cell, ParagonNodeInfo Info)>
        GetBoardNodes(int boardSno) =>
        _boardNodesCache.GetOrAdd(boardSno, ComputeBoardNodes)
            ?? Array.Empty<(ParagonGridCell, ParagonNodeInfo)>();

    private IReadOnlyList<(ParagonGridCell, ParagonNodeInfo)>? ComputeBoardNodes(int boardSno)
    {
        var board = GetBoardDef(boardSno);
        if (board is null)
            return Array.Empty<(ParagonGridCell, ParagonNodeInfo)>();

        var width = board.Width;
        var seenAny = false;
        var result = new List<(ParagonGridCell, ParagonNodeInfo)>(board.NodeCount);
        for (var row = 0; row < width; row++)
        {
            for (var col = 0; col < width; col++)
            {
                var sno = board.CellAt(row, col);
                if (sno is null) continue;
                var info = GetNodeInfo(sno.Value);
                if (info is null) continue;
                result.Add((new ParagonGridCell(row, col), info));
                seenAny = true;
            }
        }
        // Honest "empty" carries through even when the board itself
        // decoded — e.g. an authored-but-vacated test board.
        _ = seenAny;
        return result;
    }

    /// <summary>Return the cached decoded
    /// <see cref="ParagonBoardDefinition"/> for an SNO (decodes on
    /// first miss). The cache key is the SNO id;
    /// missing/undecodable SNOs memoize as <see langword="null"/> so
    /// repeat lookups stay O(1).</summary>
    internal ParagonBoardDefinition? GetBoardDef(int sno) =>
        _boardDefCache.GetOrAdd(sno, ComputeBoardDef);

    private ParagonBoardDefinition? ComputeBoardDef(int sno)
    {
        try { return _d4.ReadParagonBoard(sno); }
        catch (CascException) { return null; }
    }

    /// <summary>FR-C21 — every paragon node in the install (or the
    /// subset matching <paramref name="query"/>) projected as a
    /// fully-resolved <see cref="ParagonNodeInfo"/>. Lazy: enumeration
    /// streams one node at a time and shares the SNO-keyed decode
    /// cache with <see cref="GetNodeInfo"/> /
    /// <see cref="GetBoardNodes"/>. Malformed nodes (those whose
    /// <see cref="ParagonNodeDefinition.Parse"/> throws or whose
    /// <see cref="CoreToc"/> name is missing) are silently
    /// skipped.</summary>
    /// <remarks>
    /// <para>
    /// The query's <see cref="AssetQuery.Kind"/> /
    /// <see cref="AssetQuery.Kinds"/> are overridden to
    /// <see cref="AssetKind.ParagonNode"/> — the other facets
    /// (<see cref="AssetQuery.NameContains"/>,
    /// <see cref="AssetQuery.Where"/>,
    /// <see cref="AssetQuery.OrderByName"/>, …) apply as usual.
    /// </para>
    /// </remarks>
    public IEnumerable<ParagonNodeInfo> EnumerateNodes(AssetQuery? query = null)
    {
        var effective = (query ?? new AssetQuery()) with { Kind = AssetKind.ParagonNode };
        foreach (var r in Find(effective))
        {
            var info = GetNodeInfo(r.Sno);
            if (info is not null) yield return info;
        }
    }
}
