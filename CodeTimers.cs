/*  Copyright (c) Microsoft Corporation.  All rights reserved. */
/* AUTHOR: Vance Morrison   Date  : 10/20/2007  */
using System;
using System.Collections.Generic;
using System.Diagnostics;
/* files are best browsed in 'outline form'. You outline code with Ctrl-M Ctrl-O. */

namespace PerformanceMeasurement
{

    /// <summary>
    /// Samples represents a list of samples (floating point values) 
    /// </summary>
    public class Samples : List<float>
    {
       public static implicit operator Stats(Samples samples)
       {
           return new Stats(samples);
       }
    };

    /// <summary>
    /// Stats are computed over list of samples (floating point values). This class can calculate the standard
    /// statistics on this list (Mean, Median, StandardDeviation ...)
    /// </summary>
    public class Stats 
    {
        public Stats(Samples samples)
        {
            this.samples = new List<float>(samples.ToArray());
        }
        public float Minimum { get { if (!statsComputed) ComputeStats(); return minimum; } }
        public float Maximum { get { if (!statsComputed) ComputeStats(); return maximum; } }
        public float Median { get { if (!statsComputed) ComputeStats(); return median; } }
        public float Mean { get { if (!statsComputed) ComputeStats(); return mean; } }
        public float StandardDeviation { get { if (!statsComputed) ComputeStats(); return standardDeviation; } }
        public int Count { get { return this.samples.Count; } }
        public override string ToString()
        {
            if (!statsComputed)
                ComputeStats();
            return "mean=" + mean.ToString("f3") + " median=" + median.ToString("f3") +
                   " min=" + minimum.ToString("f3") + " max=" + maximum.ToString("f3") +
                   " sdtdev=" + standardDeviation.ToString("f3") + " samples=" + samples.Count.ToString();
        }

        #region privates

        public void ComputeStats()
        {
            minimum = float.MaxValue;
            maximum = float.MinValue;
            median = 0.0F;
            mean = 0.0F;
            standardDeviation = 0.0F;
            var count = samples.Count;

            double total = 0;
            foreach (float dataPoint in this.samples)
            {
                if (dataPoint < minimum)
                    minimum = dataPoint;
                if (dataPoint > maximum)
                    maximum = dataPoint;
                total += dataPoint;
            }

            if (count > 0)
            {
                samples.Sort();
                if (count % 2 == 1)
                    median = this.samples[count / 2];
                else
                    median = (this.samples[(count / 2) - 1] + this.samples[count / 2]) / 2;
                mean = (float)(total / count);

                double squares = 0.0;
                foreach (float dataPoint in this.samples)
                {
                    double diffFromMean = dataPoint - mean;
                    squares += diffFromMean * diffFromMean;
                }
                standardDeviation = (float)Math.Sqrt(squares / count);
            }

            statsComputed = true;
        }

        List<float> samples;
        float minimum;
        float maximum;
        float median;
        float mean;
        float standardDeviation;
        bool statsComputed;
        #endregion
    };

    /// <summary>
    /// The CodeTimer class only times one invocation of the code. Often, you want to collect many samples so
    /// that you can determine how noisy the resulting data is. This is what MultiSampleCodeTimer does.
    /// </summary>
    public class MultiSampleCodeTimer
    {
        public MultiSampleCodeTimer() : this(1) { }
        public MultiSampleCodeTimer(int sampleCount) : this(sampleCount, 1) { }
        public MultiSampleCodeTimer(int sampleCount, int iterationCount)
        {
            SampleCount = sampleCount;
            timer = new CodeTimer(iterationCount);
            timer.Prime = false;        // We will do the priming (or not).  
            Prime = true;
        }
        public MultiSampleCodeTimer(MultiSampleCodeTimer template)
            : this(template.SampleCount, template.IterationCount)
        {
            OnMeasure = template.OnMeasure;
        }

        /// <summary>
        /// If true (the default), the benchmark is run once before the actual measurement to 
        /// insure that any 'first time' initialization is complete. 
        /// </summary>
        public bool Prime;
        /// <summary>
        /// The number of times the benchmark is run in a loop for a single measument.
        /// </summary>
        public int IterationCount { get { return timer.IterationCount; } set { timer.IterationCount = value; } }
        /// <summary>
        /// The number of measurments to make for a single benchmark. 
        /// </summary>
        public int SampleCount;
        /// <summary>
        /// The smallest time (in microseconds) that can be resolved by the timer). 
        /// </summary>
        public static float ResolutionUsec { get { return 1000000.0F / Stopwatch.Frequency; } }

        public delegate void MeasureCallback(string name, int iterationCount, float scale, Samples sample);
        /// <summary>
        /// OnMeasure is signaled every time a Measure() is called. 
        /// </summary>
        public event MeasureCallback OnMeasure;

        public Stats Measure(string name, Action action)
        {
            return Measure(name, 1, action, null);
        }
        /// <summary>
        /// The main measurment routine.  Calling this will cause code:OnMeasure event to be
        /// raised.  
        /// </summary>
        /// <param name="name">name of the benchmark</param>
        /// <param name="scale">The number of times the benchmark is cloned in 'action' (typically 1)</param>
        /// <param name="action">The actual code to measure.</param>
        /// <returns>A Stats object representing the measurements (in usec)</returns>
        public Stats Measure(string name, float scale, Action action)
        {
            return Measure(name, scale, action, null);
        }
        /// <summary>
        /// The main measurment routine.  Calling this will cause code:OnMeasure event to be
        /// raised.  
        /// </summary>
        /// <param name="name">name of the benchmark</param>
        /// <param name="scale">The number of times the benchmark is cloned in 'action' (typically 1)</param>
        /// <param name="action">The actual code to measure.</param>
        /// <param name="reset">Code that will be called before 'action' to reset the state of the benchmark.</param>
        /// <returns>A Stats object representing the measurements (in usec)</returns>
        public Samples Measure(string name, float scale, Action action, Action reset)
        {
            if (reset != null && IterationCount != 1)
                throw new ApplicationException("Reset can only be used on timers with an iteration count of 1");
            var statsUSec = new Samples();
            if (Prime)
            {
                if (reset != null)
                    reset();
                action();
            }
            for (int i = 0; i < SampleCount; i++)
            {
                if (reset != null)
                    reset();
                statsUSec.Add(timer.Measure(name, scale, action));
            }

            if (OnMeasure != null)
                OnMeasure(name, IterationCount, scale, statsUSec);
            return statsUSec;
        }

        /// <summary>
        /// Prints the mean, median, min, max, and stdDev and count of the samples to the Console
        /// Useful as a target for OnMeasure
        /// </summary>
        public static MeasureCallback PrintStats = (name, iterationCount, scale, sample) => Console.WriteLine(name + ": " + name + " stats:" + ((Stats) sample));
        /// <summary>
        /// Prints the mean with a error bound (2 standard deviations, which imply a you have
        /// 95% confidence that a sampleUsec will be with the bounds (for a normal distribution). 
        /// This is a good default target for OnMeasure.  
        /// </summary>
        public static MeasureCallback Print = delegate(string name, int iterationCount, float scale, Samples samples)
            {
                var stats = (Stats)samples;
                // +- two standard deviations covers 95% of all samples in a normal distribution 
                float errorPercent = (stats.StandardDeviation * 2 * 100) / Math.Abs(stats.Mean);
                string errorString = ">400%";
                if (errorPercent < 400)
                    errorString = (errorPercent.ToString("f0") + "%").PadRight(5);
                string countString = "";
                if (iterationCount != 1)
                    countString = "count: " + iterationCount.ToString() + " ";
                Console.WriteLine(name + ": " + countString + stats.Mean.ToString("f3").PadLeft(8) + " +- " + errorString + " msec");
            };

        #region privates
        CodeTimer timer;
        #endregion
    };

    /// <summary>
    /// CodeTimer is a simple wrapper that uses System.Diagnostics.StopWatch
    /// to time the body of some code (given by a delegate), to high precision. 
    /// </summary>
    public class CodeTimer
    {
        public CodeTimer() : this(1) { }
        public CodeTimer(int iterationCount)
        {
            this.iterationCount = iterationCount;
            Prime = true;

            // Spin the CPU for a while.  This should help insure that the CPU gets out of any low power
            // mode so so that we get more stable results.
            // TODO: see if this is true, and if there is a better way of doing it.   
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 32)
                ;
        }
        /// <summary>
        /// The number of times the benchmark is run in a loop for a single measument.
        /// </summary>
        public int IterationCount
        {
            get { return iterationCount; }
            set
            {
                iterationCount = value;
                overheadValid = false;
            }
        }
        /// <summary>
        /// By default CodeTimer will run the action once before doing a
        /// measurement run.  This insures one-time actions like JIT
        /// compilation are not being measured.   However if the benchmark is
        /// not idempotent, this can be a problem.  Setting Prime=false
        /// insures that this Priming does not happen.   
        /// </summary>
        public bool Prime;
        public delegate void MeasureCallback(string name, int iterationCount, float sample);
        /// <summary>
        /// OnMeasure is signaled every time a Measure() is called. 
        /// </summary>
        public event MeasureCallback OnMeasure;
        /// <summary>
        /// The smallest time (in microseconds) that can be resolved by the timer). 
        /// </summary>
        public static float ResolutionUsec { get { return 1000000.0F / Stopwatch.Frequency; } }
        /// <summary>
        /// Returns the number of microsecond it took to run 'action', 'count' times.  
        /// </summary>
        public float Measure(string name, Action action)
        {
            return Measure(name, 1, action);
        }
        /// <summary>
        /// Returns the number of microseconds it to to run action 'count' times divided by 'scale'. 
        /// Scaling is useful if you want to normalize to a single iteration for example.  
        /// </summary>
        public float Measure(string name, float scale, Action action)
        {
            Stopwatch sw = new Stopwatch();

            // Run the action once to do any JITTing that might happen. 
            if (Prime)
                action();
            float overheadUsec = GetOverheadUsec(action);

            sw.Reset();
            sw.Start();
            for (int j = 0; j < iterationCount; j++)
                action();
            sw.Stop();

            float sampleUsec = (float)((sw.Elapsed.TotalMilliseconds * 1000.0F - overheadUsec) / scale / iterationCount);
            if (!computingOverhead && OnMeasure != null)
                OnMeasure(name, iterationCount, sampleUsec);
            return sampleUsec;
        }
        /// <summary>
        /// Prints the result of a CodeTimer to standard output.
        /// This is a good default target for OnMeasure.  
        /// </summary>
        public static MeasureCallback Print = delegate(string name, int iterationCount, float sample)
        {
            Console.WriteLine("{0}: count={1} time={2:f3} msec ", name, iterationCount, sample);
        };
        #region privates

        /// <summary>
        /// Time the overheadUsec of the harness that does nothing so we can subtract it out.
        /// 
        /// Because calling delegates on static methods is more expensive than caling delegates on
        /// instance methods we need the action to determine the overheadUsec. 
        /// </summary>
        /// <returns></returns>
        float GetOverheadUsec(Action action)
        {
            if (!overheadValid)
            {
                if (computingOverhead)
                    return 0.0F;
                computingOverhead = true;

                // Compute the overheads of calling differnet types of delegates. 
                Action emptyInstanceAction = new Action(this.emptyMethod);
                // Prime the actions (JIT them)
                Measure(null, emptyInstanceAction);
                // Compute the min over 5 runs (figuring better not to go negative) 
                instanceOverheadUsec = float.MaxValue;
                for (int i = 0; i < 5; i++)
                {
                    // We multiply by iteration count because we don't want this scaled by the
                    // count but 'Measure' does it by whether we want it or not.  
                    instanceOverheadUsec = Math.Min(Measure(null, emptyInstanceAction) * IterationCount, instanceOverheadUsec);
                }

                Action emptyStaticAction = new Action(emptyStaticMethod);
                Measure(null, emptyStaticAction);
                staticOverheadUsec = float.MaxValue;
                for (int i = 0; i < 5; i++)
                    staticOverheadUsec = Math.Min(Measure(null, emptyStaticAction) * IterationCount, staticOverheadUsec);

                computingOverhead = false;
                overheadValid = true;
            }

            if (action.Target == null)
                return staticOverheadUsec;
            else
                return instanceOverheadUsec;
        }

        static private void emptyStaticMethod() { }
        private void emptyMethod() { }

        bool overheadValid;
        bool computingOverhead;
        int iterationCount;
        float staticOverheadUsec;
        float instanceOverheadUsec;

        #endregion
    };
}

