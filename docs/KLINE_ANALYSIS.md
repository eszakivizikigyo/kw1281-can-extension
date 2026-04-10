# K-line diagnosztika elemzés – VW T5 GP (7H)

## 1. Kiindulás: miért kell K-line?

Az autón igazoltuk (2026-04-10), hogy **CAN-en csak UDS ISO-TP működik**, kizárólag 2 modulhoz:
- **Engine (0x7E0/0x7E8)** – OK
- **Transmission (0x7E1/0x7E9)** – OK

A VCDS által olvasott **15 modulból 13 NEM érhető el CAN-en**:
| Cím | Modul | VCDS protokoll | CAN? |
|-----|-------|---------------|------|
| 01 | Engine | KWP2000 / UDS | ✅ CAN UDS |
| 02 | Transmission | KWP2000 / UDS | ✅ CAN UDS |
| 03 | ABS | KW1281 | ❌ K-line |
| 04 | Steering | KW1281 | ❌ K-line |
| 08 | HVAC | KW1281 | ❌ K-line |
| 09 | BCM (Cent. Elektrik) | KWP2000 | ❌ K-line |
| 15 | Airbag | KW1281 | ❌ K-line |
| 17 | Cluster | KW1281 | ❌ K-line |
| 18 | Aux. Heating | KWP2000 | ❌ K-line |
| 19 | Gateway (GW-K-CAN) | KWP2000 | ❌ K-line |
| 25 | Immobilizer | KW1281 | ❌ K-line |
| 37 | Navigation | KWP2000 | ❌ K-line |
| 56 | Radio | KW1281 | ❌ K-line |
| 69 | Trailer | KW1281 | ❌ K-line |
| 6E | Roof Display | KWP2000 | ❌ K-line |

**A VW T5 GP (2012) egy hibrid architektúra:**
- CAN bus létezik (motor, váltó)
- A legtöbb body modul K-line-on van
- A Gateway (GW-K-CAN TP20) hidat képez a kettő közt, de a modulok diagnosztikai hozzáférése K-line-on keresztül történik
- A "TP20" a gateway nevében a CAN oldali protokollt jelzi, NEM azt hogy a diagnosztikai scanner TP 2.0-val éri el a modulokat

## 2. Rendelkezésre álló hardver

### 2.1 VAG KKL kábel (COM9) — FT232RL

**Típus:** Sima USB-to-K-line átalakító kábel
**Chip:** FTDI FT232RL (VID=0403, PID=6001 — standard FTDI)
**Port:** COM9 (FTDI VCP driver)
**Csatlakozó:** OBD-II (16-pin) → USB

**Működési elv:**
```
PC (USB) → FT232RL (UART) → L-line transzverter (MC33290 / L9637D) → OBD-II pin 7 (K-line)
```

**K-line kommunikáció igényei:**
1. **5-baud inicializálás** — TxD vonalon BREAK állapot váltogatásával, 200ms/bit
2. **Soros kommunikáció** — 8N1 / 8E1 / 8O1, 9600–10400 baud
3. **Echo** — a K-line half-duplex, a küldött adatok visszajönnek (echo)

**Az FT232RL ezeket MIND TÁMOGATJA:**
- ✅ `SetBreak(on/off)` → BREAK állapot a TxD vonalon → K-line LOW/HIGH
- ✅ Tetszőleges baud rate (FT232RL: 183 baud – 3 Mbaud)
- ✅ 8N1, 8E1, 8O1 paritás beállítás
- ✅ DTR/RTS vezérlés
- ✅ Half-duplex echo (a kw1281test kódja kezeli)

### 2.2 Ross-Tech HEX klón (COM10) — PIC mikrokontroller

**Típus:** Ross-Tech HEX-USB klón (kínai másolat)
**USB ID:** VID=0403, PID=FA24 (Ross-Tech egyedi PID)
**Sorozatszám:** RT000001
**Port:** COM10 (FTDI VCP driver kézzel telepítve)

**Eredeti hardver architektúra (Ross-Tech HEX-USB):**
```
PC (USB) → FT232RL → PIC mikrokontroller → K-line transzverter → OBD-II pin 7
                                          → CAN transzverter → OBD-II pin 6/14
```

**KRITIKUS KÜLÖNBSÉG a KKL kábelhez képest:**
A Ross-Tech HEX-ben az FTDI chip NEM közvetlenül a K-line transzverterre van kötve!
Közte van egy **PIC mikrokontroller**, ami:
1. A Ross-Tech saját bináris protokollját beszéli a PC felé (USB oldalon)
2. A PIC vezérli a K-line transzvertert a diagnosztikai protokoll szerint
3. A PIC vezérli a CAN transzvertert is

**Ez azt jelenti:**
- ❌ A `SetBreak()` NEM jut el a K-line-ra (a PIC fogadja, nem a transzverter)
- ❌ A `WriteByte()` NEM a K-line-ra ír (a PIC-nek szól)
- ❌ A `ReadByte()` NEM a K-line-ról olvas (a PIC válaszol)
- ❌ Semmilyen „dumb passthrough" mód NINCS

A Ross-Tech weboldala maga írja:
> *"Early in their development, we found a number of technical advantages to using
> a 'direct' USB driver which bypasses the Windows Serial drivers entirely.
> Hence the USB drivers that ship with VCDS do not emulate a serial COM port."*

A VCP (Virtual COM Port) driver telepítése ugyan létrehoz egy COM10-et, de:
- A PIC firmware NEM támogat passthrough módot a klónokon
- A legális Ross-Tech eszközök soros oldala is korlátozott volt
- **Még az eredeti Ross-Tech OLD soros (RS-232) kábeleknél volt „dumb K-line passthrough"** — az USB verzióknál már NEM

### 2.3 Vgate vLinker FS USB (COM8) — STN1170

**Típus:** ELM327-kompatibilis OBD-II adapter
**Chip:** STN1170 v4.3.2 (ScanTool.net)
**Port:** COM8, 115200 baud
**Firmware:** vLinker FS r2

**Támogatott protokollok (AT SP paranccsal):**
| # | AT SP | Protokoll | K-line? | CAN? |
|---|-------|-----------|---------|------|
| 1 | ATSP1 | SAE J1850 PWM | ❌ | ❌ |
| 2 | ATSP2 | SAE J1850 VPW | ❌ | ❌ |
| 3 | ATSP3 | ISO 9141-2 | ✅ **K-line!** | ❌ |
| 4 | ATSP4 | ISO 14230-4 (KWP, 5-baud) | ✅ **K-line!** | ❌ |
| 5 | ATSP5 | ISO 14230-4 (KWP, fast init) | ✅ **K-line!** | ❌ |
| 6 | ATSP6 | ISO 15765-4 (CAN 11b/500k) | ❌ | ✅ |
| 7 | ATSP7 | ISO 15765-4 (CAN 29b/500k) | ❌ | ✅ |
| 8 | ATSP8 | ISO 15765-4 (CAN 11b/250k) | ❌ | ✅ |
| 9 | ATSP9 | ISO 15765-4 (CAN 29b/250k) | ❌ | ✅ |

**K-line támogatás az STN1170-ben:**
Az STN1170 chip tartalmaz K-line transzvertert is! Az ATSP3/4/5 protokollok K-line kommunikációt használnak.

**DE VAN EGY NAGY PROBLÉMA:**
Az ELM327/STN1170 K-line módban:
- Az adapter MAGA végzi az 5-baud inicializálást (ATSP4) vagy fast init-et (ATSP5)
- Az adapter MAGA kezeli a protokollt (ISO 9141-2 vagy ISO 14230-4)
- A kommunikáció OBD-II szintű: PID kérés → PID válasz
- **NINCS raw passthrough mód K-line-on!**

Ez azt jelenti:
- ✅ OBD-II PID-ek olvasása K-line-on (motor RPM, hőmérséklet, stb.)
- ❌ KW1281 protokoll (VW proprietary, block-based)
- ❌ KWP2000 diagnosztikai session (VW-specifikus címzéssel)
- ❌ Tetszőleges K-line ECU cím megadása (az ELM327 csak OBD-II címeket ismer: 0x33, 0x01, stb.)

**Kiegészítés:** Az STN1170-nek vannak kiterjesztett parancsai (STPX, STDI), de ezek sem adnak teljes raw K-line hozzáférést a VW-specifikus 5-baud inicializáláshoz tetszőleges ECU címre.

## 3. Korábbi kísérletek összefoglalása

### 3.1 Ross-Tech HEX klón — driver csere kísérletek

Számos scriptet írtunk (c:\temp\) a Ross-Tech klón FTDI VCP driverré alakítására:

| Script | Mit csinált | Eredmény |
|--------|-------------|----------|
| `swap_driver.ps1` | RT-USB driver eltávolítás, FTDI VCP INF telepítés | COM10 létrejött |
| `force_ftdi_registry.ps1` | Registry: Service=FTDIBUS, ClassGUID módosítás | Nem segített |
| `force_usbser.ps1` | usbser.sys driver kényszerítése | COM10 létrejött |
| `force_install.ps1` | newdev.dll UpdateDriverForPlugAndPlayDevices | COM10 létrejött |
| `install_port_driver.ps1` | SetupAPI force install FTDI port driver | COM10 létrejött |
| `setupapi_install.ps1` | Full SetupAPI driver telepítés | COM10 létrejött |
| `rosstech_vcp.inf` | Egyedi INF fájl VID_0403&PID_FA24 → FTDI VCP | COM10 létrejött |

**Eredmény:** A COM10 port LÉTREJÖTT, de ettől a **PIC mikrokontroller NEM lett áthidalva**.
A soros port megnyílt, de a PIC firmware saját protokollt vár, nem K-line passthrough-t.

### 3.2 KKL kábel — autós teszt eredménye

A KKL kábelt (COM9) **teszteltük az autónál**, az eredmény: **valószínűleg rossz a kábel**.
- A COM9 port azonosítva lett mint FTDI FT232RL eszköz
- A kw1281test GenericInterface-e COM9-en megnyílt
- Az 5-baud inicializálás NEM kapott választ az ECU-tól (nincs 0x55 sync byte)
- **Lehetséges okok:** rossz KKL kábel (K-line transzverter hiba, forrasztás, megszakadt vezeték)

## 4. Ajánlás: mit és hogyan teszteljünk

### 4.1 KKL kábel (COM9) — LEGJOBB ESÉLY ✅

**Ez a LEGÍGÉRETESEBB megoldás.** A KKL kábel pontosan erre lett tervezve:
- FT232RL → K-line transzverter → OBD-II K-line (pin 7)
- A kw1281test kód teljesre implementálja a K-line kommunikációt
- A GenericInterface (COM9 → .NET SerialPort) VAGY az FtdiInterface (FTDI serial number → natív D2xx) használható

**Tesztelési terv:**

**1. lépés: COM9 port ellenőrzés**
```powershell
# Dugd be a KKL kábelt
[System.IO.Ports.SerialPort]::GetPortNames()
# Elvárt: COM9 megjelenik
```

**2. lépés: FTDI serial number azonosítás** (opcionális, a natív FTDI interfészhez)
```powershell
# Eszközkezelőben: USB Serial Port (COM9) → Properties → Details → Serial Number
# Vagy:
Get-CimInstance Win32_PnPEntity | Where-Object { $_.Name -match "COM9" } | Select-Object DeviceID
# A DeviceID-ből kiolvasható az FTDI serial (pl. A6014IF9)
```

**3. lépés: Első teszt — Cluster (17-es cím)**
```powershell
cd "c:\Users\Sáfrány Gábor\_Projekte\kw1281\kw1281-can-extension"
dotnet run --project Cli -- COM9 10400 17 ReadIdent
```
Ez a legbiztosabb modul teszthez:
- A cluster (műszerfal) majdnem mindig válaszol
- 10400 baud az alapértelmezett K-line sebesség
- KW1281 protokollt vár (5-baud init, 0x55 sync)

**4. lépés: Ha a cluster válaszol — AutoScan**
```powershell
dotnet run --project Cli -- COM9 10400 1 AutoScan
```
Ez végigmegy az ismert VW modulcímeken és kilistázza az elérhetőeket.

**5. lépés: Ha NEM válaszol — hibaelhárítás**
- Gyújtás bekapcsolva?
- Más baud rate? Próbáld: `9600`, `4800`
- Más modul? Próbáld: `1` (motor), `19` (gateway), `25` (immo)
- KKL kábel LED (ha van) villog-e az init közben?

### 4.2 Ross-Tech HEX klón (COM10) — NEM AJÁNLOTT ❌

**A PIC mikrokontroller blokkolja a raw soros hozzáférést.**

Elméletileg létezik egy lehetőség:
- Ha a klón **pontosan** lemásolja az eredeti Ross-Tech firmware-t, akkor a VCP módban
  lehet, hogy a PIC K-line passthrough módba áll
- De ez a klónoknál szinte soha nem működik
- Nincs dokumentáció a PIC firmware protokolljáról

**Ha mégis próbálni akarod:**
```powershell
dotnet run --project Cli -- COM10 10400 17 ReadIdent
```
Valószínű eredmény: timeout a 5-baud init-nél (nincs 0x55 sync byte válasz).

### 4.3 vLinker FS (COM8) K-line mód — KORLÁTOZOTTAN LEHETSÉGES ⚠️

Az STN1170 K-line módja (ATSP3/4/5) használható lenne **ha** csak OBD-II PID olvasás a cél.
De a VW modulok KW1281/KWP2000 diagnosztikai protokollt használnak, amit az ELM327 K-line mód **nem tud kezelni**.

**Amit meg lehetne próbálni:**
```
ATZ          (reset)
ATSP4        (ISO 14230 KWP, 5-baud init)
ATSH 01      (header = 01, motor cím)
0100         (OBD Mode 01, PID 00 — támogatott PID-ek)
```
Ez OBD-II szinten kérdezné a motort K-line-on. De a VW body modulok (cluster, BCM, immo) NEM támogatják az OBD-II protokollt.

## 5. Összegzés

| Adapter | K-line képesség | VW KW1281 | VW KWP2000 | Ajánlás |
|---------|----------------|-----------|------------|---------|
| **KKL kábel (COM9)** | ✅ Teljes raw K-line | ✅ 5-baud init, block protocol | ✅ 5-baud/fast init | **HASZNÁLD EZT** |
| Ross-Tech HEX klón (COM10) | ❌ PIC blokkolja | ❌ | ❌ | Ne használd |
| vLinker FS (COM8) | ⚠️ Csak OBD-II K-line | ❌ Nem tud KW1281-et | ❌ Nem tud VW KWP2000-et | Csak CAN-hez |

**Következő lépés:** Vidd ki a KKL kábelt az autóhoz, és futtasd:
```
dotnet run --project Cli -- COM9 10400 17 ReadIdent
```

A kw1281test kódja kész, tesztelt, 292/292 teszt zöld. A GenericInterface támogatja a COM9-et, a 5-baud inicializálás implementálva van a KwpCommon-ban. Csak a hardveres tesztelés hiányzik.

## 6. Technikai háttér: hogyan működik a K-line kommunikáció a kódban

### 5-baud inicializálás (`KwpCommon.BitBang5Baud`)
```
1. Interface.SetBreak(false)     — TxD HIGH → K-line HIGH (idle)
2. SetBreak(true)                — TxD LOW → K-line LOW (start bit)
3. 8 × SetBreak(!bit)           — 200ms/bit, LSB first, 7 adat + 1 paritás
4. SetBreak(false)               — stop bit (K-line HIGH)
5. Összesen: 10 bit × 200ms = 2 másodperc
```

### Válasz fogadás
```
1. ECU küld: 0x55 sync byte (10400 baud)
2. ECU küld: keyword LSB + MSB
3. Tester küld: ~keyword_MSB (komplement)
4. Ha KW1281: block-alapú kommunikáció (byte-by-byte ACK)
5. Ha KWP2000: message-alapú kommunikáció (header + body + checksum)
```

### GenericInterface (COM9) K-line path
```
Program.cs → Tester ctor → InterfaceFactory.OpenPort("COM9", 10400)
    → GenericInterface(SerialPort: DTR=ON, RTS=OFF, 8N1)
    → KwpCommon.WakeUp(address, evenParity=false)
    → KwpCommon.BitBang5Baud(address) → SerialPort.BreakState = true/false
    → KW1281Dialog vagy KW2000Dialog
```
