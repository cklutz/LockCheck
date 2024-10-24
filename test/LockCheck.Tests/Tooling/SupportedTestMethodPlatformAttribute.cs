using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockCheck.Tests.Tooling;

/// <summary>
/// Only executes the test method if the given test platform matches the current one.
/// </summary>
public sealed class SupportedTestMethodPlatformAttribute : TestMethodAttribute
{
    private readonly TestMethodAttribute? _attr;

    public SupportedTestMethodPlatformAttribute(string platformName)
    {
        PlatformName = platformName;
    }

    public SupportedTestMethodPlatformAttribute(TestMethodAttribute? attr, string platformName)
        : this(platformName)
    {
        _attr = attr;
    }

    public string PlatformName { get; }

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

        if (_attr != null)
        {
            return _attr.Execute(testMethod);
        }

        return base.Execute(testMethod);
    }
}
