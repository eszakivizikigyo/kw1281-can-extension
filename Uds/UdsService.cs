namespace BitFab.KW1281Test.Uds;

/// <summary>
/// UDS (ISO 14229) service identifiers.
/// </summary>
internal enum UdsService : byte
{
    DiagnosticSessionControl = 0x10,
    ECUReset = 0x11,
    ClearDiagnosticInformation = 0x14,
    ReadDTCInformation = 0x19,
    ReadDataByIdentifier = 0x22,
    ReadMemoryByAddress = 0x23,
    SecurityAccess = 0x27,
    CommunicationControl = 0x28,
    WriteDataByIdentifier = 0x2E,
    RoutineControl = 0x31,
    RequestDownload = 0x34,
    RequestUpload = 0x35,
    TransferData = 0x36,
    RequestTransferExit = 0x37,
    WriteMemoryByAddress = 0x3D,
    TesterPresent = 0x3E,
    ControlDTCSetting = 0x85,
}
