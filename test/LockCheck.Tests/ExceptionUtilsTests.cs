using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using LockCheck.Tests.Tooling;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockCheck.Tests;

[TestClass]
public class ExceptionUtilsTests
{
    [TestMethod]
    public void CorePlatformThrowsIOExceptionOnLock()
    {
        IOException? lockException = null;
        TestHelper.CreateLockSituation((ex, fileName) => lockException = ex as IOException);
        Assert.IsNotNull(lockException);
    }

    [TestMethod]
    public void RethrowWithLockingInformation_ShouldThrowArgumentNullException_WhenFileNameIsNull()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new Exception().RethrowWithLockingInformation((string)null!));
    }

    [TestMethod]
    public void RethrowWithLockingInformation_ShouldThrowArgumentNullException_WhenFileNamesIsNull()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new Exception().RethrowWithLockingInformation((string[])null!));
    }

    [TestMethod]
    public void RethrowWithLockingInformation_ShouldNotThrowIOException_WhenFileNamesIsEmpty()
    {
        Assert.IsFalse(new Exception().RethrowWithLockingInformation(Array.Empty<string>()));
    }

    [TestMethod]
    public void RethrowWithLockingInformation_ShouldNotThrowIOException_WhenExceptionIsNotIOException()
    {
        Assert.IsFalse(new Exception().RethrowWithLockingInformation("test.txt"));
    }

    [TestMethod]
    public void RethrowWithLockingInformation_ShouldNotThrowIOException_WhenIOExceptionIsNotDueToFileLock()
    {
        Assert.IsFalse(new IOException().RethrowWithLockingInformation("test.txt"));
    }

    [DataTestMethod]
    [DataRow(LockManagerFeatures.None)]
    [DataRow(LockManagerFeatures.UseLowLevelApi)]
    public void RethrowWithLockingInformation_ShouldRethrowWithLockingInformation_WhenLockIsFound(LockManagerFeatures features)
    {
        TestHelper.CreateLockSituation((ex, fileName) =>
        {
            var processInfos = LockManager.GetLockingProcessInfos([fileName], features).ToList();
            Assert.AreEqual(1, processInfos.Count); // Sanity, has been tested in LockManagerTests
            var expectedMessageContents = new StringBuilder();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Getting the owner is not really stable on Windows, it seems.
                ProcessInfo.Format(expectedMessageContents, processInfos, [fileName],
                    ownerOverwrite: $@"{Environment.UserDomainName}\{Environment.UserName}");
            }
            else
            {
                ProcessInfo.Format(expectedMessageContents, processInfos, [fileName]);
            }

            try
            {
                bool result = ex.RethrowWithLockingInformation(fileName, features);
                Assert.IsTrue(result);
            }
            catch (Exception re)
            {
                StringAssert.Contains(re.Message, expectedMessageContents.ToString());
                Assert.AreEqual(ex.HResult, re.HResult);
            }
        });
    }

    [TestMethod]
    public void IsFileLocked_ShouldThrowArgumentNullException_WhenExceptionIsNull()
    {
        Assert.ThrowsException<ArgumentNullException>(() => ((IOException)null!).IsFileLocked());
    }

    [TestMethod]
    public void IsFileLocked_ShouldReturnTrue_WhenFileIsLocked()
    {
        bool found = false;
        TestHelper.CreateLockSituation((ex, fileName) =>
        {
            if (ex is IOException ioex)
            {
                found = ioex.IsFileLocked();
            }
        });

        Assert.IsTrue(found);
    }
}
