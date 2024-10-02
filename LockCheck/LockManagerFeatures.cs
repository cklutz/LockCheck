using System;

namespace LockCheck
{
    [Flags]
    public enum LockManagerFeatures
    {
        None = 0,
        ThrowIfNotSupported = 1 << 0,
        UseLowLevelApi = 1 << 1,
        CheckDirectories = 1 << 2
    }
}
