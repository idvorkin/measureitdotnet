<Query Kind="Statements">
  <Reference>C:\sd\rd_fabric_n_1.binaries.amd64chk\LinqPadPlugin\LinqPadHelpers.dll</Reference>
  <Reference Relative="..\bin\Release\MeasureIt.exe">C:\hgs\measureitdotnet\bin\Release\MeasureIt.exe</Reference>
  <Namespace>LinqPadHelpers</Namespace>
  <Namespace>PerformanceMeasurement</Namespace>
</Query>

// this is not a great performacne benchmark, but a good demo :) 
LinqPadUX.ComputerSpecs.Dump();
LinqPadUX.Measure.Action(()=>{}).Dump();