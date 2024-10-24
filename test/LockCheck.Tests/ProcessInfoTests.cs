using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockCheck.Tests;

[TestClass]
public class ProcessInfoTests
{
    private class TestProcessInfo : ProcessInfo
    {
        public TestProcessInfo(int processId, DateTime startTime) : base(processId, startTime)
        {
            ExecutableName = "TestExecutable";
            ApplicationName = "TestApplication";
            Owner = "TestOwner";
            ExecutableFullPath = "C:\\TestPath\\TestExecutable.exe";
            SessionId = 1;
            LockType = "TestLockType";
            LockMode = "TestLockMode";
            LockAccess = "TestLockAccess";
        }
    }

    [TestMethod]
    public void GetHashCode_ShouldSupportDictionary()
    {
        var processInfo = new TestProcessInfo(1234, new DateTime(2023, 10, 1));

        var dictionary = new Dictionary<TestProcessInfo, bool>
        {
            { processInfo, true }
        };

        Assert.IsTrue(dictionary.ContainsKey(processInfo));
    }

    [TestMethod]
    public void Equals_ShouldReturnTrueForEqualObjects()
    {
        var processInfo1 = new TestProcessInfo(1234, new DateTime(2023, 10, 1));
        var processInfo2 = new TestProcessInfo(1234, new DateTime(2023, 10, 1));
        Assert.IsTrue(processInfo1.Equals(processInfo2));
    }

    [TestMethod]
    public void Equals_ShouldReturnFalseForDifferentObjects()
    {
        var processInfo1 = new TestProcessInfo(1234, new DateTime(2023, 10, 1));
        var processInfo2 = new TestProcessInfo(5678, new DateTime(2023, 10, 2));
        Assert.IsFalse(processInfo1.Equals(processInfo2));
    }

    [TestMethod]
    public void ToString_ShouldReturnCorrectString()
    {
        var processInfo = new TestProcessInfo(1234, new DateTime(2023, 10, 1));
        Assert.AreEqual("1234@2023-10-01T00:00:00.0000000", processInfo.ToString());
        Assert.AreEqual("1234@2023-10-01T00:00:00.0000000", processInfo.ToString(null));
    }

    [TestMethod]
    public void ToString_WithFormatF_ShouldReturnCorrectString()
    {
        var processInfo = new TestProcessInfo(1234, new DateTime(2023, 10, 1));
        Assert.AreEqual("1234@2023-10-01T00:00:00.0000000/TestApplication", processInfo.ToString("F"));
    }

    [TestMethod]
    public void Format_ShouldReturnEmptyString_WithNullLockers()
    {
        var sb = new StringBuilder();
        ProcessInfo.Format(sb, null!, []);
        Assert.AreEqual(0, sb.Length);
    }

    [TestMethod]
    public void Format_ShouldReturnEmptyString_WithEmptyLockers()
    {
        var sb = new StringBuilder();
        ProcessInfo.Format(sb, [], []);
        Assert.AreEqual(0, sb.Length);
    }

    [TestMethod]
    public void Format_ShouldThrowArgumentNullException_WithNullFileNames()
    {
        Assert.ThrowsException<ArgumentNullException>(() => ProcessInfo.Format(new(), [], null!));
    }

    [TestMethod]
    public void Format_ShouldReturnCorrectString()
    {
        var processInfo1 = new TestProcessInfo(1234, new DateTime(2023, 10, 1));
        var processInfo2 = new TestProcessInfo(5678, new DateTime(2023, 10, 2));
        var lockers = new List<ProcessInfo> { processInfo1, processInfo2 };
        var fileNames = new List<string> { "file1.txt", "file2.txt" };
        var sb = new StringBuilder();

        ProcessInfo.Format(sb, lockers, fileNames, maxProcesses: 1);

        var expected = new TestProcessInfo(processInfo1.ProcessId, processInfo1.StartTime);

        var expectedString = new StringBuilder()
            .AppendFormat("File {0} locked by: ", string.Join(", ", fileNames))
            .AppendLine($"[{expected.ApplicationName}, pid={expected.ProcessId}, owner={expected.Owner}, started={expected.StartTime:yyyy-MM-dd HH:mm:ss.fff}]")
            .AppendLine("[1 more processes...]")
            .ToString();

        Assert.AreEqual(expectedString, sb.ToString());
    }
}
