using System;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// A decoded Diablo IV <c>ItemDefinition</c> (<c>.itm</c>, SNO group
/// <see cref="SnoGroup.Item"/> = 73) — an item record. Identity +
/// localized text only; item stat/affix/power modeling stays the
/// consumer's domain spec (Appendix C).
/// </summary>
/// <remarks>
/// <see cref="SnoId"/> is the binary field (payload <c>0</c>). The
/// localized strings are resolved from the item's <b>sibling StringList
/// table</b> (<c>docs/casc-diablo4-format.md §11.4</c>, Appendix A CL-22 /
/// CL-20): group-42 SNO <c>"Item_" + snoName</c>, labels <c>Name</c> /
/// <c>Flavor</c> / <c>TransmogName</c>. Each is empty (honest sentinel)
/// when decoded byte-only, when the item has no sibling table, or when
/// that particular label is absent; the consumer owns any fallback. Raw
/// decoded text — D4 markup kept intact.
/// </remarks>
public sealed class ItemDefinition
{
    private ItemDefinition(int snoId, int itemTypeSnoId)
    {
        SnoId = snoId;
        ItemTypeSnoId = itemTypeSnoId;
    }

    /// <summary>The item's own SNO id (== the CoreTOC id).</summary>
    public int SnoId { get; }

    /// <summary>
    /// The item's <b>CoreTOC name</b> — the durable, opaque stable id (e.g.
    /// <c>"Amulet_Unique_Rogue_100"</c>), distinct from the localized
    /// <see cref="Name"/>. Populated by <see cref="Diablo4Storage.ReadItem(int,string)"/>
    /// and <see cref="Diablo4Storage.EnumerateItems(ItemClass)"/>;
    /// <see cref="string.Empty"/> on the byte-only <see cref="Parse(ReadOnlySpan{byte})"/>
    /// (no <see cref="CoreToc"/>).
    /// <br/><br/>
    /// <b>De-duping uniques (#56).</b> The live Item group carries leftover
    /// seasonal/PTR <b>duplicate</b> unique SNOs whose CoreTOC name is
    /// season-prefixed — <c>^S\d+_</c> (e.g. <c>S10_Amulet_Unique_Rogue_100_Boots</c>,
    /// a stale duplicate of <c>Amulet_Unique_Rogue_100</c> with a different slot /
    /// more explicits). They share the localized display <see cref="Name"/> with
    /// the canonical item, so any surface that de-dupes uniques by display name
    /// must prefer the <b>non</b>-season-prefixed <see cref="SnoName"/> (the
    /// <c>^S\d+_</c> test is the reliable signal — see
    /// <c>casc-diablo4-format.md §11.4</c>). No clean structural bit separates
    /// them (the <c>0x10000000</c> flag correlates ~91% but with exceptions).
    /// </summary>
    public string SnoName { get; private set; } = string.Empty;

    /// <summary>The item's base-type SNO id (payload <c>+0x0C</c>) — the
    /// <see cref="ItemType"/> (group <see cref="SnoGroup.ItemType"/> = 98) that
    /// classifies this item as a weapon / armor / jewelry / charm. Resolve it
    /// with <see cref="Diablo4Storage.ReadItemType(int)"/>. <c>0</c> when the
    /// record has none (§13 / LIB-1).</summary>
    public int ItemTypeSnoId { get; }

    /// <summary>Localized item name (sibling label <c>Name</c>), or
    /// <see cref="string.Empty"/>.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Localized flavor text (sibling label <c>Flavor</c>), or
    /// <see cref="string.Empty"/>.</summary>
    public string Flavor { get; private set; } = string.Empty;

    /// <summary>Localized transmog name (sibling label <c>TransmogName</c>;
    /// present mainly for uniques), or <see cref="string.Empty"/>.</summary>
    public string TransmogName { get; private set; } = string.Empty;

    /// <summary>Decode an Item from its raw SNO blob (identity only — the
    /// localized fields need <see cref="CoreToc"/>; use
    /// <see cref="Diablo4Storage.ReadItem(int,string)"/>).</summary>
    public static ItemDefinition Parse(ReadOnlySpan<byte> blob)
    {
        var r = new SnoRecord(blob);
        int typeSno = blob.Length >= SnoRecord.DefaultPayloadBase + 0x0C + 4 ? r.I32(0x0C) : 0;
        return new ItemDefinition(r.SnoId, typeSno);
    }

    internal void SetStrings(string name, string flavor, string transmogName)
    {
        Name = name;
        Flavor = flavor;
        TransmogName = transmogName;
    }

    internal void SetSnoName(string snoName) =>
        SnoName = snoName;
}
