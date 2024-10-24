using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using LockCheck.Tests.Tooling;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockCheck.Tests;

[TestClass]
public class TestHelperTests
{
    [DataTestMethod]
    [DataRow("usr/local/bin", "usr", "", true)]
    [DataRow("/usr/local/bin", "usr", "/", true)]
    [DataRow("/usr/local/bin", "local", "/usr/", true)]
    [DataRow("/usr/local/bin/local/foo", "local", "/usr/local/bin/", true)]
    [DataRow("/usr/local/bin", "bin", "/usr/local/", true)]
    [DataRow("/usr/local/bin", "loc", null, false)]
    [DataRow("/usr/local/bin", "localX", null, false)]
    [DataRow("/usr/local/bin", "binXX", null, false)]
    [DataRow("\\Users\\Homer\\Documents", "Users", "\\", true)]
    [DataRow("Users\\Homer\\Documents", "Users", "", true)]
    [DataRow("C:\\Users\\Homer\\Documents", "C:", "", true)]
    [DataRow("C:\\Users\\Homer\\Documents", "Users", "C:\\", true)]
    [DataRow("C:\\Users\\Homer\\Documents\\Users\\Foo", "Users", "C:\\Users\\Homer\\Documents\\", true)]
    [DataRow("C:\\Users\\Homer\\Documents", "Homer", "C:\\Users\\", true)]
    [DataRow("C:\\Users\\Homer\\Documents", "Documents", "C:\\Users\\Homer\\", true)]
    [DataRow("C:\\Users\\Homer\\Documents", "Ho", null, false)]
    [DataRow("C:\\Users\\Homer\\Documents", "HomerX", null, false)]
    [DataRow("C:\\Users\\Homer\\Documents", "DocumentsXX", null, false)]
    public void TryGetPathBeforeLastSegment(string path, string segment, string expectedPrefix, bool expectedResult)
    {
        bool result = TestHelper.TryGetPathBeforeLastSegment(path, segment, out var prefix);

        Assert.AreEqual(expectedResult, result);
        Assert.AreEqual(expectedPrefix, prefix);
    }

    [DataTestMethod]
    [DataRow("/usr/local/bin", "usr", "/local/bin", true)]
    [DataRow("/usr/local/bin", "local", "/bin", true)]
    [DataRow("/usr/local/bin", "bin", "", true)]
    [DataRow("/usr/local/bin/local/foo", "local", "/foo", true)]
    [DataRow("/usr/local/bin", "loc", null, false)]
    [DataRow("/usr/local/bin", "localX", null, false)]
    [DataRow("/usr/local/bin", "binXX", null, false)]
    [DataRow("C:\\Users\\Homer\\Documents", "Users", "\\Homer\\Documents", true)]
    [DataRow("\\Users\\Homer\\Documents", "Users", "\\Homer\\Documents", true)]
    [DataRow("Users\\Homer\\Documents", "Users", "\\Homer\\Documents", true)]
    [DataRow("Users\\Homer\\Documents", "Documents", "", true)]
    [DataRow("C:\\Users\\Homer\\Documents", "C:", "\\Users\\Homer\\Documents", true)]
    [DataRow("C:\\Users\\Homer\\Documents\\Users\\Foo", "Users", "\\Foo", true)]
    [DataRow("C:\\Users\\Homer\\Documents", "Ho", null, false)]
    [DataRow("C:\\Users\\Homer\\Documents", "HomerX", null, false)]
    [DataRow("C:\\Users\\Homer\\Documents", "DocumentsXX", null, false)]
    public void TryGetPathAfterLastSegment(string path, string segment, string expectedSuffix, bool expectedResult)
    {
        bool result = TestHelper.TryGetPathAfterLastSegment(path, segment, out var suffix);

        Assert.AreEqual(expectedResult, result);
        Assert.AreEqual(expectedSuffix, suffix);
    }
}
