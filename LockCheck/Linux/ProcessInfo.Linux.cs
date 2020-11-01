using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LockCheck.Linux;

namespace LockCheck
{
    public partial class ProcessInfo
    {
        internal ProcessInfo(LockInfo lockInfo)
        {
            SessionId = -1;
            ProcessId = lockInfo.ProcessId;
            LockType = lockInfo.LockType;
            LockMode = lockInfo.LockMode;
            LockAccess = lockInfo.LockAccess;
        }

        public string LockType { get; set; }
        public string LockMode { get; set; }
        public string LockAccess { get; set; }
    }
}
