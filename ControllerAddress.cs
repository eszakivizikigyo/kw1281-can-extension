using System;

namespace BitFab.KW1281Test
{
    /// <summary>
    /// VW controller addresses
    /// </summary>
    enum ControllerAddress
    {
        Ecu = 0x01,              // J623 Engine
        Transmission = 0x02,     // J743 DSG DQ500
        AbsBrakes = 0x03,        // ESP MK25A XT
        SteeringAngle = 0x04,    // Steering Angle Sensor
        AutoHvac = 0x08,         // J301 Climatic
        CentralElectric = 0x09,  // J519 BCM
        SecAirHeating = 0x0B,    // J604 Secondary Air Heating
        Airbags = 0x15,          // J234
        Cluster = 0x17,          // J285 Instrument Cluster
        AuxHeat = 0x18,          // J737 Standheizung
        CanGateway = 0x19,       // J533 (integrated in BCM)
        Immobilizer = 0x25,      // J334
        CentralLocking = 0x35,
        Navigation = 0x37,       // J794 RNS315
        CCM = 0x46,
        Radio = 0x56,
        Trailer = 0x69,          // Vonóhorog
        CtrlHeadRoof = 0x6E,     // J702 Dachdisplay
        RadioManufacturing = 0x7C,
    }

    internal static class ControllerAddressExtensions
    {
        public static string GetControllerName(byte address)
        {
            if (Enum.IsDefined(typeof(ControllerAddress), (int)address))
            {
                return ((ControllerAddress)address).ToString();
            }
            return $"Module 0x{address:X2}";
        }
    }
}
