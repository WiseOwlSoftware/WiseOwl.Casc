namespace WiseOwl.Casc;

/// <summary>
/// Options for opening a <see cref="CascStorage"/>. All properties are
/// <c>init</c>-only; use a collection-style initializer.
/// </summary>
public sealed record CascOpenOptions
{
    /// <summary>The TACT product code (e.g. <c>fenris</c> for Diablo IV,
    /// <c>wow</c>, <c>pro</c>). When <see langword="null"/> it is taken from
    /// the install's <c>.build.info</c> <c>Product</c> column.</summary>
    public string? Product { get; init; }

    /// <summary>Validate envelope/BLTE hashes while reading. Off by default
    /// (the local index already guarantees identity); turn on for
    /// integrity diagnostics.</summary>
    public bool ValidateHashes { get; init; }

    /// <summary>Sensible defaults.</summary>
    public static CascOpenOptions Default { get; } = new();
}
