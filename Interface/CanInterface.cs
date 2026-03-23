using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace BitFab.KW1281Test.Interface
{
    /// <summary>
    /// CAN interface implementation for HEX-V2 and compatible adapters
    /// Uses AT command protocol similar to ELM327
    /// </summary>
    internal class CanInterface : IDisposable
    {
        private readonly SerialPort _port;
        private readonly object _lock = new object();
        private bool _isInitialized = false;

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
                BaudRate = baudRate, // HEX-V2 typically uses 115200 for USB
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
        /// Initialize the CAN interface with basic settings
        /// </summary>
        public bool Initialize()
        {
            lock (_lock)
            {
                try
                {
                    Log.WriteLine("Initializing CAN interface...");

                    // Reset device
                    if (!SendCommand("ATZ"))
                    {
                        Log.WriteLine("Failed to reset device");
                        return false;
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
        /// Set CAN bus speed
        /// </summary>
        /// <param name="speed">Speed in kbps (e.g., 500 for 500kbps)</param>
        public bool SetCanSpeed(int speed)
        {
            string command = speed switch
            {
                500 => "ATSP6", // CAN 500kbps, 11-bit ID
                250 => "ATSP7", // CAN 250kbps, 11-bit ID
                _ => throw new ArgumentException($"Unsupported CAN speed: {speed}kbps")
            };

            return SendCommand(command);
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
        /// Send an AT command and wait for OK response
        /// </summary>
        private bool SendCommand(string command)
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
        /// Read response from the interface until prompt character '>'
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
        /// Send a CAN message
        /// </summary>
        public bool SendCanMessage(CanMessage message)
        {
            lock (_lock)
            {
                try
                {
                    // Format: [ID] [Data bytes]
                    // Example: 7DF 02 01 00
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
        /// Receive a CAN message
        /// </summary>
        public CanMessage? ReceiveCanMessage(int timeoutMs = 1000)
        {
            lock (_lock)
            {
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
        /// Clear receive buffer
        /// </summary>
        public void ClearReceiveBuffer()
        {
            lock (_lock)
            {
                _port.DiscardInBuffer();
            }
        }
    }
}
