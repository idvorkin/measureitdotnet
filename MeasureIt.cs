/***************************************************************************/
/*                            MeasureIt.cs                                 */
/***************************************************************************/
/* A harness that does a bunch of interesting performance experiments 
 * with the .NET Runtime

   Copyright (c) Microsoft Corporation.  All rights reserved.
   AUTHOR: Vance Morrison   Date  : 10/20/2007  */

/* This program uses code hyperlinks available as part of the HyperAddin */
/* Visual Studio plug-in. It is available from http://www.codeplex.com/hyperAddin */
/***************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;
using PerformanceMeasurement;
using System.Reflection;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using System.Security;
using System.Diagnostics;
using System.Text.RegularExpressions;

/* files are best browsed in 'outline form'. You outline code with Ctrl-M Ctrl-O. */
/* See code:MeasureIt to get started */

/// <summary>
/// MeasureIt is a simple harness that does a bunch of microbenchmark
/// performance experiments on the .NET runtime. It leverages the
/// code:MultiSampleCodeTimer to do the measuments and code:StatsLogger to
/// collect the results and display them as a HTML page.
/// 
/// The structure of MeasureIt is simple: There is a set of static functions
/// called Measure* which are a set of related measurements. The main program
/// code:MeasureIt.Main is responible for parsing command line argumends and
/// calling the Measure* functions. Use 'MeasureTheRuntime /? for usage.
/// 
/// Adding more measurements is as easy as adding another 'Measure*' static
/// method. Once the method exists the Main program will automatically call it
/// (it uses reflection to do this).
/// 
/// #Tips for writing a good microbenchmark
/// 
///     The most important aspect of writing a good microbenchmark is insuring
///     the time interval that you are measuruing is reasonably large (eg at
///     least 100us or more) It is already the case that the code:CodeTimer
///     helps by executing the code to measure many time (1000 in the
///     benchmarks below), however since even looping has overheadUsec (codeTimer
///     do their best to correct for it, but it can only do so much). Thus it
///     is best that what you want to measure is large in comparison to the
///     overheadUsec looping.
///     
///     You will see that in the code below often the thing being measured is
///     cloned 10 times. This has two good effects. The first is that it
///     increases the time interval being measured (as discussed above). But it
///     turns out that very small loops are prone to the exact layout of memory
///     and cache lines, leading to unreproducable results. By cloning the test
///     10 times you reduce this effect considerably.
///     
///     Even with all these mitgations, large run-to-run variations are
///     definately possible (although I have tried to control all the effects I
///     know about). Always take performance numbers as suspect until enough
///     data is collected and the results can be modeled (predicted), but some
///     reasonable rationale.
///     
///     Understand the inherent limitation of microbencharks. They only test
///     one very specific aspect of performance, and often other factors are
///     much more important in real programs. In particular microbencharks are
///     almost never memory cache limited, but real programs tend to be. Also
///     microbenchmarks don't tend to give 'real' inputs to the APIs they test.
///     Often the exact inputs don't matter, but for some APIs (eg hash tables,
///     sorting), it can be a big issue.
///     
///     It also pays to step through the code you are profiling in the debugger
///     to make certain that you are not being blindsided by something. The JIT
///     may have optimized what you wanted to measure away completely, or there
///     may be other unusual overheadUsec you are not aware of. This is all part of
///     the requirement to develop a model that explains the performance data
///     you get, and to not REALLY believe the numbers until you have created
///     such a model.
///     
///     As a way of cutting down on variation, I don't suggest believing
///     numbers that were gathered by running he appliation under a debugger.
///     In theory it should not matter, but I have seen cases where it does,
///     and until I understand just exactly when it matters, I would do all
///     measurment by running the application normally.
///     
///     Note that you will get sizable differences depending on exacty which
///     CPU you do the run. Newer CPUs from the same manufacturer can also be
///     different, so keep that in mind when looking at the numbers.
/// </summary>
static class MeasureIt
{
    // Uncomment the following attribute to test Addomain shared code.  
    // [LoaderOptimization(LoaderOptimization.MultiDomain)]
    static public int Main(string[] args)
    {
        bool skipMachineStats = false;
        List<MethodInfo> areas = new List<MethodInfo>();
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("/"))
            {
                MethodInfo area = GetAreaMethod(args[i]);
                if (area == null)
                {
                    Console.WriteLine("Unrecognised area '" + args[i] + "'. Use /? for list.");
                    return 1;
                }
                areas.Add(area);
            }
            else if (string.Compare(args[i], "/skipMachineStats", StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare(args[i], "/s") == 0)
                skipMachineStats = true;
            else if (string.Compare(args[i], "/UsersGuide", StringComparison.OrdinalIgnoreCase) == 0)
                UsersGuide.DisplayConsoleAppUsersGuide("UsersGuide.htm");
            else if (string.Compare(args[i], "/SetHighPerf", StringComparison.OrdinalIgnoreCase) == 0)
                return PowerManagment.Set(PowerManagment.HighPerformance) ? 0 : 1;
            else if (string.Compare(args[i], "/SetBalenced", StringComparison.OrdinalIgnoreCase) == 0)
                return PowerManagment.Set(PowerManagment.Balenced) ? 0 : 1;
            else if (string.Compare(args[i], "/SetPowerSaver", StringComparison.OrdinalIgnoreCase) == 0)
                return PowerManagment.Set(PowerManagment.PowerSaver) ? 0 : 1;
            else if (string.Compare(args[i], "/Edit", StringComparison.OrdinalIgnoreCase) == 0)
            {
                string srcDir = UnpackAttachedSource(null);
                string programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
                string slnFile = null;
                // To avoid the conversion dialog, use the V9 (VS2008) 
                string vs9 = Path.Combine(programFiles, "Microsoft Visual Studio 9.0");
                if (Directory.Exists(vs9))
                    slnFile = Path.Combine(srcDir, "MeasureIt.V9.sln");
                else
                {
                    string vs8 = Path.Combine(programFiles, "Microsoft Visual Studio 8");
                    if (Directory.Exists(vs8))
                        slnFile = Path.Combine(srcDir, "MeasureIt.sln");
                }
                if (slnFile != null)
                {
                    Console.WriteLine("Launching solution " + slnFile);
                    Process process = new Process();
                    process.StartInfo = new ProcessStartInfo(slnFile);
                    process.Start();
                }
                return 0;
            }
            else if (string.Compare(args[i], "/?", StringComparison.OrdinalIgnoreCase) == 0)
            {
                Console.WriteLine("Usage MeasureTheRuntime [/?] [/usersGuide] [/skipMachineStats] [areas]");
                Console.WriteLine();
                Console.WriteLine("Runs a set of microbenchmarks.");
                Console.WriteLine();
                Console.WriteLine("Qualifiers:");
                Console.WriteLine("    /edit               Unpack and edit the source code.  ");
                Console.WriteLine("    /userGuide          Display the users guide.  ");
                Console.WriteLine("    /setHighPerf        Set power policy to 'High Performance'.  ");
                Console.WriteLine("    /setBalenced        Set power policy to 'Balenced'.  ");
                Console.WriteLine("    /setPowerSaver      Set power policy to 'Power Saver'.  ");
                Console.WriteLine("    /skipMachineStats   (/s) Speeds startup by skipping machine stats.");
                Console.WriteLine();
                Console.WriteLine("Areas: (no area means all these areas)");
                foreach (MethodInfo areaMethod in GetAreaMethods(false))
                    Console.WriteLine("    " + areaMethod.Name.Substring(7));
                Console.WriteLine();
                Console.WriteLine("Private Areas: (Must be explicitly mentioned on command line.)");
                foreach (MethodInfo areaMethod in GetAreaMethods(true))
                    Console.WriteLine("    " + areaMethod.Name.Substring(7));
                return 1;
            }
            else
            {
                Console.WriteLine("Unrecognised qualifer " + args[i] + " use /? for usage.");
                return 1;
            }
        }
        if (areas.Count == 0)
            areas = GetAreaMethods(false);

        StatsCollection data = new StatsCollection();
        logger = new StatsLogger(data);
        Console.WriteLine("Collecting Stats on the computer.");
        logger.CaptureCurrentMachineInfo(Assembly.GetExecutingAssembly(), skipMachineStats);

        string reportFileName = Path.Combine(Environment.GetEnvironmentVariable("TEMP"), "MeasureIt.html");
        RunAtHighPerfPolicy(delegate 
        { 
            Stats methodCallStats = RunTests(areas);
            logger.Scale = methodCallStats.Median;
            logger.UnitsDescription = "Scaled where EmptyStaticFunction = 1.0 (" +
                (logger.Scale * 1000).ToString("f1") + " nsec = 1.0 units)";

            Console.WriteLine("Writing report to " + reportFileName + ".");
            logger.DisplayHtmlReport(reportFileName);
            Console.WriteLine("Launching Internet Explorer on " + reportFileName + ".");
        });

        StatsLogger.LaunchIE(reportFileName); Console.WriteLine("Done.");
        return 0;
    }
    private static Stats RunTests(List<MethodInfo> areas)
    {
        Console.WriteLine("Measuring interesting runtime operations.  Use /? or /usersGuide for more help.");
        timer1000 = new MultiSampleCodeTimer(10, 1000);
        timer1000.OnMeasure += logger.AddWithCount;

        timer1 = new MultiSampleCodeTimer(10, 1);
        timer1.OnMeasure += logger.AddWithCount;

        timer100 = new MultiSampleCodeTimer(10, 100);
        timer100.OnMeasure += logger.AddWithCount; 

        timer10 = new MultiSampleCodeTimer(10, 10);
        timer10.OnMeasure += logger.AddWithCount; 

        // Just to show you the amount of measurement error you get (since it should be 0)
        timer1000.Measure("NOTHING", 1, delegate
        {
        });

        // Do this one unconditionally, because it is used as the baseline for everything else. 
        Stats methodCallStats = timer1000.Measure("MethodCalls: EmptyStaticFunction()", 10, delegate
        {
            Class.EmptyStaticFunction();
            Class.EmptyStaticFunction();
            Class.EmptyStaticFunction();
            Class.EmptyStaticFunction();
            Class.EmptyStaticFunction();
            Class.EmptyStaticFunction();
            Class.EmptyStaticFunction();
            Class.EmptyStaticFunction();
            Class.EmptyStaticFunction();
            Class.EmptyStaticFunction();
        });

        // This also has the nice effect of kicking the CPU out of any low
        // power state.  
        int dummy;
        timer1000.Measure("Loop 1K times", 1, delegate
        {
            int k = 0;
            while (k < 1000)
                k++;        // still in danger of being optimized.  
            dummy = k;      // avoid optimization.  
        });

        foreach (MethodInfo areaMethod in areas)
        {
            string area = areaMethod.Name.Substring(7);
            Console.WriteLine("Running " + area);
            logger.Category = area;
            areaMethod.Invoke(null, null);
            logger.Category = null;
        }
        return methodCallStats;
    }

    /* To add another suite of benchmarks, simply add a method that begins */
    /* with 'Measure' to this class.  If the method is public it will be   */
    /* run by default.  Private methods must be explictly mentioned        */
    /* see file:UsersGuide.htm for more */
    static public void MeasureMethodCalls()
    {
        Class aClass = new Class();
        SealedClass aSealedClass = new SealedClass();
        StructWithInterface aStructWithInterface = new StructWithInterface();
        AnInterface aInterface = new Class();
        SuperClass aSuperClass = new Class();
        ValueType aValueType;

        timer1000.Measure("EmptyStaticFunction(arg1,...arg5)", 10, delegate
        {
            Class.EmptyStaticFunction5Arg(1, 2, 3, 4, 5);
            Class.EmptyStaticFunction5Arg(1, 2, 3, 4, 5);
            Class.EmptyStaticFunction5Arg(1, 2, 3, 4, 5);
            Class.EmptyStaticFunction5Arg(1, 2, 3, 4, 5);
            Class.EmptyStaticFunction5Arg(1, 2, 3, 4, 5);
            Class.EmptyStaticFunction5Arg(1, 2, 3, 4, 5);
            Class.EmptyStaticFunction5Arg(1, 2, 3, 4, 5);
            Class.EmptyStaticFunction5Arg(1, 2, 3, 4, 5);
            Class.EmptyStaticFunction5Arg(1, 2, 3, 4, 5);
            Class.EmptyStaticFunction5Arg(1, 2, 3, 4, 5);
        });


        timer1000.Measure("aClass.EmptyInstanceFunction()", 10, delegate
        {
            aClass.EmptyInstanceFunction();
            aClass.EmptyInstanceFunction();
            aClass.EmptyInstanceFunction();
            aClass.EmptyInstanceFunction();
            aClass.EmptyInstanceFunction();
            aClass.EmptyInstanceFunction();
            aClass.EmptyInstanceFunction();
            aClass.EmptyInstanceFunction();
            aClass.EmptyInstanceFunction();
            aClass.EmptyInstanceFunction();
        });

        timer1000.Measure("aClass.Interface()", 10, delegate
        {
            aInterface.InterfaceMethod();
            aInterface.InterfaceMethod();
            aInterface.InterfaceMethod();
            aInterface.InterfaceMethod();
            aInterface.InterfaceMethod();
            aInterface.InterfaceMethod();
            aInterface.InterfaceMethod();
            aInterface.InterfaceMethod();
            aInterface.InterfaceMethod();
            aInterface.InterfaceMethod();
        });

        // note that these calls inline away completely, but that is what I wanted to show. Namely
        // calling an interface method directly on a sealed class has no extra overheadUsec over a normal
        // call
        timer1000.Measure("aSealedClass.Interface() (inlined)", 10, delegate
        {
            aSealedClass.InterfaceMethod();
            aSealedClass.InterfaceMethod();
            aSealedClass.InterfaceMethod();
            aSealedClass.InterfaceMethod();
            aSealedClass.InterfaceMethod();
            aSealedClass.InterfaceMethod();
            aSealedClass.InterfaceMethod();
            aSealedClass.InterfaceMethod();
            aSealedClass.InterfaceMethod();
            aSealedClass.InterfaceMethod();
        });

        // note that these calls inline away completely, but that is what I wanted to show. Namely
        // calling an interface method directly on a struct has no extra overheadUsec over a normal call
        timer1000.Measure("aStructWithInterface.Interface() (inlined)", 10, delegate
        {
            aStructWithInterface.InterfaceMethod();
            aStructWithInterface.InterfaceMethod();
            aStructWithInterface.InterfaceMethod();
            aStructWithInterface.InterfaceMethod();
            aStructWithInterface.InterfaceMethod();
            aStructWithInterface.InterfaceMethod();
            aStructWithInterface.InterfaceMethod();
            aStructWithInterface.InterfaceMethod();
            aStructWithInterface.InterfaceMethod();
            aStructWithInterface.InterfaceMethod();
        });

        timer1000.Measure("aClass.VirtualMethod()", 10, delegate
        {
            aSuperClass.VirtualMethod();
            aSuperClass.VirtualMethod();
            aSuperClass.VirtualMethod();
            aSuperClass.VirtualMethod();
            aSuperClass.VirtualMethod();
            aSuperClass.VirtualMethod();
            aSuperClass.VirtualMethod();
            aSuperClass.VirtualMethod();
            aSuperClass.VirtualMethod();
            aSuperClass.VirtualMethod();
        });

        timer1000.Measure("Class.ReturnsValueType()", 10, delegate
        {
            aValueType = Class.ReturnsValueType();
            aValueType = Class.ReturnsValueType();
            aValueType = Class.ReturnsValueType();
            aValueType = Class.ReturnsValueType();
            aValueType = Class.ReturnsValueType();
            aValueType = Class.ReturnsValueType();
            aValueType = Class.ReturnsValueType();
            aValueType = Class.ReturnsValueType();
            aValueType = Class.ReturnsValueType();
            aValueType = Class.ReturnsValueType();
        });
    }
    static public void MeasureFieldAccess()
    {

        Class aClass = new Class();
        string aString = "aString";

        timer1000.Measure("aStaticInt++", 10, delegate
        {
            Class.aStaticInt++;
            Class.aStaticInt++;
            Class.aStaticInt++;
            Class.aStaticInt++;
            Class.aStaticInt++;
            Class.aStaticInt++;
            Class.aStaticInt++;
            Class.aStaticInt++;
            Class.aStaticInt++;
            Class.aStaticInt++;
        });

        timer1000.Measure("aClass.aInstanceInt++", 10, delegate
        {
            aClass.aInstanceInt++;
            aClass.aInstanceInt++;
            aClass.aInstanceInt++;
            aClass.aInstanceInt++;
            aClass.aInstanceInt++;
            aClass.aInstanceInt++;
            aClass.aInstanceInt++;
            aClass.aInstanceInt++;
            aClass.aInstanceInt++;
            aClass.aInstanceInt++;
        });


        timer1000.Measure("aClass.aIntstanceString = \"hi\"", 10, delegate
        {
            string aLocalString = "hi";
            Class local = aClass;
            local.aInstanceString = aLocalString;
            local.aInstanceString = aLocalString;
            local.aInstanceString = aLocalString;
            local.aInstanceString = aLocalString;
            local.aInstanceString = aLocalString;
            local.aInstanceString = aLocalString;
            local.aInstanceString = aLocalString;
            local.aInstanceString = aLocalString;
            local.aInstanceString = aLocalString;
            local.aInstanceString = aLocalString;
        });

        timer1000.Measure("aStaticString = aString", 10, delegate
        {
            Class.aStaticString = aString;
            Class.aStaticString = aString;
            Class.aStaticString = aString;
            Class.aStaticString = aString;
            Class.aStaticString = aString;
            Class.aStaticString = aString;
            Class.aStaticString = aString;
            Class.aStaticString = aString;
            Class.aStaticString = aString;
            Class.aStaticString = aString;
        });
    }
    static public void MeasureObjectOps()
    {
        Type classType = typeof(Class);
        Class classInstance = new Class();
        object aObjectString = "aString1";
        bool aBool;
        string aString;

        timer1000.Measure("(aObjectString is String)", 10, delegate
        {
            bool b;
            b = aObjectString is String;
            b = aObjectString is String;
            b = aObjectString is String;
            b = aObjectString is String;
            b = aObjectString is String;
            b = aObjectString is String;
            b = aObjectString is String;
            b = aObjectString is String;
            b = aObjectString is String;
            b = aObjectString is String;
            aBool = b;
        });

        timer1000.Measure("(aObjectString as String)", 10, delegate
        {
            string s;
            s = aObjectString as string;
            s = aObjectString as string;
            s = aObjectString as string;
            s = aObjectString as string;
            s = aObjectString as string;
            s = aObjectString as string;
            s = aObjectString as string;
            s = aObjectString as string;
            s = aObjectString as string;
            s = aObjectString as string;
            aString = s;
        });

        timer1000.Measure("(string) aObjectString)", 10, delegate
        {
            string s;
            s = (string)aObjectString;
            s = (string)aObjectString;
            s = (string)aObjectString;
            s = (string)aObjectString;
            s = (string)aObjectString;
            s = (string)aObjectString;
            s = (string)aObjectString;
            s = (string)aObjectString;
            s = (string)aObjectString;
            s = (string)aObjectString;
            aString = s;

        });

        timer1000.Measure("new Class()", 10, delegate
        {
            new Class();
            new Class();
            new Class();
            new Class();
            new Class();
            new Class();
            new Class();
            new Class();
            new Class();
            new Class();
        });

        timer1000.Measure("new FinalizableClass()", 10, delegate
        {
            new FinalizableClass();
            new FinalizableClass();
            new FinalizableClass();
            new FinalizableClass();
            new FinalizableClass();
            new FinalizableClass();
            new FinalizableClass();
            new FinalizableClass();
            new FinalizableClass();
            new FinalizableClass();
        });

        timer1000.Measure("Activator.CreateInstance<Class>()", 10, delegate
        {
            Activator.CreateInstance<Class>();
            Activator.CreateInstance<Class>();
            Activator.CreateInstance<Class>();
            Activator.CreateInstance<Class>();
            Activator.CreateInstance<Class>();
            Activator.CreateInstance<Class>();
            Activator.CreateInstance<Class>();
            Activator.CreateInstance<Class>();
            Activator.CreateInstance<Class>();
            Activator.CreateInstance<Class>();
        });
        timer1000.Measure("(Class) Activator.CreateInstance(classType)", 10, delegate
        {
            classInstance = (Class)Activator.CreateInstance(classType);
            classInstance = (Class)Activator.CreateInstance(classType);
            classInstance = (Class)Activator.CreateInstance(classType);
            classInstance = (Class)Activator.CreateInstance(classType);
            classInstance = (Class)Activator.CreateInstance(classType);
            classInstance = (Class)Activator.CreateInstance(classType);
            classInstance = (Class)Activator.CreateInstance(classType);
            classInstance = (Class)Activator.CreateInstance(classType);
            classInstance = (Class)Activator.CreateInstance(classType);
            classInstance = (Class)Activator.CreateInstance(classType);
        });

        timer1000.Measure("(Class) classInstance.MemberWiseClone()", 10, delegate
        {
            classInstance = (Class)classInstance.Clone();
            classInstance = (Class)classInstance.Clone();
            classInstance = (Class)classInstance.Clone();
            classInstance = (Class)classInstance.Clone();
            classInstance = (Class)classInstance.Clone();
            classInstance = (Class)classInstance.Clone();
            classInstance = (Class)classInstance.Clone();
            classInstance = (Class)classInstance.Clone();
            classInstance = (Class)classInstance.Clone();
            classInstance = (Class)classInstance.Clone();
        });


        // TODO casts that fail. 
    }
    static unsafe public void MeasureArrays()
    {
        int[] aIntArray = new int[20];
        fixed (int* aIntPtr = aIntArray)
        {
            int aInt = 1;
            AnInterface[] aInterfaceArray = new Class[10];
            Class aClass = new Class();
            string[] aStringArray = new string[10];
            string aString = "foo";

            timer1000.Measure("aIntArray[i] = 1", 10, delegate
            {
                aIntArray[0] = aInt;
                aIntArray[1] = aInt;
                aIntArray[2] = aInt;
                aIntArray[3] = aInt;
                aIntArray[4] = aInt;
                aIntArray[5] = aInt;
                aIntArray[6] = aInt;
                aIntArray[7] = aInt;
                aIntArray[8] = aInt;
                aIntArray[9] = aInt;
            });

            timer1000.Measure("localIntPtr[i] = 1", 10, delegate
            {
                fixed (int* localIntPtr = aIntArray)
                {
                    localIntPtr[0] = aInt;
                    localIntPtr[1] = aInt;
                    localIntPtr[2] = aInt;
                    localIntPtr[3] = aInt;
                    localIntPtr[4] = aInt;
                    localIntPtr[5] = aInt;
                    localIntPtr[6] = aInt;
                    localIntPtr[7] = aInt;
                    localIntPtr[8] = aInt;
                    localIntPtr[9] = aInt;
                }
            });

            timer1000.Measure("aIntPtr[i] = 1", 10, delegate
            {
                aIntPtr[0] = aInt;
                aIntPtr[1] = aInt;
                aIntPtr[2] = aInt;
                aIntPtr[3] = aInt;
                aIntPtr[4] = aInt;
                aIntPtr[5] = aInt;
                aIntPtr[6] = aInt;
                aIntPtr[7] = aInt;
                aIntPtr[8] = aInt;
                aIntPtr[9] = aInt;
            });

            timer1000.Measure("string[i] = aString", 10, delegate
            {
                aStringArray[0] = aString;
                aStringArray[1] = aString;
                aStringArray[2] = aString;
                aStringArray[3] = aString;
                aStringArray[4] = aString;
                aStringArray[5] = aString;
                aStringArray[6] = aString;
                aStringArray[7] = aString;
                aStringArray[8] = aString;
                aStringArray[9] = aString;
            });

            timer1000.Measure("aInterfaceArray[i] = aClass", 10, delegate
            {
                aInterfaceArray[0] = aClass;
                aInterfaceArray[1] = aClass;
                aInterfaceArray[2] = aClass;
                aInterfaceArray[3] = aClass;
                aInterfaceArray[4] = aClass;
                aInterfaceArray[5] = aClass;
                aInterfaceArray[6] = aClass;
                aInterfaceArray[7] = aClass;
                aInterfaceArray[8] = aClass;
                aInterfaceArray[9] = aClass;
            });

            timer1000.Measure("1 for...Length aIntArray[i] = 1", 1, delegate
            {
                int[] localIntArray = aIntArray;
                for (int i = 0; i < localIntArray.Length; i++)
                    localIntArray[i] = aInt;
            });

            timer1000.Measure("1 for...10 aIntArray[i] = 1", 1, delegate
            {
                int[] localIntArray = aIntArray;
                for (int i = 0; i < 10; i++)
                    localIntArray[i] = aInt;
            });

            timer1000.Measure("1 for...Length aStringArray[i] = 1", 1, delegate
            {
                string[] localStringArray = aStringArray;
                for (int i = 0; i < localStringArray.Length; i++)
                    localStringArray[i] = aString;
            });

            timer1000.Measure("1 for...Length aInterfaceArray[i] = 1", 1, delegate
            {
                AnInterface[] localInterfaceArray = aInterfaceArray;
                for (int i = 0; i < localInterfaceArray.Length; i++)
                    localInterfaceArray[i] = aClass;
            });

        }
    }
    static public void MeasureDelegates()
    {
        Predicate<int> action = delegate(int i) { return i > 0; };
        IAsyncResult result = action.BeginInvoke(3, null, null);
        bool resultValue = action.EndInvoke(result);

        Class aClass = new Class();
        MyDelegate aInstanceDelegate = new MyDelegate(aClass.EmptyInstanceFunction);
        MyDelegate aStaticDelegate = new MyDelegate(Class.EmptyStaticFunction);

        timer1000.Measure("new MyDelegate(aClass.EmptyInstanceFunction)", 10, delegate
        {
            MyDelegate aMyDelegate;
            aMyDelegate = new MyDelegate(aClass.EmptyInstanceFunction);
            aMyDelegate = new MyDelegate(aClass.EmptyInstanceFunction);
            aMyDelegate = new MyDelegate(aClass.EmptyInstanceFunction);
            aMyDelegate = new MyDelegate(aClass.EmptyInstanceFunction);
            aMyDelegate = new MyDelegate(aClass.EmptyInstanceFunction);
            aMyDelegate = new MyDelegate(aClass.EmptyInstanceFunction);
            aMyDelegate = new MyDelegate(aClass.EmptyInstanceFunction);
            aMyDelegate = new MyDelegate(aClass.EmptyInstanceFunction);
            aMyDelegate = new MyDelegate(aClass.EmptyInstanceFunction);
            aMyDelegate = new MyDelegate(aClass.EmptyInstanceFunction);
        });

        timer1000.Measure("new MyDelegate(Class.StaticFunction)", 10, delegate
        {
            MyDelegate aMyDelegate;
            aMyDelegate = new MyDelegate(Class.EmptyStaticFunction);
            aMyDelegate = new MyDelegate(Class.EmptyStaticFunction);
            aMyDelegate = new MyDelegate(Class.EmptyStaticFunction);
            aMyDelegate = new MyDelegate(Class.EmptyStaticFunction);
            aMyDelegate = new MyDelegate(Class.EmptyStaticFunction);
            aMyDelegate = new MyDelegate(Class.EmptyStaticFunction);
            aMyDelegate = new MyDelegate(Class.EmptyStaticFunction);
            aMyDelegate = new MyDelegate(Class.EmptyStaticFunction);
            aMyDelegate = new MyDelegate(Class.EmptyStaticFunction);
            aMyDelegate = new MyDelegate(Class.EmptyStaticFunction);
            aMyDelegate = new MyDelegate(Class.EmptyStaticFunction);
        });

        timer1000.Measure("aInstanceDelegate()", 10, delegate
        {
            aInstanceDelegate();
            aInstanceDelegate();
            aInstanceDelegate();
            aInstanceDelegate();
            aInstanceDelegate();
            aInstanceDelegate();
            aInstanceDelegate();
            aInstanceDelegate();
            aInstanceDelegate();
            aInstanceDelegate();
        });

        timer1000.Measure("aStaticDelegate()", 10, delegate
        {
            aStaticDelegate();
            aStaticDelegate();
            aStaticDelegate();
            aStaticDelegate();
            aStaticDelegate();
            aStaticDelegate();
            aStaticDelegate();
            aStaticDelegate();
            aStaticDelegate();
            aStaticDelegate();
        });
    }
    static public void MeasureEvents()
    {
        Class c = new Class();

        // The target is an empty function. 
        c.AnEvent += new MyDelegate(c.EmptyInstanceFunction);


        c.MeasureFire10(timer1000);
    }
    static public void MeasureGenericsOnValueType()
    {
        GenericClass<int> aGenericClassWithInt = new GenericClass<int>();
        // GenericValueClass<int> aGenericValueClassWithInt = new GenericValueClass<int>();

        object intObject = 3;
        int aInt = 3;

        timer1000.Measure("aGenericClassWithInt.aGenericInstanceFieldT = aInt", 10, delegate
        {
            aGenericClassWithInt.aGenericInstanceFieldT = aInt;
            aGenericClassWithInt.aGenericInstanceFieldT = aInt;
            aGenericClassWithInt.aGenericInstanceFieldT = aInt;
            aGenericClassWithInt.aGenericInstanceFieldT = aInt;
            aGenericClassWithInt.aGenericInstanceFieldT = aInt;
            aGenericClassWithInt.aGenericInstanceFieldT = aInt;
            aGenericClassWithInt.aGenericInstanceFieldT = aInt;
            aGenericClassWithInt.aGenericInstanceFieldT = aInt;
            aGenericClassWithInt.aGenericInstanceFieldT = aInt;
            aGenericClassWithInt.aGenericInstanceFieldT = aInt;
        });

        timer1000.Measure("GenericClass<int>.aGenericStaticFieldT = aInt", 10, delegate
        {
            GenericClass<int>.aGenericStaticFieldT = aInt;
            GenericClass<int>.aGenericStaticFieldT = aInt;
            GenericClass<int>.aGenericStaticFieldT = aInt;
            GenericClass<int>.aGenericStaticFieldT = aInt;
            GenericClass<int>.aGenericStaticFieldT = aInt;
            GenericClass<int>.aGenericStaticFieldT = aInt;
            GenericClass<int>.aGenericStaticFieldT = aInt;
            GenericClass<int>.aGenericStaticFieldT = aInt;
            GenericClass<int>.aGenericStaticFieldT = aInt;
            GenericClass<int>.aGenericStaticFieldT = aInt;
        });

        timer1000.Measure("aGenericClassWithInt.ClassGenericInstanceMethod()", 10, delegate
        {
            aGenericClassWithInt.ClassGenericInstanceMethod();
            aGenericClassWithInt.ClassGenericInstanceMethod();
            aGenericClassWithInt.ClassGenericInstanceMethod();
            aGenericClassWithInt.ClassGenericInstanceMethod();
            aGenericClassWithInt.ClassGenericInstanceMethod();
            aGenericClassWithInt.ClassGenericInstanceMethod();
            aGenericClassWithInt.ClassGenericInstanceMethod();
            aGenericClassWithInt.ClassGenericInstanceMethod();
            aGenericClassWithInt.ClassGenericInstanceMethod();
            aGenericClassWithInt.ClassGenericInstanceMethod();
        });

        timer1000.Measure("GenericClass<int>.ClassGenericStaticMethod()", 10, delegate
        {
            GenericClass<int>.ClassGenericStaticMethod();
            GenericClass<int>.ClassGenericStaticMethod();
            GenericClass<int>.ClassGenericStaticMethod();
            GenericClass<int>.ClassGenericStaticMethod();
            GenericClass<int>.ClassGenericStaticMethod();
            GenericClass<int>.ClassGenericStaticMethod();
            GenericClass<int>.ClassGenericStaticMethod();
            GenericClass<int>.ClassGenericStaticMethod();
            GenericClass<int>.ClassGenericStaticMethod();
            GenericClass<int>.ClassGenericStaticMethod();
        });


        timer1000.Measure("Class.GenericMethod<int>()", 10, delegate
        {
            Class.GenericMethod<int>();
            Class.GenericMethod<int>();
            Class.GenericMethod<int>();
            Class.GenericMethod<int>();
            Class.GenericMethod<int>();
            Class.GenericMethod<int>();
            Class.GenericMethod<int>();
            Class.GenericMethod<int>();
            Class.GenericMethod<int>();
            Class.GenericMethod<int>();
        });

    }
    static public void MeasureGenericsOnReferenceType()
    {
        GenericClass<string> aGenericClassWithString = new GenericClass<string>();
        // GenericValueClass<string> aGenericValueClassWithString = new GenericValueClass<string>();

        object stringObject = "foo";
        string aString = "foo";

        timer1000.Measure("aGenericClassWithString.aGenericInstanceFieldT = aString", 10, delegate
        {
            aGenericClassWithString.aGenericInstanceFieldT = aString;
            aGenericClassWithString.aGenericInstanceFieldT = aString;
            aGenericClassWithString.aGenericInstanceFieldT = aString;
            aGenericClassWithString.aGenericInstanceFieldT = aString;
            aGenericClassWithString.aGenericInstanceFieldT = aString;
            aGenericClassWithString.aGenericInstanceFieldT = aString;
            aGenericClassWithString.aGenericInstanceFieldT = aString;
            aGenericClassWithString.aGenericInstanceFieldT = aString;
            aGenericClassWithString.aGenericInstanceFieldT = aString;
            aGenericClassWithString.aGenericInstanceFieldT = aString;
        });

        timer1000.Measure("GenericClass<string>.aGenericStaticFieldT = aString", 10, delegate
        {
            GenericClass<string>.aGenericStaticFieldT = aString;
            GenericClass<string>.aGenericStaticFieldT = aString;
            GenericClass<string>.aGenericStaticFieldT = aString;
            GenericClass<string>.aGenericStaticFieldT = aString;
            GenericClass<string>.aGenericStaticFieldT = aString;
            GenericClass<string>.aGenericStaticFieldT = aString;
            GenericClass<string>.aGenericStaticFieldT = aString;
            GenericClass<string>.aGenericStaticFieldT = aString;
            GenericClass<string>.aGenericStaticFieldT = aString;
            GenericClass<string>.aGenericStaticFieldT = aString;
        });

        timer1000.Measure("aGenericClassWithString.ClassGenericInstanceMethod()", 10, delegate
        {
            aGenericClassWithString.ClassGenericInstanceMethod();
            aGenericClassWithString.ClassGenericInstanceMethod();
            aGenericClassWithString.ClassGenericInstanceMethod();
            aGenericClassWithString.ClassGenericInstanceMethod();
            aGenericClassWithString.ClassGenericInstanceMethod();
            aGenericClassWithString.ClassGenericInstanceMethod();
            aGenericClassWithString.ClassGenericInstanceMethod();
            aGenericClassWithString.ClassGenericInstanceMethod();
            aGenericClassWithString.ClassGenericInstanceMethod();
            aGenericClassWithString.ClassGenericInstanceMethod();
        });

        timer1000.Measure("GenericClass<string>.ClassGenericStaticMethod()", 10, delegate
        {
            GenericClass<string>.ClassGenericStaticMethod();
            GenericClass<string>.ClassGenericStaticMethod();
            GenericClass<string>.ClassGenericStaticMethod();
            GenericClass<string>.ClassGenericStaticMethod();
            GenericClass<string>.ClassGenericStaticMethod();
            GenericClass<string>.ClassGenericStaticMethod();
            GenericClass<string>.ClassGenericStaticMethod();
            GenericClass<string>.ClassGenericStaticMethod();
            GenericClass<string>.ClassGenericStaticMethod();
            GenericClass<string>.ClassGenericStaticMethod();
        });


        timer1000.Measure("Class.GenericMethod<string>()", 10, delegate
        {
            Class.GenericMethod<string>();
            Class.GenericMethod<string>();
            Class.GenericMethod<string>();
            Class.GenericMethod<string>();
            Class.GenericMethod<string>();
            Class.GenericMethod<string>();
            Class.GenericMethod<string>();
            Class.GenericMethod<string>();
            Class.GenericMethod<string>();
            Class.GenericMethod<string>();
        });
    }
    static public void MeasureIteration()
    {
        List<string> sList = new List<string>();
        List<int> iList = new List<int>();
        int[] aList = new int[100];
        int iResult = 0;
        string sResult = null;
        for (int i = 0; i < 100; i++)
        {
            aList[i] = i;
            sList.Add("test");
            iList.Add(i);
        }

        timer1000.Measure("sum numbers 1-20", 1, delegate
        {
            int result = 0;
            for (int i = 0; i < 20; i++)
                result += i;
            iResult = result;
        });

        timer1000.Measure("sum numbers 1-100", 1, delegate
        {
            int result = 0;
            for (int i = 0; i < 100; i++)
                result += i;
            iResult = result;
        });

        timer1000.Measure("foreach over List<String> (100 elems)", 1, delegate
        {
            foreach (string s in sList)
                sResult = s;
        });
        timer1000.Measure("for over List<String> (100 elems)", 1, delegate
        {
            for (int i = 0; i < sList.Count; i++)
                sResult = sList[i];
        });

        timer1000.Measure("foreach over List<int> (100 elems)", 1, delegate
        {
            foreach (int i in iList)
                iResult = i;
        });
        timer1000.Measure("for over List<int> (100 elems)", 1, delegate
        {
            for (int i = 0; i < iList.Count; i++)
                iResult = iList[i];
        });

        timer1000.Measure("for with check int[] (100 elems)", 1, delegate
        {
            for (int i = 0; i < 100; i++)
                iResult = aList[i];
        });
        timer1000.Measure("for with bound Optimization int[] (100 elems)", 1, delegate
        {
            int[] arr = aList;
            for (int i = 0; i < arr.Length; i++)
                iResult = arr[i];
        });

        ValueType[] vArr = new ValueType[100];
        timer1000.Measure("foreach over ValueType[] (100 elems)", 1, delegate
        {
            foreach (ValueType v in vArr)
                iResult = v.x;
        });
        timer1000.Measure("Foreach method over ValueType[] (100 elems)", 1, delegate
        {
            ValueType.Foreach(vArr, delegate(ref ValueType v)
            {
                iResult = v.x;
            });
        });

    }
    static public void MeasureTypeReflection()
    {
        RuntimeTypeHandle typeHandle = default(RuntimeTypeHandle);
        Type type = null;
        object anObject = "aString";
        object anArray = new string[0];
        bool result = false;

        timer1000.Measure("anObject.GetType()", 10, delegate
        {
            type = anObject.GetType();
            type = anObject.GetType();
            type = anObject.GetType();
            type = anObject.GetType();
            type = anObject.GetType();
            type = anObject.GetType();
            type = anObject.GetType();
            type = anObject.GetType();
            type = anObject.GetType();
            type = anObject.GetType();
        });

        // Fetching types of arrays are slower than normal objects. 
        timer1000.Measure("anArray.GetType()", 10, delegate
        {
            type = anArray.GetType();
            type = anArray.GetType();
            type = anArray.GetType();
            type = anArray.GetType();
            type = anArray.GetType();
            type = anArray.GetType();
            type = anArray.GetType();
            type = anArray.GetType();
            type = anArray.GetType();
            type = anArray.GetType();
        });

        timer1000.Measure("typeof(string[])", 10, delegate
        {
            type = typeof(string[]);
            type = typeof(string[]);
            type = typeof(string[]);
            type = typeof(string[]);
            type = typeof(string[]);
            type = typeof(string[]);
            type = typeof(string[]);
            type = typeof(string[]);
            type = typeof(string[]);
        });

        // Baseline: fetching the System.Type for a literal type
        timer1000.Measure("typeof(string)", 10, delegate
        {
            type = typeof(string);
            type = typeof(string);
            type = typeof(string);
            type = typeof(string);
            type = typeof(string);
            type = typeof(string);
            type = typeof(string);
            type = typeof(string);
            type = typeof(string);
        });

        // Geting the type RuntimeTypeHandle is much faster!
        timer1000.Measure("typeof(string).TypeHandle            ", 10, delegate
        {
            typeHandle = typeof(string).TypeHandle;
            typeHandle = typeof(string).TypeHandle;
            typeHandle = typeof(string).TypeHandle;
            typeHandle = typeof(string).TypeHandle;
            typeHandle = typeof(string).TypeHandle;
            typeHandle = typeof(string).TypeHandle;
            typeHandle = typeof(string).TypeHandle;
            typeHandle = typeof(string).TypeHandle;
            typeHandle = typeof(string).TypeHandle;
            typeHandle = typeof(string).TypeHandle;
        });

        // Baseline: checking if an object is of a type
        timer1000.Measure("anObject.GetType() == type           ", 10, delegate
        {
            result = anObject.GetType() == type;
            result = anObject.GetType() == type;
            result = anObject.GetType() == type;
            result = anObject.GetType() == type;
            result = anObject.GetType() == type;
            result = anObject.GetType() == type;
            result = anObject.GetType() == type;
            result = anObject.GetType() == type;
            result = anObject.GetType() == type;
            result = anObject.GetType() == type;
        });

        // But if you are checking against a particular type, it is fast.  
        timer1000.Measure("anObject.GetType() == typeof(string) ", 10, delegate
        {
            result = anObject.GetType() == typeof(string);
            result = anObject.GetType() == typeof(string);
            result = anObject.GetType() == typeof(string);
            result = anObject.GetType() == typeof(string);
            result = anObject.GetType() == typeof(string);
            result = anObject.GetType() == typeof(string);
            result = anObject.GetType() == typeof(string);
            result = anObject.GetType() == typeof(string);
            result = anObject.GetType() == typeof(string);
            result = anObject.GetType() == typeof(string);
        });

        // #End1 Even faster than isInst, which is suprising.
        timer1000.Measure("anObject is string                   ", 10, delegate
        {
            result = anObject is string;
            result = anObject is string;
            result = anObject is string;
            result = anObject is string;
            result = anObject is string;
            result = anObject is string;
            result = anObject is string;
            result = anObject is string;
            result = anObject is string;
            result = anObject is string;
        });
    }
    static public void MeasureMethodReflection()
    {
        Class aClass = new Class();
        timer1000.Measure("Non-reflection EmptyStaticFunction() ", 10, delegate
        {
            Class.EmptyStaticFunction();
            Class.EmptyStaticFunction();
            Class.EmptyStaticFunction();
            Class.EmptyStaticFunction();
            Class.EmptyStaticFunction();
            Class.EmptyStaticFunction();
            Class.EmptyStaticFunction();
            Class.EmptyStaticFunction();
            Class.EmptyStaticFunction();
            Class.EmptyStaticFunction();
        });

        timer1000.Measure("Non-reflection EmptyStaticFunction5Arg() ", 10, delegate
        {
            Class.EmptyStaticFunction5Arg(1, 2, 3, 4, 5);
            Class.EmptyStaticFunction5Arg(1, 2, 3, 4, 5);
            Class.EmptyStaticFunction5Arg(1, 2, 3, 4, 5);
            Class.EmptyStaticFunction5Arg(1, 2, 3, 4, 5);
            Class.EmptyStaticFunction5Arg(1, 2, 3, 4, 5);
            Class.EmptyStaticFunction5Arg(1, 2, 3, 4, 5);
            Class.EmptyStaticFunction5Arg(1, 2, 3, 4, 5);
            Class.EmptyStaticFunction5Arg(1, 2, 3, 4, 5);
            Class.EmptyStaticFunction5Arg(1, 2, 3, 4, 5);
            Class.EmptyStaticFunction5Arg(1, 2, 3, 4, 5);
        });


        MethodInfo mi = typeof(Class).GetMethod("EmptyStaticFunction", BindingFlags.Static | BindingFlags.Public);
        timer1000.Measure("Method.Invoke EmptyStaticFunction()", 10, delegate
        {
            mi.Invoke(null, null);
            mi.Invoke(null, null);
            mi.Invoke(null, null);
            mi.Invoke(null, null);
            mi.Invoke(null, null);
            mi.Invoke(null, null);
            mi.Invoke(null, null);
            mi.Invoke(null, null);
            mi.Invoke(null, null);
            mi.Invoke(null, null);
        });

        mi = typeof(Class).GetMethod("EmptyStaticFunction5Arg", BindingFlags.Static | BindingFlags.Public);
        timer1000.Measure("Method.Invoke EmptyStaticFunction5Arg()", 10, delegate
        {
            mi.Invoke(null, new object[] { 1, 2, 3, 4, 5 });
            mi.Invoke(null, new object[] { 1, 2, 3, 4, 5 });
            mi.Invoke(null, new object[] { 1, 2, 3, 4, 5 });
            mi.Invoke(null, new object[] { 1, 2, 3, 4, 5 });
            mi.Invoke(null, new object[] { 1, 2, 3, 4, 5 });
            mi.Invoke(null, new object[] { 1, 2, 3, 4, 5 });
            mi.Invoke(null, new object[] { 1, 2, 3, 4, 5 });
            mi.Invoke(null, new object[] { 1, 2, 3, 4, 5 });
            mi.Invoke(null, new object[] { 1, 2, 3, 4, 5 });
            mi.Invoke(null, new object[] { 1, 2, 3, 4, 5 });
        });



    }
    static public void MeasureFieldReflection()
    {
        Type aClass = typeof(Class);
        FieldInfo aInstanceIntField = aClass.GetField("aInstanceInt");
        Class aClassInstance = new Class();
        int intValue = -1;

        // This is here for a baseline. 
        timer1000.Measure("aClassInstance.aInstanceInt++", 10, delegate
        {
            aClassInstance.aInstanceInt++;
            aClassInstance.aInstanceInt++;
            aClassInstance.aInstanceInt++;
            aClassInstance.aInstanceInt++;
            aClassInstance.aInstanceInt++;
            aClassInstance.aInstanceInt++;
            aClassInstance.aInstanceInt++;
            aClassInstance.aInstanceInt++;
            aClassInstance.aInstanceInt++;
            aClassInstance.aInstanceInt++;
        });

        timer1000.Measure("(int) aInstanceIntField.GetValue(aClassInstance)", 10, delegate
        {
            intValue = (int)aInstanceIntField.GetValue(aClassInstance);
        });

        timer1000.Measure("aInstanceIntField.SetValue(aClassInstance, intValue)", 10, delegate
        {
            aInstanceIntField.SetValue(aClassInstance, intValue);
        });
    }
    static public void MeasureCustomAttributes()
    {
        Type flagsAttribute = Type.GetType("System.FlagsAttribute");
        Type myEnum = Type.GetType("MyEnum");
        Attribute[] ret;
        timer1000.Measure("GetCustomAttribute() failure", delegate
        {
            ret = Attribute.GetCustomAttributes(myEnum, flagsAttribute, false);
        });

        Type myFlagsEnum = Type.GetType("MyFlagsEnum");
        timer1000.Measure("GetCustomAttribute() success", delegate
        {
            ret = Attribute.GetCustomAttributes(myFlagsEnum, flagsAttribute, false);
        });
    }
    static public void MeasurePInvoke()
    {
        // This is a call to a native routine that does essentially nothing.  
        timer1000.Measure("FullTrustCall()", 1, delegate
        {
            PinvokeClass.FullTrustCall(0, 0);
        });

        // Some of the overhead of PInvoke is hosted into the prolog of the method that has the
        // PINVOKE call in it.   Thus two calls in the same routine will have lower average costs
        // than two calls in diffenet routines because they share the prolog overhead.  This
        // demonstrates this property. 
        timer1000.Measure("FullTrustCall() (2 call average)", 2, delegate
        {
            PinvokeClass.FullTrustCall(0, 0);
            PinvokeClass.FullTrustCall(0, 0);
        });

        // In this case, we are not cloning the benchmark to reduce measurement error but rather
        // to show that some costs are shared among all calls within the same method.  Thus this
        // benchmark gives a resonable estimate of what happens if there is a PINVOKE call in a
        // loop that is run many times in a single method.  
        timer1000.Measure("10 FullTrustCall() (10 call average)", 10, delegate
        {
            PinvokeClass.FullTrustCall(0, 0);
            PinvokeClass.FullTrustCall(0, 0);
            PinvokeClass.FullTrustCall(0, 0);
            PinvokeClass.FullTrustCall(0, 0);
            PinvokeClass.FullTrustCall(0, 0);
            PinvokeClass.FullTrustCall(0, 0);
            PinvokeClass.FullTrustCall(0, 0);
            PinvokeClass.FullTrustCall(0, 0);
            PinvokeClass.FullTrustCall(0, 0);
            PinvokeClass.FullTrustCall(0, 0);
        });

        // Pinvoke is much more expensive if we need to peform security checks (done by default)
        // this benchmark demonstrates this. 
        timer1000.Measure("1 PartialTrustCall", 1, delegate
        {
            PinvokeClass.PartialTrustCall(0, 0);
        });

        // I repeat the same pattern used for the full-trust case, however it turns out that
        // nothign is shared among calls in the partial trust case, so you should get the same
        // average cost per call regardless of the number of calls in a method.  
        timer1000.Measure("PartialTrustCall() (2 call average)", 2, delegate
        {
            PinvokeClass.PartialTrustCall(0, 0);
            PinvokeClass.PartialTrustCall(0, 0);
        });
        timer1000.Measure("PartialTrustCall() (10 call average)", 10, delegate
        {
            PinvokeClass.PartialTrustCall(0, 0);
            PinvokeClass.PartialTrustCall(0, 0);
            PinvokeClass.PartialTrustCall(0, 0);
            PinvokeClass.PartialTrustCall(0, 0);
            PinvokeClass.PartialTrustCall(0, 0);
            PinvokeClass.PartialTrustCall(0, 0);
            PinvokeClass.PartialTrustCall(0, 0);
            PinvokeClass.PartialTrustCall(0, 0);
            PinvokeClass.PartialTrustCall(0, 0);
            PinvokeClass.PartialTrustCall(0, 0);
        });

    }
    static public void MeasureThreading()
    {
        string aString = "Foo";
        int threadId;
        Thread thread;

        timer1000.Measure("Thread.CurrentThread", 10, delegate
        {
            thread = Thread.CurrentThread;
            thread = Thread.CurrentThread;
            thread = Thread.CurrentThread;
            thread = Thread.CurrentThread;
            thread = Thread.CurrentThread;
            thread = Thread.CurrentThread;
            thread = Thread.CurrentThread;
            thread = Thread.CurrentThread;
            thread = Thread.CurrentThread;
            thread = Thread.CurrentThread;
        });

        timer1000.Measure("threadId = Thread.CurrentThread.ManagedThreadId", 10, delegate
        {
            threadId = Thread.CurrentThread.ManagedThreadId;
            threadId = Thread.CurrentThread.ManagedThreadId;
            threadId = Thread.CurrentThread.ManagedThreadId;
            threadId = Thread.CurrentThread.ManagedThreadId;
            threadId = Thread.CurrentThread.ManagedThreadId;
            threadId = Thread.CurrentThread.ManagedThreadId;
            threadId = Thread.CurrentThread.ManagedThreadId;
            threadId = Thread.CurrentThread.ManagedThreadId;
            threadId = Thread.CurrentThread.ManagedThreadId;
            threadId = Thread.CurrentThread.ManagedThreadId;
        });

        timer1000.Measure("aTLSInt++", 10, delegate
        {
            Class.aTLSInt++;
            Class.aTLSInt++;
            Class.aTLSInt++;
            Class.aTLSInt++;
            Class.aTLSInt++;
            Class.aTLSInt++;
            Class.aTLSInt++;
            Class.aTLSInt++;
            Class.aTLSInt++;
            Class.aTLSInt++;
        });

        timer1000.Measure("aTLSString = aString", 10, delegate
        {
            Class.aTLSString = aString;
            Class.aTLSString = aString;
            Class.aTLSString = aString;
            Class.aTLSString = aString;
            Class.aTLSString = aString;
            Class.aTLSString = aString;
            Class.aTLSString = aString;
            Class.aTLSString = aString;
            Class.aTLSString = aString;
            Class.aTLSString = aString;
        });

        timer1000.Measure("Interlocked.CompareExchange 0", 10, delegate
        {
            int val = 0;
            Interlocked.CompareExchange(ref val, 0, val);
            Interlocked.CompareExchange(ref val, 0, val);
            Interlocked.CompareExchange(ref val, 0, val);
            Interlocked.CompareExchange(ref val, 0, val);
            Interlocked.CompareExchange(ref val, 0, val);
            Interlocked.CompareExchange(ref val, 0, val);
            Interlocked.CompareExchange(ref val, 0, val);
            Interlocked.CompareExchange(ref val, 0, val);
            Interlocked.CompareExchange(ref val, 0, val);
            Interlocked.CompareExchange(ref val, 0, val);
        });

        timer1000.Measure("Interlocked.CompareExchange i", 10, delegate
        {
            int val = 0;
            Interlocked.CompareExchange(ref val, val++, val);
            Interlocked.CompareExchange(ref val, val++, val);
            Interlocked.CompareExchange(ref val, val++, val);
            Interlocked.CompareExchange(ref val, val++, val);
            Interlocked.CompareExchange(ref val, val++, val);
            Interlocked.CompareExchange(ref val, val++, val);
            Interlocked.CompareExchange(ref val, val++, val);
            Interlocked.CompareExchange(ref val, val++, val);
            Interlocked.CompareExchange(ref val, val++, val);
            Interlocked.CompareExchange(ref val, val++, val);
        });

        timer1000.Measure("Spin Interlocked.CompareExchange", 10, delegate
        {
            int val = 0;
            int i;
            do i = val; while (Interlocked.CompareExchange(ref val, i + 1, i) != i);
            do i = val; while (Interlocked.CompareExchange(ref val, i + 1, i) != i);
            do i = val; while (Interlocked.CompareExchange(ref val, i + 1, i) != i);
            do i = val; while (Interlocked.CompareExchange(ref val, i + 1, i) != i);
            do i = val; while (Interlocked.CompareExchange(ref val, i + 1, i) != i);
            do i = val; while (Interlocked.CompareExchange(ref val, i + 1, i) != i);
            do i = val; while (Interlocked.CompareExchange(ref val, i + 1, i) != i);
            do i = val; while (Interlocked.CompareExchange(ref val, i + 1, i) != i);
            do i = val; while (Interlocked.CompareExchange(ref val, i + 1, i) != i);
            do i = val; while (Interlocked.CompareExchange(ref val, i + 1, i) != i);

        });
    }
    static public void MeasureLocks()
    {
        object o = new object();
        ReaderWriterLock rwLock = new ReaderWriterLock();

        // Monitor is the standard .NET lock
        timer1000.Measure("Monitor lock", 1, delegate
        {
            Monitor.Enter(o); Monitor.Exit(o);
        });

        // The lock statement is just syntatic sugar for Monitor.Enter() Monitor.Exit().  There is
        // a finally clause, which might cause a small amount of overhead, but should be noise. 
        timer1000.Measure("lock statement", 1, delegate
        {
            lock (o) { }
        });

        // Reader-writer locks are more expensive to take and leave, but allow better scaling on
        // multiprocessors since many readers can be in the lock at a time.  
        // ReaderWriterLockSlim (V3.5) is faster, though.  
        timer1000.Measure("ReaderWriterLock ReadLock", 1, delegate
        {
            rwLock.AcquireReaderLock(-1);
            rwLock.ReleaseReaderLock();
        });

        timer1000.Measure("ReaderWriterLock WriteLock", 1, delegate
        {
            rwLock.AcquireWriterLock(-1);
            rwLock.ReleaseWriterLock();
        });

#if VERSION3_5
        ReaderWriterLockSlim rwLockSlim = new ReaderWriterLockSlim();
        ReaderWriterLockSlim rwLockSlimRec = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        timer1000.Measure("ReaderWriterLockSlim ReadLock", 1, delegate
        {
            rwLockSlim.EnterReadLock();
            rwLockSlim.ExitReadLock();
        });

        timer1000.Measure("ReaderWriterLockSlim WriteLock Recursive", 1, delegate
        {
            rwLockSlimRec.EnterWriteLock();
            rwLockSlimRec.ExitWriteLock();
        });

        timer1000.Measure("1 ReaderWriterLockSlim ReadLock Recursive", 1, delegate
        {
            rwLockSlimRec.EnterReadLock();
            rwLockSlimRec.ExitReadLock();
        });

        timer1000.Measure("1 ReaderWriterLockSlim WriteLock", 1, delegate
        {
            rwLockSlim.EnterWriteLock();
            rwLockSlim.ExitWriteLock();
            });
#endif
    }
    static public void MeasureVarags()
    {
        // This is the standard way in C# to pass a variable number of parameters. Because an array
        // need to be created ON EACH CALL, it is pretty expensive. The take away here is that you
        // can you should use explicit overloads for common cases so that the true varargs case
        // happens rarely.   
        timer1000.Measure("Class.ParamsArgMethod(1)", 10, delegate
        {
            Class.ParamsArgMethod(1);
            Class.ParamsArgMethod(1);
            Class.ParamsArgMethod(1);
            Class.ParamsArgMethod(1);
            Class.ParamsArgMethod(1);
            Class.ParamsArgMethod(1);
            Class.ParamsArgMethod(1);
            Class.ParamsArgMethod(1);
            Class.ParamsArgMethod(1);
            Class.ParamsArgMethod(1);
        });

        // The runtime supports variable arguments that are much more efficient than the C#
        // version because no array allocation needs to be made.  However C# does not support a
        // good syntax for it (callers have to use the painful __arglist() syntax).  
        timer1000.Measure("Class.VarArgMethod(1)", 10, delegate
        {
            Class.VarargsMethod(__arglist(1));
            Class.VarargsMethod(__arglist(1));
            Class.VarargsMethod(__arglist(1));
            Class.VarargsMethod(__arglist(1));
            Class.VarargsMethod(__arglist(1));
            Class.VarargsMethod(__arglist(1));
            Class.VarargsMethod(__arglist(1));
            Class.VarargsMethod(__arglist(1));
            Class.VarargsMethod(__arglist(1));
            Class.VarargsMethod(__arglist(1));
        });
    }
    static public void MeasureGuid()
    {

        Guid g;

        // This is the way most people make a guid and it is slow. 
        timer1000.Measure("new Guid(string)", 10, delegate
        {
            g = new Guid("044973cd-251f-4dff-a3e9-9d6307286b05");
            g = new Guid("044973cd-251f-4dff-a3e9-9d6307286b05");
            g = new Guid("044973cd-251f-4dff-a3e9-9d6307286b05");
            g = new Guid("044973cd-251f-4dff-a3e9-9d6307286b05");
            g = new Guid("044973cd-251f-4dff-a3e9-9d6307286b05");
            g = new Guid("044973cd-251f-4dff-a3e9-9d6307286b05");
            g = new Guid("044973cd-251f-4dff-a3e9-9d6307286b05");
            g = new Guid("044973cd-251f-4dff-a3e9-9d6307286b05");
            g = new Guid("044973cd-251f-4dff-a3e9-9d6307286b05");
            g = new Guid("044973cd-251f-4dff-a3e9-9d6307286b05");
        });

        // This is MUCH faster. 
        timer1000.Measure("new Guid(int, ..))", 10, delegate
        {
            g = new Guid(0x044973cd, 0x251f, 0x4dff, 0xa3, 0xe9, 0x9d, 0x63, 0x07, 0x28, 0x6b, 0x05);
            g = new Guid(0x044973cd, 0x251f, 0x4dff, 0xa3, 0xe9, 0x9d, 0x63, 0x07, 0x28, 0x6b, 0x05);
            g = new Guid(0x044973cd, 0x251f, 0x4dff, 0xa3, 0xe9, 0x9d, 0x63, 0x07, 0x28, 0x6b, 0x05);
            g = new Guid(0x044973cd, 0x251f, 0x4dff, 0xa3, 0xe9, 0x9d, 0x63, 0x07, 0x28, 0x6b, 0x05);
            g = new Guid(0x044973cd, 0x251f, 0x4dff, 0xa3, 0xe9, 0x9d, 0x63, 0x07, 0x28, 0x6b, 0x05);
            g = new Guid(0x044973cd, 0x251f, 0x4dff, 0xa3, 0xe9, 0x9d, 0x63, 0x07, 0x28, 0x6b, 0x05);
            g = new Guid(0x044973cd, 0x251f, 0x4dff, 0xa3, 0xe9, 0x9d, 0x63, 0x07, 0x28, 0x6b, 0x05);
            g = new Guid(0x044973cd, 0x251f, 0x4dff, 0xa3, 0xe9, 0x9d, 0x63, 0x07, 0x28, 0x6b, 0x05);
            g = new Guid(0x044973cd, 0x251f, 0x4dff, 0xa3, 0xe9, 0x9d, 0x63, 0x07, 0x28, 0x6b, 0x05);
            g = new Guid(0x044973cd, 0x251f, 0x4dff, 0xa3, 0xe9, 0x9d, 0x63, 0x07, 0x28, 0x6b, 0x05);
        });

        // However if you can initialize the GUID as a static (rather and create as needed) 
        // it is even faster.  
        timer1000.Measure("g = staticGuid)", 10, delegate
        {
            g = GuidClass.staticGuid;
            g = GuidClass.staticGuid;
            g = GuidClass.staticGuid;
            g = GuidClass.staticGuid;
            g = GuidClass.staticGuid;
            g = GuidClass.staticGuid;
            g = GuidClass.staticGuid;
            g = GuidClass.staticGuid;
            g = GuidClass.staticGuid;
            g = GuidClass.staticGuid;
        });
    }
    static public void MeasureRegex()
    {
        string line = "    this is a test";
        string pattern = @"this\s*(\S*)";
        string result = null;

        for (int j = 0; j < 5; j++)
        {
            timer1000.Measure("Regex fetch word len=" + line.Length.ToString(), 1, delegate
            {
                Match m = Regex.Match(line, pattern);
                if (m.Success)
                    result = m.Groups[1].Value;
            });

            Regex pat = new Regex(pattern, RegexOptions.Compiled);

            timer1000.Measure("Compiled Regex fetch word len=" + line.Length.ToString(), 1, delegate
            {
                Match m = pat.Match(line);
                if (m.Success)
                    result = m.Groups[1].Value;
            });

            timer1000.Measure("ByHand fetch word len=" + line.Length.ToString(), 1, delegate
            {
                int idx = line.IndexOf("this");
                if (idx >= 0)
                {
                    int startWord = -1;
                    for (int i = idx; i < line.Length; i++)
                    {
                        if (Char.IsWhiteSpace(line[i]))
                        {
                            if (startWord >= 0)
                            {
                                result = line.Substring(startWord, i - startWord);
                                return;
                            }
                        }
                        else
                        {
                            if (startWord < 0)
                                startWord = i;
                        }
                    }

                    if (startWord < 0)
                        result = "";
                    else
                        result = line.Substring(startWord);
                }
            });

            line = "hello there I am a very happy camper right now as my string is long" + line;
        }

    }
    static public void MeasureEnums()
    {
        MyEnum aEnum = MyEnum.Blue;
        timer1000.Measure("aEnum.ToString()", delegate
        {
            aEnum.ToString();
        });

        MyFlagsEnum aFlagsEnum = MyFlagsEnum.Option1;
        timer1000.Measure("aFlagsEnum.ToString() (1 flag)", delegate
        {
            aFlagsEnum.ToString();
        });
        aFlagsEnum = MyFlagsEnum.Option1 | MyFlagsEnum.Option2 | MyFlagsEnum.Option3 | MyFlagsEnum.Option4;
        timer1000.Measure("aEnum.ToString() (4 flags)", delegate
        {
            aFlagsEnum.ToString();
        });
    }

    #region private
    private static Guid origPowerPolicy;
    private static void SetHighPerfPowerPolicy()
    {
        Guid curPolicy = PowerManagment.CurrentPolicy;
        if (curPolicy != PowerManagment.HighPerformance)
        {
            origPowerPolicy = curPolicy;
            PowerManagment.Set(PowerManagment.HighPerformance);
            if (PowerManagment.CurrentPolicy == PowerManagment.HighPerformance)
                Console.WriteLine("Set power plan to 'High Performance'.");
            else
                Console.WriteLine("Warning: Could not set power plan to 'High Performance'.");
        }
        else
            Console.WriteLine("Power Policy already set to 'High Performance'.");

    }
    private static void RestorePowerPolicy()
    {
        if (origPowerPolicy != Guid.Empty && origPowerPolicy != PowerManagment.CurrentPolicy)
        {
            Console.WriteLine("Restoring power policy.");
            PowerManagment.Set(origPowerPolicy);
            origPowerPolicy = Guid.Empty;
        }
    }
    private static void RunAtHighPerfPolicy(Action action)
    {
        var handler = new ConsoleCancelEventHandler(delegate { RestorePowerPolicy(); });
        try
        {
            Console.CancelKeyPress += handler;
            SetHighPerfPowerPolicy();
            action();
        }
        finally
        {
            Console.CancelKeyPress -= handler;
            RestorePowerPolicy();
        }
    }


    private static List<MethodInfo> GetAreaMethods(bool nonPublic)
    {
        List<MethodInfo> ret = new List<MethodInfo>();
        BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
        if (nonPublic == true)
            flags = BindingFlags.NonPublic | BindingFlags.Static;

        MethodInfo[] methods = typeof(MeasureIt).GetMethods(flags);
        foreach (MethodInfo method in methods)
        {
            if (method.Name.StartsWith("Measure") && method.GetParameters().Length == 0)
                ret.Add(method);
        }
        return ret;
    }
    private static MethodInfo GetAreaMethod(string area)
    {
        return typeof(MeasureIt).GetMethod("Measure" + area, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.IgnoreCase);
    }
    private static T[][] CreateJaggedArray<T>(int dim1, int dim2)
    {
        T[][] ret = new T[dim1][];
        for (int i = 0; i < ret.Length; i++)
            ret[i] = new T[dim2];
        return ret;
    }
    private static string UnpackAttachedSource(string targetDir)
    {
        System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
        if (targetDir == null)
        {
            string appPath = assembly.ManifestModule.FullyQualifiedName;
            targetDir = Path.ChangeExtension(appPath, ".src");
        }

        Console.WriteLine("Unpacking source to " + targetDir);
        foreach (string resourceName in assembly.GetManifestResourceNames())
        {
            string targetFileName = Path.Combine(targetDir, resourceName);
            // Console.WriteLine("    Unpacking " + resourceName);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFileName));
            ResourceUtilities.UnpackResourceAsFile(resourceName, targetFileName);
        }
        return targetDir;
    }


    // The diferent timers have different loop counts.  
    // Choose the one that is big enough to be stable but small enough
    // to run in a reasonable amount of time (ideally 10-100ms)
    private static MultiSampleCodeTimer timer1000;
    private static MultiSampleCodeTimer timer100;
    private static MultiSampleCodeTimer timer10;
    private static MultiSampleCodeTimer timer1;
    private static StatsLogger logger;
    #endregion
};

#region Support Classes
// classes and method needed to perform the experiments. 

enum MyEnum
{
    Red,
    Blue,
    Black,
};

[Flags]
enum MyFlagsEnum
{
    Option1 = 1,
    Option2 = 2,
    Option3 = 4,
    Option4 = 8,
};

public interface AnInterface
{
    void InterfaceMethod();
}

public abstract class SuperClass
{
    public abstract void VirtualMethod();
}

public struct ValueType
{
    public delegate void ForeachAction(ref ValueType arg);
    public static void Foreach(ValueType[] array, ForeachAction action)
    {
        for (int i = 0; i < array.Length; i++)
            action(ref array[i]);
    }
    public int x;
    public int y;
    public int z;
}

public delegate void MyDelegate();

public struct StructWithInterface : AnInterface
{
    public void InterfaceMethod()
    {
    }
}

public sealed class SealedClass : SuperClass, AnInterface
{
    public override void VirtualMethod()
    {
    }
    public void InterfaceMethod()
    {
    }
}

/// <summary>
/// A example class.  It inherits, overrides, has intefaces etc.  
/// It excercises most of the common runtime features 
/// </summary>
public class Class : SuperClass, AnInterface
{
    public event MyDelegate AnEvent;
    public object Clone() { return this.MemberwiseClone(); }

    public override void VirtualMethod() { }
    public void InterfaceMethod() { }

    public int aInstanceInt;
    public string aInstanceString;

    public static int aStaticInt;
    public static string aStaticString = "Hello";
    public static ValueType aStaticValueType;

    [ThreadStatic]
    public static int aTLSInt;
    [ThreadStatic]
    public static string aTLSString;
    [ThreadStatic]
    public static ValueType aTLSValueType;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static void EmptyStaticFunction()
    {
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static void EmptyStaticFunction5Arg(int arg1, int arg2, int arg3, int arg4, int arg5)
    {
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static ValueType ReturnsValueType()
    {
        return new ValueType();
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void EmptyInstanceFunction()
    {
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static void GenericMethod<T>()
    {
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static int ParamsArgMethod(params int[] args)
    {
        return args[0];
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static int VarargsMethod(__arglist)
    {
        // ArgIterator args = new ArgIterator(__arglist);
        return 0;
    }

    public void MeasureFire10(MultiSampleCodeTimer timer)
    {
        timer.Measure("Fire Events", 10, delegate
        {
            AnEvent();
            AnEvent();
            AnEvent();
            AnEvent();
            AnEvent();
            AnEvent();
            AnEvent();
            AnEvent();
            AnEvent();
            AnEvent();
        });
    }
}

public class FinalizableClass
{
    public static int numConstructorCalls;
    public static int numFinalizerCalls;
    public FinalizableClass()
    {
        numConstructorCalls++;
    }
    ~FinalizableClass()
    {
        numFinalizerCalls++;
    }
}


public class GenericClass<T>
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public T ClassGenericInstanceMethod()
    {
        return aGenericInstanceFieldT;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static T ClassGenericStaticMethod()
    {
        return aGenericStaticFieldT;
    }

    public T aGenericInstanceFieldT;
    public static T aGenericStaticFieldT;
}

/// <summary>
/// An example of a value class (struct) which has a generic type parameter.
/// </summary>
public struct GenericValueClass<T>
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public T GenericInstanceMethod()
    {
        return aGenericInstanceFieldT;
    }
    public T aGenericInstanceFieldT;
}

public class PinvokeClass
{
    [DllImport("kernel32", EntryPoint = "Sleep"), SuppressUnmanagedCodeSecurityAttribute]
    public static extern void FullTrustSleep(int msec);

    /// <summary>
    /// FullTrustCall does a PINVOKE where we indicate we can skip security checks.
    /// This is approrpiate for full Trust code   
    /// 
    /// mscoree!GetXMLElement is a function that happens to aways just return failure HRESULT
    /// and thus is only 2 instructions longs (mov EAX, XXXX, ret)
    /// </summary>
    [DllImport("mscoree", EntryPoint = "GetXMLElement"), SuppressUnmanagedCodeSecurityAttribute]
    public static extern void FullTrustCall(int x, int y);

    /// <summary>
    /// This declaration does not have SuppressUnmanagedCodeSecurityAttribute
    /// thus it will have a security check on every call.  
    /// </summary>
    [DllImport("mscoree", EntryPoint = "GetXMLElement")]
    public static extern void PartialTrustCall(int x, int y);
}

public class GuidClass
{
    public static Guid staticGuid = new Guid("044973cd-251f-4dff-a3e9-9d6307286b05");
    public static int anInt;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static Guid identityGuid(Guid g) { return g; }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static int identityInt(int i) { return i; }
}

public class Misc
{
    public static string ToString(int i)
    {
        char[] chars = new char[12];
        int ptr = 12;
        uint u = (uint)i;
        if (i < 0)
            u = (uint)-i;
        do
        {
            chars[--ptr] = (char)((int)'0' + u % 10);
            u = u / 10;
        } while (u > 0);

        if (i < 0)
            chars[--ptr] = '-';

        string ret = new String(chars, ptr, 12 - ptr);
        return ret;
    }
    public unsafe static string UnsafeToString(int i)
    {
        char* chars = stackalloc char[12];
        int ptr = 12;
        uint u = (uint)i;
        if (i < 0)
            u = (uint)-i;
        do
        {
            chars[--ptr] = (char)((int)'0' + u % 10);
            u = u / 10;
        } while (u > 0);

        if (i < 0)
            chars[--ptr] = '-';

        string ret = new String(chars, ptr, 12 - ptr);
        return ret;
    }

    public static unsafe int Serialize(ref ValueType value, byte[] bytes, int offset)
    {
        int size = sizeof(ValueType);
        offset += size;
        if (offset > bytes.Length)
            throw new IndexOutOfRangeException();
        fixed (ValueType* valuePtr = &value)
        fixed (byte* bytesPtr = &bytes[offset])
        {
            memcpy(bytesPtr, (byte*)valuePtr, size);
        }
        return offset + size;
    }

    public unsafe static void memcpy(byte* target, byte* src, int count)
    {
        for (int i = 0; i < count; i++)
            target[i] = src[i];
    }

    public static int X(int q)
    {
        int sum = 0;
        for (int i = 0; i < q; i++)
            for (int j = 0; j < q; j++)
                sum += Y(i);
        return sum;
    }
    public static int Y(int q)
    {
        int sum = 0;
        for (int i = 0; i < q; i++)
            for (int j = 0; j < q; j++)
                sum += i;
        return sum;
    }
}

#endregion

