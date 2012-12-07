<Query Kind="Statements">
  <Reference>C:\sd\rd_fabric_n_1.binaries.amd64chk\LinqPadPlugin\LinqPadHelpers.dll</Reference>
  <NuGetReference>MeasureIt.exe</NuGetReference>
  <Namespace>LinqPadHelpers</Namespace>
  <Namespace>PerformanceMeasurement</Namespace>
</Query>

LinqPadUX.ComputerSpecs.Dump();
LinqPadUX.Measure.NamedActions(new NamedAction[]
{
	new NamedAction("new guid",()=>{new Guid();}),
	new NamedAction("TypeOf guid",()=>{typeof(Guid).ToString();}),
}).Dump();