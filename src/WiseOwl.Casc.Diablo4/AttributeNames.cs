using System;
using System.Collections.Generic;
using System.Text;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// FR-C25 — resolve a Diablo IV <c>AttributeId</c> (the raw
/// <c>eAttribute</c> int on a <see cref="NodeAttribute"/> /
/// <see cref="ParagonGlyphAffixDefinition"/>) to its in-game
/// localized name via the engine's <c>AttributeDescriptions</c>
/// StringList (sno <c>4080</c>) — the same source the tooltip
/// renderer uses.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pipeline.</b> AttributeId → label key (clean-room curated
/// map, see <see cref="LabelByAttributeId"/>) → templated body in
/// sno <c>4080</c> via the existing per-locale StringList machinery
/// → stripped name (templates removed, color tags removed,
/// whitespace normalised; <see cref="StripTemplate"/>). Examples
/// (build <c>3.0.2.71886</c>):
/// </para>
/// <list type="table">
///   <listheader><term>AttributeId</term><description>Label
///     <c>→</c> Template <c>→</c> Stripped name</description></listheader>
///   <item><term>9</term><description><c>Strength → "[{VALUE}|~|] Strength" → "Strength"</c></description></item>
///   <item><term>133</term><description><c>Hitpoints_Max_Bonus → "[{VALUE}|~|] Maximum Life" → "Maximum Life"</c></description></item>
///   <item><term>481</term><description><c>Armor_Bonus → "+[{VALUE}] Armor" → "Armor"</c></description></item>
///   <item><term>950</term><description><c>Damage_Percent_Bonus_Vs_Elites → "+[{VALUE}*100|1%|] Damage to Elites" → "Damage to Elites"</c></description></item>
/// </list>
/// <para>
/// <b>Coverage.</b> <see cref="LabelByAttributeId"/> covers every
/// <c>AttributeId</c> the Optimizer surfaced in their FR-C21 / FR-C25
/// probes plus the long-tail set observed via the
/// <c>Generic_&lt;Rarity&gt;_&lt;Token&gt;</c> node-name convention
/// (<c>SnoScan attrmap</c>; the empirical first-party observation).
/// AttributeIds not in the map return <see langword="null"/> from
/// <see cref="Diablo4Storage.GetAttributeName(int, string)"/> — honest sentinel
/// (consumer falls back to <c>"Attribute &lt;id&gt;"</c>); future
/// builds adding new attributes can extend the map without API
/// changes.
/// </para>
/// <para>
/// <b>Ambiguity.</b> Some <c>AttributeId</c> values are
/// power-budget categories shared by multiple distinct stats (e.g.
/// <c>481</c> covers Armor / ArmorPercent / DamageReduction* — the
/// CL-66 finding). The map returns the <i>primary</i> name
/// ("Armor"); the per-node disambiguation lives on
/// <see cref="ParagonNodeStat.StatName"/> via the
/// <see cref="ParagonNodeInfoBuilder"/> token fallback (the budget-
/// category sub-stat is in the node name, not in the
/// AttributeId).
/// </para>
/// </remarks>
public static class AttributeNames
{
    /// <summary>The shipped clean-room curated mapping from
    /// <c>AttributeId</c> to its primary <c>AttributeDescriptions</c>
    /// label. See <see cref="AttributeNames"/> for the source of the
    /// curation + the ambiguity note.</summary>
    public static IReadOnlyDictionary<int, string> LabelByAttributeId { get; }
        = new SortedDictionary<int, string>
        {
            // Basic-four player stats.
            { 9,  "Strength" },
            { 10, "Intelligence" },
            { 11, "Willpower" },
            { 12, "Dexterity" },

            // Element resistance (NParam carries the element variant —
            // the displayed name folds the element through {VALUE1}).
            { 79, "Resistance" },

            // Hitpoints — flat + percent variants share a "Maximum Life"
            // display name (the % variant uses Hitpoints_Max_Percent_Bonus).
            { 133, "Hitpoints_Max_Bonus" },
            { 142, "Hitpoints_Max_Percent_Bonus" },

            // Healing.
            { 65,  "Bonus_Healing_Received_Percent" },
            { 648, "Hitpoints_Regen_Per_Second" },

            // Resources.
            { 161, "Resource_Max_Bonus" },
            { 176, "Resource_Cost_Reduction_Percent_All" },
            { 187, "Resource_Gain_Bonus_Percent" },

            // Speed / cooldowns.
            { 208, "Movement_Bonus_Run_Speed" },
            { 221, "Attack_Speed_Percent_Bonus" },
            { 237, "Power_Cooldown_Reduction_Percent_All" },

            // Damage (general + crit).
            { 252, "Damage_Percent_All_From_Skills" },
            { 254, "Damage_Type_Percent_Bonus" },
            { 275, "Crit_Percent_Bonus" },
            { 288, "Crit_Damage_Percent" },

            // Defense.
            { 331, "Block_Chance_Bonus" },
            { 339, "Dodge_Chance_Bonus" },
            { 361, "CC_Duration_Reduction" },
            { 373, "Thorns_Flat" },

            // Budget category — Armor is the canonical primary name; the
            // ArmorPercent / DamageReductionFrom* / DamageReductionWhile*
            // siblings disambiguate via the node-name token on
            // ParagonNodeStat.StatName (CL-69 / CL-76).
            { 481, "Armor_Bonus" },

            // DoT damage taken/dealt.
            { 706, "DOT_DPS_Bonus_Percent_Per_Damage_Type" },
            { 708, "DOT_DPS_Bonus_Percent_Per_Damage_Type" },

            // Damage-to-* (Vulnerable / Near / Far / Low / High / etc.).
            { 735,  "Vulnerable_Health_Damage_Bonus" },
            { 1102, "Damage_Bonus_To_Near" },
            { 1104, "Damage_Bonus_To_Far" },
            { 1114, "Damage_Bonus_To_Low_Health" },
            { 1116, "Damage_Bonus_To_HIgh_Health" },
            { 1120, "Damage_Bonus_At_High_Health" },

            // Damage-on-* / Damage-while-* / Damage-to-CC.
            { 925, "Movement_Speed_Bonus_On_Elite_Kill" },
            { 926, "Damage_Bonus_On_Elite_Kill_Combined" },
            { 950, "Damage_Percent_Bonus_Vs_Elites" },
            { 954, "Damage_Percent_Bonus_Vs_CC_All" },
            { 956, "Damage_Percent_Bonus_Vs_CC_All" },
            { 959, "Damage_Percent_Bonus_Vs_CC_All" },

            // Fortify.
            { 746, "Fortified_Health_Application_Bonus" },
            { 747, "Damage_Percent_Bonus_When_Fortified" },

            // Barriers.
            { 1124, "Barrier_Bonus_Percent" },
        };

    /// <summary>
    /// FR-C27 (CL-88) — the <b>season-stable</b> mapping from a
    /// <c>ParagonNode</c> name <b>token</b> (the
    /// <c>Generic_&lt;Rarity&gt;_&lt;Token&gt;</c> suffix) to its
    /// <c>AttributeDescriptions</c> label. This is the durable half of the
    /// resolver: the raw <c>AttributeId</c> is a <b>registry ordinal</b> that
    /// the engine renumbers whenever it inserts attributes (Season 14 moved
    /// <c>Armor</c> 481→482, <c>Damage_Bonus_At_High_Health</c> 1120→1123,
    /// <c>Barrier</c> 1124→1127, …), so the id is worthless as a durable key —
    /// but the node-name <b>token</b> never changes. <see cref="Diablo4Storage"/>
    /// scans the live <c>Generic_</c> nodes at runtime to learn the
    /// <c>id → token</c> map for the current build, then this table turns the
    /// token into a label the existing <c>AttributeDescriptions</c> (sno 4080)
    /// pipeline localizes. The result auto-tracks every season's id shifts
    /// with no code change. <see cref="LabelByAttributeId"/> is retained only
    /// as a defensive fallback for ids whose token isn't scannable.
    /// </summary>
    /// <remarks>Several node tokens fold to one display label — every
    /// <c>Resistance&lt;Element&gt;</c> token resolves to <c>"Resistance"</c>
    /// (the element rides in the attribute's NParam). Tokens whose label is
    /// ambiguous or absent from the live scan are omitted (honest
    /// <see langword="null"/> over a wrong name).</remarks>
    public static IReadOnlyDictionary<string, string> LabelByToken { get; }
        = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Core stats.
            ["Str"] = "Strength",
            ["Int"] = "Intelligence",
            ["Will"] = "Willpower",
            ["Dex"] = "Dexterity",

            // Element resistance (element variant folds through NParam).
            ["ResistanceCold"] = "Resistance",
            ["ResistanceFire"] = "Resistance",
            ["ResistanceLightning"] = "Resistance",
            ["ResistancePoison"] = "Resistance",
            ["ResistanceShadow"] = "Resistance",
            ["ResistanceAll"] = "Resistance",

            // Life.
            ["HPFlat"] = "Hitpoints_Max_Bonus",
            ["HPPercent"] = "Hitpoints_Max_Percent_Bonus",
            ["HPRegen"] = "Hitpoints_Regen_Per_Second",

            // Resources.
            ["ResourceCostReduction"] = "Resource_Cost_Reduction_Percent_All",
            ["ResourceGain"] = "Resource_Gain_Bonus_Percent",

            // Speed / cooldown.
            ["MoveSpeed"] = "Movement_Bonus_Run_Speed",
            ["AttackSpeed"] = "Attack_Speed_Percent_Bonus",
            ["CDR"] = "Power_Cooldown_Reduction_Percent_All",

            // Damage (general + crit).
            ["Damage"] = "Damage_Percent_All_From_Skills",
            ["CriticalChance"] = "Crit_Percent_Bonus",
            ["CriticalDamage"] = "Crit_Damage_Percent",

            // Defense.
            ["BlockChance"] = "Block_Chance",   // the AttributeDescriptions key (the legacy id-map's "Block_Chance_Bonus" is absent from sno 4080)
            ["DodgeChance"] = "Dodge_Chance_Bonus",
            ["CCDurationReduction"] = "CC_Duration_Reduction",
            ["Thorns"] = "Thorns_Flat",
            ["Armor"] = "Armor_Bonus",

            // Conditional damage — the season-shifting tail the curated
            // id-map can't track (all carry clean Generic_ node tokens).
            ["DamageToVulnerable"] = "Vulnerable_Health_Damage_Bonus",
            ["DamageToElite"] = "Damage_Percent_Bonus_Vs_Elites",
            ["DamageToCC"] = "Damage_Percent_Bonus_Vs_CC_All",
            ["DamageToNear"] = "Damage_Bonus_To_Near",
            ["DamageToFar"] = "Damage_Bonus_To_Far",
            ["DamageToLow"] = "Damage_Bonus_To_Low_Health",
            ["DamageToHigh"] = "Damage_Bonus_To_HIgh_Health",
            ["DamageWhileHealthy"] = "Damage_Bonus_At_High_Health",

            // Fortify.
            ["BonusFortify"] = "Fortified_Health_Application_Bonus",
            ["DamageWhileFortified"] = "Damage_Percent_Bonus_When_Fortified",

            // Elite-kill on-kill bonuses.
            ["MoveSpeedEliteKill"] = "Movement_Speed_Bonus_On_Elite_Kill",
        };

    /// <summary>
    /// FR-C27 (CL-88) — the base <c>AttributeDescriptions</c> label for each
    /// compound (tag/element/resource-conditional) base <c>AttributeId</c>.
    /// These ids sit in the engine's <b>stable low range</b> (all &lt; 481;
    /// unmoved through Season 14, unlike the shifting single-id tail), so the
    /// compound resolver anchors them by id: it resolves the incoming id to
    /// its base label here, then keys <see cref="NameByCompoundLabelKey"/> on
    /// <c>(label, ParamPlus12)</c> — the label + the stable tag/element GBID
    /// are both season-durable, retiring the id from the compound key.
    /// </summary>
    public static IReadOnlyDictionary<int, string> CompoundBaseLabelById { get; }
        = new Dictionary<int, string>
        {
            { 161, "Resource_Max_Bonus" },
            { 223, "Attack_Speed_Percent_Bonus_Per_Skill_Tag" },
            { 238, "Skill_Tag_Cooldown_Reduction_Percent" },
            { 254, "Damage_Type_Percent_Bonus" },
            { 258, "Damage_Percent_Bonus_To_Targets_Affected_By_Skill_Tag" },
            { 259, "Damage_Percent_Bonus_Per_Skill_Tag" },
            { 290, "Crit_Damage_Percent_Per_Skill_Tag" },
        };

    /// <summary>FR-C28 (CL-85) — compound-key map resolving the
    /// tag-conditional attribute names where the same
    /// <see cref="LabelByAttributeId"/> entry can't disambiguate
    /// (e.g. <c>AttributeId 259</c> = <c>DamageBonusTag</c> — the same id
    /// covers Abyss / Demonology / Conjuration / Hellfire / ... damage,
    /// the per-tag identity carried in <c>ParamPlus12</c>). Keyed on the
    /// raw <c>(int AttributeId, uint ParamPlus12)</c> tuple; the value
    /// is the resolved display name as the engine renders it in the
    /// in-game tooltip (e.g. <c>(259, 0x32ABA6FB) → "Demonology
    /// Damage"</c>). Surfaced to the consumer via
    /// <see cref="Diablo4Storage.GetAttributeName(int, uint, string)"/>;
    /// <see cref="ParagonNodeInfoBuilder"/> consults it on every node
    /// stat row.
    /// </summary>
    /// <remarks>
    /// <para><b>Provenance.</b> The map is built from the empirical
    /// scan of every <c>ParagonGlyphAffix</c> (group 112) and
    /// <c>ParagonNode</c> (group 106) record on the live build
    /// <c>3.0.2.71886</c> — every observed <c>(AttributeId, ParamPlus12)</c>
    /// tuple where <c>ParamPlus12 ≠ 0xFFFFFFFF</c> (the "no parameter"
    /// sentinel). Tag GBIDs were cracked clean-room via the
    /// <c>Skill_&lt;TagName&gt;</c> DJB2-lowercase pattern (see
    /// <c>docs/d4-hash-dictionary.md</c> — 19 confirmed); uncracked
    /// tags use the empirical name derived from the per-affix /
    /// per-node sno-name convention
    /// (<c>&lt;TagName&gt;Damage_&lt;Class&gt;_&lt;Slot&gt;</c> on
    /// affixes; <c>Generic_Magic_Damage&lt;TagName&gt;</c> on nodes)
    /// — surfaced as the consumer-visible label even when the engine
    /// internal name is not yet hash-recovered.</para>
    /// <para><b>Base templates.</b> The per-AttributeId base label
    /// is determined by the engine's <c>AttributeDescriptions</c> entry
    /// (sno 4080). E.g. <c>(259, Tag)</c> uses
    /// <c>Damage_Percent_Bonus_Per_Skill_Tag = +[...] {VALUE1} Damage</c>
    /// → <c>"{Tag} Damage"</c>. <c>(290, Tag)</c> uses
    /// <c>Crit_Damage_Percent_Per_Skill_Tag</c> → <c>"{Tag} Critical
    /// Strike Damage"</c>. The map bakes the substituted enUS string
    /// directly; localisation extension is a future iteration.</para>
    /// </remarks>
    public static IReadOnlyDictionary<(int AttributeId, uint ParamPlus12), string>
        LabelByCompoundKey { get; }
        = BuildCompoundKeyMap();

    private static Dictionary<(int AttributeId, uint ParamPlus12), string>
        BuildCompoundKeyMap()
    {
        var m = new Dictionary<(int, uint), string>();

        // === AttributeId 161 (Resource_Max_Bonus, +Resource enum) ===
        // The resource-type enum (1=Fury / 5=Spirit / 6=Essence / 9=Faith
        // / 10=Wrath / 11=Dominance) — Generic_Magic_Maximum<Resource>
        // / class_Magic_<Resource> node-name convention.
        m[(161, 1)]  = "Maximum Fury";
        m[(161, 5)]  = "Maximum Spirit";
        m[(161, 6)]  = "Maximum Essence";
        m[(161, 9)]  = "Maximum Faith";
        m[(161, 10)] = "Maximum Wrath";
        m[(161, 11)] = "Maximum Dominance";

        // === AttributeId 223 (Attack_Speed_Percent_Bonus_Per_Skill_Tag) ===
        // Generic_Magic_AttackSpeed<TagName> node convention.
        m[(223, 0xACF2CA8DU)] = "Corpse Attack Speed";  // Necro_Magic_AttackSpeedCorpse
        m[(223, 0xC71462A3U)] = "Basic Attack Speed";   // *AttackSpeedBasic

        // === AttributeId 238 (Skill_Tag_Cooldown_Reduction_Percent) ===
        // Per-skill-tag cooldown reduction.
        m[(238, 0x730FE54DU)] = "Conjuration Cooldown Reduction";  // Sorc_Magic_ConjurationCooldown
        m[(238, 0x731B99DDU)] = "Subterfuge Cooldown Reduction";   // Rogue_Magic_SubterfugeCooldown
        m[(238, 0xCCA1AF65U)] = "Companion Cooldown Reduction";    // Druid_Magic_CompanionCDR
        m[(238, 0xE43A2895U)] = "Trap Cooldown Reduction";         // Rogue_Magic_TrapCooldown (Skill_Trap)
        m[(238, 0xF36D805AU)] = "Imbue Cooldown Reduction";        // Rogue_Magic_ImbueCDR
        m[(238, 0xF4EE66C7U)] = "Mobility Cooldown Reduction";     // Rogue_Magic_MobilityCooldown (Skill_Mobility)

        // === AttributeId 254 (Damage_Type_Percent_Bonus, +ElementId enum) ===
        // Generic_Magic_Damage<Element> node-name convention.
        m[(254, 0)] = "Physical Damage";
        m[(254, 1)] = "Fire Damage";
        m[(254, 2)] = "Lightning Damage";
        m[(254, 3)] = "Cold Damage";
        m[(254, 4)] = "Poison Damage";
        m[(254, 5)] = "Shadow Damage";
        m[(254, 6)] = "Holy Damage";

        // === AttributeId 258 (Damage_Percent_Bonus_To_Targets_Affected_By_Skill_Tag) ===
        // Damage to enemies affected by the named skill-tag.
        m[(258, 0xE43A2895U)] = "Damage to Trapped Enemies";  // Skill_Trap

        // === AttributeId 259 (Damage_Percent_Bonus_Per_Skill_Tag, +TagGBID) ===
        // The largest tag-conditional cluster — every "<Tag>Damage" /
        // "DamageWith<Tag>" / "<Tag>SkillDamage" affix maps here.
        //  Cracked GBIDs (Skill_<Tag> DJB2-lowercase pattern):
        m[(259, 0x6A1F0A80U)] = "Abyss Damage";        // Skill_Abyss
        m[(259, 0x32ABA6FBU)] = "Demonology Damage";   // Skill_Demonology — the FR-C28 anchor (Warlock_Rare_006)
        m[(259, 0x6D657409U)] = "Hellfire Damage";     // Skill_Hellfire
        m[(259, 0xCEAEA388U)] = "Occult Damage";       // Skill_Occult
        m[(259, 0xE43A2895U)] = "Trap Damage";         // Skill_Trap
        m[(259, 0x12674CDCU)] = "Cutthroat Damage";    // Skill_Cutthroat
        m[(259, 0xE4B9B478U)] = "Marksman Damage";     // Skill_Marksman
        m[(259, 0x6A3673AEU)] = "Blood Damage";        // Skill_Blood
        m[(259, 0xE4303EA2U)] = "Bone Damage";         // Skill_Bone
        m[(259, 0xE43BC256U)] = "Wolf Damage";         // Skill_Wolf
        m[(259, 0x6B67A5C3U)] = "Shade Damage";        // Skill_Shade
        m[(259, 0x0F479E53U)] = "Incarnate Damage";    // Skill_Incarnate
        m[(259, 0x5FCEB9D4U)] = "Grenade Damage";      // Skill_Grenade
        m[(259, 0xE87A54CDU)] = "Zealot Damage";       // Skill_Zealot
        m[(259, 0x8C8DF55AU)] = "Juggernaut Damage";   // Skill_Juggernaut
        m[(259, 0xFFFA158BU)] = "Disciple Damage";     // Skill_Disciple
        m[(259, 0xD5D1FA40U)] = "Recast Damage";       // Skill_Recast
        m[(259, 0x6625AC6BU)] = "Shapeshifting Damage";// Skill_Shapeshifting
        //  Uncracked GBIDs (empirical name from affix / node sno-name):
        m[(259, 0x945652E5U)] = "Archfiend Damage";    // ArchfiendDamage_*
        m[(259, 0x730FE54DU)] = "Conjuration Damage";  // ConjurationDamage_*
        m[(259, 0xCCA1AF65U)] = "Companion Damage";    // CompanionDamage_* / Druid_Magic_DamageCompanion
        m[(259, 0xACF2CA8DU)] = "Corpse Damage";       // DamageWithCorpse_*
        m[(259, 0x8ED92461U)] = "Earthquake Damage";   // DamageWithEarthquakes_*
        m[(259, 0x059D3AB9U)] = "Desecrated Ground Damage";  // DamageWithDesecratedGround_*
        m[(259, 0x869C223DU)] = "Dust Devil Damage";   // DamageWithDustDevils_*
        m[(259, 0x6926BA03U)] = "Ice Spike Damage";    // DamageWithIceSpikes_*
        m[(259, 0x537B93C0U)] = "Earth Damage";        // Druid_Magic_DamageEarth
        m[(259, 0x54834901U)] = "Storm Damage";        // Druid_Magic_DamageStorm
        m[(259, 0xC83AEA4DU)] = "Nature Damage";       // Druid_Magic_DamageNature
        m[(259, 0x97F199BEU)] = "Minion Damage";       // Generic_Magic_MinionDamage
        m[(259, 0xA8F1E20AU)] = "Marksmanship Damage"; // Marksmanship-class generics
        m[(259, 0x6EAA3D26U)] = "Ultimate Damage";     // Generic_Magic_DamageWithUltimates
        m[(259, 0x23C5136BU)] = "Gorilla Damage";      // GorillaDmg_*
        m[(259, 0x3AD91E1FU)] = "Jaguar Damage";       // HoneJaguarDamage_*
        m[(259, 0x9B3405AFU)] = "Eagle Damage";        // EagleSkillDamage_*
        m[(259, 0x01B4CAEFU)] = "Centipede Damage";    // CentiSkillDamage_*
        m[(259, 0xB57CAFFDU)] = "Judicator Damage";    // DamageWithJudicatorSkills_*
        m[(259, 0xDB454EEBU)] = "Lightning Spear Damage"; // DamageWithLightningSubPower_*
        m[(259, 0xE7168ED3U)] = "Lightning Spear Damage"; // (parallel entry on same affix)
        m[(259, 0x2C7E5386U)] = "Mastery Damage";      // MasteryDamage_*

        // === AttributeId 263 (Damage_Percent_Bonus_Per_Weapon_Requirement) ===
        // ParamPlus12 is a weapon-type SNO ref (group 38 ItemDefinition).
        m[(263, 0x000ED2BFU)] = "Damage with Swords";    // 971967 Sword_
        m[(263, 0x000ED2C1U)] = "Damage with Maces";     // 971969 Mace_
        m[(263, 0x000ED2C2U)] = "Damage with Axes";      // 971970 Axe_
        m[(263, 0x000ED2C4U)] = "Damage with Polearms";  // 971972 Polearm_
        m[(263, 0x00273C13U)] = "Damage with Maces";     // 2571283 — secondary mace ref

        // === AttributeId 290 (Crit_Damage_Percent_Per_Skill_Tag) ===
        m[(290, 0x537B93C0U)] = "Earth Critical Strike Damage";        // EarthCritDamage_*
        m[(290, 0x730FE54DU)] = "Conjuration Critical Strike Damage";  // CritDamageWithConjuration_*

        // === AttributeId 390 (resource on kill — class resource enum) ===
        m[(390, 1)] = "Fury On Kill";
        m[(390, 5)] = "Spirit On Kill";
        m[(390, 6)] = "Essence On Kill";

        // === AttributeId 588 (pet attack speed) ===
        m[(588, 2)] = "Golem Attack Speed";  // Necromancer_Magic_GolemAttackSpeed

        // === AttributeId 708 (DOT_DPS_Bonus_Percent_Per_Damage_Type, +DotType enum) ===
        m[(708, 0)] = "Bleeding Damage Over Time";
        m[(708, 1)] = "Burning Damage Over Time";
        m[(708, 5)] = "Shadow Damage Over Time";

        // === AttributeId 954 (Damage_Percent_Bonus_Vs_CC_Target, +CCType enum) ===
        m[(954, 2)] = "Damage to Stunned Enemies";
        m[(954, 9)] = "Damage to Chilled Enemies";  // DamageToChilled_Willpower_Side

        // === AttributeId 959 (Damage_Percent_Bonus_Against_Dot_Type, +DotType enum) ===
        m[(959, 0)] = "Damage to Bleeding Enemies";
        m[(959, 1)] = "Damage to Burning Enemies";
        m[(959, 4)] = "Damage to Poisoned Enemies";
        m[(959, 5)] = "Damage to Shadow DoT'd Enemies";

        // === AttributeId 962 (Damage_Percent_Bonus_Per_Shapeshift_Form, +Form enum) ===
        m[(962, 0)] = "Damage while in Human Form";
        m[(962, 1)] = "Damage while in Werebear Form";
        m[(962, 2)] = "Damage while in Werewolf Form";

        // === AttributeId 965 (Damage_Percent_Bonus_While_Affected_By_Power, +PowerSno) ===
        m[(965, 0x00034334U)] = "Damage while Berserk is Active";  // 213812 Barbarian_Berserk power

        // === AttributeId 981 (Pet/SubPower damage, +PowerSno) ===
        m[(981, 0x0006B668U)] = "Skeleton Warrior Damage";  // 440424 — Necromancer
        m[(981, 0x0006B7F9U)] = "Skeleton Mage Damage";     // 440825 — Necromancer
        m[(981, 0x0006D1ECU)] = "Golem Damage";             // 446956 — Necromancer
        m[(981, 0x000E0567U)] = "Crackling Energy Damage";  // 918887 — Sorcerer
        m[(981, 0x000FBDC9U)] = "Dust Devil Damage";        // 1031113 — Barbarian DustDevil
        m[(981, 0x000FBEDEU)] = "Earthquake Damage";        // 1031390 — Barbarian Earthquake
        m[(981, 0x001225CCU)] = "Stun Grenade Damage";      // 1188300 — Demon Hunter
        m[(981, 0x001D54ABU)] = "Arrow Storm Damage";       // 1922219 — Demon Hunter
        m[(981, 0x001E1DDAU)] = "Swarm Damage";             // 1973210 — Druid
        m[(981, 0x0022892CU)] = "Judgement Damage";         // 2263852 — Paladin

        // === AttributeId 991 (Power duration bonus, +PowerSno) ===
        m[(991, 0x000FBEDEU)] = "Earthquake Duration";      // 1031390 — Barbarian Earthquake

        // === AttributeId 994 (Power potency, +PowerSno) ===
        m[(994, 0x001E2309U)] = "Spirit Feather Potency";   // 1975049
        m[(994, 0x001E2E91U)] = "Circle Potency";           // 1977105
        m[(994, 0x0021FD04U)] = "Wrath Hex Damage";         // 2227460 — Warlock

        // === AttributeId 1037 (DustDevil size bonus or similar) ===
        m[(1037, 0x000FBDC9U)] = "Dust Devil Size";         // 1031113 — Barbarian DustDevil

        return m;
    }

    /// <summary>
    /// FR-C27 (CL-88) — the season-robust re-key of
    /// <see cref="LabelByCompoundKey"/> onto <c>(baseLabel, ParamPlus12)</c>.
    /// Derived at load from <see cref="LabelByCompoundKey"/> (the source of
    /// the enUS strings) + <see cref="CompoundBaseLabelById"/> (the base id →
    /// label anchor): every <c>(id, param) → name</c> entry whose base id has
    /// a known label becomes <c>(label, param) → name</c>. The compound
    /// resolver keys on this so a base-id renumber (a future season shifting
    /// e.g. 259) doesn't strand the tag-conditional names — the label + the
    /// tag/element GBID are both durable.
    /// </summary>
    public static IReadOnlyDictionary<(string BaseLabel, uint ParamPlus12), string>
        NameByCompoundLabelKey { get; } = BuildCompoundLabelMap();

    private static Dictionary<(string, uint), string> BuildCompoundLabelMap()
    {
        var m = new Dictionary<(string, uint), string>();
        foreach (var ((id, param), name) in LabelByCompoundKey)
            if (CompoundBaseLabelById.TryGetValue(id, out var label))
                m[(label, param)] = name;
        return m;
    }

    /// <summary>Strip an <c>AttributeDescriptions</c> template down
    /// to its bare display name — remove the
    /// <c>[{VALUE…|…|}]</c> placeholders, the standalone
    /// <c>{VALUE…}</c> variant tokens, the engine's color tags
    /// (<c>{c_label}</c>/<c>{/c}</c> etc.), the leading sign/bracket
    /// markup (<c>+</c>/<c>[</c>/<c>(</c>), the trailing colon, and
    /// normalise whitespace. The returned name is what the engine
    /// would render with all numeric / variant placeholders removed
    /// — e.g. <c>"+[{VALUE}*100|1%|] Damage to Elites" → "Damage to
    /// Elites"</c>.</summary>
    public static string StripTemplate(string template)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;

        var sb = new StringBuilder(template.Length);
        var i = 0;
        while (i < template.Length)
        {
            var ch = template[i];
            if (ch == '[')
            {
                // Skip the entire [...] block — they wrap value
                // placeholders.
                var close = template.IndexOf(']', i + 1);
                if (close < 0) { sb.Append(template, i, template.Length - i); break; }
                i = close + 1;
            }
            else if (ch == '{')
            {
                // Skip {VALUE…}, {c_…}, {/c} tags wholesale.
                var close = template.IndexOf('}', i + 1);
                if (close < 0) { sb.Append(template, i, template.Length - i); break; }
                i = close + 1;
            }
            else
            {
                sb.Append(ch);
                i++;
            }
        }

        // Clean up the orphan sign characters left between placeholders
        // (e.g. "Lucky Hit: Up to a + Chance to Knockback" — the `+`
        // belonged to the stripped `+[{VALUE}*100|1%|]` block; with
        // the placeholder gone the bare sign reads as noise). Match
        // sign tokens surrounded by whitespace or at edge positions.
        var raw = sb.ToString();
        raw = StripOrphanSigns(raw);
        var collapsed = CollapseWhitespace(raw);
        collapsed = collapsed.TrimStart('+', '-', ' ', '\t');
        return collapsed.Trim();
    }

    private static string StripOrphanSigns(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (ch == '+' || ch == '-')
            {
                // Orphan if (a) it's preceded by whitespace (or start of
                // string) AND (b) followed by whitespace (or end of
                // string). Both edges flag it as a placeholder leftover
                // rather than a meaningful operator.
                var prevWs = i == 0 || char.IsWhiteSpace(s[i - 1]);
                var nextWs = i == s.Length - 1 || char.IsWhiteSpace(s[i + 1]);
                if (prevWs && nextWs) continue;
            }
            sb.Append(ch);
        }
        return sb.ToString();
    }

    private static string CollapseWhitespace(string s)
    {
        var sb = new StringBuilder(s.Length);
        var inWs = false;
        foreach (var ch in s)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!inWs) { sb.Append(' '); inWs = true; }
            }
            else
            {
                sb.Append(ch);
                inWs = false;
            }
        }
        return sb.ToString();
    }
}
