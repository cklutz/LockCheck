using System;

namespace LockCheck;

/// <summary>
/// Alter the behavior when retrieving locking information.
/// </summary>
[Flags]
public enum LockManagerFeatures
{
    /// <summary>
    /// Use default settings.
    /// </summary>
    None = 0,
    /// <summary>
    /// Instead of returning an empty list of processes on non-supported platforms,
    /// throw a <see cref="PlatformNotSupportedException"/> exception.
    /// </summary>
    ThrowIfNotSupported = 1 << 0,
    /// <summary>
    /// Use low level APIs, where available. Otherwise use the platform's default APIs.
    /// </summary>
    UseLowLevelApi = 1 << 1,
    /// <summary>
    /// Also check process current working directories.
    /// </summary>
    CheckDirectories = 1 << 2
}
