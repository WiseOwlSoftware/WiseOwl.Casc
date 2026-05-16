// Minimal compiler-feature polyfills so the same modern C# (records,
// init-only setters) compiles on netstandard2.0. Linked into each packable
// project for the netstandard2.0 target only (see the .csproj files); the
// modern TFMs already provide these in-box.

#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;

    /// <summary>Enables <c>init</c>-only setters / positional records on
    /// netstandard2.0.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit { }
}
#endif
