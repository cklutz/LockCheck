using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockCheck.Tests
{
    [TestClass]
    public class ExceptionUtilsTests
    {
        [TestMethod]
        public void CorePlatformThrowsIOExceptionOnLock()
        {
            IOException lockException = null;
            TestHelper.CreateLockSituation((ex, fileName) => lockException = ex as IOException);
            Assert.IsNotNull(lockException);
        }

        [TestMethod]
        public void IsFileLockedRecognizesLock()
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

        [DataTestMethod]
        [DataRow(LockManagerFeatures.None)]
        [DataRow(LockManagerFeatures.UseLowLevelApi)]
        public void RethrownExceptionContainsInformation(LockManagerFeatures features)
        {
            TestHelper.CreateLockSituation((ex, fileName) =>
            {
                var processInfos = LockManager.GetLockingProcessInfos(new[] { fileName }, features).ToList();
                Assert.AreEqual(1, processInfos.Count); // Sanity, has been tested in LockManagerTests
                var expectedMessageContents = new StringBuilder();
                ProcessInfo.Format(expectedMessageContents, processInfos, new[] { fileName });

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
    }
}
