<Query Kind="Statements">
  <Reference>C:\sd\rd_fabric_n_1.binaries.amd64chk\LinqPadPlugin\LinqPadHelpers.dll</Reference>
  <NuGetReference>MeasureIt.exe</NuGetReference>
  <Namespace>LinqPadHelpers</Namespace>
  <Namespace>PerformanceMeasurement</Namespace>
</Query>

// this is not a great performacne benchmark, but a good demo :) 
LinqPadUX.ComputerSpecs.Dump();
LinqPadUX.Measure.Action(()=>{}).Dump();