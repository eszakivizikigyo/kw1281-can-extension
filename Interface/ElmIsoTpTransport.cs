using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace BitFab.KW1281Test.Interface;

/// <summary>
/// ISO-TP (ISO 15765-2) transport using ELM327/STN built-in protocol handling.
/// Uses CAN protocol 6 (ISO 15765-4, 500kbps, 11-bit) with CAF ON.
/// The ELM327 handles all ISO-TP framing (SF, FF, CF, FC) internally.
/// 
/// For vehicles that use UDS over ISO-TP without VW TP 2.0 (e.g., VW T5).
/// </summary>
internal class ElmIsoTpTransport : ICanTransport
{
    private readonly CanInterface _can;
    private readonly uint _txId;
    private readonly uint _rxId;
    private byte[]? _lastResponse;

    public bool IsOpen { get; private set; }

    /// <summary>
    /// Create a transport for UDS communication with a specific ECU.
    /// </summary>
    /// <param name="canInterface">Initialized CanInterface (Initialize() must have been called)</param>
    /// <param name="txCanId">Tester → ECU CAN ID (e.g., 0x7E0 for engine)</param>
    /// <param name="rxCanId">ECU → Tester CAN ID (e.g., 0x7E8 for engine)</param>
    public ElmIsoTpTransport(CanInterface canInterface, uint txCanId, uint rxCanId)
    {
        _can = canInterface ?? throw new ArgumentNullException(nameof(canInterface));
        _txId = txCanId;
        _rxId = rxCanId;
    }

    /// <summary>
    /// Configure the ELM327 for ISO-TP protocol mode and set TX/RX addresses.
    /// </summary>
    public bool Open(int speedKbps = 500)
    {
        try
        {
            Log.WriteLine($"Opening ISO-TP transport: TX=0x{_txId:X3} RX=0x{_rxId:X3}");

            // Set CAN protocol with ISO-TP framing enabled
            string protocolCmd = speedKbps switch
            {
                500 => "ATSP6",  // ISO 15765-4, CAN 500kbps, 11-bit
                250 => "ATSP8",  // ISO 15765-4, CAN 250kbps, 11-bit
                _ => throw new ArgumentException($"Unsupported CAN speed: {speedKbps}")
            };

            if (!_can.SendCommand(protocolCmd)) return false;

            // Ensure CAN auto-formatting is ON (ELM327 handles ISO-TP framing)
            if (!_can.SendCommand("ATCAF1")) return false;

            // Enable CAN flow control (sends FC frames for multi-frame responses)
            // Must be explicitly re-enabled after raw CAN mode (ATCAF0) may have disabled it
            if (!_can.SendCommand("ATCFC1")) return false;

            // Set TX header (tester → ECU CAN ID)
            if (!_can.SendCommand($"ATSH{_txId:X3}")) return false;

            // Set RX filter (ECU → tester CAN ID)
            if (!_can.SendCommand($"ATCRA{_rxId:X3}")) return false;

            // Explicitly configure Flow Control: header = TX ID, data = CTS/BS=0/STmin=0
            // Required after raw CAN mode — STN1170 may not auto-configure FC correctly
            if (!_can.SendCommand($"ATFCSH{_txId:X3}")) return false;  // FC frame CAN ID
            if (!_can.SendCommand("ATFCSD300000")) return false;        // FC: CTS, BS=0, STmin=0
            if (!_can.SendCommand("ATFCSM1")) return false;             // Use custom FC settings

            // Turn off headers so response contains only UDS payload
            if (!_can.SendCommand("ATH0")) return false;

            // Disable spaces in responses for easier parsing
            if (!_can.SendCommand("ATS0")) return false;

            // Longer response timeout for slow ECUs (0xFF = ~1020ms)
            if (!_can.SendCommand("ATSTFF")) return false;

            IsOpen = true;
            Log.WriteLine("ISO-TP transport opened successfully");
            return true;
        }
        catch (Exception ex)
        {
            Log.WriteLine($"Failed to open ISO-TP transport: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Send UDS payload via ISO-TP. The ELM327 handles framing and waits for the response.
    /// The response is buffered and can be retrieved with ReceiveData().
    /// </summary>
    public bool SendData(byte[] data)
    {
        if (!IsOpen) return false;

        try
        {
            // Convert UDS payload to hex string
            // In protocol mode with CAF ON, ELM327 adds the PCI byte automatically
            var hex = BitConverter.ToString(data).Replace("-", "");

            Log.WriteLine($"ISO-TP TX: [{data.Length}] {hex}");
            _lastResponse = null;

            // Send and capture response
            var response = SendAndReceive(hex);

            if (response == null)
            {
                Log.WriteLine("ISO-TP: No response received");
                return true; // Send succeeded even if no response
            }

            _lastResponse = response;
            Log.WriteLine($"ISO-TP RX: [{response.Length}] {BitConverter.ToString(response).Replace("-", "")}");
            return true;
        }
        catch (Exception ex)
        {
            Log.WriteLine($"ISO-TP send error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Return the response from the last SendData call.
    /// </summary>
    public byte[]? ReceiveData()
    {
        return _lastResponse;
    }

    public void Dispose()
    {
        IsOpen = false;
    }

    /// <summary>
    /// Send hex command and parse the ISO-TP response.
    /// In protocol mode, ELM327 returns the assembled UDS response payload.
    /// </summary>
    private byte[]? SendAndReceive(string hexPayload)
    {
        var lines = _can.SendCommandWithResponseLines(hexPayload, timeoutMs: 5000);

        if (lines.Count == 0)
            return null;

        // Check for error responses
        var first = lines[0];
        if (first.Contains("NO DATA") ||
            first.Contains("CAN ERROR") ||
            first.Contains("?") ||
            first.Contains("ERROR") ||
            first.Contains("UNABLE TO CONNECT") ||
            first.Contains("STOPPED"))
        {
            return null;
        }

        return ParseIsoTpResponse(lines);
    }

    /// <summary>
    /// Parse ELM327 ISO-TP protocol mode response to raw UDS bytes.
    /// With ATH0 + ATS0 + ATCAF1:
    ///   Single frame: "5003003201F4" (just payload hex)
    ///   Multi-frame:  line 0 = "014" (total byte count)
    ///                 line 1 = "0:62F190575631" (seq 0: data)
    ///                 line 2 = "1:5A5A5A374858C" (seq 1: data)
    ///                 line 3 = "2:48303137303737" (seq 2: data)
    /// </summary>
    private static byte[]? ParseIsoTpResponse(List<string> lines)
    {
        if (lines.Count == 0) return null;

        // Filter out "SEARCHING..." and "OK" lines
        var dataLines = lines
            .Where(l => !l.Contains("SEARCHING") && !l.Contains("OK") && l.Length > 0)
            .ToList();

        if (dataLines.Count == 0) return null;

        // Check if this is a multi-frame response (contains lines with "N:" pattern)
        if (dataLines.Any(l => l.Contains(':')))
        {
            return ParseMultiFrameResponse(dataLines);
        }

        // Single frame: just hex bytes
        return HexToBytes(dataLines[0]);
    }

    /// <summary>
    /// Parse multi-frame ISO-TP response from ELM327.
    /// First line is total byte count (hex), subsequent lines are "seqNum:hexData".
    /// </summary>
    private static byte[]? ParseMultiFrameResponse(List<string> lines)
    {
        var allData = new StringBuilder();

        foreach (var line in lines)
        {
            var colonIdx = line.IndexOf(':');
            if (colonIdx >= 0)
            {
                // Extract hex data after the colon
                allData.Append(line.Substring(colonIdx + 1));
            }
            // Lines without colon (like the byte count "014") are skipped
        }

        return HexToBytes(allData.ToString());
    }

    private static byte[]? HexToBytes(string hex)
    {
        hex = hex.Trim();
        if (hex.Length < 2 || hex.Length % 2 != 0)
            return null;

        try
        {
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }
        catch
        {
            return null;
        }
    }
}
