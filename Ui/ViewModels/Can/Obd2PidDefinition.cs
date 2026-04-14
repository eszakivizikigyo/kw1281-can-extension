using System;

namespace BitFab.KW1281Test.Ui.ViewModels.Can;

/// <summary>
/// Definition of a standard OBD2 PID (SAE J1979 / ISO 15031-5).
/// </summary>
public record Obd2PidDefinition(
    byte Service,
    byte Pid,
    string Name,
    string Unit,
    int ResponseBytes,
    Func<byte[], double> Decode)
{
    /// <summary>Standard Mode 01 PIDs for live data.</summary>
    public static readonly Obd2PidDefinition[] StandardPids =
    [
        new(0x01, 0x04, "Engine Load", "%", 1, d => d[0] * 100.0 / 255),
        new(0x01, 0x05, "Coolant Temp", "°C", 1, d => d[0] - 40),
        new(0x01, 0x06, "Short Term Fuel Trim B1", "%", 1, d => (d[0] - 128) * 100.0 / 128),
        new(0x01, 0x07, "Long Term Fuel Trim B1", "%", 1, d => (d[0] - 128) * 100.0 / 128),
        new(0x01, 0x0B, "Intake MAP", "kPa", 1, d => d[0]),
        new(0x01, 0x0C, "Engine RPM", "rpm", 2, d => (d[0] * 256 + d[1]) / 4.0),
        new(0x01, 0x0D, "Vehicle Speed", "km/h", 1, d => d[0]),
        new(0x01, 0x0E, "Timing Advance", "°", 1, d => d[0] / 2.0 - 64),
        new(0x01, 0x0F, "Intake Air Temp", "°C", 1, d => d[0] - 40),
        new(0x01, 0x10, "MAF Rate", "g/s", 2, d => (d[0] * 256 + d[1]) / 100.0),
        new(0x01, 0x11, "Throttle Position", "%", 1, d => d[0] * 100.0 / 255),
        new(0x01, 0x1C, "OBD Standard", "", 1, d => d[0]),
        new(0x01, 0x1F, "Run Time", "s", 2, d => d[0] * 256 + d[1]),
        new(0x01, 0x2F, "Fuel Level", "%", 1, d => d[0] * 100.0 / 255),
        new(0x01, 0x33, "Barometric Pressure", "kPa", 1, d => d[0]),
        new(0x01, 0x42, "Control Module Voltage", "V", 2, d => (d[0] * 256 + d[1]) / 1000.0),
        new(0x01, 0x46, "Ambient Air Temp", "°C", 1, d => d[0] - 40),
        new(0x01, 0x5C, "Engine Oil Temp", "°C", 1, d => d[0] - 40),
    ];
}
