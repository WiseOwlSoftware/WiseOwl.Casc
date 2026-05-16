using System;

namespace WiseOwl.Casc;

/// <summary>Base type for all errors raised by WiseOwl.Casc.</summary>
public class CascException : Exception
{
    /// <summary>Create a CASC error with a message.</summary>
    public CascException(string message) : base(message) { }

    /// <summary>Create a CASC error with a message and inner cause.</summary>
    public CascException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>The on-disk CASC layout could not be understood (corrupt or an
/// unsupported format/version was encountered).</summary>
public sealed class CascFormatException(string message) : CascException(message);

/// <summary>The requested content could not be located in this storage.</summary>
public sealed class CascContentNotFoundException(string message) : CascException(message);

/// <summary>The content is encrypted with a key this library does not have.
/// BLTE <c>'E'</c> (Salsa20) chunks require a TACT key the consumer has not
/// supplied.</summary>
public sealed class CascEncryptedContentException(string message) : CascException(message);
