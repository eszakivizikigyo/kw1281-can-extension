using System;

namespace BitFab.KW1281Test.Interface;

/// <summary>
/// Abstraction for CAN-based diagnostic transport.
/// Implemented by Tp20Channel (VW TP 2.0) and ElmIsoTpTransport (ISO 15765).
/// </summary>
internal interface ICanTransport : IDisposable
{
    bool IsOpen { get; }

    bool SendData(byte[] data);

    byte[]? ReceiveData();
}
