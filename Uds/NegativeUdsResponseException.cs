using System;

namespace BitFab.KW1281Test.Uds;

/// <summary>
/// Thrown when an ECU returns a UDS negative response (service 0x7F).
/// </summary>
internal class NegativeUdsResponseException : Exception
{
    public UdsService RequestedService { get; }
    public UdsNrc Nrc { get; }

    public NegativeUdsResponseException(UdsService requestedService, UdsNrc nrc)
        : base($"Negative UDS response: {requestedService} → {nrc} (0x{(byte)nrc:X2})")
    {
        RequestedService = requestedService;
        Nrc = nrc;
    }
}
