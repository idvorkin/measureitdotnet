using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PerformanceMeasurement
{
    public struct NamedAction
    {
        public NamedAction(string name, Action theAction)
        {
            this.Name = name;
            this.TheAction = theAction;
        }
        public string Name;
        public Action TheAction;
    };

    public class FluentMeasure
    {
        public FluentMeasure()
        {
        }

        public FluentMeasure WithSampleCount(int i)
        {
            return new FluentMeasure(this) {sampleCount = i};
        }

        public FluentMeasure WithIterationCount(int i)
        {
            return new FluentMeasure(this) {iterationCount = i};
        }

        public StatsCollection Action(Action action)
        {
            return Action("default", action);
        }

        public StatsCollection Action(string name, Action action)
        {
            return TimeIt(sampleCount, iterationCount, new List<NamedAction>(){new NamedAction{Name=name,TheAction=action}});
        }

        public StatsCollection NamedActions(IEnumerable<NamedAction> namedActions)
        {
            return TimeIt(sampleCount, iterationCount, namedActions);
        }

        private static StatsCollection TimeIt(int sampleCount, int iterationCount, IEnumerable<NamedAction> namedActions)
        {
            var collector = new StatsCollection();
            var logger = new StatsLogger(collector);
            var timer = new MultiSampleCodeTimer(sampleCount, iterationCount);
            timer.OnMeasure +=
                (name, iterations, scale, samples) => { logger.AddWithCount(name, iterations, scale, samples); };
            namedActions.ToList().ForEach(namedAction => timer.Measure(namedAction.Name, namedAction.TheAction));
            return collector;

        }

        // reasonable defaults for these values.
        private int sampleCount = 1000;
        private int iterationCount = 10;

        private FluentMeasure(FluentMeasure fluentMeasure)
        {
            this.sampleCount = fluentMeasure.sampleCount;
            this.iterationCount = fluentMeasure.iterationCount;
        }
    };


    public class LinqPadUX
    {
        public static string DefaultBenchmarkToWebBrowser()
        {
            MeasureIt.Main(new String[1]);
            return "Look in your web browser";
        }

        public static ComputerSpecs ComputerSpecs
        {
            get { return new ComputerSpecs(); }
        }

        public static FluentMeasure Measure
        {
            get { return new FluentMeasure(); } 
        }
    }
}
