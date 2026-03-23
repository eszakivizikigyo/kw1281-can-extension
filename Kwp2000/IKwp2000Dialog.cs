using Service = BitFab.KW1281Test.Kwp2000.DiagnosticService;

namespace BitFab.KW1281Test.Kwp2000;

/// <summary>
/// Abstraction for KWP2000 diagnostic communication, independent of transport layer.
/// Implementations: KW2000Dialog (K-line), Kwp2000CanDialog (CAN via TP 2.0).
/// </summary>
internal interface IKwp2000Dialog
{
    Kwp2000Message SendReceive(Service service, byte[] body, bool excludeAddresses = false);

    void StartDiagnosticSession(byte v1, byte v2);

    void EcuReset(byte value);

    byte[] ReadMemoryByAddress(uint address, byte count);

    byte[] WriteMemoryByAddress(uint address, byte count, byte[] data);

    void DumpMem(uint address, uint length, string dumpFileName);
}
