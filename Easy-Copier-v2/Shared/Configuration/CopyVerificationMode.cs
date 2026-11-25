namespace EasyCopier.Shared.Configuration;

/// <summary>
/// Specifies the post-copy data verification strategy.
/// </summary>
public enum CopyVerificationMode
{
    /// <summary>No verification is performed after copying.</summary>
    None,

    /// <summary>Quick verification: checks that all files exist and match exact byte lengths.</summary>
    QuickSizeAndCount,

    /// <summary>Fast hardware-accelerated SIMD hashing using XxHash64.</summary>
    FastXxHash64,

    /// <summary>Full cryptographic verification using standard SHA-256 hashes.</summary>
    FullSha256
}

