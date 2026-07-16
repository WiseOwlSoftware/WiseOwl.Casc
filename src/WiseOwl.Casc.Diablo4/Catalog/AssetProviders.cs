using System;
using System.Collections.Generic;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// FR-C20 — builds the <see cref="IAssetProvider"/> registry for a
/// <see cref="Diablo4Storage"/>. One provider per <see cref="AssetKind"/>;
/// each is thin — it enumerates <c>CoreTOC</c> and delegates decode to the
/// storage's existing typed reader. New families slot in here.
/// </summary>
internal static class AssetProviders
{
    /// <summary>The paragon board UI scene the singleton render recipes
    /// project from.</summary>
    private const int BoardSceneSno = 657304;
    private static readonly SnoGroup UiSceneGroup = (SnoGroup)46;

    public static IReadOnlyList<IAssetProvider> For(Diablo4Storage d4)
    {
        var toc = d4.CoreToc;
        string loc = Diablo4Storage.DefaultLocale;

        return
        [
            // ── render / composition recipes ────────────────────────────
            new SingletonProvider(AssetKind.ParagonNodeRender, true,
                UiSceneGroup, BoardSceneSno, "ParagonBoard.NodeRender",
                () => d4.ReadParagonNodeRecipe()),
            new SingletonProvider(AssetKind.ParagonBoardGrid, true,
                UiSceneGroup, BoardSceneSno, "ParagonBoard.Grid",
                () => d4.ReadParagonBoardGrid()),
            new SnoProvider(AssetKind.TiledStyle, true, SnoGroup.UiStyle, toc,
                filter: null, tagger: null,
                decode: id => d4.TryReadTiledStyle(id, out var ts) ? ts : null),
            new SnoProvider(AssetKind.TextureAtlas, true, SnoGroup.Texture, toc,
                filter: e => e.Name.StartsWith("2DUI", StringComparison.OrdinalIgnoreCase),
                tagger: e => AtlasTags(d4, e.Id),
                decode: id => d4.TextureMeta.TryGet(id, out var td) ? td : (object?)null),
            new SelectionHighlightProvider(d4),

            // ── paragon domain ──────────────────────────────────────────
            new SnoProvider(AssetKind.ParagonBoard, false, SnoGroup.ParagonBoard, toc,
                null, null, id => d4.ReadParagonBoard(id)),
            new SnoProvider(AssetKind.ParagonNode, false, SnoGroup.ParagonNode, toc,
                null, null, id => d4.ReadParagonNode(id)),
            new SnoProvider(AssetKind.ParagonGlyph, false, SnoGroup.ParagonGlyph, toc,
                null, null, id => d4.ReadParagonGlyph(id)),
            new SnoProvider(AssetKind.ParagonGlyphAffix, false, SnoGroup.ParagonGlyphAffix, toc,
                null, null, id => d4.ReadParagonGlyphAffix(id)),
            new SnoProvider(AssetKind.Power, false, SnoGroup.Power, toc,
                null, null, id => d4.ReadPower(id, loc)),
            new SnoProvider(AssetKind.PlayerClass, false, SnoGroup.PlayerClass, toc,
                null, null, id => d4.ReadPlayerClass(id)),
            new SingletonProvider(AssetKind.AttributeFormulas, false,
                SnoGroup.GameBalance, 201912, "AttributeFormulas",
                () => d4.ReadAttributeFormulas()),

            // ── broader domain (extensible) ─────────────────────────────
            new SnoProvider(AssetKind.Item, false, SnoGroup.Item, toc,
                null, null, id => d4.ReadItem(id, loc)),
            new SnoProvider(AssetKind.Affix, false, SnoGroup.Affix, toc,
                null, null, id => d4.ReadAffix(id, loc)),
            new SnoProvider(AssetKind.ItemType, false, SnoGroup.ItemType, toc,
                null, null, id => d4.ReadItemType(id)),
        ];
    }

    /// <summary>Cheap, filterable atlas tags from the preloaded combined-meta:
    /// <c>2dui</c> + <c>codec:&lt;codec&gt;</c> (so a query can filter by codec
    /// without decoding pixels). Missing meta ⇒ just <c>2dui</c>.</summary>
    internal static IReadOnlyList<string> AtlasTags(Diablo4Storage d4, int sno) =>
        d4.TextureMeta.TryGet(sno, out var td)
            ? ["2dui", $"codec:{td.Codec.ToString().ToLowerInvariant()}"]
            : ["2dui"];

    /// <summary>Build the <see cref="AssetKind.TextureAtlas"/> ref for a SNO
    /// (used by handle reverse-lookup), with the same name + tags an enumerate
    /// would produce.</summary>
    internal static AssetRef AtlasRef(Diablo4Storage d4, int sno) =>
        new(AssetKind.TextureAtlas, SnoGroup.Texture, sno,
            d4.CoreToc.GetName(SnoGroup.Texture, sno) ?? string.Empty, AtlasTags(d4, sno));
}

/// <summary>FR-C20 — a per-SNO provider: enumerates a <see cref="SnoGroup"/>
/// from <c>CoreTOC</c> (optionally filtered/tagged) and decodes one by id
/// through a reader delegate (exception-safe).</summary>
internal sealed class SnoProvider(
    AssetKind kind, bool isRenderRecipe, SnoGroup group, CoreToc toc,
    Func<SnoEntry, bool>? filter,
    Func<SnoEntry, IReadOnlyList<string>>? tagger,
    Func<int, object?> decode) : IAssetProvider
{
    public AssetKind Kind => kind;
    public bool IsRenderRecipe => isRenderRecipe;

    public IEnumerable<AssetRef> Enumerate()
    {
        foreach (var e in toc.EntriesInGroup(group))
        {
            if (filter is not null && !filter(e)) continue;
            yield return new AssetRef(kind, group, e.Id, e.Name,
                tagger?.Invoke(e) ?? Array.Empty<string>());
        }
    }

    public bool TryDecode(AssetRef r, out object value)
    {
        try
        {
            if (decode(r.Sno) is { } v) { value = v; return true; }
        }
        catch (Exception ex) when (ex is CascException or FormatException or ArgumentException
            or System.IO.IOException or IndexOutOfRangeException or OverflowException)
        {
            // malformed / absent / sentinel id → not decodable; report false.
        }
        value = null!;
        return false;
    }
}

/// <summary>FR-C20 — a provider for a single, well-known asset (a recipe
/// projected from a scene, or a fixed table).</summary>
internal sealed class SingletonProvider(
    AssetKind kind, bool isRenderRecipe, SnoGroup group, int sno, string name,
    Func<object?> decode) : IAssetProvider
{
    public AssetKind Kind => kind;
    public bool IsRenderRecipe => isRenderRecipe;

    public IEnumerable<AssetRef> Enumerate()
    {
        yield return new AssetRef(kind, group, sno, name, Array.Empty<string>());
    }

    public bool TryDecode(AssetRef r, out object value)
    {
        try
        {
            if (decode() is { } v) { value = v; return true; }
        }
        catch (Exception ex) when (ex is CascException or FormatException or ArgumentException
            or System.IO.IOException or IndexOutOfRangeException or OverflowException)
        {
        }
        value = null!;
        return false;
    }
}

/// <summary>FR-C20 — the selection-highlight provider: discovers the
/// <see cref="TiledStyleDefinition"/> recipes (group <see cref="SnoGroup.UiStyle"/>)
/// that compose the selection-highlight atlases, tagging each with its
/// <see cref="SelectionShape"/> (from the authored TiledStyle name) and source
/// atlas. Decode returns the <see cref="TiledStyleDefinition"/>.</summary>
internal sealed class SelectionHighlightProvider(Diablo4Storage d4) : IAssetProvider
{
    public AssetKind Kind => AssetKind.SelectionHighlight;
    public bool IsRenderRecipe => true;

    public IEnumerable<AssetRef> Enumerate()
    {
        var atlases = new HashSet<int>();
        foreach (var n in SelectionHighlight.AtlasNames)
            if (d4.CoreToc.TryGetId(SnoGroup.Texture, n, out var s)) atlases.Add(s);

        foreach (var e in d4.CoreToc.EntriesInGroup(SnoGroup.UiStyle))
        {
            if (!d4.TryReadTiledStyle(e.Id, out var ts) || ts.SourceImageHandle is 0 or 0xFFFFFFFF)
                continue;
            if (!d4.TryGetIconFrame(ts.SourceImageHandle, out var atlas, out _) || !atlases.Contains(atlas))
                continue;
            var shape = SelectionHighlight.ShapeOf(e.Name);
            yield return new AssetRef(AssetKind.SelectionHighlight, SnoGroup.UiStyle, e.Id, e.Name,
                [shape.ToString().ToLowerInvariant(), $"atlas:{atlas}"]);
        }
    }

    public bool TryDecode(AssetRef r, out object value)
    {
        if (d4.TryReadTiledStyle(r.Sno, out var ts)) { value = ts; return true; }
        value = null!;
        return false;
    }
}
