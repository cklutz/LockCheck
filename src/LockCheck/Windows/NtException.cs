using System.ComponentModel;

namespace LockCheck.Windows
{
    internal class NtException : Win32Exception
    {
        public NtException(uint status, string message)
            : base(message)
        {
            HResult = unchecked((int)status);
        }

        public NtException(int error, uint status, string message)
            : base(error, message)
        {
            HResult = unchecked((int)status);
        }
    }
}