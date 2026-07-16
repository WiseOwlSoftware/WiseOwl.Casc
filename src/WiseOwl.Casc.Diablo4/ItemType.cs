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
    private const int WeaponFamilyOffset = 0x30; // -1 => not a weapon
    private const int ArmorScalarOffset = 0x3C;  // 0 => jewelry, >0 => armor
    private const int SlotOffset = 0x44;         // >0 for armor/jewelry
    private const int MinLength = SnoRecord.DefaultPayloadBase + SlotOffset + 4;

    private ItemType(int snoId, string name, ItemClass @class, bool equippable, int weaponFamily)
    {
        SnoId = snoId;
        Name = name;
        Class = @class;
        IsEquippable = equippable;
        WeaponFamily = weaponFamily;
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

    /// <summary>Decode an item base type from its raw SNO blob and CoreTOC
    /// name. Malformed/short records classify as <see cref="ItemClass.Other"/>
    /// (non-equippable).</summary>
    /// <param name="snoId">The base type's SNO id.</param>
    /// <param name="name">The base type's CoreTOC name.</param>
    /// <param name="blob">The raw g98 record bytes.</param>
    public static ItemType Parse(int snoId, string name, ReadOnlySpan<byte> blob)
    {
        if (blob.Length < MinLength)
            return new ItemType(snoId, name, ItemClass.Other, false, -1);

        var r = new SnoRecord(blob);
        int kind = r.I32(KindOffset);
        int subKind = r.I32(SubKindOffset);
        int weaponFamily = r.I32(WeaponFamilyOffset);
        float armorScalar = r.F32(ArmorScalarOffset);
        int slot = r.I32(SlotOffset);

        bool equippable = kind is 32 or 48;
        ItemClass cls = Classify(equippable, subKind, weaponFamily, armorScalar, slot);
        return new ItemType(snoId, name, cls, equippable, cls == ItemClass.Weapon ? weaponFamily : -1);
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
