using System;
using System.Collections.Generic;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// The kind of a <see cref="MonsterNameFragment"/> — where the fragment sits in
/// a composed elite-monster name (FR-C35, CL-105).
/// </summary>
public enum MonsterNameKind
{
    /// <summary>A leading fragment (token ends <c>Prefix…</c>) — e.g. the affix
    /// portion that precedes the monster's base name.</summary>
    Prefix,
    /// <summary>A trailing fragment (token ends <c>Suffix…</c>).</summary>
    Suffix,
    /// <summary>Neither an explicit prefix nor suffix token (unclassified).</summary>
    Other,
}

/// <summary>
/// One entry of the Diablo IV <c>MonsterNames</c> registry (FR-C35, CL-105) — a
/// localized name <b>fragment</b> the game composes into an elite/special
/// monster's displayed name (e.g. <c>FrozenSuffix004</c> → <c>"Frostburn"</c>,
/// <c>ElectricLanceSuffix001</c> → <c>"Boltrend"</c>).
/// </summary>
/// <param name="Token">The registry token / StringList label (e.g.
/// <c>"FrozenSuffix004"</c>). Stable across builds; the composition key.</param>
/// <param name="Text">The localized display fragment (e.g. <c>"Frostburn"</c>).</param>
/// <param name="Kind">Whether the token is a <see cref="MonsterNameKind.Prefix"/>
/// or <see cref="MonsterNameKind.Suffix"/> (classified from the token name; the
/// game composes a full name from a base + prefix/suffix fragments).</param>
public readonly record struct MonsterNameFragment(
    string Token, string Text, MonsterNameKind Kind);

/// <summary>
/// The decoded Diablo IV <c>MonsterNames</c> registry (FR-C35, CL-105): the
/// localized name-affix fragments the game composes into elite/special monster
/// display names.
/// </summary>
/// <remarks>
/// The fragment text lives in the <c>MonsterNames</c> <b>StringList</b> (group 42,
/// name-matched to the <c>MonsterNames</c> GameBalance registry SNO 44325);
/// each label is a composition token (<c>&lt;Name&gt;Prefix&lt;NNN&gt;</c> /
/// <c>&lt;Name&gt;Suffix&lt;NNN&gt;</c>) and its value is the shown fragment. This
/// reader surfaces those entries typed + classified; a consumer assembles a full
/// elite name from a base monster name plus a prefix and/or suffix fragment, the
/// same "fragments the game composes" pattern as affix/aspect display names
/// (§11.3 / FR-C30). <b>Prefix/suffix is inferred from the token spelling</b>
/// (honest label discipline — the game's exact composition rule is engine-side);
/// the token + text are byte-verified.
/// </remarks>
public sealed class MonsterNameRegistry
{
    /// <summary>The default <c>MonsterNames</c> StringList SNO name (resolved via
    /// CoreTOC; the registry GameBalance SNO is 44325, the StringList is 125915
    /// on build <c>3.1.1.72836</c>).</summary>
    public const string StringListName = "MonsterNames";

    private readonly IReadOnlyList<MonsterNameFragment> _fragments;

    internal MonsterNameRegistry(string locale, IReadOnlyList<MonsterNameFragment> fragments)
    {
        Locale = locale;
        _fragments = fragments;
    }

    /// <summary>The locale the fragment text was resolved in.</summary>
    public string Locale { get; }

    /// <summary>All name fragments (token, localized text, prefix/suffix kind).</summary>
    public IReadOnlyList<MonsterNameFragment> Fragments => _fragments;

    /// <summary>The prefix fragments only.</summary>
    public IEnumerable<MonsterNameFragment> Prefixes
    {
        get { foreach (var f in _fragments) if (f.Kind == MonsterNameKind.Prefix) yield return f; }
    }

    /// <summary>The suffix fragments only.</summary>
    public IEnumerable<MonsterNameFragment> Suffixes
    {
        get { foreach (var f in _fragments) if (f.Kind == MonsterNameKind.Suffix) yield return f; }
    }

    /// <summary>Build the registry from a resolved <c>token → text</c> map (the
    /// <c>MonsterNames</c> StringList entries). Classifies each token by its
    /// spelling.</summary>
    internal static MonsterNameRegistry FromEntries(
        string locale, IEnumerable<KeyValuePair<string, string>> entries)
    {
        var list = new List<MonsterNameFragment>();
        foreach (var (token, text) in entries)
            list.Add(new MonsterNameFragment(token, text, Classify(token)));
        list.Sort((a, b) => string.CompareOrdinal(a.Token, b.Token));
        return new MonsterNameRegistry(locale, list);
    }

    private static MonsterNameKind Classify(string token)
    {
        // Tokens are shaped "<Name>Prefix<NNN>" / "<Name>Suffix<NNN>" — strip the
        // trailing digits, then test the Prefix/Suffix keyword.
        int end = token.Length;
        while (end > 0 && char.IsDigit(token[end - 1])) end--;
        var stem = token.AsSpan(0, end);
        if (stem.EndsWith("Prefix", StringComparison.Ordinal)) return MonsterNameKind.Prefix;
        if (stem.EndsWith("Suffix", StringComparison.Ordinal)) return MonsterNameKind.Suffix;
        return MonsterNameKind.Other;
    }
}
