## Measure it to create micro benchmarks in dot net

MeasureIt is now published on [NuGet](https://nuget.org/packages/MeasureIt.exe) , and updated to play nicely with LinqPad. To use open this [linq file](http://share.linqpad.net/e4vgtt.linq) or: 
1) Add MeasureIt.exe as a NuGet assembly. 
2)  Experiment with statements like: 

```
  LinqPadUX.ComputerSpecs.Dump();
  LinqPadUX.Measure.Action(()=>{}).Dump();}}
```

![](http://farm9.staticflickr.com/8207/8250931825_f87332d50a_o.png)

From  [Vance Morrison](http://blogs.msdn.com/b/vancem/)::

> Almost a year ago now I wrote [part 1](http://msdn.microsoft.com/en-us/magazine/cc500596.aspx)  and [part 2](http://msdn.microsoft.com/en-us/magazine/cc507639.aspx) of a MSDN article entitled 'Measure Early and Measure Often for Good Performance'.  In this article I argued that if you want to design high performance applications you need to be measuring performance early and often in the design process.   To help doing this I posted a tool call 'MeasureIt'.  It basically makes it easy to write benchmarks for .NET code.   In particular it also comes with at set of built-in benchmarks that measure the most important fundamental operations in .NET, so you can know what is expensive and what is not. 

Even though you can extract the code for MeasureIt by running MeasureIt /edit, it's published here under source control in case the community wishes to make changes. 
