<Query Kind="Statements">
  <NuGetReference>MeasureIt.exe</NuGetReference>
  <Namespace>PerformanceMeasurement</Namespace>
</Query>

// this is not a great performacne benchmark, but a good demo :) 
LinqPadUX.ComputerSpecs.Dump();
LinqPadUX.Measure.Action(()=>{}).Dump();
