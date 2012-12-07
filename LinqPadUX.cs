using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PerformanceMeasurement
{
    class LinqPadUX
    {
        public string DefaultBenchmarkToWebBrowser()
        {
            MeasureIt.Main(new String[1]);
            return "Look in your web browser";
        }
        public ComputerSpecs ComputerSpecs()
        {
            return new ComputerSpecs();
        }
    }
}
