using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockCheck.Tests.Tooling;

/// <summary>
/// Only executes test in the test class that match the given test platform.
/// </summary>
public sealed class SupportedTestClassPlatformAttribute : TestClassAttribute
{
    public SupportedTestClassPlatformAttribute(string platformName)
        : this(platformName, false)
    {

    }

    public SupportedTestClassPlatformAttribute(string platformName, bool requiresAdminRights)
    {
        PlatformName = platformName;
        RequiresAdminRights = requiresAdminRights;
    }

    public string PlatformName { get; }
    public bool RequiresAdminRights { get; }

    public override TestMethodAttribute? GetTestMethodAttribute(TestMethodAttribute? testMethodAttribute)
    {
        if (testMethodAttribute is SupportedTestMethodPlatformAttribute ta)
        {
            return ta;
        }

        return new SupportedTestMethodPlatformAttribute(base.GetTestMethodAttribute(testMethodAttribute), PlatformName.ToString(), RequiresAdminRights);
    }
}
