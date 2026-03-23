using BitFab.KW1281Test.Interface;
using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace BitFab.KW1281Test;

/// <summary>
/// Factory for creating the appropriate IInterface implementation based on port name and OS.
/// </summary>
internal static class InterfaceFactory
{
    /// <summary>
    /// Opens the appropriate serial port interface based on the port name pattern.
    /// FTDI serial numbers (8 alphanumeric chars) → FtdiInterface
    /// Linux /dev/ paths → LinuxInterface
    /// Everything else → GenericInterface
    /// </summary>
    public static IInterface OpenPort(string portName, int baudRate)
    {
        if (Regex.IsMatch(portName.ToUpper(), @"\A[A-Z0-9]{8}\Z"))
        {
            Logger.Log.WriteLine($"Opening FTDI serial port {portName}");
            return new FtdiInterface(portName, baudRate);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
            portName.StartsWith("/dev/", StringComparison.CurrentCultureIgnoreCase))
        {
            Logger.Log.WriteLine($"Opening Linux serial port {portName}");
            return new LinuxInterface(portName, baudRate);
        }
        else
        {
            Logger.Log.WriteLine($"Opening Generic serial port {portName}");
            return new GenericInterface(portName, baudRate);
        }
    }
}
