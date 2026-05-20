using System;
using System.Collections.Generic;
using System.Text;

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
    private PowerDefinition(int snoId, IReadOnlyList<PowerScriptFormula> scriptFormulas)
    {
        SnoId = snoId;
        ScriptFormulas = scriptFormulas;
    }

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

    /// <summary>Decode a Power from its raw SNO blob. Identity +
    /// <see cref="ScriptFormulas"/>; the localized fields need
    /// <see cref="CoreToc"/>; use
    /// <see cref="Diablo4Storage.ReadPower(int,string)"/>.</summary>
    public static PowerDefinition Parse(ReadOnlySpan<byte> blob)
    {
        var snoId = new SnoRecord(blob).SnoId;
        var formulas = DecodeScriptFormulas(blob);
        return new PowerDefinition(snoId, formulas);
    }

    internal void SetStrings(string name, string description)
    {
        Name = name;
        Description = description;
    }

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
    private static IReadOnlyList<PowerScriptFormula> DecodeScriptFormulas(
        ReadOnlySpan<byte> blob)
    {
        // Anchor on the ("0", 0.0) terminator record (Layout A: a clean
        // 16-byte block with bytes [0x30 0 0 0 | 0x06 0 0 0 | 0 0 0 0 | 0 0 0 0]).
        // Scan for the LAST such anchor in the blob, then walk BACKWARD
        // in 16-byte strides decoding records. This avoids the spurious
        // overlap problem where Layouts B/C alias the trailing pad of
        // a preceding Layout A record.
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
        if (terminatorOff is null) return Array.Empty<PowerScriptFormula>();

        // Walk BACKWARD in 16-byte strides, decoding each record. Stop
        // when a block doesn't match any known layout. This collects
        // records in REVERSE positional order (terminator first, then
        // sentinel, then SF_N..0); reverse before returning.
        var records = new List<(string Text, float Value)>();
        for (int off = terminatorOff.Value; off >= 0; off -= 16)
        {
            if (!TryReadSlotRecord(blob, off, out var text, out var value))
                break;
            records.Add((text, value));
        }
        records.Reverse();

        // Strip the trailing ("0", 0.0) terminator + ("10", 10.0) sentinel
        // (every anchored legendary has both; powers without them just
        // lose the trim).
        int end = records.Count;
        if (end >= 1 && records[end - 1].Text == "0" && records[end - 1].Value == 0.0f)
            end--;
        if (end >= 1 && records[end - 1].Text == "10" && records[end - 1].Value == 10.0f)
            end--;

        var result = new PowerScriptFormula[end];
        for (int i = 0; i < end; i++)
            result[i] = new PowerScriptFormula(i, records[i].Text, records[i].Value);
        return result;
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
    /// Layout-A slot record — the clean, common layout used by the
    /// majority of the 72 legendary Power blobs:
    /// <c>[ascii@0..3][type=0x06@4..7][float@8..11][pad=0@12..15]</c>.
    /// Phase 2 will lift additional layouts (Greater Hex / Dominion's
    /// 4-char-ASCII records, Demonic Spicules's expression-bearing
    /// records) once the AST evaluator can drive a layout-disambiguating
    /// heuristic. On match, returns the 4-byte ASCII chunk (trimmed of
    /// trailing nulls) and the IEEE-754 float; otherwise returns
    /// false.</summary>
    private static bool TryReadSlotRecord(
        ReadOnlySpan<byte> blob, int off,
        out string text, out float value)
    {
        text = string.Empty;
        value = 0;
        if (off + 16 > blob.Length) return false;
        var b = blob.Slice(off, 16);

        // Layout A: type=0x06 at +4, float at +8, pad=0 at +12. The
        // ASCII chunk at +0..3 must start with a printable byte and
        // end with a null (within the 4-byte chunk) — i.e. text length
        // 1..3. Records with 4 printable bytes (no null in the chunk)
        // belong to other layouts and are deferred to Phase 2.
        if (ReadU32(b, 4) == 0x06u && ReadU32(b, 12) == 0u)
        {
            var t = ReadAscii4(b, 0);
            if (t.Length is >= 1 and <= 3)
            {
                text = t;
                value = BitConverter.ToSingle(BitConverter.GetBytes(ReadU32(b, 8)), 0);
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
        Text.IndexOf("SF_", StringComparison.Ordinal) >= 0 ||
        Text.IndexOfAny(ExpressionOperators) >= 0;

    private static readonly char[] ExpressionOperators =
        new[] { '+', '-', '*', '/', '(', ')' };
}
