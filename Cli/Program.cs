using BitFab.KW1281Test;
using BitFab.KW1281Test.Interface;
using BitFab.KW1281Test.Kwp2000;
using BitFab.KW1281Test.Logging;
using BitFab.KW1281Test.Uds;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using BitFab.KW1281Test.EDC15;
using System.IO;

namespace BitFab.KW1281Test.Cli;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            Logger.Log = new FileLog("KW1281Test.log");

            Logger.CommandAndArgs.Add(
                Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]));
            Logger.CommandAndArgs.AddRange(args);

            var tester = new Program();
            tester.Run(args);
        }
        catch (UnableToProceedException)
        {
        }
        catch (Exception ex)
        {
            Logger.Log.WriteLine($"Caught: {ex.GetType()} {ex.Message}");
            Logger.Log.WriteLine($"Unhandled exception: {ex}");
        }
        finally
        {
            Logger.Log.Close();
        }
    }

    void Run(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("KW1281Test: Yesterday's diagnostics...");
        Thread.Sleep(2000);
        Console.WriteLine("Today.");
        Thread.Sleep(2000);
        Console.ResetColor();
        Console.WriteLine();

        var version = typeof(Tester).GetTypeInfo().Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
        Logger.Log.WriteLine($"Version {version} (https://github.com/gmenounos/kw1281test/releases)");
        Logger.Log.WriteLine($"Command Line: {string.Join(' ', Logger.CommandAndArgs)}");
        Logger.Log.WriteLine($"OSVersion: {Environment.OSVersion}");
        Logger.Log.WriteLine($".NET Version: {Environment.Version}");
        Logger.Log.WriteLine($"Culture: {CultureInfo.InstalledUICulture}");

        if (args.Length < 4)
        {
            ShowUsage();
            return;
        }

        try
        {
            // This seems to increase the accuracy of our timing loops
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;
        }
        catch(Win32Exception)
        {
            // Ignore if we don't have permission to increase our priority
        }

        string portName = args[0];
        var baudRate = int.Parse(args[1]);
        int controllerAddress = int.Parse(args[2], NumberStyles.HexNumber);
        var command = args[3];
        uint address = 0;
        uint length = 0;
        byte value = 0;
        int softwareCoding = 0;
        int workshopCode = 0;
        byte channel = 0;
        ushort channelValue = 0;
        ushort? login = null;
        byte groupNumber = 0;
        var addressValuePairs = new List<KeyValuePair<ushort, byte>>();

        if (string.Compare(command, "ReadEeprom", ignoreCase: true) == 0 ||
            string.Compare(command, "ReadRAM", ignoreCase: true) == 0 ||
            string.Compare(command, "ReadROM", ignoreCase: true) == 0)
        {
            if (args.Length < 5)
            {
                ShowUsage();
                return;
            }

            address = Utils.ParseUint(args[4]);
        }
        else if (string.Compare(command, "DumpMarelliMem", ignoreCase: true) == 0 ||
                 string.Compare(command, "DumpEeprom", ignoreCase: true) == 0 ||
                 string.Compare(command, "DumpMem", ignoreCase: true) == 0 ||
                 string.Compare(command, "DumpRam", ignoreCase: true) == 0 ||
                 string.Compare(command, "DumpRBxMem", ignoreCase: true) == 0 ||
                 string.Compare(command, "DumpRBxMemOdd", ignoreCase: true) == 0 ||
                 string.Compare(command, "DumpRom", ignoreCase: true) == 0)
        {
            if (args.Length < 6)
            {
                ShowUsage();
                return;
            }

            address = Utils.ParseUint(args[4]);
            length = Utils.ParseUint(args[5]);

            if (args.Length > 6)
            {
                _filename = args[6];
            }
        }
        else if (string.Compare(command, "WriteEeprom", ignoreCase: true) == 0)
        {
            if (args.Length < 6)
            {
                ShowUsage();
                return;
            }

            address = Utils.ParseUint(args[4]);
            value = (byte)Utils.ParseUint(args[5]);
        }
        else if (string.Compare(command, "LoadEeprom", ignoreCase: true) == 0)
        {
            if (args.Length < 6)
            {
                ShowUsage();
                return;
            }

            address = Utils.ParseUint(args[4]);
            _filename = args[5];
        }
        else if (string.Compare(command, "SetSoftwareCoding", ignoreCase: true) == 0)
        {
            if (args.Length < 6)
            {
                ShowUsage();
                return;
            }

            softwareCoding = (int)Utils.ParseUint(args[4]);
            if (softwareCoding > 32767)
            {
                Logger.Log.WriteLine("SoftwareCoding cannot be greater than 32767.");
                return;
            }
            workshopCode = (int)Utils.ParseUint(args[5]);
            if (workshopCode > 99999)
            {
                Logger.Log.WriteLine("WorkshopCode cannot be greater than 99999.");
                return;
            }
        }
        else if (string.Compare(command, "DumpEdc15Eeprom", ignoreCase: true) == 0)
        {
            if (args.Length < 4)
            {
                ShowUsage();
                return;
            }

            if (args.Length > 4)
            {
                _filename = args[4];
            }
        }
        else if (string.Compare(command, "WriteEdc15Eeprom", ignoreCase: true) == 0)
        {
            // WriteEdc15Eeprom ADDRESS1 VALUE1 [ADDRESS2 VALUE2 ... ADDRESSn VALUEn]

            if (args.Length < 6)
            {
                ShowUsage();
                return;
            }

            var dateString = DateTime.Now.ToString("s").Replace(':', '-');
            _filename = $"EDC15_EEPROM_{dateString}.bin";
            
            if (!Utils.ParseAddressesAndValues(args.Skip(4).ToList(), out addressValuePairs))
            {
                ShowUsage();
                return;
            }
        }
        else if (string.Compare(command, "AdaptationRead", ignoreCase: true) == 0)
        {
            if (args.Length < 5)
            {
                ShowUsage();
                return;
            }

            channel = byte.Parse(args[4]);

            if (args.Length > 5)
            {
                login = ushort.Parse(args[5]);
            }
        }
        else if (
            string.Compare(command, "AdaptationSave", ignoreCase: true) == 0 ||
            string.Compare(command, "AdaptationTest", ignoreCase: true) == 0)
        {
            if (args.Length < 6)
            {
                ShowUsage();
                return;
            }

            channel = byte.Parse(args[4]);
            channelValue = ushort.Parse(args[5]);

            if (args.Length > 6)
            {
                login = ushort.Parse(args[6]);
            }
        }
        else if (
            string.Compare(command, "BasicSetting", ignoreCase: true) == 0 ||
            string.Compare(command, "GroupRead", ignoreCase: true) == 0)
        {
            if (args.Length < 5)
            {
                ShowUsage();
                return;
            }

            groupNumber = byte.Parse(args[4]);
        }
        else if (
            string.Compare(command, "FindLogins", ignoreCase: true) == 0)
        {
            if (args.Length < 5)
            {
                ShowUsage();
                return;
            }

            login = ushort.Parse(args[4]);
        }

        using var @interface = InterfaceFactory.OpenPort(portName, baudRate);
        var tester = new Tester(@interface, controllerAddress);
        
        switch (command.ToLower())
        {
            case "cantest":
                TestCanInterface(portName, baudRate);
                return;

            case "canmonitor":
                MonitorCanBus(portName, baudRate);
                return;

            case "cantp":
                TestTp20Channel(portName, baudRate, (byte)controllerAddress);
                return;

            case "canautoscan":
                CanAutoScan(portName, baudRate);
                return;

            case "canuds":
                TestUdsDialog(portName, baudRate, (byte)controllerAddress);
                return;

            case "canmulti":
                CanMultiEcu(portName, baudRate);
                return;

            case "autoscan":
                AutoScan(@interface);
                return;

            case "dumprbxmem":
                tester.DumpRBxMem(address, length, _filename);
                tester.EndCommunication();
                return;

            case "dumprbxmemodd":
                tester.DumpRBxMem(address, length, _filename, evenParityWakeup: false);
                tester.EndCommunication();
                return;

            case "getskc":
                tester.GetSkc();
                tester.EndCommunication();
                return;

            case "togglerb4mode":
                tester.ToggleRB4Mode();
                tester.EndCommunication();
                return;

            default:
                break;
        }

        ControllerInfo ecuInfo = tester.Kwp1281Wakeup();

        switch (command.ToLower())
        {
            case "actuatortest":
                tester.ActuatorTest();
                break;

            case "adaptationread":
                tester.AdaptationRead(channel, login, ecuInfo.WorkshopCode);
                break;

            case "adaptationsave":
                tester.AdaptationSave(channel, channelValue, login, ecuInfo.WorkshopCode);
                break;

            case "adaptationtest":
                tester.AdaptationTest(channel, channelValue, login, ecuInfo.WorkshopCode);
                break;

            case "basicsetting":
                tester.BasicSettingRead(groupNumber);
                break;

            case "clarionvwpremium4safecode":
                tester.ClarionVWPremium4SafeCode();
                break;

            case "clearfaultcodes":
                tester.ClearFaultCodes();
                break;

            case "delcovwpremium5safecode":
                tester.DelcoVWPremium5SafeCode();
                break;

            case "dumpccmrom":
                tester.DumpCcmRom(_filename);
                break;

            case "dumpclusternecrom":
                tester.DumpClusterNecRom(_filename);
                break;

            case "dumpedc15eeprom":
            {
                var eeprom = tester.ReadWriteEdc15Eeprom(_filename);
                Edc15VM.DisplayEepromInfo(eeprom);
            }
                break;

            case "dumpeeprom":
                tester.DumpEeprom(address, length, _filename);
                break;

            case "dumpmarellimem":
                tester.DumpMarelliMem(address, length, ecuInfo, _filename);
                return;

            case "dumpmem":
                tester.DumpMem(address, length, _filename);
                break;

            case "dumpram":
                tester.DumpRam(address, length, _filename);
                break;

            case "dumprom":
                tester.DumpRom(address, length, _filename);
                break;

            case "findlogins":
                tester.FindLogins(login!.Value, ecuInfo.WorkshopCode);
                break;

            case "getclusterid":
                tester.GetClusterId();
                break;

            case "groupread":
                tester.GroupRead(groupNumber);
                break;

            case "loadeeprom":
                tester.LoadEeprom(address, _filename!);
                break;

            case "mapeeprom":
                tester.MapEeprom(_filename);
                break;

            case "readeeprom":
                tester.ReadEeprom(address);
                break;

            case "readram":
                tester.ReadRam(address);
                break;

            case "readrom":
                tester.ReadRom(address);
                break;

            case "readfaultcodes":
                tester.ReadFaultCodes();
                break;

            case "readident":
                tester.ReadIdent();
                break;

            case "readsoftwareversion":
                tester.ReadSoftwareVersion();
                break;

            case "reset":
                tester.Reset();
                break;

            case "setsoftwarecoding":
                tester.SetSoftwareCoding(softwareCoding, workshopCode);
                break;

            case "writeedc15eeprom":
                tester.ReadWriteEdc15Eeprom(_filename, addressValuePairs);
                break;

            case "writeeeprom":
                tester.WriteEeprom(address, value);
                break;

            default:
                ShowUsage();
                break;
        }

        tester.EndCommunication();
    }

    private static void AutoScan(IInterface @interface)
    {
        var kwp1281Addresses = new List<string>();
        var kwp2000Addresses = new List<string>();
        foreach (var evenParity in new bool[] { false, true })
        {
            var parity = evenParity ? "(EvenParity)" : "";
            for (var address = 0; address < 0x80; address++)
            {
                var tester = new Tester(@interface, address);
                try
                {
                    Logger.Log.WriteLine($"Attempting to wake up controller at address {address:X}{parity}...");
                    tester.Kwp1281Wakeup(evenParity, failQuietly: true);
                    tester.EndCommunication();
                    kwp1281Addresses.Add($"{address:X}{parity}");
                }
                catch (UnableToProceedException)
                {
                }
                catch (UnexpectedProtocolException)
                {
                    kwp2000Addresses.Add($"{address:X}{parity}");
                }
            }
        }

        Logger.Log.WriteLine($"AutoScan Results:");
        Logger.Log.WriteLine($"KWP1281: {string.Join(' ', kwp1281Addresses)}");
        Logger.Log.WriteLine($"KWP2000: {string.Join(' ', kwp2000Addresses)}");
    }

    private static void TestCanInterface(string portName, int baudRate)
    {
        Logger.Log.WriteLine("=== CAN Interface Test ===");
        Logger.Log.WriteLine($"Port: {portName}, Baud: {baudRate}");
        
        try
        {
            using var canInterface = new CanInterface(portName, baudRate);
            
            Logger.Log.WriteLine("Initializing CAN interface...");
            if (!canInterface.Initialize())
            {
                Logger.Log.WriteLine("Failed to initialize CAN interface");
                return;
            }
            
            Logger.Log.WriteLine("Setting CAN speed to 500 kbps...");
            if (!canInterface.SetCanSpeed(500))
            {
                Logger.Log.WriteLine("Failed to set CAN speed");
                return;
            }
            
            Logger.Log.WriteLine("CAN interface ready!");
            Logger.Log.WriteLine();
            
            // Send a test OBD-II request (Mode 01, PID 00 - supported PIDs)
            Logger.Log.WriteLine("Sending test OBD-II request (Mode 01, PID 00)...");
            var requestMsg = new CanMessage(0x7DF, new byte[] { 0x02, 0x01, 0x00, 0, 0, 0, 0, 0 });
            
            if (canInterface.SendCanMessage(requestMsg))
            {
                Logger.Log.WriteLine("Request sent successfully");
                
                // Try to receive response
                Logger.Log.WriteLine("Waiting for response...");
                for (int i = 0; i < 5; i++)
                {
                    var response = canInterface.ReceiveCanMessage(1000);
                    if (response != null)
                    {
                        Logger.Log.WriteLine($"Received: {response}");
                    }
                    else
                    {
                        Logger.Log.WriteLine("No response received");
                        break;
                    }
                }
            }
            else
            {
                Logger.Log.WriteLine("Failed to send request");
            }
            
            Logger.Log.WriteLine();
            Logger.Log.WriteLine("CAN test completed");
        }
        catch (Exception ex)
        {
            Logger.Log.WriteLine($"CAN test failed: {ex.Message}");
            Logger.Log.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private static void MonitorCanBus(string portName, int baudRate)
    {
        Logger.Log.WriteLine("=== CAN Bus Monitor ===");
        Logger.Log.WriteLine($"Port: {portName}, Baud: {baudRate}");
        Logger.Log.WriteLine("Press any key to stop monitoring.");
        Logger.Log.WriteLine();

        try
        {
            using var canInterface = new CanInterface(portName, baudRate);

            if (!canInterface.Initialize())
            {
                Logger.Log.WriteLine("Failed to initialize CAN interface");
                return;
            }

            if (!canInterface.SetCanSpeed(500))
            {
                Logger.Log.WriteLine("Failed to set CAN speed");
                return;
            }

            // Enable CAN monitor mode - show all traffic
            canInterface.SetMonitorMode(true);

            Logger.Log.WriteLine("Monitoring CAN bus traffic (500 kbps)...");
            Logger.Log.WriteLine("----------------------------------------------");

            var messageCount = 0;
            while (!Console.KeyAvailable)
            {
                var msg = canInterface.ReceiveCanMessage(500);
                if (msg != null)
                {
                    messageCount++;
                    Logger.Log.WriteLine($"[{messageCount,6}] {msg}");
                }
            }

            // Consume the key press
            Console.ReadKey(intercept: true);

            Logger.Log.WriteLine();
            Logger.Log.WriteLine($"Monitoring stopped. Total messages: {messageCount}");
        }
        catch (Exception ex)
        {
            Logger.Log.WriteLine($"CAN monitor failed: {ex.Message}");
        }
    }

    private static void TestTp20Channel(string portName, int baudRate, byte controllerAddress)
    {
        Logger.Log.WriteLine("=== VW TP 2.0 KWP2000 Diagnostic Test ===");
        Logger.Log.WriteLine($"Port: {portName}, Baud: {baudRate}, Controller: 0x{controllerAddress:X2}");

        try
        {
            using var canInterface = new CanInterface(portName, baudRate);

            Logger.Log.WriteLine("Initializing CAN interface...");
            if (!canInterface.Initialize())
            {
                Logger.Log.WriteLine("Failed to initialize CAN interface");
                return;
            }

            if (!canInterface.SetCanSpeed(500))
            {
                Logger.Log.WriteLine("Failed to set CAN speed to 500 kbps");
                return;
            }

            Logger.Log.WriteLine("CAN interface ready. Opening TP 2.0 channel...");

            using var channel = new Tp20Channel(canInterface, controllerAddress);
            if (!channel.Open())
            {
                Logger.Log.WriteLine("Failed to open TP 2.0 channel");
                return;
            }

            using var kwp2000 = new Kwp2000CanDialog(channel);

            // Send ReadECUIdentification (service 0x1A, sub 0x9B)
            Logger.Log.WriteLine("Reading ECU identification...");
            try
            {
                var response = kwp2000.SendReceive(
                    DiagnosticService.readEcuIdentification,
                    new byte[] { 0x9B });
                Logger.Log.WriteLine($"ECU Identification: {Utils.DumpAscii(response.Body)}");
            }
            catch (Exception ex)
            {
                Logger.Log.WriteLine($"ReadECUIdentification failed: {ex.Message}");
            }

            // Send TesterPresent (service 0x3E) to verify connectivity
            Logger.Log.WriteLine();
            Logger.Log.WriteLine("Sending TesterPresent...");
            try
            {
                kwp2000.SendReceive(
                    DiagnosticService.testerPresent,
                    Array.Empty<byte>());
                Logger.Log.WriteLine("TesterPresent positive response received!");
            }
            catch (Exception ex)
            {
                Logger.Log.WriteLine($"TesterPresent failed: {ex.Message}");
            }

            Logger.Log.WriteLine();
            Logger.Log.WriteLine("TP 2.0 diagnostic test completed.");
        }
        catch (Exception ex)
        {
            Logger.Log.WriteLine($"TP 2.0 test failed: {ex.Message}");
            Logger.Log.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private static void CanAutoScan(string portName, int baudRate)
    {
        Logger.Log.WriteLine("=== CAN AutoScan ===");
        Logger.Log.WriteLine($"Port: {portName}, Baud: {baudRate}");
        Logger.Log.WriteLine("Scanning VW TP 2.0 addresses 0x01-0x7F...");
        Logger.Log.WriteLine();

        try
        {
            using var canInterface = new CanInterface(portName, baudRate);

            if (!canInterface.Initialize())
            {
                Logger.Log.WriteLine("Failed to initialize CAN interface");
                return;
            }

            if (!canInterface.SetCanSpeed(500))
            {
                Logger.Log.WriteLine("Failed to set CAN speed to 500 kbps");
                return;
            }

            var foundModules = new List<(byte Address, string Name, string Protocol, string Ident)>();

            for (byte address = 0x01; address < 0x80; address++)
            {
                Logger.Log.Write($"\rScanning 0x{address:X2}...");

                using var channel = new Tp20Channel(canInterface, address);
                if (!channel.Open())
                {
                    continue;
                }

                var name = ControllerAddressExtensions.GetControllerName(address);
                var protocol = "";
                var ident = "";

                // Try KWP2000 identification first
                try
                {
                    using var kwp2000 = new Kwp2000CanDialog(channel);
                    var response = kwp2000.SendReceive(
                        DiagnosticService.readEcuIdentification,
                        new byte[] { 0x9B });
                    ident = Utils.DumpAscii(response.Body).Trim();
                    protocol = "KWP2000";
                }
                catch
                {
                    // KWP2000 failed — try UDS ReadDataByIdentifier (DID F190 = VIN or F187 = PartNumber)
                    try
                    {
                        // Re-open channel since previous dialog may have disrupted it
                        using var channel2 = new Tp20Channel(canInterface, address);
                        if (channel2.Open())
                        {
                            using var uds = new UdsCanDialog(channel2);
                            var partData = uds.ReadDataByIdentifier(0xF187);
                            if (partData.Length > 2)
                            {
                                ident = Utils.DumpAscii(partData[2..]).Trim();
                            }
                            protocol = "UDS";
                        }
                    }
                    catch
                    {
                        protocol = "TP2.0";
                    }
                }

                foundModules.Add((address, name, protocol, ident));
                Logger.Log.WriteLine($"\r  0x{address:X2} {name,-20} [{protocol,-7}] {ident}");
            }

            Logger.Log.WriteLine();
            Logger.Log.WriteLine($"=== CAN AutoScan Results: {foundModules.Count} module(s) found ===");

            foreach (var (address, name, protocol, ident) in foundModules)
            {
                Logger.Log.WriteLine($"  0x{address:X2} {name,-20} [{protocol,-7}] {ident}");
            }
        }
        catch (Exception ex)
        {
            Logger.Log.WriteLine($"\nCAN AutoScan failed: {ex.Message}");
        }
    }

    private static void TestUdsDialog(string portName, int baudRate, byte controllerAddress)
    {
        Logger.Log.WriteLine("=== UDS (ISO 14229) Diagnostic Test ===");
        Logger.Log.WriteLine($"Port: {portName}, Baud: {baudRate}, Controller: 0x{controllerAddress:X2}");

        try
        {
            using var canInterface = new CanInterface(portName, baudRate);

            if (!canInterface.Initialize())
            {
                Logger.Log.WriteLine("Failed to initialize CAN interface");
                return;
            }

            if (!canInterface.SetCanSpeed(500))
            {
                Logger.Log.WriteLine("Failed to set CAN speed to 500 kbps");
                return;
            }

            using var channel = new Tp20Channel(canInterface, controllerAddress);
            if (!channel.Open())
            {
                Logger.Log.WriteLine("Failed to open TP 2.0 channel");
                return;
            }

            using var uds = new UdsCanDialog(channel);

            // DiagnosticSessionControl → Extended session (0x03)
            Logger.Log.WriteLine("Starting extended diagnostic session...");
            try
            {
                var sessionResponse = uds.DiagnosticSessionControl(0x03);
                Logger.Log.WriteLine($"Session started ({sessionResponse.Length} bytes response)");
            }
            catch (NegativeUdsResponseException ex)
            {
                Logger.Log.WriteLine($"DiagnosticSessionControl failed: {ex.Message}");
                Logger.Log.WriteLine("Continuing with default session...");
            }

            // ReadDataByIdentifier: DID F190 = VIN
            Logger.Log.WriteLine();
            Logger.Log.WriteLine("Reading VIN (DID F190)...");
            try
            {
                var vinData = uds.ReadDataByIdentifier(0xF190);
                if (vinData.Length >= 2)
                {
                    // Response: [DID_high, DID_low, ...VIN_bytes]
                    var vin = System.Text.Encoding.ASCII.GetString(vinData, 2, vinData.Length - 2);
                    Logger.Log.WriteLine($"VIN: {vin}");
                }
            }
            catch (NegativeUdsResponseException ex)
            {
                Logger.Log.WriteLine($"Read VIN failed: {ex.Message}");
            }

            // ReadDataByIdentifier: DID F187 = Part Number
            Logger.Log.WriteLine();
            Logger.Log.WriteLine("Reading Part Number (DID F187)...");
            try
            {
                var partData = uds.ReadDataByIdentifier(0xF187);
                if (partData.Length >= 2)
                {
                    var partNum = Utils.DumpAscii(partData[2..]);
                    Logger.Log.WriteLine($"Part Number: {partNum}");
                }
            }
            catch (NegativeUdsResponseException ex)
            {
                Logger.Log.WriteLine($"Read Part Number failed: {ex.Message}");
            }

            // TesterPresent
            Logger.Log.WriteLine();
            Logger.Log.WriteLine("Sending TesterPresent...");
            try
            {
                uds.TesterPresent();
                Logger.Log.WriteLine("TesterPresent OK");
            }
            catch (NegativeUdsResponseException ex)
            {
                Logger.Log.WriteLine($"TesterPresent failed: {ex.Message}");
            }

            Logger.Log.WriteLine();
            Logger.Log.WriteLine("UDS diagnostic test completed.");
        }
        catch (Exception ex)
        {
            Logger.Log.WriteLine($"UDS test failed: {ex.Message}");
        }
    }

    private static void CanMultiEcu(string portName, int baudRate)
    {
        Logger.Log.WriteLine("=== CAN Multi-ECU Diagnostic Session ===");
        Logger.Log.WriteLine($"Port: {portName}, Baud: {baudRate}");
        Logger.Log.WriteLine("Opening simultaneous TP 2.0 channels to multiple ECUs...");
        Logger.Log.WriteLine();

        try
        {
            using var canInterface = new CanInterface(portName, baudRate);

            if (!canInterface.Initialize())
            {
                Logger.Log.WriteLine("Failed to initialize CAN interface");
                return;
            }

            if (!canInterface.SetCanSpeed(500))
            {
                Logger.Log.WriteLine("Failed to set CAN speed to 500 kbps");
                return;
            }

            using var router = new CanRouter(canInterface);
            using var session = new Tp20Session(router);

            // Phase 1: Discover available modules by trying well-known addresses
            byte[] targetAddresses =
            [
                0x01, // Engine (ECU)
                0x02, // Transmission
                0x09, // Central Electronics
                0x17, // Instrument Cluster
                0x19, // CAN Gateway
                0x46, // Central Comfort
                0x56, // Radio
            ];

            var openModules = new List<(byte Address, string Name)>();

            foreach (var address in targetAddresses)
            {
                var name = ControllerAddressExtensions.GetControllerName(address);
                Logger.Log.Write($"  Trying 0x{address:X2} ({name})...");

                try
                {
                    session.OpenChannel(address);
                    openModules.Add((address, name));
                    Logger.Log.WriteLine(" OPEN");
                }
                catch
                {
                    Logger.Log.WriteLine(" no response");
                }
            }

            if (openModules.Count == 0)
            {
                Logger.Log.WriteLine();
                Logger.Log.WriteLine("No modules responded. Check CAN bus connection.");
                return;
            }

            Logger.Log.WriteLine();
            Logger.Log.WriteLine($"{openModules.Count} channel(s) open simultaneously.");
            Logger.Log.WriteLine();

            // Phase 2: Read identification from each open module
            foreach (var (address, name) in openModules)
            {
                var channel = session.GetChannel(address);
                if (channel == null) continue;

                Logger.Log.WriteLine($"--- 0x{address:X2} {name} ---");

                // Try KWP2000 ReadECUIdentification
                try
                {
                    using var kwp2000 = new Kwp2000CanDialog(channel);
                    var response = kwp2000.SendReceive(
                        DiagnosticService.readEcuIdentification,
                        new byte[] { 0x9B });
                    Logger.Log.WriteLine($"  ECU Ident: {Utils.DumpAscii(response.Body).Trim()}");
                }
                catch
                {
                    // Try UDS as fallback
                    try
                    {
                        using var uds = new UdsCanDialog(channel);
                        var partData = uds.ReadDataByIdentifier(0xF187);
                        if (partData.Length > 2)
                        {
                            Logger.Log.WriteLine($"  Part No:   {Utils.DumpAscii(partData[2..]).Trim()}");
                        }
                    }
                    catch
                    {
                        Logger.Log.WriteLine("  (identification not available)");
                    }
                }

                // Keep-alive to maintain all channels
                session.SendKeepAliveAll();
            }

            Logger.Log.WriteLine();
            Logger.Log.WriteLine($"Multi-ECU session completed. {session.ChannelCount} channel(s) were open.");
        }
        catch (Exception ex)
        {
            Logger.Log.WriteLine($"\nMulti-ECU session failed: {ex.Message}");
        }
    }

    private static void ShowUsage()
    {
        Logger.Log.WriteLine("""
Usage: KW1281Test PORT BAUD ADDRESS COMMAND [args]
                
PORT = COM1|COM2|etc. (Windows)
    /dev/ttyXXXX (Linux)
    AABBCCDD (macOS/Linux FTDI cable serial number)
BAUD = 10400|9600|etc.
ADDRESS = Controller address, e.g. 1 (ECU), 17 (cluster), 46 (CCM), 56 (radio)
COMMAND =
    ActuatorTest
    AdaptationRead CHANNEL [LOGIN]
        CHANNEL = Channel number (0-99)
        LOGIN = Optional login (0-65535)
    AdaptationSave CHANNEL VALUE [LOGIN]
        CHANNEL = Channel number (0-99)
        VALUE = Channel value (0-65535)
        LOGIN = Optional login (0-65535)
    AdaptationTest CHANNEL VALUE [LOGIN]
        CHANNEL = Channel number (0-99)
        VALUE = Channel value (0-65535)
        LOGIN = Optional login (0-65535)
    AutoScan
    BasicSetting GROUP
        GROUP = Group number (0-255)
        (Group 0: Raw controller data)
    CanAutoScan
        Scan CAN bus for all VW TP 2.0 modules (addresses 0x01-0x7F)
    CanMonitor
        Monitor CAN bus traffic (passive, press any key to stop)
    CanMulti
        Open simultaneous TP 2.0 channels to multiple ECUs and read identification
    CanTest
        Test CAN interface initialization and basic communication
    CanTp
        Open a VW TP 2.0 channel and send a KWP2000 TesterPresent request
    CanUds
        Open a VW TP 2.0 channel and test UDS (ISO 14229) services
    ClarionVWPremium4SafeCode
    ClearFaultCodes
    DelcoVWPremium5SafeCode
    DumpEdc15Eeprom [FILENAME]
        FILENAME = Optional filename
    DumpEeprom START LENGTH [FILENAME]
        START = Start address in decimal (e.g. 0) or hex (e.g. 0x0)
        LENGTH = Number of bytes in decimal (e.g. 2048) or hex (e.g. 0x800)
        FILENAME = Optional filename
    DumpMarelliMem START LENGTH [FILENAME]
        START = Start address in decimal (e.g. 3072) or hex (e.g. 0xC00)
        LENGTH = Number of bytes in decimal (e.g. 1024) or hex (e.g. 0x400)
        FILENAME = Optional filename
    DumpMem START LENGTH [FILENAME]
        START = Start address in decimal (e.g. 8192) or hex (e.g. 0x2000)
        LENGTH = Number of bytes in decimal (e.g. 65536) or hex (e.g. 0x10000)
        FILENAME = Optional filename
    DumpRam START LENGTH [FILENAME]
        START = Start address in decimal (e.g. 8192) or hex (e.g. 0x2000)
        LENGTH = Number of bytes in decimal (e.g. 65536) or hex (e.g. 0x10000)
        FILENAME = Optional filename
    DumpRBxMem START LENGTH [FILENAME]
        START = Start address in decimal (e.g. 66560) or hex (e.g. 0x10400)
        LENGTH = Number of bytes in decimal (e.g. 1024) or hex (e.g. 0x400)
        FILENAME = Optional filename
    DumpRom START LENGTH [FILENAME]
        START = Start address in decimal (e.g. 8192) or hex (e.g. 0x2000)
        LENGTH = Number of bytes in decimal (e.g. 65536) or hex (e.g. 0x10000)
        FILENAME = Optional filename
    FindLogins LOGIN
        LOGIN = Known good login (0-65535)
    GetSKC
    GroupRead GROUP
        GROUP = Group number (0-255)
        (Group 0: Raw controller data)
    LoadEeprom START FILENAME
        START = Start address in decimal (e.g. 0) or hex (e.g. 0x0)
        FILENAME = Name of file containing binary data to load into EEPROM
    MapEeprom
    ReadFaultCodes
    ReadIdent
    ReadEeprom ADDRESS
        ADDRESS = Address in decimal (e.g. 4361) or hex (e.g. 0x1109)
    ReadRAM ADDRESS
        ADDRESS = Address in decimal (e.g. 4361) or hex (e.g. 0x1109)
    ReadROM ADDRESS
        ADDRESS = Address in decimal (e.g. 4361) or hex (e.g. 0x1109)
    ReadSoftwareVersion
    Reset
    SetSoftwareCoding CODING WORKSHOP
        CODING = Software coding in decimal (e.g. 4361) or hex (e.g. 0x1109)
        WORKSHOP = Workshop code in decimal (e.g. 4361) or hex (e.g. 0x1109)
    ToggleRB4Mode
    WriteEdc15Eeprom ADDRESS1 VALUE1 [ADDRESS2 VALUE2 ... ADDRESSn VALUEn]
        ADDRESS = EEPROM address in decimal (0-511) or hex (0x00-0x1FF)
        VALUE = Value to be stored in decimal (0-255) or hex (0x00-0xFF)
    WriteEeprom ADDRESS VALUE
        ADDRESS = Address in decimal (e.g. 4361) or hex (e.g. 0x1109)
        VALUE = Value in decimal (e.g. 138) or hex (e.g. 0x8A)
""");
    }

    private string? _filename = null;
}
