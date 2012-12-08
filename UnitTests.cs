using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace PerformanceMeasurement
{
    [TestFixture]
    internal class UnitTests
    {
        [TestCase]
        public void TestMeasureActionDoesNotCrash()
        {
            var stats = LinqPadUX.ComputerSpecs;
            var timeIt = LinqPadUX.Measure.Action(() => "Hello".GetHashCode());
        }
        [TestCase]
        public void TestDefaultBenchmarkToWebBrowserDoesNotCrash()
        {
            LinqPadUX.DefaultBenchmarkToWebBrowser();
        }
    }
}
