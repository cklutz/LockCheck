using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace LockCheck.Linux
{
    internal static class NativeMethods
    {
        public const int EAGAIN      = 11; // Resource unavailable, try again (same value as EWOULDBLOCK),
        public const int EWOULDBLOCK = EAGAIN; // Operation would block.
    }
}