using System;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockCheck.Tests
{
    [TestClass]
    public class ExceptionUtilsTests
    {
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
