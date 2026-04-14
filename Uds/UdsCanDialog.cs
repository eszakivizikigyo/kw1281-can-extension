using BitFab.KW1281Test.Interface;
using BitFab.KW1281Test.Kwp2000;
using System;
using System.Collections.Generic;

namespace BitFab.KW1281Test.Uds;

/// <summary>
/// UDS (ISO 14229) diagnostic dialog over CAN bus.
/// Works with any ICanTransport: VW TP 2.0 (Tp20Channel) or ISO-TP (ElmIsoTpTransport).
/// </summary>
internal class UdsCanDialog : IDisposable
{
    private readonly ICanTransport _transport;

    public UdsCanDialog(ICanTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));

        if (!_transport.IsOpen)
        {
            throw new InvalidOperationException("Transport must be open");
        }
    }

    /// <summary>
    /// Send a UDS request and receive the positive response.
    /// Handles 0x7F negative responses with ResponsePending (0x78) retry.
    /// </summary>
    public byte[] SendReceive(UdsService service, byte[] data)
    {
        var payload = new byte[1 + data.Length];
        payload[0] = (byte)service;
        Array.Copy(data, 0, payload, 1, data.Length);

        if (!_transport.SendData(payload))
        {
            throw new InvalidOperationException("Failed to send UDS message");
        }

        while (true)
        {
            var response = _transport.ReceiveData();
            if (response == null || response.Length < 1)
            {
                throw new InvalidOperationException("No response received");
            }

            var responseSid = response[0];

            // Negative response
            if (responseSid == 0x7F && response.Length >= 3)
            {
                var requestedService = (UdsService)response[1];
                var nrc = (UdsNrc)response[2];

                Log.WriteLine($"UDS Negative: {requestedService} → {nrc}");

                if (nrc == UdsNrc.RequestCorrectlyReceivedResponsePending)
                {
                    continue; // Wait for actual response
                }

                throw new NegativeUdsResponseException(requestedService, nrc);
            }

            // Positive response: SID + 0x40
            var expectedPositive = (byte)((byte)service + 0x40);
            if (responseSid != expectedPositive)
            {
                throw new InvalidOperationException(
                    $"Unexpected UDS response SID: 0x{responseSid:X2} (expected 0x{expectedPositive:X2})");
            }

            // Return body (everything after the positive response SID)
            var body = new byte[response.Length - 1];
            Array.Copy(response, 1, body, 0, body.Length);

            Log.WriteLine($"UDS {service} → positive ({body.Length} bytes)");
            return body;
        }
    }

    public byte[] DiagnosticSessionControl(byte sessionType)
    {
        return SendReceive(UdsService.DiagnosticSessionControl, new[] { sessionType });
    }

    public void ECUReset(byte resetType)
    {
        SendReceive(UdsService.ECUReset, new[] { resetType });
    }

    public void TesterPresent()
    {
        SendReceive(UdsService.TesterPresent, new byte[] { 0x00 });
    }

    public byte[] ReadDataByIdentifier(ushort did)
    {
        return SendReceive(UdsService.ReadDataByIdentifier,
            new[] { (byte)(did >> 8), (byte)(did & 0xFF) });
    }

    public void WriteDataByIdentifier(ushort did, byte[] data)
    {
        var request = new List<byte>
        {
            (byte)(did >> 8),
            (byte)(did & 0xFF)
        };
        request.AddRange(data);
        SendReceive(UdsService.WriteDataByIdentifier, request.ToArray());
    }

    public byte[] SecurityAccess(byte accessType, byte[] securityData)
    {
        var request = new List<byte> { accessType };
        request.AddRange(securityData);
        return SendReceive(UdsService.SecurityAccess, request.ToArray());
    }

    public byte[] ReadDTCInformation(byte subFunction, byte[] data)
    {
        var request = new List<byte> { subFunction };
        request.AddRange(data);
        return SendReceive(UdsService.ReadDTCInformation, request.ToArray());
    }

    public void ClearDiagnosticInformation(uint groupOfDtc)
    {
        SendReceive(UdsService.ClearDiagnosticInformation, new[]
        {
            (byte)((groupOfDtc >> 16) & 0xFF),
            (byte)((groupOfDtc >> 8) & 0xFF),
            (byte)(groupOfDtc & 0xFF)
        });
    }

    public byte[] ReadMemoryByAddress(uint address, uint length)
    {
        // addressAndLengthFormatIdentifier: high nibble = length size, low nibble = address size
        byte addressSize = GetByteSize(address);
        byte lengthSize = GetByteSize(length);
        byte formatId = (byte)((lengthSize << 4) | addressSize);

        var request = new List<byte> { formatId };
        AddBigEndian(request, address, addressSize);
        AddBigEndian(request, length, lengthSize);

        return SendReceive(UdsService.ReadMemoryByAddress, request.ToArray());
    }

    public void WriteMemoryByAddress(uint address, byte[] data)
    {
        byte addressSize = GetByteSize(address);
        byte lengthSize = GetByteSize((uint)data.Length);
        byte formatId = (byte)((lengthSize << 4) | addressSize);

        var request = new List<byte> { formatId };
        AddBigEndian(request, address, addressSize);
        AddBigEndian(request, (uint)data.Length, lengthSize);
        request.AddRange(data);

        SendReceive(UdsService.WriteMemoryByAddress, request.ToArray());
    }

    public byte[] RoutineControl(byte subFunction, ushort routineId, byte[] routineData)
    {
        var request = new List<byte>
        {
            subFunction,
            (byte)(routineId >> 8),
            (byte)(routineId & 0xFF)
        };
        request.AddRange(routineData);
        return SendReceive(UdsService.RoutineControl, request.ToArray());
    }

    public void Dispose()
    {
        _transport.Dispose();
    }

    internal static byte GetByteSize(uint value)
    {
        if (value <= 0xFF) return 1;
        if (value <= 0xFFFF) return 2;
        if (value <= 0xFFFFFF) return 3;
        return 4;
    }

    private static void AddBigEndian(List<byte> list, uint value, byte size)
    {
        for (int i = size - 1; i >= 0; i--)
        {
            list.Add((byte)((value >> (i * 8)) & 0xFF));
        }
    }
}
