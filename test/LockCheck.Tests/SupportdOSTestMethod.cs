using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockCheck.Tests
{
    public sealed class SupportedTestClassPlatformAttribute : TestClassAttribute
    {
        public SupportedTestClassPlatformAttribute(string platformName)
        {
            PlatformName = platformName;
        }

        public string PlatformName { get; }

        public override TestMethodAttribute GetTestMethodAttribute(TestMethodAttribute testMethodAttribute)
        {
            if (testMethodAttribute is SupportedTestMethodPlatformAttribute ta)
            {
                return ta;
            }

            return new SupportedTestMethodPlatformAttribute(base.GetTestMethodAttribute(testMethodAttribute), PlatformName.ToString());
        }
    }

    public sealed class SupportedTestMethodPlatformAttribute : TestMethodAttribute
    {
        private readonly TestMethodAttribute _attr;

        public SupportedTestMethodPlatformAttribute(string platformName)
        {
            PlatformName = platformName;
        }

        public SupportedTestMethodPlatformAttribute(TestMethodAttribute attr, string platformName)
            : this(platformName)
        {
            _attr = attr;
        }

        public string PlatformName { get; }

        public override TestResult[] Execute(ITestMethod testMethod)
        {
            // Report status passed. Most examples on the Internet use "Inconclusive".
            // This is not how we like to have it, because it looks "bad" in test reports
            // and might hide actual issues to easily.
            var outcomeIfSkipped = UnitTestOutcome.Passed;
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
                            $"Test has not been skipped, because it is only supported on platform '{PlatformName}'.")
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
}
