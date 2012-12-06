using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

/// <summary>
/// PowerManagement allows you to access the funtionality of the Control Panel -> Power Options
/// dialog in windows.  (Currently we only use VISTA APIs). 
/// </summary>
public static unsafe class PowerManagment
{
    public static Guid HighPerformance = new Guid(0x8c5e7fda, 0xe8bf, 0x4a96, 0x9a, 0x85, 0xa6, 0xe2, 0x3a, 0x8c, 0x63, 0x5c);
    public static Guid Balenced        = new Guid(0x381b4222, 0xf694, 0x41f0, 0x96, 0x85, 0xff, 0x5b, 0xb2, 0x60, 0xdf, 0x2e);
    public static Guid PowerSaver      = new Guid(0xa1841308, 0x3541, 0x4fab, 0xbc, 0x81, 0xf7, 0x15, 0x56, 0xf2, 0x0b, 0x4a);

    public static Guid CurrentPolicy
    {
        get
        {
            Guid* retPolicy = null;
            Guid ret = Guid.Empty;
            try
            {
                int callRet = PowerGetActiveScheme(IntPtr.Zero, ref retPolicy);
                if (callRet == 0)
                {
                    ret = *retPolicy;
                    Marshal.FreeHGlobal((IntPtr)retPolicy);
                }
            }
            catch (Exception) { }
            return ret;
        }
    }
    public static bool Set(Guid newPolicy)
    {
        try
        {
            return PowerSetActiveScheme(IntPtr.Zero, ref newPolicy) == 0;
        }
        catch (Exception) { }
        return false;
    }

    #region private 
        [DllImport("powrprof.dll")]
    private static extern int PowerGetActiveScheme(IntPtr ReservedZero, ref Guid* policyGuidRet);

    [DllImport("powrprof.dll")]
    private static extern int PowerSetActiveScheme(IntPtr ReservedZero, ref Guid policyGuid);
    #endregion
}

