using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace BitFab.KW1281Test.Interface
{
    /// <summary>
    /// CAN interface for ELM327 and compatible adapters (ELM327, HEX-V2).
    /// Supports two modes:
    /// - OBD mode (default): standard ELM327 OBD-II protocol handling
    /// - Raw CAN mode: direct CAN frame send/receive for VW TP 2.0 / UDS
    /// </summary>
    internal class CanInterface : IDisposable
    {
        private readonly SerialPort _port;
        private readonly object _lock = new object();
        private bool _isInitialized;
        private bool _rawMode;
        private uint _currentTxHeader = uint.MaxValue;
        private readonly Queue<CanMessage> _frameBuffer = new();

        /// <summary>Whether the interface is in raw CAN mode (ATCAF0 + ATH1).</summary>
        public bool IsRawMode => _rawMode;

        public int ReadTimeout
        {
            get => _port.ReadTimeout;
            set => _port.ReadTimeout = value;
        }

        public int WriteTimeout
        {
            get => _port.WriteTimeout;
            set => _port.WriteTimeout = value;
        }

        public CanInterface(string portName, int baudRate = 115200)
        {
            _port = new SerialPort(portName)
            {
                BaudRate = baudRate,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                RtsEnable = false,
                DtrEnable = true,
                ReadTimeout = 1000,
                WriteTimeout = 1000,
                NewLine = "\r" // AT commands use CR as terminator
            };

            _port.Open();
            Thread.Sleep(100); // Allow interface to stabilize
        }

        // Internal constructor for unit testing (accepts pre-configured SerialPort)
        internal CanInterface(SerialPort port)
        {
            _port = port;
        }

        public void Dispose()
        {
            try
            {
                if (_isInitialized)
                {
                    SendCommand("ATZ"); // Reset device
                }
                _port?.Close();
                _port?.Dispose();
            }
            catch
            {
                // Ignore errors during disposal
            }
        }

        /// <summary>
        /// Initialize the CAN interface with basic ELM327 settings.
        /// Auto-detects baud rate if the initial rate fails.
        /// </summary>
        public bool Initialize()
        {
            lock (_lock)
            {
                try
                {
                    Log.WriteLine("Initializing CAN interface...");

                    // Try ATZ at the current baud rate first
                    if (!TryReset())
                    {
                        // Auto-detect baud rate
                        Log.WriteLine("ATZ failed — trying other baud rates...");
                        int[] candidates = [38400, 9600, 115200, 500000];
                        bool found = false;

                        foreach (var rate in candidates)
                        {
                            if (rate == _port.BaudRate) continue;
                            Log.WriteLine($"Trying {rate} baud...");
                            _port.BaudRate = rate;
                            if (TryReset())
                            {
                                Log.WriteLine($"ELM327 detected at {rate} baud");
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {
                            Log.WriteLine("Failed to reset device at any baud rate");
                            return false;
                        }
                    }

                    Thread.Sleep(500); // Wait for reset

                    // Turn off echo
                    if (!SendCommand("ATE0"))
                    {
                        Log.WriteLine("Failed to disable echo");
                        return false;
                    }

                    // Turn off spaces in responses
                    if (!SendCommand("ATS0"))
                    {
                        Log.WriteLine("Failed to disable spaces");
                        return false;
                    }

                    // Turn off line feeds
                    if (!SendCommand("ATL0"))
                    {
                        Log.WriteLine("Failed to disable line feeds");
                        return false;
                    }

                    // Set protocol to automatic
                    if (!SendCommand("ATSP0"))
                    {
                        Log.WriteLine("Failed to set protocol");
                        return false;
                    }

                    Log.WriteLine("CAN interface initialized successfully");
                    _isInitialized = true;
                    return true;
                }
                catch (Exception ex)
                {
                    Log.WriteLine($"Failed to initialize CAN interface: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Initialize raw CAN mode for VW TP 2.0 and UDS communication.
        /// Must be called after Initialize(). Enables direct CAN frame send/receive
        /// by disabling ELM327's ISO-TP auto-formatting.
        /// </summary>
        /// <param name="speedKbps">CAN bus speed: 500 or 250 kbps</param>
        public bool InitializeRawCan(int speedKbps = 500)
        {
            lock (_lock)
            {
                try
                {
                    Log.WriteLine($"Initializing raw CAN mode at {speedKbps} kbps...");

                    // Set CAN protocol (11-bit IDs)
                    string protocolCmd = speedKbps switch
                    {
                        500 => "ATSP6", // ISO 15765-4 CAN (11-bit, 500 kbps)
                        250 => "ATSP8", // ISO 15765-4 CAN (11-bit, 250 kbps)
                        _ => throw new ArgumentException($"Unsupported CAN speed: {speedKbps} kbps")
                    };
                    if (!SendCommand(protocolCmd))
                    {
                        Log.WriteLine("Failed to set CAN protocol");
                        return false;
                    }

                    // Disable CAN Auto Formatting — raw byte mode, no ISO-TP interpretation
                    if (!SendCommand("ATCAF0"))
                    {
                        Log.WriteLine("Failed to disable CAN auto formatting");
                        return false;
                    }

                    // Show CAN headers (IDs) in responses
                    if (!SendCommand("ATH1"))
                    {
                        Log.WriteLine("Failed to enable headers");
                        return false;
                    }

                    // Disable adaptive timing for predictable behavior
                    if (!SendCommand("ATAT0"))
                    {
                        Log.WriteLine("Failed to disable adaptive timing");
                        return false;
                    }

                    // Set response timeout: 0x32 = 50 decimal → 50 × 4.096ms ≈ 200ms
                    // This is the time ELM327 waits for CAN responses after sending a frame
                    if (!SendCommand("ATST32"))
                    {
                        Log.WriteLine("Failed to set timeout");
                        return false;
                    }

                    // Disable DLC display
                    if (!SendCommand("ATD0"))
                    {
                        Log.WriteLine("Failed to disable DLC display");
                        return false;
                    }

                    // Clear any receive address filter (receive from all IDs)
                    if (!SendCommand("ATAR"))
                    {
                        Log.WriteLine("Failed to clear receive filter");
                        return false;
                    }

                    _rawMode = true;
                    _currentTxHeader = uint.MaxValue;
                    _frameBuffer.Clear();
                    Log.WriteLine("Raw CAN mode initialized successfully");
                    return true;
                }
                catch (Exception ex)
                {
                    Log.WriteLine($"Failed to initialize raw CAN mode: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Set CAN bus speed (OBD mode only).
        /// </summary>
        /// <param name="speed">Speed in kbps (e.g., 500 for 500kbps)</param>
        public bool SetCanSpeed(int speed)
        {
            string command = speed switch
            {
                500 => "ATSP6", // CAN 500kbps, 11-bit ID
                250 => "ATSP8", // CAN 250kbps, 11-bit ID
                _ => throw new ArgumentException($"Unsupported CAN speed: {speed}kbps")
            };

            return SendCommand(command);
        }

        /// <summary>
        /// Set CAN receive address filter. When set, only frames with matching
        /// CAN IDs are received. Pass null to clear the filter (receive all).
        /// </summary>
        public bool SetRxFilter(uint? canId)
        {
            if (canId.HasValue)
            {
                return SendCommand($"ATCRA{canId.Value:X3}");
            }
            else
            {
                return SendCommand("ATAR");
            }
        }

        /// <summary>
        /// Enable or disable CAN monitor mode (passive listening).
        /// ATMA = Monitor All, receives all CAN traffic without filtering.
        /// </summary>
        public bool SetMonitorMode(bool enable)
        {
            if (enable)
            {
                // Set CAN monitor all mode - adapter will stream all CAN frames
                return SendCommand("ATMA");
            }
            else
            {
                // Send a single CR to exit monitor mode
                lock (_lock)
                {
                    _port.Write("\r");
                    Thread.Sleep(100);
                    _port.DiscardInBuffer();
                }
                return true;
            }
        }

        /// <summary>
        /// Attempt ATZ reset at current baud rate. Returns true if the device responds with "ELM".
        /// Does NOT hold _lock — caller must be outside lock or use carefully.
        /// </summary>
        private bool TryReset()
        {
            try
            {
                _port.DiscardInBuffer();
                _port.DiscardOutBuffer();

                // Send a bare CR first to interrupt any pending state
                _port.Write("\r");
                Thread.Sleep(100);
                _port.DiscardInBuffer();

                Log.WriteLine($"Sending: ATZ");
                _port.WriteLine("ATZ");

                var response = ReadResponse();
                Log.WriteLine($"Response: {response}");

                return response.Contains("ELM");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Send an AT command and wait for OK response.
        /// </summary>
        internal bool SendCommand(string command)
        {
            lock (_lock)
            {
                try
                {
                    _port.DiscardInBuffer();
                    _port.DiscardOutBuffer();

                    Log.WriteLine($"Sending: {command}");
                    _port.WriteLine(command);

                    var response = ReadResponse();
                    Log.WriteLine($"Response: {response}");

                    return response.Contains("OK") || response.Contains("ELM");
                }
                catch (TimeoutException)
                {
                    Log.WriteLine($"Timeout waiting for response to: {command}");
                    return false;
                }
                catch (Exception ex)
                {
                    Log.WriteLine($"Error sending command {command}: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Read response from the interface until prompt character '>'.
        /// Returns a single string (lines joined, whitespace stripped).
        /// </summary>
        private string ReadResponse()
        {
            var sb = new StringBuilder();
            var endTime = DateTime.Now.AddMilliseconds(ReadTimeout);

            while (DateTime.Now < endTime)
            {
                try
                {
                    if (_port.BytesToRead > 0)
                    {
                        var ch = (char)_port.ReadChar();
                        if (ch == '>')
                        {
                            break; // Prompt character indicates end of response
                        }
                        if (ch != '\r' && ch != '\n' && ch != '\0')
                        {
                            sb.Append(ch);
                        }
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
                catch (TimeoutException)
                {
                    break;
                }
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Read response from the interface until prompt '>', preserving line boundaries.
        /// Each non-empty line is returned as a separate string.
        /// Used in raw CAN mode where multiple CAN frames may appear in one response.
        /// </summary>
        private List<string> ReadResponseLines(int? timeoutMs = null)
        {
            var lines = new List<string>();
            var sb = new StringBuilder();
            var timeout = timeoutMs ?? ReadTimeout;
            var endTime = DateTime.Now.AddMilliseconds(timeout);

            while (DateTime.Now < endTime)
            {
                try
                {
                    if (_port.BytesToRead > 0)
                    {
                        var ch = (char)_port.ReadChar();
                        if (ch == '>')
                        {
                            break;
                        }
                        if (ch == '\r' || ch == '\n')
                        {
                            var line = sb.ToString().Trim();
                            if (line.Length > 0)
                            {
                                lines.Add(line);
                            }
                            sb.Clear();
                        }
                        else if (ch != '\0')
                        {
                            sb.Append(ch);
                        }
                    }
                    else
                    {
                        Thread.Sleep(5);
                    }
                }
                catch (TimeoutException)
                {
                    break;
                }
            }

            // Capture any remaining text
            var lastLine = sb.ToString().Trim();
            if (lastLine.Length > 0)
            {
                lines.Add(lastLine);
            }

            return lines;
        }

        /// <summary>
        /// Send a CAN message. In raw mode, sets the TX header and sends data bytes.
        /// Any CAN frames received as a response are buffered for later retrieval.
        /// </summary>
        public bool SendCanMessage(CanMessage message)
        {
            lock (_lock)
            {
                try
                {
                    if (_rawMode)
                    {
                        return SendCanMessageRaw(message);
                    }

                    // OBD mode: format as [ID][Data]
                    var idStr = message.IsExtended ? $"{message.Id:X8}" : $"{message.Id:X3}";
                    var dataStr = BitConverter.ToString(message.Data).Replace("-", "");
                    var command = $"{idStr}{dataStr}";

                    Log.WriteLine($"Sending CAN message: {message}");
                    _port.WriteLine(command);

                    var response = ReadResponse();
                    return !response.Contains("?") && !response.Contains("ERROR");
                }
                catch (Exception ex)
                {
                    Log.WriteLine($"Error sending CAN message: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Send a CAN message in raw mode. Sets ATSH header if changed, sends data,
        /// and buffers any received response frames.
        /// </summary>
        private bool SendCanMessageRaw(CanMessage message)
        {
            // Set TX CAN ID header if it changed since last send
            if (message.Id != _currentTxHeader)
            {
                var headerCmd = message.IsExtended
                    ? $"ATSH{message.Id:X8}"
                    : $"ATSH{message.Id:X3}";

                _port.DiscardInBuffer();
                _port.WriteLine(headerCmd);
                var headerResp = ReadResponse();
                if (!headerResp.Contains("OK"))
                {
                    Log.WriteLine($"Failed to set TX header: {headerResp}");
                    return false;
                }
                _currentTxHeader = message.Id;
            }

            // Send raw data bytes (ELM327 requires at least 1 byte)
            var dataStr = message.DataLength > 0
                ? BitConverter.ToString(message.Data).Replace("-", "")
                : "00";

            Log.WriteLine($"TX raw CAN: ID=0x{message.Id:X3} [{message.DataLength}] {dataStr}");
            _port.DiscardInBuffer();
            _port.WriteLine(dataStr);

            // Read response lines — may contain CAN frames, "NO DATA", or errors
            var lines = ReadResponseLines();

            foreach (var line in lines)
            {
                if (line.Contains("?") || line.Contains("ERROR") || line.Contains("CAN ERROR"))
                {
                    Log.WriteLine($"CAN send error: {line}");
                    return false;
                }
            }

            // Buffer any received CAN frames from the response
            BufferReceivedFrames(lines);
            return true;
        }

        /// <summary>
        /// Receive a CAN message. In raw mode, first checks the frame buffer,
        /// then uses ATMA monitor mode to passively listen for incoming frames.
        /// </summary>
        public CanMessage? ReceiveCanMessage(int timeoutMs = 1000)
        {
            lock (_lock)
            {
                // Check the frame buffer first (frames captured during sends)
                if (_frameBuffer.Count > 0)
                {
                    return _frameBuffer.Dequeue();
                }

                if (_rawMode)
                {
                    return ReceiveCanMessageRaw(timeoutMs);
                }

                try
                {
                    var oldTimeout = _port.ReadTimeout;
                    _port.ReadTimeout = timeoutMs;

                    var response = ReadResponse();

                    _port.ReadTimeout = oldTimeout;

                    if (string.IsNullOrWhiteSpace(response) || 
                        response.Contains("NO DATA") || 
                        response.Contains("?"))
                    {
                        return null;
                    }

                    // Parse response: "7E8 06 41 00 BE 3E B8 13"
                    return ParseCanMessage(response);
                }
                catch (TimeoutException)
                {
                    return null;
                }
                catch (Exception ex)
                {
                    Log.WriteLine($"Error receiving CAN message: {ex.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// Receive CAN frames in raw mode using ATMA (Monitor All).
        /// Enters monitor mode, reads frames until at least one is captured
        /// or timeout expires, then exits monitor mode.
        /// </summary>
        private CanMessage? ReceiveCanMessageRaw(int timeoutMs)
        {
            try
            {
                _port.DiscardInBuffer();
                _port.Write("ATMA\r");

                var sb = new StringBuilder();
                var endTime = DateTime.Now.AddMilliseconds(timeoutMs);

                while (DateTime.Now < endTime)
                {
                    if (_port.BytesToRead > 0)
                    {
                        var ch = (char)_port.ReadChar();
                        if (ch == '\r' || ch == '\n')
                        {
                            var line = sb.ToString().Trim();
                            sb.Clear();

                            if (string.IsNullOrEmpty(line) ||
                                line.Contains("SEARCHING") ||
                                line.Contains("ATMA") ||
                                line.Contains("STOPPED"))
                            {
                                continue;
                            }

                            var msg = ParseCanMessage(line);
                            if (msg != null)
                            {
                                // Got a frame — exit monitor mode and return it
                                ExitMonitorMode();
                                return msg;
                            }
                        }
                        else if (ch != '\0' && ch != '>')
                        {
                            sb.Append(ch);
                        }
                    }
                    else
                    {
                        Thread.Sleep(5);
                    }
                }

                // Timeout — exit monitor mode
                ExitMonitorMode();
                return null;
            }
            catch (Exception ex)
            {
                Log.WriteLine($"Error in raw CAN receive: {ex.Message}");
                try { ExitMonitorMode(); } catch { }
                return null;
            }
        }

        /// <summary>
        /// Exit ATMA monitor mode by sending a character and waiting for prompt.
        /// </summary>
        private void ExitMonitorMode()
        {
            _port.Write("\r");
            // Wait for '>' prompt with short timeout
            var endTime = DateTime.Now.AddMilliseconds(500);
            while (DateTime.Now < endTime)
            {
                if (_port.BytesToRead > 0)
                {
                    var ch = (char)_port.ReadChar();
                    if (ch == '>')
                    {
                        break;
                    }
                }
                else
                {
                    Thread.Sleep(5);
                }
            }
            _port.DiscardInBuffer();
        }

        /// <summary>
        /// Parse response lines into CAN messages and add them to the frame buffer.
        /// Skips non-frame lines like "NO DATA", "OK", "SEARCHING", etc.
        /// </summary>
        internal void BufferReceivedFrames(List<string> lines)
        {
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) ||
                    line.Contains("NO DATA") ||
                    line.Contains("OK") ||
                    line.Contains("SEARCHING") ||
                    line.Contains("STOPPED") ||
                    line.Contains("?"))
                {
                    continue;
                }

                var msg = ParseCanMessage(line);
                if (msg != null)
                {
                    _frameBuffer.Enqueue(msg);
                    Log.WriteLine($"RX buffered: {msg}");
                }
            }
        }

        /// <summary>
        /// Number of CAN frames currently in the receive buffer.
        /// </summary>
        public int BufferedFrameCount => _frameBuffer.Count;

        /// <summary>
        /// Parse a CAN message from string response.
        /// Supports both space-separated ("7E8 06 41 00 BE 3E B8 13")
        /// and compact/no-space ("7E8064100BE3EB813") formats.
        /// </summary>
        internal static CanMessage? ParseCanMessage(string response)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(response))
                {
                    return null;
                }

                // Check if response contains spaces (ATS1 mode) or is compact (ATS0 mode)
                if (response.Contains(' '))
                {
                    return ParseSpaceSeparated(response);
                }
                else
                {
                    return ParseCompact(response);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine($"Error parsing CAN message '{response}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parse space-separated format: "7E8 06 41 00 BE 3E B8 13"
        /// </summary>
        private static CanMessage? ParseSpaceSeparated(string response)
        {
            var parts = response.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 1)
            {
                return null;
            }

            var idStr = parts[0];
            var isExtended = idStr.Length > 3;
            var id = Convert.ToUInt32(idStr, 16);

            var data = new List<byte>();
            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i].Length == 2)
                {
                    data.Add(Convert.ToByte(parts[i], 16));
                }
            }

            return new CanMessage(id, data.ToArray(), isExtended);
        }

        /// <summary>
        /// Parse compact/no-space format: "7E8064100BE3EB813"
        /// First 3 chars = standard ID (or 8 chars for extended ID), rest = hex data bytes
        /// </summary>
        private static CanMessage? ParseCompact(string response)
        {
            // Determine ID length: if all first 8 chars are hex and total length > 3+2,
            // we need a heuristic. Standard ELM327 uses 3 hex chars for 11-bit IDs.
            // Extended IDs use 8 hex chars.
            // Heuristic: if length is odd after removing 3-char ID, try 8-char ID.
            int idLength;
            if (response.Length > 8 && (response.Length - 8) % 2 == 0)
            {
                // Could be extended ID (8 hex chars) - check if 3-char ID also works
                if ((response.Length - 3) % 2 == 0)
                {
                    // Both could work; prefer standard 3-char ID for 11-bit CAN
                    idLength = 3;
                }
                else
                {
                    idLength = 8;
                }
            }
            else if (response.Length >= 3 && (response.Length - 3) % 2 == 0)
            {
                idLength = 3;
            }
            else if (response.Length >= 8 && (response.Length - 8) % 2 == 0)
            {
                idLength = 8;
            }
            else
            {
                return null;
            }

            var idStr = response[..idLength];
            var isExtended = idLength > 3;
            var id = Convert.ToUInt32(idStr, 16);

            var dataHex = response[idLength..];
            var data = new List<byte>();
            for (int i = 0; i < dataHex.Length; i += 2)
            {
                data.Add(Convert.ToByte(dataHex.Substring(i, 2), 16));
            }

            return new CanMessage(id, data.ToArray(), isExtended);
        }

        /// <summary>
        /// Set the ELM327 response timeout (ATST).
        /// Timeout in CAN mode ≈ value × 4.096ms.
        /// Example: 0x32 (50) → ~200ms, 0xFF (255) → ~1044ms.
        /// </summary>
        public bool SetResponseTimeout(byte value)
        {
            return SendCommand($"ATST{value:X2}");
        }

        /// <summary>
        /// Clear receive buffer (both serial port and frame buffer).
        /// </summary>
        public void ClearReceiveBuffer()
        {
            lock (_lock)
            {
                _port.DiscardInBuffer();
                _frameBuffer.Clear();
            }
        }
    }
}
