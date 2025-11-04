# OPEN-Vag
OPEN VAG a GUI version of kw1281test
OPEN VAG is a modern, clean, and professional diagnostic tool for K-Line (KW1281) vehicles. It wraps the powerful kw1281test CLI tool in a dark-mode GUI with:

Live output streaming
One-click presets
EEPROM/ROM dumping
Fault code reading & clearing
No log files
No CMD windows
Single .exe file
System Requirements
Quick Start
Download OPEN_VAG.exe
Connect your KKL cable to:
OBD2 port (under dash)
USB port on PC
Ignition ON (engine OFF)
Run OPEN_VAG.exe
Select your module → Click a command
Interface Guide
1. Connection Tab
Field | Description -- | -- COM Port | Auto-detected. Click Refresh if missing. Baud Rate | Auto-set by preset (e.g., 10400) Address | Module address (e.g., 17 = Cluster) Presets | One-click setup: Cluster, ECU, CCM, Radio Stop | Cancel long operations (dumps, scans)
Item | Requirement -- | -- OS | Windows 7, 8, 10, 11 Cable | KKL USB (FTDI/CH340) or VAG-COM Python | Not needed (bundled) .NET | Bundled
Quick Start
Download OPEN_VAG.exe
Connect your KKL cable to:

OBD2 port (under dash)
USB port on PC

Ignition ON (engine OFF)
Run OPEN_VAG.exe
Select your module → Click a command

Interface Guide

Connection Tab
FieldDescriptionCOM PortAuto-detected. Click Refresh if missing.Baud RateAuto-set by preset (e.g., 10400)AddressModule address (e.g., 17 = Cluster)PresetsOne-click setup: Cluster, ECU, CCM, RadioStopCancel long operations (dumps, scans)
2. Command Tabs

TabCommon UsesBasicRead Ident, Fault Codes, Clear DTCsAdaptationChannel read/save (e.g., mileage, keys)GroupLive data (Group Read)EEPROMRead/Write single bytesMemoryDump EEPROM/ROM/RAM (save as .bin)RadioGet SAFE code, toggle RB4MiscCustom commands, coding

Common Tasks
Read Fault Codes

Select ECU preset
Go to Basic tab
Click Read Fault Codes

Clear Fault Codes

After reading, click Clear Fault Codes
Turn ignition OFF → ON to verify

Dump Instrument Cluster EEPROM

Select Cluster preset
Go to Memory tab
Click Dump EEPROM
Enter: 0 → 2048 → Choose save location
Wait ~30 seconds

Get Radio SAFE Code

Select Radio preset
Go to Radio tab
Click Delco Premium 5 SAFE Code (or Clarion)

Advanced: Custom Command

Go to Misc tab
Click Custom Command
Enter: ReadEeprom 0x1109 → OK

Use any kw1281test command:
Full CLI Reference

Troubleshooting

ProblemFix"Cannot find kw1281test.exe"Rebuild with --add-data "kw1281test.exe;."No COM portsInstall CH340/FTDI drivers"FormatException: COM1"Use quoted port (fixed in v1.1+)No responseCheck ignition ON, correct module addressDumps failTry lower baud (9600) or different cable
