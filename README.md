# kw1281test
VW KW1281 Protocol Test Tool

This tool can send some KW1281 (and a few KW2000) commands over a dumb serial->KKL or USB->KKL cable.
It also supports CAN bus diagnostics (KWP2000/UDS) via ELM327-compatible interfaces.
If you have a legacy Ross-Tech USB cable, you can probably use that cable by
installing the Virtual COM Port drivers: https://www.ross-tech.com/vag-com/usb/virtual-com-port.php
Functionality includes reading/writing the EEPROMs of VW MKIV Golf/Jetta/Beetle/Passat instrument clusters and Comfort Control Modules, reading and clearing fault codes, changing the software coding of modules, performing an actuator test of various modules and retrieving the SAFE code of the Delco Premium V radio.

The tool is available in two variants:
- **CLI** — Command-line interface (original)
- **GUI** — Cross-platform graphical interface (Avalonia UI) for Windows, macOS and Linux

The tool is written in C#, targetting .NET 9.0 and runs under Windows 10/11 (most serial ports), macOS and Linux (macOS/Linux need an FTDI serial port and D2xx drivers). It may also run under
Windows 7.

You can download a precompiled version for Windows, macOS and Linux (x64) from the Releases page: https://github.com/gmenounos/kw1281test/releases/

Otherwise, here's how to build it yourself:

##### Project structure

```
kw1281test.slnx
├── kw1281test/          ← Shared library (protocol, interfaces, clusters)
├── Cli/                 ← CLI frontend
├── Ui/                  ← GUI frontend (Avalonia UI)
├── Tests/               ← Library unit tests
└── Tests.Ui/            ← UI unit tests
```

##### Compiling the tool

1. You will need the .NET Core SDK,
which you can find here: https://dotnet.microsoft.com/download
(Click on the "Download .NET Core SDK" link and follow the instructions) or Microsoft Visual Studio
(free Community Edition here: https://visualstudio.microsoft.com/vs/community/)

2. Download the source code: https://github.com/gmenounos/kw1281test/archive/master.zip
and unzip it into a folder on your computer.

3. Open up a command prompt on your computer and go into the folder where you unzipped
the source code. Type `dotnet build kw1281test.slnx` to build all projects.
Or, load up the project in Visual Studio and Ctrl-Shift-B.

4. You can run the CLI by typing `dotnet run --project Cli`

5. You can run the GUI by typing `dotnet run --project Ui`

6. To run all tests: `dotnet test kw1281test.slnx`

##### CLI usage

```
Usage: KW1281Test PORT BAUD ADDRESS COMMAND [args]

PORT = COM1|COM2|etc. (Windows)
        /dev/ttyXXXX (Linux)
        AABBCCDD (macOS/Linux FTDI cable serial number)
BAUD = 10400|9600|etc.
ADDRESS = Controller address, e.g. 1 (ECU), 17 (cluster), 46 (CCM), 56 (radio)
COMMAND =
    ActuatorTest
    AdaptationRead CHANNEL [LOGIN]
        CHANNEL = Channel number (0-99)
        LOGIN = Optional login (0-65535)
    AdaptationSave CHANNEL VALUE [LOGIN]
        CHANNEL = Channel number (0-99)
        VALUE = Channel value (0-65535)
        LOGIN = Optional login (0-65535)
    AdaptationTest CHANNEL VALUE [LOGIN]
        CHANNEL = Channel number (0-99)
        VALUE = Channel value (0-65535)
        LOGIN = Optional login (0-65535)
    AutoScan
    BasicSetting GROUP
        GROUP = Group number (0-255)
        (Group 0: Raw controller data)
    ClarionVWPremium4SafeCode
    ClearFaultCodes
    DelcoVWPremium5SafeCode
    DumpEdc15Eeprom [FILENAME]
        FILENAME = Optional filename
    DumpEeprom START LENGTH [FILENAME]
        START = Start address in decimal (e.g. 0) or hex (e.g. 0x0)
        LENGTH = Number of bytes in decimal (e.g. 2048) or hex (e.g. 0x800)
        FILENAME = Optional filename
    DumpMarelliMem START LENGTH [FILENAME]
        START = Start address in decimal (e.g. 3072) or hex (e.g. 0xC00)
        LENGTH = Number of bytes in decimal (e.g. 1024) or hex (e.g. 0x400)
        FILENAME = Optional filename
    DumpMem START LENGTH [FILENAME]
        START = Start address in decimal (e.g. 8192) or hex (e.g. 0x2000)
        LENGTH = Number of bytes in decimal (e.g. 65536) or hex (e.g. 0x10000)
        FILENAME = Optional filename
    DumpRBxMem START LENGTH [FILENAME]
        START = Start address in decimal (e.g. 66560) or hex (e.g. 0x10400)
        LENGTH = Number of bytes in decimal (e.g. 1024) or hex (e.g. 0x400)
        FILENAME = Optional filename
    DumpRom START LENGTH [FILENAME]
        START = Start address in decimal (e.g. 8192) or hex (e.g. 0x2000)
        LENGTH = Number of bytes in decimal (e.g. 65536) or hex (e.g. 0x10000)
    GetSKC
    GroupRead GROUP
        GROUP = Group number (0-255)
        (Group 0: Raw controller data)
    LoadEeprom START FILENAME
        START = Start address in decimal (e.g. 0) or hex (e.g. 0x0)
        FILENAME = Name of file containing binary data to load into EEPROM
    MapEeprom
    ReadFaultCodes
    ReadIdent
    ReadEeprom ADDRESS
        ADDRESS = Address in decimal (e.g. 4361) or hex (e.g. 0x1109)
    ReadRAM ADDRESS
        ADDRESS = Address in decimal (e.g. 4361) or hex (e.g. 0x1109)
    ReadROM ADDRESS
        ADDRESS = Address in decimal (e.g. 4361) or hex (e.g. 0x1109)
    ReadSoftwareVersion
    Reset
    SetSoftwareCoding CODING WORKSHOP
        CODING = Software coding in decimal (e.g. 4361) or hex (e.g. 0x1109)
        WORKSHOP = Workshop code in decimal (e.g. 4361) or hex (e.g. 0x1109)
    ToggleRB4Mode
    WriteEdc15Eeprom ADDRESS1 VALUE1 [ADDRESS2 VALUE2 ... ADDRESSn VALUEn]
        ADDRESS = EEPROM address in decimal (0-511) or hex (0x00-0x1FF)
    VALUE = Value to be stored in decimal (0-255) or hex (0x00-0xFF)
    WriteEeprom ADDRESS VALUE
        ADDRESS = Address in decimal (e.g. 4361) or hex (e.g. 0x1109)
        VALUE = Value in decimal (e.g. 138) or hex (e.g. 0x8A)
```

##### GUI usage

The graphical interface provides the same functionality as the CLI in an interactive window:

- **K-Line diagnostics**: Fault codes, measuring groups, adaptation, actuator tests, coding, EEPROM, memory, cluster operations
- **CAN bus diagnostics**: CAN monitor, auto scan, KWP2000/UDS diagnostics, multi-ECU dashboard
- **Features**: Light/dark theme, settings persistence, log filtering/coloring, confirmation dialogs for destructive operations, hex viewer

To launch: `dotnet run --project Ui`

To publish for distribution:
```
.\Publish.ps1
```

##### Credits
- Protocol Info: https://www.blafusel.de/obd/obd2_kw1281.html  
- VW Radio Reverse Engineering Info: https://github.com/mnaberez/vwradio  
- 6502bench SourceGen: https://6502bench.com/
- EDC15 flashing info and seed/key algorithm: https://github.com/fjvva/ecu-tool
- Contributions
    - [IJskonijn](https://github.com/IJskonijn)
    - [jpadie](https://github.com/jpadie)
    - [kerekt](https://github.com/kerekt)
    - [Olivier Fauchon](https://github.com/ofauchon)
    - [Jonathan Klamroth](https://github.com/jonnykl)
    - [Martin Sestak](https://github.com/poure-1)