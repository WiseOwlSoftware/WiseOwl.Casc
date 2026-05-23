using System;
using System.Collections.Generic;
using System.Text;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// Project a raw <see cref="ParagonNodeDefinition"/> into a
/// display-ready <see cref="ParagonNodeInfo"/> for FR-C21 — evaluates
/// magnitudes, infers stat units, resolves stat names from the node
/// name convention, and produces the catalog references for the
/// shipped texture / power links.
/// </summary>
/// <remarks>
/// Builder is internal — consumers reach the projection through
/// <c>Catalog.GetNodeInfo</c>. Keeping the implementation file
/// separate from the public projection-types file
/// (<see cref="ParagonNodeInfo"/>) makes the boundary obvious.
/// </remarks>
internal static class ParagonNodeInfoBuilder
{
    /// <summary>Project a decoded node into its display-ready info.</summary>
    /// <param name="d4">Storage façade for sibling reads (power name).</param>
    /// <param name="catalog">Catalog façade for handle/SNO resolution.</param>
    /// <param name="node">The decoded node.</param>
    /// <param name="name">The node's CoreTOC name (resolved by the
    /// caller — we don't re-do the lookup here).</param>
    /// <param name="formulas">The shared
    /// <see cref="AttributeFormulaTable"/> (resolved once by the
    /// caller; the builder is called many times per board).</param>
    public static ParagonNodeInfo Build(
        Diablo4Storage d4,
        Catalog catalog,
        ParagonNodeDefinition node,
        string name,
        AttributeFormulaTable formulas)
    {
        ArgumentNullException.ThrowIfNull(d4);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(formulas);

        var kind = ClassifyKind(node);
        var icon = ResolveAtlasRef(catalog, node.HIcon);
        var iconMask = ResolveAtlasRef(catalog, node.HIconMask);
        var (power, powerName) = ResolvePower(d4, node.SnoPassivePower);

        var stats = kind is ParagonNodeKind.Start
            or ParagonNodeKind.Socket
            or ParagonNodeKind.Gate
            ? Array.Empty<ParagonNodeStat>()
            : BuildStats(catalog, node, name, formulas);

        return new ParagonNodeInfo(
            Sno: node.SnoId,
            Name: name,
            Kind: kind,
            Rarity: node.Rarity,
            Icon: icon,
            IconMask: iconMask,
            PassivePower: power,
            PassivePowerName: powerName,
            Stats: stats,
            HasSocket: node.HasSocket,
            IsGate: node.IsGate);
    }

    private static ParagonNodeKind ClassifyKind(ParagonNodeDefinition node)
    {
        if (node.IsStart) return ParagonNodeKind.Start;
        if (node.IsGate) return ParagonNodeKind.Gate;
        if (node.HasSocket) return ParagonNodeKind.Socket;
        return node.Rarity switch
        {
            ParagonRarity.Magic => ParagonNodeKind.Magic,
            ParagonRarity.Rare => ParagonNodeKind.Rare,
            ParagonRarity.Legendary => ParagonNodeKind.Legendary,
            _ => ParagonNodeKind.Normal,
        };
    }

    private static AssetRef? ResolveAtlasRef(Catalog catalog, uint handle)
    {
        if (handle == 0u || handle == 0xFFFFFFFFu) return null;
        return catalog.TryResolveHandle(handle, out var atlas, out _) ? atlas : null;
    }

    private static (AssetRef? Ref, string? Name) ResolvePower(
        Diablo4Storage d4, int snoPassivePower)
    {
        if (snoPassivePower is 0 or -1) return (null, null);
        var powerName = d4.CoreToc.GetName(SnoGroup.Power, snoPassivePower);
        if (powerName is null) return (null, null);
        // Localized display name via sibling StringList (§6.7);
        // best-effort — null when the sibling table is missing.
        string? displayName = null;
        try
        {
            var p = d4.ReadPower(snoPassivePower);
            displayName = string.IsNullOrEmpty(p.Name) ? null : p.Name;
        }
        catch (CascException)
        {
            // The power record exists in the CoreTOC but failed to
            // decode (rare; placeholders / unimplemented stubs). We
            // already have the asset reference identity from the
            // CoreTOC; the display name is the only thing we can't
            // resolve here, so leave it null.
        }
        var assetRef = new AssetRef(
            AssetKind.Power, SnoGroup.Power, snoPassivePower, powerName,
            Array.Empty<string>());
        return (assetRef, displayName);
    }

    private static ParagonNodeStat[] BuildStats(
        Catalog catalog,
        ParagonNodeDefinition node,
        string name,
        AttributeFormulaTable formulas)
    {
        var nameToken = ExtractStatToken(name);
        var attrs = node.Attributes;
        if (attrs.Count == 0) return Array.Empty<ParagonNodeStat>();
        var result = new ParagonNodeStat[attrs.Count];
        for (var i = 0; i < attrs.Count; i++)
            result[i] = BuildStat(catalog, attrs[i], nameToken, formulas);
        return result;
    }

    private static ParagonNodeStat BuildStat(
        Catalog catalog,
        NodeAttribute a,
        string? nameToken,
        AttributeFormulaTable formulas)
    {
        // Magnitude — bare-constant nodes carry an inline text like
        // "5"; multiplier-formula nodes reference a shared formula by
        // gbidFormula. Resolve to the formula text, then evaluate.
        string? formulaName = null;
        string? formulaText = null;
        AssetRef? formulaRef = null;
        if (a.IsInline)
        {
            formulaText = a.InlineFormula;
        }
        else if (formulas.TryGetNameByGbid(a.FormulaGbid, out var fn)
            && formulas.TryGetFormulaText(fn, out var ft))
        {
            formulaName = fn;
            formulaText = ft;
            formulaRef = new AssetRef(
                AssetKind.AttributeFormulas,
                SnoGroup.GameBalance,
                formulas.SnoId,
                fn,
                Array.Empty<string>());
        }

        double? flatValue = null;
        if (!string.IsNullOrEmpty(formulaText))
        {
            var v = ParagonMagnitudeFormula.Evaluate(formulaText);
            if (!double.IsNaN(v)) flatValue = v;
        }

        var statName = ResolveStatName(nameToken, a.AttributeId);
        var unit = InferUnit(nameToken, a.AttributeId, formulaText);

        return new ParagonNodeStat(
            AttributeId: a.AttributeId,
            StatName: statName,
            Variant: a.NParam,
            VariantName: null,
            FlatValue: flatValue,
            Unit: unit,
            Formula: formulaRef,
            InlineFormula: a.IsInline ? a.InlineFormula : null);
    }

    /// <summary>The trailing token of a <c>Generic_&lt;Rarity&gt;_&lt;Token&gt;</c>
    /// node name (e.g. <c>"Armor"</c>, <c>"DamageToElite"</c>,
    /// <c>"ResistanceCold"</c>). Returns <see langword="null"/> for
    /// non-Generic names (class-specific rares like
    /// <c>Warlock_Rare_006</c>).</summary>
    internal static string? ExtractStatToken(string nodeName)
    {
        if (!nodeName.StartsWith("Generic_", StringComparison.Ordinal))
            return null;
        var lastUnderscore = nodeName.LastIndexOf('_');
        return lastUnderscore > 0 ? nodeName[(lastUnderscore + 1)..] : null;
    }

    /// <summary>Humanize a stat token: split CamelCase boundaries and
    /// expand the handful of standard abbreviations. <c>"Str" → "Strength"</c>,
    /// <c>"DamageToElite" → "Damage to Elite"</c>,
    /// <c>"HPFlat" → "Max Life (Flat)"</c>.</summary>
    /// <param name="token">The trailing token from a Generic_* node
    /// name, or <see langword="null"/> for class-specific nodes.</param>
    /// <param name="attributeId">Fallback when the node name carries
    /// no stat token (class-specific rares); returned as
    /// <c>"Attribute &lt;id&gt;"</c>.</param>
    internal static string ResolveStatName(string? token, int attributeId)
    {
        if (token is null)
            return $"Attribute {attributeId}";

        // Whole-token replacements first (the abbreviations the engine
        // uses where the CamelCase split would otherwise mangle them).
        switch (token)
        {
            case "Str": return "Strength";
            case "Int": return "Intelligence";
            case "Will": return "Willpower";
            case "Dex": return "Dexterity";
            case "HPFlat": return "Max Life (Flat)";
            case "HPPercent": return "Max Life";
            case "HPRegen": return "Life Regeneration";
            case "CDR": return "Cooldown Reduction";
            case "CCDurationReduction": return "Crowd Control Duration Reduction";
            case "MoveSpeed": return "Movement Speed";
            case "AttackSpeed": return "Attack Speed";
            case "AttackSpeedBasic": return "Attack Speed (Basic Skills)";
        }

        // General CamelCase split with the prepositions kept lowercase
        // for the natural-language read (Damage TO Elite reads worse
        // than Damage to Elite).
        var humanized = HumanizeCamelCase(token);
        return humanized;
    }

    private static string HumanizeCamelCase(string token)
    {
        var sb = new StringBuilder(token.Length + 4);
        for (var i = 0; i < token.Length; i++)
        {
            var ch = token[i];
            var addSpace =
                i > 0
                && char.IsUpper(ch)
                && (char.IsLower(token[i - 1])
                    || (i + 1 < token.Length && char.IsLower(token[i + 1])));
            if (addSpace) sb.Append(' ');
            sb.Append(ch);
        }
        var split = sb.ToString();

        // Lowercase the small connecting words for natural reading.
        // "Damage To Elite" → "Damage to Elite";
        // "Damage Reduction From Vulnerable" → "Damage Reduction from Vulnerable".
        split = ReplaceWord(split, " To ", " to ");
        split = ReplaceWord(split, " From ", " from ");
        split = ReplaceWord(split, " With ", " with ");
        split = ReplaceWord(split, " While ", " while ");
        return split;
    }

    private static string ReplaceWord(string s, string from, string to) =>
        s.Contains(from, StringComparison.Ordinal)
            ? s.Replace(from, to, StringComparison.Ordinal)
            : s;

    /// <summary>Infer the display unit. Token-based heuristic: pure
    /// player-stat tokens (Str/Int/Will/Dex), the <c>HPFlat</c> raw-HP
    /// token, all <c>Resistance*</c> raw-points stats, the <c>Thorns</c>
    /// raw-damage stat, and Normal-rarity nodes (bare numeric constant)
    /// render as <see cref="StatUnit.Flat"/>; everything else
    /// — the budget-multiplied magnitudes that the in-game tooltip
    /// shows with a <c>%</c> sign — renders as <see cref="StatUnit.Percent"/>.</summary>
    internal static StatUnit InferUnit(
        string? token, int attributeId, string? formulaText)
    {
        // Bare numeric constant (Normal-rarity) ⇒ Flat. Detect by the
        // absence of an identifier (no letters) in the formula text.
        if (!string.IsNullOrEmpty(formulaText) &&
            !ContainsLetter(formulaText)) return StatUnit.Flat;

        if (token is not null)
        {
            // ResistanceMax* (the cap) is the percent exception within
            // the otherwise-Flat Resistance family — check it first.
            if (token.StartsWith("ResistanceMax", StringComparison.Ordinal))
                return StatUnit.Percent;
            if (token.StartsWith("Resistance", StringComparison.Ordinal))
                return StatUnit.Flat;

            switch (token)
            {
                // Player stats — raw points.
                case "Str":
                case "Int":
                case "Will":
                case "Dex":
                // Raw-HP (the "Flat" suffix is the engine's marker).
                case "HPFlat":
                case "Thorns":
                case "Essence":
                case "MaximumWrath":
                case "MaximumDominance":
                case "HealingBonus":
                case "BonusFortify":
                    return StatUnit.Flat;
            }

            // Token present but doesn't match any Flat pattern — Percent.
            // (No AttributeId fallback needed; the token is authoritative
            // when it exists.)
            return StatUnit.Percent;
        }

        // Token absent (class-specific rares). Lean on the AttributeId for
        // the player-stat / Resistance / HP-flat / resource-max / Thorns
        // budget categories; everything else is Percent.
        switch (attributeId)
        {
            case 9 or 10 or 11 or 12:
            case 79:                                // Resistance
            case 133:                               // HP (flat)
            case 161:                               // primary resource max
            case 373:                               // Thorns
                return StatUnit.Flat;
        }

        return StatUnit.Percent;
    }

    private static bool ContainsLetter(string s)
    {
        foreach (var c in s)
            if (char.IsLetter(c)) return true;
        return false;
    }
}
