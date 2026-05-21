using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// A decoded Diablo IV <c>PowerDefinition</c> (<c>.pow</c>, SNO group
/// <see cref="SnoGroup.Power"/> = 29) — a skill/passive/legendary power.
/// Identity + localized text + the structured Script Formula slot table
/// (FR-C13 Phase 1); the engine-internal AST evaluator that resolves
/// non-trivial SF_N expressions to runtime values is the consumer's
/// stat-effect model per the library boundary (Appendix C). FR-C13
/// Phase 2 (forthcoming) lifts the trivial-numeric resolution into the
/// library; until then, expression-typed slots (<see cref="PowerScriptFormula.IsExpression"/>)
/// carry their raw text form (e.g. <c>"SF_1 / 3"</c>) for consumer-side
/// evaluation.
/// </summary>
/// <remarks>
/// <see cref="SnoId"/> is the binary field (payload <c>0</c>). The
/// localized <see cref="Name"/> / <see cref="Description"/> are resolved
/// from the power's <b>sibling StringList table</b> via the generalized
/// sibling-string convention (<c>docs/casc-diablo4-format.md §11.2</c>,
/// Appendix A CL-22 / CL-20): group-42 SNO <c>"Power_" + snoName</c>,
/// labels <c>name</c> / <c>desc</c>. Empty (honest sentinel) when decoded
/// byte-only (no <see cref="CoreToc"/>) or when the power has no sibling
/// table; the consumer owns any fallback. Raw decoded text — D4 markup
/// kept intact.
/// </remarks>
public sealed class PowerDefinition
{
    private PowerDefinition(
        int snoId,
        IReadOnlyList<PowerScriptFormula> scriptFormulas,
        IReadOnlyDictionary<string, double> resolvedFormulas,
        IReadOnlyDictionary<string, double> compiledFormulas,
        IReadOnlyDictionary<int, IReadOnlyList<float>> embeddedLiteralsBySlot)
    {
        SnoId = snoId;
        ScriptFormulas = scriptFormulas;
        ResolvedFormulas = resolvedFormulas;
        CompiledFormulas = compiledFormulas;
        _embeddedLiteralsBySlot = embeddedLiteralsBySlot;
    }

    private readonly IReadOnlyDictionary<int, IReadOnlyList<float>> _embeddedLiteralsBySlot;

    /// <summary>The power's own SNO id (== the CoreTOC id).</summary>
    public int SnoId { get; }

    /// <summary>Localized power name (sibling label <c>name</c>), or
    /// <see cref="string.Empty"/> if unresolved. See the type remarks.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Localized power description (sibling label <c>desc</c>;
    /// carries D4 markup/substitution tokens), or
    /// <see cref="string.Empty"/>.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// FR-C13 Phase 1 — the positional Script Formula slot table decoded
    /// from the Power record's tail data. Each entry is one
    /// <c>DT_STRING_FORMULA</c>'s compiled form: a text form (numeric
    /// like <c>"0.02"</c> or an arithmetic expression like
    /// <c>"SF_1 / 3"</c>) and the IEEE-754 literal value (the engine's
    /// pre-computed scalar when the text is numeric; <see cref="float.NaN"/>
    /// when the text is an expression). Indexed positionally:
    /// <see cref="PowerScriptFormula.Index"/> matches the
    /// <c>[SF_<i>n</i>...]</c> placeholders in <see cref="Description"/>
    /// (the engine's format-string SF_N indices).
    /// <br/><br/>
    /// Empty when the power has no script formulas (most active skills
    /// and many structural / informational powers fall here). Non-empty
    /// for the 72 legendary node powers + others using the
    /// <c>DT_STRING_FORMULA</c> mechanism. The trailing sentinel value
    /// (<c>"10"</c> / 10.0 followed by <c>"0"</c> / 0.0 in every
    /// anchored legendary) is intentionally not surfaced — it is an
    /// engine-internal max-rank marker, not an SF_N value.
    /// </summary>
    public IReadOnlyList<PowerScriptFormula> ScriptFormulas { get; }

    /// <summary>
    /// FR-C13 Phase 2 — the resolved per-power <c>{SF_<i>N</i> →
    /// value}</c> map. Each key is <c>"SF_<i>N</i>"</c> (positionally
    /// matching the engine's <c>[SF_<i>n</i>...]</c> placeholders in
    /// <see cref="Description"/>); each value is the slot's resolved
    /// double — the IEEE-754 single-precision literal for trivial-
    /// numeric slots (<see cref="PowerScriptFormula.LiteralValue"/>
    /// promoted to <see cref="double"/>) OR the evaluator's result for
    /// expression-text slots (e.g. Demonic Spicules's
    /// <c>"SF_1 / 3"</c> slot resolves to <c>60 / 3 = 20</c>).
    /// <br/><br/>
    /// Expression slots can reference other slots' values by SF_N;
    /// resolution iterates until a fixed point is reached or an
    /// unresolvable reference forces <see cref="double.NaN"/> for that
    /// slot. Function calls inside slot expressions (rare on legendary
    /// node powers; common on Barbarian / Necromancer active skills)
    /// surface as <see cref="FunctionRefs"/> alongside the resolved
    /// value (the call returns <see cref="double.NaN"/> until the
    /// consumer registers a resolver).
    /// <br/><br/>
    /// Empty when the power has no <see cref="ScriptFormulas"/>.
    /// </summary>
    public IReadOnlyDictionary<string, double> ResolvedFormulas { get; }

    /// <summary>
    /// FR-C13 Phase 3 — the engine's compiled-form <c>{SF_<i>N</i> →
    /// value}</c> map, derived from the binary <c>DT_STRING_FORMULA</c>
    /// records' IEEE-754 floats (for literal slots) and from binary AST
    /// opcode operands (for expression slots). The values are the
    /// engine-truth: the IEEE-754 singles the engine literally executes,
    /// promoted to <see cref="double"/>.
    /// <br/><br/>
    /// Distinct from <see cref="ResolvedFormulas"/> only when the engine
    /// has compiled a slot's text form (e.g. <c>"0.02"</c> →
    /// <c>0.02f</c>) inconsistently — in that case the binary IEEE-754
    /// single is canonical and this map reflects it, while
    /// <see cref="ResolvedFormulas"/> reflects the text-parsed value.
    /// In normal operation the two maps agree to float precision for all
    /// 9 Warlock legendary anchors (the FR-C13 R5 regression gate).
    /// <br/><br/>
    /// For expression slots (Demonic Spicules's <c>SF_2 = "SF_1 / 3"</c>
    /// is the canonical anchor): the operator is parsed from the slot's
    /// text, but numeric operands are substituted from the binary AST
    /// opcode region's embedded IEEE-754 singles in encounter order.
    /// So Demonic Spicules's <c>SF_2</c> evaluates as
    /// <c>SF_1 / binary_literal[0]</c> = <c>60 / 3.0f</c> = <c>20</c>;
    /// the <c>3.0f</c> comes from the compiled record's binary bytes,
    /// not from re-parsing the text <c>"3"</c>.
    /// <br/><br/>
    /// AST opcode encoding for non-literal sub-trees (e.g. function
    /// calls inside expression records) is not yet fully decoded
    /// (devlog 0035 §3). For powers whose compiled AST encoding is
    /// outside the current decoder's scope, the slot is left at
    /// <see cref="double.NaN"/> — the consumer compares non-NaN entries
    /// for cross-validation.
    /// </summary>
    public IReadOnlyDictionary<string, double> CompiledFormulas { get; }

    /// <summary>
    /// FR-C13 Phase 2 — engine-function references the power's
    /// formulas + localized <see cref="Description"/> rely on. The
    /// library surfaces each call (name + resolved-arg values)
    /// structurally; the consumer registers a per-name resolver to
    /// substitute the engine-runtime value (player-state accessors
    /// like <c>PlayerHealthMax()</c> are outside CASC's domain).
    /// Empty for the typical legendary passive (no function calls in
    /// the format string); non-empty for powers like Barbarian
    /// <c>Warbringer</c> whose format uses
    /// <c>[SF_1 * PlayerHealthMax()]</c>.
    /// </summary>
    public IReadOnlyList<PowerFunctionRef> FunctionRefs { get; private set; } =
        Array.Empty<PowerFunctionRef>();

    /// <summary>Decode a Power from its raw SNO blob. Identity +
    /// <see cref="ScriptFormulas"/> + <see cref="ResolvedFormulas"/>;
    /// the localized fields (and the <see cref="FunctionRefs"/> scan,
    /// which reads them) need <see cref="CoreToc"/>; use
    /// <see cref="Diablo4Storage.ReadPower(int,string)"/>.</summary>
    public static PowerDefinition Parse(ReadOnlySpan<byte> blob)
    {
        var snoId = new SnoRecord(blob).SnoId;
        var (formulas, embeddedLits) = DecodeScriptFormulas(blob);
        var resolved = ResolveScriptFormulas(formulas);
        var compiled = ResolveCompiledFormulas(formulas, embeddedLits);
        return new PowerDefinition(snoId, formulas, resolved, compiled, embeddedLits);
    }

    internal void SetStrings(string name, string description)
    {
        Name = name;
        Description = description;
        FunctionRefs = ScanFunctionRefs(description, ResolvedFormulas);
    }

    /// <summary>
    /// FR-C13 Phase 2 — resolve the positional <see cref="ScriptFormulas"/>
    /// slot table into a <c>SF_N → double</c> map. Trivial-numeric slots
    /// (text matches a number literal, <see cref="PowerScriptFormula.IsExpression"/>
    /// = <see langword="false"/>) promote their <see cref="PowerScriptFormula.LiteralValue"/>
    /// directly. Expression-text slots (e.g. Demonic Spicules's
    /// <c>"SF_1 / 3"</c>) are evaluated through
    /// <see cref="PowerScriptFormulaEvaluator"/> against the other
    /// slots' resolved values; iterative passes resolve forward
    /// dependencies, and circular / unresolvable references collapse
    /// to <see cref="double.NaN"/> for that slot.
    /// </summary>
    private static IReadOnlyDictionary<string, double> ResolveScriptFormulas(
        PowerScriptFormula[] slots)
    {
        if (slots.Length == 0) return EmptyResolvedFormulas;

        // Build the lookup by SF_N name. Start each slot at NaN
        // (unresolved); iterate to fixed point.
        var resolved = new Dictionary<string, double>(slots.Length);
        var pending = new Dictionary<int, string>(slots.Length);
        foreach (var slot in slots)
        {
            var key = "SF_" + slot.Index.ToString(CultureInfo.InvariantCulture);
            if (!slot.IsExpression)
            {
                // Trivial-numeric: the float was the compiled-form
                // literal; promote to double directly.
                resolved[key] = slot.LiteralValue;
            }
            else
            {
                resolved[key] = double.NaN;
                pending[slot.Index] = slot.Text;
            }
        }

        // Iterative resolution. Up to N passes; on each pass attempt to
        // evaluate every pending slot — if the result is non-NaN AND
        // didn't trigger a function call (no resolver here at parse
        // time), commit it. Stop early when no progress was made.
        for (int pass = 0; pass < slots.Length && pending.Count > 0; pass++)
        {
            var progressed = false;
            foreach (var idx in new List<int>(pending.Keys))
            {
                var text = pending[idx];
                double Lookup(int slot)
                {
                    var k = "SF_" + slot.ToString(CultureInfo.InvariantCulture);
                    return resolved.TryGetValue(k, out var v) ? v : double.NaN;
                }

                EvaluationResultLocal r;
                try { r = EvalSafe(text, Lookup); }
                catch { continue; }
                if (!double.IsNaN(r.Value))
                {
                    var k = "SF_" + idx.ToString(CultureInfo.InvariantCulture);
                    resolved[k] = r.Value;
                    pending.Remove(idx);
                    progressed = true;
                }
            }
            if (!progressed) break;
        }

        return resolved;
    }

    private static readonly IReadOnlyDictionary<string, double> EmptyResolvedFormulas =
        new Dictionary<string, double>(0);

    /// <summary>
    /// FR-C13 Phase 3 — resolve <see cref="CompiledFormulas"/> from the
    /// per-slot binary form. For literal slots the IEEE-754 single read
    /// from the compiled record is promoted to <see cref="double"/>
    /// directly. For expression slots the operator is parsed from the
    /// text but numeric operands are substituted from
    /// <paramref name="embeddedLiteralsBySlot"/> in left-to-right
    /// encounter order; SF_N references resolve against this same map
    /// (iterative fixed-point), so chains like Demonic Spicules's
    /// <c>SF_2 = "SF_1 / 3"</c> evaluate as <c>SF_1 / binary_lit[0]</c>
    /// (where <c>binary_lit[0]</c> is the IEEE-754 single read from the
    /// compiled AST's embedded operand at offset +40 of the expression
    /// record). When the binary literal list is empty (the expression's
    /// operands aren't literals — e.g. function calls), falls back to
    /// the text-parsed literal so cross-validation still proceeds for
    /// the literal-only operand sub-trees.
    /// </summary>
    private static IReadOnlyDictionary<string, double> ResolveCompiledFormulas(
        PowerScriptFormula[] slots,
        IReadOnlyDictionary<int, IReadOnlyList<float>> embeddedLiteralsBySlot)
    {
        if (slots.Length == 0) return EmptyResolvedFormulas;

        var compiled = new Dictionary<string, double>(slots.Length);
        var pending = new Dictionary<int, string>(slots.Length);
        foreach (var slot in slots)
        {
            var key = "SF_" + slot.Index.ToString(CultureInfo.InvariantCulture);
            if (!slot.IsExpression)
            {
                // Binary IEEE-754 single read from the slot record at
                // either +8 (Layout A) or +12 (Layout B/C) — this is the
                // engine-stored literal, not a text re-parse.
                compiled[key] = slot.LiteralValue;
            }
            else
            {
                compiled[key] = double.NaN;
                pending[slot.Index] = slot.Text;
            }
        }

        // Iterative fixed-point pass — same shape as Phase 2's
        // ResolveScriptFormulas but the evaluator substitutes binary
        // embedded literals for numeric sub-trees.
        for (int pass = 0; pass < slots.Length && pending.Count > 0; pass++)
        {
            var progressed = false;
            foreach (var idx in new List<int>(pending.Keys))
            {
                var text = pending[idx];
                double Lookup(int slot)
                {
                    var k = "SF_" + slot.ToString(CultureInfo.InvariantCulture);
                    return compiled.TryGetValue(k, out var v) ? v : double.NaN;
                }

                embeddedLiteralsBySlot.TryGetValue(idx, out var embLits);
                EvaluationResultLocal r;
                try { r = EvalSafeWithBinaryLiterals(text, Lookup, embLits); }
                catch { continue; }
                if (!double.IsNaN(r.Value))
                {
                    var k = "SF_" + idx.ToString(CultureInfo.InvariantCulture);
                    compiled[k] = r.Value;
                    pending.Remove(idx);
                    progressed = true;
                }
            }
            if (!progressed) break;
        }

        return compiled;
    }

    private static EvaluationResultLocal EvalSafeWithBinaryLiterals(
        string expression,
        Func<int, double> slotLookup,
        IReadOnlyList<float>? binaryLiterals)
    {
        var r = PowerScriptFormulaEvaluator.EvaluateWithBinaryLiterals(
            expression, slotLookup, binaryLiterals);
        return new EvaluationResultLocal(r.Value);
    }

    /// <summary>Local result wrapper for the slot-resolution loop —
    /// <see cref="PowerScriptFormulaEvaluator"/> returns its public
    /// type, but this internal pass needs only the numeric result and
    /// discards the function-refs collected during slot evaluation
    /// (those surface via the <see cref="ScanFunctionRefs"/> path over
    /// the Description format string instead).</summary>
    private readonly record struct EvaluationResultLocal(double Value);

    private static EvaluationResultLocal EvalSafe(
        string expression, Func<int, double> slotLookup)
    {
        var r = PowerScriptFormulaEvaluator.Evaluate(expression, slotLookup);
        return new EvaluationResultLocal(r.Value);
    }

    /// <summary>
    /// FR-C13 Phase 2 — scan the localized <see cref="Description"/>
    /// for <c>[expression|formatter|]</c> placeholders and surface any
    /// engine-function calls they contain as
    /// <see cref="PowerFunctionRef"/>. The format string syntax uses
    /// square brackets to wrap an expression + optional formatter
    /// directive (e.g. <c>[SF_0*100|%x|]</c>); within an expression a
    /// function call appears as <c>Identifier()</c> with zero or more
    /// arguments. Scanning is best-effort: malformed brackets or
    /// formatter-only text inside brackets is skipped.
    /// </summary>
    private static IReadOnlyList<PowerFunctionRef> ScanFunctionRefs(
        string description,
        IReadOnlyDictionary<string, double> resolved)
    {
        if (string.IsNullOrEmpty(description)) return Array.Empty<PowerFunctionRef>();
        var refs = new List<PowerFunctionRef>();

        double Lookup(int slot)
        {
            var k = "SF_" + slot.ToString(CultureInfo.InvariantCulture);
            return resolved.TryGetValue(k, out var v) ? v : double.NaN;
        }

        // Format-string placeholders take the shape `[expression|formatter|]`
        // (or just `[expression]`). The formatter is `|...|` after the
        // expression and is consumer-side concern; the library evaluates
        // only the expression part. The expression may contain SF refs,
        // numerics, operators, parens, and function calls.
        var seenFunctions = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in FormatPlaceholderRegex.Matches(description))
        {
            var inside = m.Groups[1].Value;
            var pipe = inside.IndexOf('|');
            var expr = pipe < 0 ? inside : inside.Substring(0, pipe);
            if (string.IsNullOrWhiteSpace(expr)) continue;
            // Only scan expressions that contain a probable function
            // call (an identifier followed by '('). The cheap upfront
            // filter avoids spending the parser on every SF_N-only
            // placeholder.
            if (!FunctionCallProbeRegex.IsMatch(expr)) continue;
            try
            {
                var r = PowerScriptFormulaEvaluator.Evaluate(expr, Lookup);
                foreach (var fr in r.FunctionRefs)
                {
                    if (seenFunctions.Add(fr.Name + "(" + fr.Args.Count + ")"))
                        refs.Add(fr);
                }
            }
            catch
            {
                // Malformed expression — skip this placeholder; library
                // is best-effort, no fabrication.
            }
        }
        return refs.Count == 0 ? Array.Empty<PowerFunctionRef>() : (IReadOnlyList<PowerFunctionRef>)refs;
    }

    private static readonly Regex FormatPlaceholderRegex =
        new Regex(@"\[([^\]]+)\]", RegexOptions.Compiled);

    private static readonly Regex FunctionCallProbeRegex =
        new Regex(@"[A-Za-z_][A-Za-z0-9_]*\s*\(", RegexOptions.Compiled);

    /// <summary>
    /// Decode the per-Power Script Formula slot table from the tail data
    /// of the Power blob.
    /// <br/><br/>
    /// Each slot is one <c>DT_STRING_FORMULA</c>'s compiled form — a
    /// 16-byte record carrying the literal text (a 4-byte ASCII chunk,
    /// possibly padded with trailing nulls), a type tag (<c>0x06</c> for
    /// numeric literals), an IEEE-754 float, and a 4-byte padding /
    /// alignment field. The structure has two observed layouts across
    /// the 72 legendary Power blobs:
    /// <list type="bullet">
    /// <item><b>Layout A</b> (most common — Pyrosis, Fathomless, Overmind,
    /// Ritualism, Dynamism, Dominion, …): <c>[ascii@0..3][type=0x06@4..7][float@8..11][pad@12..15]</c>.</item>
    /// <item><b>Layout B</b> (Demonic Spicules, Greater Hex, …):
    /// <c>[ascii@0..3][pad@4..7][type=0x06@8..11][float@12..15]</c> — the
    /// type and pad fields are swapped (and the ASCII chunk is 4 chars
    /// of a longer null-terminated string).</item>
    /// </list>
    /// Both layouts share the diagnostic that the type tag is
    /// <c>0x00000006</c> and exactly one of the float-bearing positions
    /// holds an IEEE-754 single. The decoder accepts either layout per
    /// record (records within the same Power may differ — Demonic
    /// Spicules's <c>"SF_1 / 3"</c> expression record uses a different
    /// inner shape than its <c>"0.02"</c> trivial record).
    /// <br/><br/>
    /// The slot table is the LAST contiguous run of 16-byte slot records
    /// in the blob, terminated by a <c>("0", 0.0)</c> record. The
    /// trailing <c>"10"</c> sentinel (engine max-rank marker, present
    /// identically on every anchored legendary) is stripped from the
    /// returned list.
    /// </summary>
    private static (PowerScriptFormula[] Slots,
        IReadOnlyDictionary<int, IReadOnlyList<float>> EmbeddedLiterals) DecodeScriptFormulas(
        ReadOnlySpan<byte> blob)
    {
        // Anchor on the ("0", 0.0) terminator record (Layout A: a clean
        // 16-byte block with bytes [0x30 0 0 0 | 0x06 0 0 0 | 0 0 0 0 | 0 0 0 0]).
        // Scan for the LAST such anchor in the blob, then walk BACKWARD
        // decoding records (literal 16/20-byte or Phase 3 expression
        // 48-byte). The 4-byte pad following an expression record
        // explains the 52-byte backward stride.
        int? terminatorOff = null;
        for (int off = blob.Length - 16; off >= 0; off -= 4)
        {
            if (off + 16 > blob.Length) continue;
            if (IsTerminatorRecord(blob, off))
            {
                terminatorOff = off;
                break;
            }
        }
        if (terminatorOff is null)
            return (Array.Empty<PowerScriptFormula>(), EmptyEmbeddedLiterals);

        // Walk BACKWARD decoding each record. Each step accepts a
        // literal record (16 or 20 bytes) OR a Phase 3 expression
        // record (48 bytes). Stride tried in order: -16, -20, -52.
        // Collects in REVERSE positional order; reverse before
        // returning.
        var records = new List<(string Text, float Value, IReadOnlyList<float> Embedded)>();
        int cur = terminatorOff.Value;
        while (cur >= 0)
        {
            if (TryReadSlotRecord(blob, cur, out var text, out var value))
            {
                records.Add((text, value, Array.Empty<float>()));
            }
            else if (TryReadExpressionRecord(blob, cur, out var exprText, out var exprEmbedded))
            {
                records.Add((exprText, float.NaN, exprEmbedded));
            }
            else
            {
                break;
            }

            // Look-ahead: try literal strides first (16, 20), then the
            // expression-record stride (52). The 52-byte stride MUST
            // land on an actual expression-record start (type=0x05) —
            // accepting a literal at -52 would jump past the real slot
            // region into early-tail blocks (Pyrosis et al. have runs
            // of literal records preceding the slot table that aren't
            // SF_N entries).
            if (cur - 16 >= 0 && TryReadAnyRecord(blob, cur - 16))
                cur -= 16;
            else if (cur - 20 >= 0 && TryReadAnyRecord(blob, cur - 20))
                cur -= 20;
            else if (cur - 52 >= 0 && TryReadExpressionRecord(blob, cur - 52, out _, out _))
                cur -= 52;
            else
                break;
        }
        records.Reverse();

        // Strip the trailing ("0", 0.0) terminator + ALL trailing
        // ("10", 10.0) sentinels (every anchored legendary has at least
        // one "10" sentinel; some — like Greater Hex — repeat it; powers
        // without these just lose the trim).
        int end = records.Count;
        if (end >= 1 && records[end - 1].Text == "0" && records[end - 1].Value == 0.0f)
            end--;
        while (end >= 1 && records[end - 1].Text == "10" && records[end - 1].Value == 10.0f)
            end--;

        var result = new PowerScriptFormula[end];
        Dictionary<int, IReadOnlyList<float>>? embDict = null;
        for (int i = 0; i < end; i++)
        {
            result[i] = new PowerScriptFormula(i, records[i].Text, records[i].Value);
            if (records[i].Embedded.Count > 0)
            {
                embDict ??= new Dictionary<int, IReadOnlyList<float>>();
                embDict[i] = records[i].Embedded;
            }
        }
        return (result, embDict ?? EmptyEmbeddedLiterals);
    }

    private static readonly IReadOnlyDictionary<int, IReadOnlyList<float>> EmptyEmbeddedLiterals =
        new Dictionary<int, IReadOnlyList<float>>(0);

    /// <summary>Look-ahead probe — returns <see langword="true"/> if a
    /// literal OR expression record starts at the given offset. Used by
    /// the backward-walk look-ahead to decide which stride to advance.</summary>
    private static bool TryReadAnyRecord(ReadOnlySpan<byte> blob, int off) =>
        TryReadSlotRecord(blob, off, out _, out _) ||
        TryReadExpressionRecord(blob, off, out _, out _);

    /// <summary>
    /// FR-C13 Phase 3 — try to interpret a 48-byte run at
    /// <paramref name="off"/> as a COMPILED EXPRESSION record. The
    /// canonical anchor is Demonic Spicules's
    /// <c>SF_2 = "SF_1 / 3"</c> slot, whose 48-byte structure is:
    /// <code>
    ///   +0..3   pad = 0
    ///   +4..15  ASCII text (NULL-terminated within 12 bytes — e.g.
    ///           "SF_1 / 3\0\0\0\0")
    ///   +16..19 type tag = 0x05 (expression marker)
    ///   +20..23 opcode marker (observed = 7 on the anchor; not yet
    ///           decoded — treated as opaque)
    ///   +24..35 pad = 0
    ///   +36..39 type tag = 0x06 (embedded-literal marker)
    ///   +40..43 IEEE-754 single — the binary operand value
    ///   +44..47 trailing opcode marker (observed = 0x0E on the anchor;
    ///           opaque)
    /// </code>
    /// The text gives the operator tree (consumed by
    /// <see cref="PowerScriptFormulaEvaluator"/>); the IEEE-754 single
    /// at <c>off+40</c> is the engine-truth operand bound to the FIRST
    /// numeric literal encountered during left-to-right evaluation
    /// (Demonic Spicules's <c>3</c> in <c>"SF_1 / 3"</c>). Returns the
    /// text + the singleton embedded-literal list on match.
    /// </summary>
    private static bool TryReadExpressionRecord(
        ReadOnlySpan<byte> blob, int off,
        out string text, out IReadOnlyList<float> embeddedLiterals)
    {
        text = string.Empty;
        embeddedLiterals = Array.Empty<float>();
        if (off < 0 || off + 48 > blob.Length) return false;

        // First 4 bytes must be zero (the leading pad).
        if (ReadU32(blob, off) != 0u) return false;

        // Type-tag check — 0x05 expression marker at +16, 0x06
        // embedded-literal marker at +36.
        if (ReadU32(blob, off + 16) != 0x05u) return false;
        if (ReadU32(blob, off + 36) != 0x06u) return false;

        // ASCII text must start with a printable byte at +4 and be
        // NULL-terminated by +16. Demonic Spicules's "SF_1 / 3" is 8
        // chars + 4 nulls; allow 1..12 chars to leave room for the
        // type tag at +16.
        var b4 = blob[off + 4];
        if (b4 < 0x20 || b4 >= 0x7F) return false;
        var sb = new StringBuilder(12);
        for (int i = 4; i < 16; i++)
        {
            var c = blob[off + i];
            if (c == 0) break;
            if (c < 0x20 || c >= 0x7F) return false;
            sb.Append((char)c);
        }
        if (sb.Length == 0) return false;
        text = sb.ToString();

        // Embedded literal — IEEE-754 single at +40.
        var litU = ReadU32(blob, off + 40);
        var lit = BitConverter.ToSingle(BitConverter.GetBytes(litU), 0);
        embeddedLiterals = new[] { lit };
        return true;
    }

    /// <summary>The ("0", 0.0) terminator record marks the end of the
    /// Script Formula slot table on every anchored legendary. Detected
    /// as: bytes 0..3 = "0\0\0\0" (ASCII "0" + nulls), bytes 4..7 = type
    /// tag 0x06, bytes 8..11 = 0.0f, bytes 12..15 = 0 (pad).</summary>
    private static bool IsTerminatorRecord(ReadOnlySpan<byte> blob, int off)
    {
        if (off + 16 > blob.Length) return false;
        return blob[off] == 0x30 && blob[off + 1] == 0 && blob[off + 2] == 0 && blob[off + 3] == 0 &&
               ReadU32(blob, off + 4) == 0x06u &&
               ReadU32(blob, off + 8) == 0u &&
               ReadU32(blob, off + 12) == 0u;
    }

    private static uint ReadU32(ReadOnlySpan<byte> b, int o) =>
        (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));

    /// <summary>Try to interpret 16 bytes at <paramref name="off"/> as a
    /// slot record (compiled <c>DT_STRING_FORMULA</c> with type tag
    /// 0x06). Two layouts observed across the 72 legendary Power blobs:
    /// <list type="bullet">
    /// <item><b>Layout A</b> (Pyrosis, Fathomless, Overmind, Ritualism,
    /// Dynamism, Dominion, …): <c>[ascii@0..3 (1..3 chars + null)]
    /// [type=0x06@4..7][float@8..11][pad=0@12..15]</c>.</item>
    /// <item><b>Layout B</b> (Greater Hex, Demonic Spicules's literal
    /// slots, …): <c>[ascii@0..3 (4 chars, no null)][pad=0@4..7]
    /// [type=0x06@8..11][float@12..15]</c> — the longer 4-character
    /// ASCII text needs the full 4 bytes; the engine pads with a
    /// 4-byte zero before the type tag and float.</item>
    /// </list>
    /// Layouts are disambiguated by the ASCII chunk's length: a null
    /// byte within the 4-byte chunk ⇒ Layout A (1..3 chars); all 4
    /// bytes printable ⇒ Layout B. On match, returns the decoded text
    /// (trimmed of trailing nulls) and the IEEE-754 float; otherwise
    /// returns false.</summary>
    private static bool TryReadSlotRecord(
        ReadOnlySpan<byte> blob, int off,
        out string text, out float value)
    {
        text = string.Empty;
        value = 0;
        if (off + 16 > blob.Length) return false;
        var b = blob.Slice(off, 16);

        // Layout A: ASCII@+0..3 (1..3 chars + null), type@+4, float@+8, pad@+12.
        var tA = ReadAscii4(b, 0);
        if (tA.Length is >= 1 and <= 3 &&
            ReadU32(b, 4) == 0x06u && ReadU32(b, 12) == 0u)
        {
            text = tA;
            value = BitConverter.ToSingle(BitConverter.GetBytes(ReadU32(b, 8)), 0);
            return true;
        }

        // Layout B: ASCII@+0..3 (exactly 4 chars, no null), pad@+4, type@+8, float@+12.
        if (tA.Length == 4 && ReadU32(b, 4) == 0u && ReadU32(b, 8) == 0x06u)
        {
            text = tA;
            value = BitConverter.ToSingle(BitConverter.GetBytes(ReadU32(b, 12)), 0);
            return true;
        }

        // Layout C: pad@+0..3 (zeros), ASCII@+4..7, type@+8, float@+12.
        // Observed on Greater Hex's sentinel/terminator records (where
        // the ASCII chunk is in the second 4-byte slot rather than the
        // first), and likely on a similar pattern across other powers.
        if (ReadU32(b, 0) == 0u && ReadU32(b, 8) == 0x06u)
        {
            var tC = ReadAscii4(b, 4);
            if (tC.Length >= 1)
            {
                text = tC;
                value = BitConverter.ToSingle(BitConverter.GetBytes(ReadU32(b, 12)), 0);
                return true;
            }
        }

        return false;
    }

    private static string ReadAscii4(ReadOnlySpan<byte> b, int o)
    {
        // Require the 4-byte chunk to start with a printable byte (so we
        // skip records whose first byte is zero — those aren't slot
        // records, they're zero-padding between blocks).
        var b0 = b[o];
        if (b0 < 0x20 || b0 >= 0x7F) return string.Empty;
        var sb = new StringBuilder(4);
        for (int i = 0; i < 4; i++)
        {
            var c = b[o + i];
            if (c == 0) break;
            if (c < 0x20 || c >= 0x7F) return string.Empty;
            sb.Append((char)c);
        }
        return sb.ToString();
    }
}

/// <summary>
/// FR-C13 Phase 1 — one slot of a Power's Script Formula table. Each
/// slot is a positional, named entry the engine resolves through its
/// runtime formula evaluator; the library surfaces the entry's decoded
/// text form + literal IEEE-754 value (when the text is a numeric
/// literal) so the consumer can evaluate the SF_N references in
/// <see cref="PowerDefinition.Description"/>'s format string.
/// </summary>
/// <param name="Index">Positional index — matches the engine's
/// <c>[SF_<i>n</i>...]</c> placeholders in the localized
/// <see cref="PowerDefinition.Description"/> (e.g.
/// <c>[SF_0*100|%x|]</c> references <see cref="Index"/> = 0).</param>
/// <param name="Text">The decoded text form of the slot's
/// <c>DT_STRING_FORMULA.Value</c>. For trivial-numeric slots this is
/// the literal text (<c>"0.02"</c>, <c>"60"</c>, <c>".15"</c>, …); for
/// expression slots this is an arithmetic expression over other SF_N
/// references (<c>"SF_1 / 3"</c> on Demonic Spicules' SF_2).</param>
/// <param name="LiteralValue">The engine's IEEE-754 single-precision
/// scalar for the slot — populated for trivial-numeric slots from the
/// compiled-form float; <see cref="float.NaN"/> for expression slots
/// (Phase 2 will resolve those library-side).</param>
public readonly record struct PowerScriptFormula(
    int Index, string Text, float LiteralValue)
{
    /// <summary><see langword="true"/> when <see cref="Text"/> is an
    /// arithmetic expression rather than a literal number (no
    /// <see cref="LiteralValue"/> available — needs Phase 2's
    /// evaluator). Detected structurally: the text contains
    /// non-numeric characters typical of expressions
    /// (<c>SF_</c>, operators, function calls).</summary>
    public bool IsExpression =>
        Text.Contains("SF_", StringComparison.Ordinal) ||
        Text.AsSpan().IndexOfAny(ExpressionOperators) >= 0;

    private static readonly SearchValues<char> ExpressionOperators =
        SearchValues.Create("+-*/()");
}
