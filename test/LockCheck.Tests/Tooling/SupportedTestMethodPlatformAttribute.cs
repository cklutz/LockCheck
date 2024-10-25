using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockCheck.Tests.Tooling;

/// <summary>
/// Only executes the test method if the given test platform matches the current one.
/// </summary>
public sealed partial class SupportedTestMethodPlatformAttribute : TestMethodAttribute
{
    private readonly TestMethodAttribute? _attr;

    public SupportedTestMethodPlatformAttribute(string platformName)
        : this(platformName, false)
    {
    }

    public SupportedTestMethodPlatformAttribute(string platformName, bool requiresAdminRights)
    {
        PlatformName = platformName;
        RequiresAdminRights = requiresAdminRights;
    }

    public SupportedTestMethodPlatformAttribute(TestMethodAttribute? attr, string platformName, bool requiresAdminRights)
        : this(platformName, requiresAdminRights)
    {
        _attr = attr;
    }

    public string PlatformName { get; }
    public bool RequiresAdminRights { get; }

    public override TestResult[] Execute(ITestMethod testMethod)
    {
        // Default status if platform doesn't match.
        //
        // Most examples on the Internet use "Inconclusive".
        // Technically, there is nothing inconclusive here, because we *know* they
        // cannot simply run on the given platform. It would be nice if MSTest had
        // some explicit "skipped" status. It does have the [Ignore] attribute, but
        // this status cannot be applied programmatically.
        //
        // We use "NotFound" because that has the following effects:
        //
        // - CLI (dotnet test/vstest.console.exe) reports the tests as "skipped" (Go figure!)
        // - VS Test Explorer shows them with a blue Information icon, rather than the
        //   yellow Warning icon that you would get for Inconclusive..
        //
        var outcomeIfSkipped = UnitTestOutcome.NotFound;

        OSPlatform platform;
        switch (PlatformName.ToLowerInvariant())
        {
            case "windows":
                platform = OSPlatform.Windows;
                break;
            case "linux":
                platform = OSPlatform.Linux;
                break;
            default:
                platform = OSPlatform.Create(PlatformName);
                // A platform we did not really expect. Mark this test as inconclusive
                // so it lights up in the results.
                outcomeIfSkipped = UnitTestOutcome.Inconclusive;
                break;
        }

        if (!RuntimeInformation.IsOSPlatform(platform))
        {
            return
            [
                new()
                {
                    Outcome = outcomeIfSkipped,
                    TestFailureException = new PlatformNotSupportedException(
                        $"Test has not been executed, because it is only supported on platform '{PlatformName}'.")
                }
            ];
        }

        if (RequiresAdminRights && !IsUserAdministrator())
        {
            return
            [
                new()
                {
                    Outcome = outcomeIfSkipped,
                    TestFailureException = new PlatformNotSupportedException(
                        $"Test has not been executed, because it requires to current user to have administrative rights.")
                }
            ];
        }

        if (_attr != null)
        {
            return _attr.Execute(testMethod);
        }

        return base.Execute(testMethod);
    }

#if NET
    [LibraryImport("libc")]
    private static partial uint getuid();
#endif

    private static bool IsUserAdministrator()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            // Check if the user is in the Administrator role
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
#if NET
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            uint uid = getuid();
            return uid == 0;
        }
#endif
        else
        {
            throw new PlatformNotSupportedException();
        }
    }
}
