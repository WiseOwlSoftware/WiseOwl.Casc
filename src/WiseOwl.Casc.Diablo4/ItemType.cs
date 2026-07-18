using System;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// The broad equipment category of an <see cref="ItemType"/> — the top-level
/// grouping a gear/item API filters on (LIB-1). Derived structurally from the
/// item base-type record, not from its name.
/// </summary>
public enum ItemClass
{
    /// <summary>Not equippable gear — a consumable, currency, gem/rune, quest
    /// item, cache, key, crafting reagent, essence, and so on.</summary>
    Other,

    /// <summary>A weapon-slot item: melee and ranged weapons plus the off-hands
    /// that share the weapon slot (shield, focus, totem).</summary>
    Weapon,

    /// <summary>A body-armor slot: helm, chest, gloves, legs, boots, and the
    /// like.</summary>
    Armor,

    /// <summary>Jewelry — amulet or ring.</summary>
    Jewelry,

    /// <summary>A charm.</summary>
    Charm,
}

/// <summary>
/// A decoded Diablo IV item <b>base type</b> — an entry in the item-type
/// dictionary (SNO group <see cref="SnoGroup.ItemType"/> = 98, the game's
/// <c>eItemType</c> enum: <c>Sword</c>, <c>Amulet</c>, <c>Helm</c>,
/// <c>Charm</c>, <c>HealthPotion</c>, …). Every <see cref="ItemDefinition"/>
/// references its base type (<see cref="ItemDefinition.ItemTypeSnoId"/>); this
/// record is what classifies an item as a weapon / armor / jewelry / charm
/// (LIB-1).
/// </summary>
/// <remarks>
/// Byte layout (clean-room, <c>docs/casc-diablo4-format.md §13</c>): payload
/// base <c>0x10</c>; <c>snoId</c> at payload <c>0</c>. The classification
/// fields (all payload-relative): a <b>kind</b> word at <c>+0x08</c>
/// (<c>32</c>/<c>48</c> ⇒ equippable gear; smaller values ⇒ non-gear), a
/// <b>sub-kind</b> at <c>+0x0C</c> (<c>5</c> ⇒ Charm), a <b>weapon-family</b>
/// enum at <c>+0x30</c> (<c>≥ 0</c> ⇒ a weapon-slot item, <c>-1</c> ⇒ not a
/// weapon), an <b>armor-value scalar</b> at <c>+0x3C</c> (<c>0</c> ⇒ jewelry,
/// <c>&gt; 0</c> ⇒ body armor), and a <b>slot</b> word at <c>+0x44</c>
/// (<c>&gt; 0</c> for armor/jewelry). Verified across the full g98 set on build
/// 3.1.1.72836. The type <see cref="Name"/> is the CoreTOC name — a durable,
/// opaque stable id (Appendix C).
/// </remarks>
public sealed class ItemType
{
    private const int KindOffset = 0x08;         // 32/48 => equippable gear
    private const int SubKindOffset = 0x0C;      // 5 => Charm
    private const int EItemTypeDescriptorOffset = 0x28; // VLA whose [0] is the eItemType ordinal
    private const int WeaponFamilyOffset = 0x30; // -1 => not a weapon
    private const int ArmorScalarOffset = 0x3C;  // 0 => jewelry, >0 => armor
    private const int SlotOffset = 0x44;         // >0 for armor/jewelry
    private const int MinLength = SnoRecord.DefaultPayloadBase + SlotOffset + 4;

    private ItemType(int snoId, string name, ItemClass @class, bool equippable, int weaponFamily, int eItemType)
    {
        SnoId = snoId;
        Name = name;
        Class = @class;
        IsEquippable = equippable;
        WeaponFamily = weaponFamily;
        EItemType = eItemType;
    }

    /// <summary>The base type's own SNO id (== the CoreTOC id; the value an
    /// item's <see cref="ItemDefinition.ItemTypeSnoId"/> points at).</summary>
    public int SnoId { get; }

    /// <summary>The base type's CoreTOC name (e.g. <c>"Sword"</c>,
    /// <c>"Amulet"</c>, <c>"Charm"</c>) — a durable, opaque stable id.</summary>
    public string Name { get; }

    /// <summary>The broad equipment category (weapon / armor / jewelry / charm
    /// / other), derived structurally from the record.</summary>
    public ItemClass Class { get; }

    /// <summary>Whether this is equippable gear (the kind word is
    /// <c>32</c>/<c>48</c>) as opposed to a consumable, currency, quest item,
    /// and so on.</summary>
    public bool IsEquippable { get; }

    /// <summary>The raw weapon-family enum (payload <c>+0x30</c>): a coarse
    /// grouping shared across related weapons (e.g. Axe/Sword/Mace all share
    /// one value), or <c>-1</c> when this is not a weapon-slot item.</summary>
    public int WeaponFamily { get; }

    /// <summary>
    /// The engine <c>eItemType</c> <b>ordinal</b> — the value that appears in an
    /// affix's <see cref="AffixDefinition.AllowedItemTypes"/> pool (#51, CL-108).
    /// Decoded as the first entry of the record's item-type array (the
    /// <c>DT_VARIABLEARRAY</c> at payload <c>+0x28</c>). This is the missing link
    /// that names an affix pool: e.g. <c>Helm</c>=16, <c>ChestArmor</c>=17,
    /// <c>Gloves</c>=28, <c>Boots</c>=29, <c>Legs</c>=30, <c>Amulet</c>=26,
    /// <c>Ring</c>=19, <c>Axe</c>=1, <c>Bow</c>=10, <c>Charm</c>=71 (build
    /// 3.1.1.72836). <b>1H/2H and class variants share one ordinal</b>
    /// (<c>Axe</c> and <c>Axe2H</c> are both <c>1</c>) — the ordinal is the base
    /// type, so a name lookup is one-ordinal-to-many-names.
    /// <br/><br/>
    /// <b>−1</b> when the record carries no item-type array (non-gear types) or
    /// it is malformed. A small number of ordinals that appear in affix pools
    /// (e.g. <c>9</c>, <c>23</c>) have <i>no</i> g98 <see cref="ItemType"/> record
    /// and so cannot be named from the data — they are engine-aggregate/legacy
    /// values; use <see cref="Diablo4Storage.GetItemTypeName(int)"/> which returns
    /// <see langword="null"/> for those.
    /// </summary>
    public int EItemType { get; }

    /// <summary>Decode an item base type from its raw SNO blob and CoreTOC
    /// name. Malformed/short records classify as <see cref="ItemClass.Other"/>
    /// (non-equippable).</summary>
    /// <param name="snoId">The base type's SNO id.</param>
    /// <param name="name">The base type's CoreTOC name.</param>
    /// <param name="blob">The raw g98 record bytes.</param>
    public static ItemType Parse(int snoId, string name, ReadOnlySpan<byte> blob)
    {
        if (blob.Length < MinLength)
            return new ItemType(snoId, name, ItemClass.Other, false, -1, -1);

        var r = new SnoRecord(blob);
        int kind = r.I32(KindOffset);
        int subKind = r.I32(SubKindOffset);
        int weaponFamily = r.I32(WeaponFamilyOffset);
        float armorScalar = r.F32(ArmorScalarOffset);
        int slot = r.I32(SlotOffset);
        int eItemType = ReadEItemType(r, blob.Length);

        bool equippable = kind is 32 or 48;
        ItemClass cls = Classify(equippable, subKind, weaponFamily, armorScalar, slot);
        return new ItemType(snoId, name, cls, equippable, cls == ItemClass.Weapon ? weaponFamily : -1, eItemType);
    }

    /// <summary>Read the <c>eItemType</c> ordinal — the first int32 of the
    /// item-type <c>DT_VARIABLEARRAY</c> at payload <c>+0x28</c>. <b>−1</b> when
    /// the descriptor is absent/malformed or the ordinal is out of range.</summary>
    private static int ReadEItemType(SnoRecord r, int len)
    {
        if (SnoRecord.DefaultPayloadBase + EItemTypeDescriptorOffset + 8 > len) return -1;
        int dataOff = r.I32(EItemTypeDescriptorOffset);
        int byteSize = r.I32(EItemTypeDescriptorOffset + 4);
        if (dataOff <= 0 || byteSize < 4 || SnoRecord.DefaultPayloadBase + dataOff + 4 > len) return -1;
        int ordinal = r.I32(dataOff);
        return ordinal is >= 0 and <= 500 ? ordinal : -1;
    }

    private static ItemClass Classify(bool equippable, int subKind, int weaponFamily, float armorScalar, int slot)
    {
        if (!equippable) return ItemClass.Other;
        if (subKind == 5) return ItemClass.Charm;
        if (weaponFamily >= 0) return ItemClass.Weapon;
        // weaponFamily == -1 ⇒ armor or jewelry; a real slot excludes odd
        // non-gear equippables (e.g. Essence, which has slot 0).
        if (slot <= 0) return ItemClass.Other;
        return armorScalar > 0f ? ItemClass.Armor : ItemClass.Jewelry;
    }
}
