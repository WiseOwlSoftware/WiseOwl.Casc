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
}
