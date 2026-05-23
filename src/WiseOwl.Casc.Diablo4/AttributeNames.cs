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
/// <see cref="Diablo4Storage.GetAttributeName"/> — honest sentinel
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
