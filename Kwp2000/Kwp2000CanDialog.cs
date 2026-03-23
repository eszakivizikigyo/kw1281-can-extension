using BitFab.KW1281Test.Interface;
using System;
using System.Collections.Generic;
using System.IO;
using Service = BitFab.KW1281Test.Kwp2000.DiagnosticService;

namespace BitFab.KW1281Test.Kwp2000;

/// <summary>
/// KWP2000 diagnostic dialog over CAN bus using VW TP 2.0 transport.
/// Over CAN, KWP2000 messages are just [service_byte, ...body] without
/// the K-line framing (format byte, addresses, checksum).
/// </summary>
internal class Kwp2000CanDialog : IKwp2000Dialog, IDisposable
{
    private readonly Tp20Channel _channel;

    public Kwp2000CanDialog(Tp20Channel channel)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));

        if (!_channel.IsOpen)
        {
            throw new InvalidOperationException("TP 2.0 channel must be open");
        }
    }

    public Kwp2000Message SendReceive(
        Service service, byte[] body, bool excludeAddresses = false)
    {
        // Build raw KWP2000 payload: [service, ...body]
        var payload = new byte[1 + body.Length];
        payload[0] = (byte)service;
        Array.Copy(body, 0, payload, 1, body.Length);

        if (!_channel.SendData(payload))
        {
            throw new InvalidOperationException("Failed to send KWP2000 message over TP 2.0");
        }

        while (true)
        {
            var response = _channel.ReceiveData();
            if (response == null || response.Length < 1)
            {
                throw new InvalidOperationException("No response received over TP 2.0");
            }

            var responseService = (Service)response[0];
            var responseBody = new List<byte>();
            for (int i = 1; i < response.Length; i++)
            {
                responseBody.Add(response[i]);
            }

            // Create Kwp2000Message without addresses (CAN doesn't use them in the KWP2000 layer)
            var message = new Kwp2000Message(responseService, responseBody);

            Log.WriteLine($"CAN Received: {message.DescribeService()}");

            // Handle negative response
            if ((byte)message.Service == 0x7F)
            {
                if (responseBody.Count >= 2 &&
                    responseBody[0] == (byte)service &&
                    responseBody[1] == (byte)ResponseCode.reqCorrectlyRcvdRspPending)
                {
                    continue; // Wait for actual response
                }
                throw new NegativeResponseException(message);
            }

            if (!message.IsPositiveResponse(service))
            {
                throw new InvalidOperationException($"Unexpected response: {message.Service}");
            }

            return message;
        }
    }

    public void StartDiagnosticSession(byte v1, byte v2)
    {
        var responseMessage = SendReceive(Service.startDiagnosticSession, new[] { v1, v2 });
        if (responseMessage.Body[0] != v1)
        {
            throw new InvalidOperationException(
                $"Unexpected diagnosticMode: {responseMessage.Body[0]:X2}");
        }
    }

    public void EcuReset(byte value)
    {
        SendReceive(Service.ecuReset, new[] { value });
    }

    public byte[] ReadMemoryByAddress(uint address, byte count)
    {
        var addressBytes = Utils.GetBytes(address);
        var responseMessage = SendReceive(Service.readMemoryByAddress,
            new byte[]
            {
                addressBytes[2], addressBytes[1], addressBytes[0],
                count
            });
        return responseMessage.Body.ToArray();
    }

    public byte[] WriteMemoryByAddress(uint address, byte count, byte[] data)
    {
        var addressBytes = Utils.GetBytes(address);
        var messageBytes = new List<byte>
        {
            addressBytes[2], addressBytes[1], addressBytes[0],
            count
        };
        messageBytes.AddRange(data);
        var responseMessage = SendReceive(Service.writeMemoryByAddress, messageBytes.ToArray());
        return responseMessage.Body.ToArray();
    }

    public void DumpMem(uint address, uint length, string dumpFileName)
    {
        StartDiagnosticSession(0x84, 0x14);

        // Keep-alive to maintain TP 2.0 channel during session switch
        _channel.SendKeepAlive();

        Log.WriteLine($"Saving memory dump to {dumpFileName}");
        DumpMemory(address, length, maxReadLength: 32, dumpFileName);
        Log.WriteLine($"Saved memory dump to {dumpFileName}");

        EcuReset(0x01);
    }

    private void DumpMemory(
        uint startAddr, uint length, byte maxReadLength, string fileName)
    {
        using var fs = File.Create(fileName, maxReadLength, FileOptions.WriteThrough);
        for (uint addr = startAddr; addr < (startAddr + length); addr += maxReadLength)
        {
            var readLength = (byte)Math.Min(startAddr + length - addr, maxReadLength);
            try
            {
                var blockBytes = ReadMemoryByAddress(addr, readLength);
                fs.Write(blockBytes, 0, blockBytes.Length);

                if (blockBytes.Length != readLength)
                {
                    throw new InvalidOperationException(
                        $"Expected {readLength} bytes from ReadMemoryByAddress() but received {blockBytes.Length} bytes");
                }
            }
            catch (NegativeResponseException)
            {
                Log.WriteLine("Failed to read memory.");
            }
            finally
            {
                fs.Flush();
            }
        }
    }

    public void Dispose()
    {
        _channel.Dispose();
    }
}
