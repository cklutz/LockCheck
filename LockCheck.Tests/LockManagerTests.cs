using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockCheck.Tests
{
    [TestClass]
    public class LockManagerTests
    {
        [DataTestMethod]
        [DataRow(LockManagerFeatures.None)]
        [DataRow(LockManagerFeatures.UseLowLevelApi)]
        public void LockInformationAvailable(LockManagerFeatures features)
        {
            var process = Process.GetCurrentProcess();

            TestHelper.CreateLockSituation((_, fileName) =>
            {
                var processInfos = LockManager.GetLockingProcessInfos([fileName], features).ToList();
                Assert.AreEqual(1, processInfos.Count);
                Assert.AreEqual(process.Id, processInfos[0].ProcessId);
                Assert.AreEqual(process.SessionId, processInfos[0].SessionId);
                Assert.AreEqual(process.StartTime, processInfos[0].StartTime);
                Assert.IsNotNull(processInfos[0].ApplicationName);
                Assert.AreEqual(process.MainModule.FileName.ToLowerInvariant(), processInfos[0].ExecutableFullPath?.ToLowerInvariant());
                // Might contain domain, computername, etc. in SAM form
                StringAssert.Contains(processInfos[0].Owner?.ToLowerInvariant(), Environment.UserName.ToLowerInvariant());
                // Might have an .exe suffix or not.
                StringAssert.Contains(processInfos[0].ExecutableName?.ToLowerInvariant(), process.ProcessName.ToLowerInvariant());
            });
        }


        [DataTestMethod]
        [DataRow(LockManagerFeatures.None | LockManagerFeatures.CheckDirectories)]
        [DataRow(LockManagerFeatures.UseLowLevelApi | LockManagerFeatures.CheckDirectories)]
        public void LockInformationAvailableForDirectory(LockManagerFeatures features)
        {
            TestHelper.CreateProcessWithCurrentDirectory(((string TemporaryDirectory, int ProcessId, int SessionId, DateTime ProcessStartTime, string ProcessName, string ExecutableFullPath) args) =>
            {
                var processInfos = LockManager.GetLockingProcessInfos([args.TemporaryDirectory], features).ToList();
                Assert.AreEqual(1, processInfos.Count);
                Assert.AreEqual(args.ProcessId, processInfos[0].ProcessId);
                Assert.AreEqual(args.SessionId, processInfos[0].SessionId);
                Assert.AreEqual(args.ProcessStartTime, processInfos[0].StartTime);
                Assert.AreEqual(args.ExecutableFullPath.ToLowerInvariant(), processInfos[0].ExecutableFullPath?.ToLowerInvariant());
                // Might contain domain, computername, etc. in SAM form
                StringAssert.Contains(processInfos[0].Owner?.ToLowerInvariant(), Environment.UserName.ToLowerInvariant());
                // Might have an .exe suffix or not.
                StringAssert.Contains(processInfos[0].ExecutableName?.ToLowerInvariant(), args.ProcessName.ToLowerInvariant());
            });
        }
    }
}
