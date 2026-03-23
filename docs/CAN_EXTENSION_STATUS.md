# KW1281Test – CAN Extension: Állapot és Implementációs Terv

## 1. Eddigi munka összefoglalása

### 1.1 Elkészült komponensek

#### Fázis 1: CAN alapinfrastruktúra ✅ KÉSZ

##### `Interface/CanMessage.cs` (54 sor)
- CAN 2.0 üzenet reprezentáció
- 11-bit standard (max 0x7FF) és 29-bit extended (max 0x1FFFFFFF) CAN ID támogatás
- 0-8 byte payload validációval
- ID tartomány validáció konstruktorban
- `ToString()` formázott kiíráshoz
- **11 unit teszt**

##### `Interface/CanInterface.cs` (~420 sor)
- ELM327/HEX-V2 kompatibilis CAN interfész implementáció
- AT parancs alapú inicializálás (ATZ, ATE0, ATS0, ATL0, ATSP0)
- CAN sebesség beállítás: 500 kbps (ATSP6), 250 kbps (ATSP7)
- Üzenet küldés és fogadás (`SendCanMessage`, `ReceiveCanMessage`)
- `ParseCanMessage` – kompakt (ATS0) és szóközös formátum támogatás
- Monitor mód (`SetMonitorMode` / ATMA) – passzív CAN forgalom figyelés
- Thread-safe (lock-olt műveletek)
- `IDisposable` implementáció reset-tel
- **11 unit teszt** a `ParseCanMessage` logikára

##### `Program.cs` – CAN parancsok
- `cantest` – CAN interfész inicializálás + OBD-II MODE 01 PID 00 teszt
- `canmonitor` – passzív CAN busz forgalom figyelés (bármely gomb megnyomásáig)
- `cantp` – VW TP 2.0 csatorna teszt (TesterPresent + ReadECUIdentification)
- Help szöveg frissítve mindhárom parancshoz

#### Fázis 2: VW TP 2.0 Transport Protocol ✅ KÉSZ

##### `Kwp2000/Tp20Types.cs` (~110 sor)
- `Tp20OpCode` enum – TP 2.0 műveleti kódok:
  - `ChannelSetupRequest` (0xC0), `ChannelSetupResponse` (0xD0)
  - `ChannelParametersRequest` (0xA0), `ChannelParametersResponse` (0xA1)
  - `ConnectionTest` (0xA3), `Disconnect` (0xA8)
- `Tp20FrameType` enum – adatátviteli kerettípusok:
  - `WaitingForAck_MoreToFollow` (0x0), `WaitingForAck_LastPacket` (0x1)
  - `NotWaitingForAck_MoreToFollow` (0x2), `NotWaitingForAck_LastPacket` (0x3)
  - `Ack_NotReady` (0x9), `Ack_Ready` (0xB)
- `Tp20ChannelParameters` osztály:
  - `BlockSize`, `T1Ms` (ACK timeout), `T3Ms` (inter-frame delay)
  - `DecodeTimingByte()` / `EncodeTimingByte()` – TP 2.0 timing byte kódolás/dekódolás
  - 4 egység: 0.1ms, 1ms, 10ms, 100ms (upper nibble) × 0-15 érték (lower nibble)

##### `Kwp2000/Tp20Channel.cs` (~450 sor)
- Teljes VW TP 2.0 csatorna életciklus kezelés
- **Channel Setup**: 4 lépéses nyitás
  1. Setup Request küldés (CAN ID: `0x200 + module_address`)
  2. Setup Response fogadás (dinamikus TX/RX CAN ID-k)
  3. Parameters Request (BS=15, T1=100ms, T3=10ms)
  4. Parameters Response feldolgozás
- **Adatátvitel**: szegmentálás és összerakás
  - `SendData()` – KWP2000 üzenet szegmentálása, max 7 byte/keret, ACK kezelés
  - `ReceiveData()` – többkeretű válasz összerakása
- **ACK kezelés**: `WaitForAck()`, `SendAck()` a TP 2.0 flow control-hoz
- **Keep-alive**: `SendKeepAlive()` – `ConnectionTest` (0xA3) küldés timeout megelőzésére
- **Csatorna bontás**: `Close()` – `Disconnect` (0xA8) küldés
- `PadTo8()` helper – CAN keret 8 byte-ra padolása
- `IDisposable` implementáció

##### Unit tesztek (összesen 199)
- `Tests/CanMessageTests.cs` – 11 teszt (ID validáció, ToString, konstruktor)
- `Tests/CanInterfaceTests.cs` – 11 teszt (ParseCanMessage kompakt/szóközös formátum)
- `Tests/Tp20TypesTests.cs` – 23 teszt (timing encode/decode, roundtrip, enum értékek, defaults)
- `Tests/Tp20ChannelTests.cs` – 5 teszt (PadTo8 helper)
- `Tests/Kwp2000CanDialogTests.cs` – 5 teszt (interfész compliance, IDisposable, metódusok, BoschRBx ctor)
- `Tests/CanAutoScanTests.cs` – 8 teszt (GetControllerName ismert/ismeretlen címek)
- `Tests/UdsTests.cs` – 28 teszt (UdsService enum, UdsNrc enum, exception, UdsCanDialog, GetByteSize)
- `Tests/CanRouterTests.cs` – 9 teszt (típus struktúra, metódusok, Tp20Channel konstruktor kompatibilitás)
- `Tests/Tp20SessionTests.cs` – 8 teszt (típus struktúra, IDisposable, metódusok, property-k)
- Korábbi tesztek: 91 (Cluster, Tester, Program)

### 1.2 Jelenlegi architektúra

```
Program.cs ("cantp" / "canmulti" parancs)
    ↓
Tester.Kwp2000CanWakeup() / Tp20Session
    ↓
IKwp2000Dialog  ←── Transport absztrakció
    ├── KW2000Dialog       (K-line, IKwpCommon-alapú)
    └── Kwp2000CanDialog   (CAN, TP 2.0 fölötti)
            ↓
        Tp20Channel (VW TP 2.0 csatorna)
            ↓
        CanRouter (keret-routing, multi-ECU pufferelés)
            ↓
        CanInterface (AT parancsok soros porton)
            ↓
        ELM327 / HEX-V2 adapter
            ↓
        CAN busz (járműhálózat, 500 kbps)
```

### 1.3 Ami NINCS még kész
- `CanInterface` **nem implementálja az `IInterface`-t** – és nem is fogja (eltérő absztrakció)
- Nincs `ICanInterface` interfész – a `Tp20Channel` a `CanRouter`-en keresztül kommunikál
- `Edc15VM` még közvetlenül `KW2000Dialog`-ot használ (K-line specifikus)

---

## 2. Implementációs Terv

### Fázis 1: CAN alapinfrastruktúra stabilizálás ✅ KÉSZ
**Cél:** Robusztus, tesztelt CAN kommunikáció

| # | Feladat | Állapot | Leírás |
|---|---------|---------|--------|
| 1.1 | ✅ `CanInterface` tesztek | KÉSZ | 11 unit teszt: ParseCanMessage kompakt + szóközös formátum |
| 1.2 | ✅ `CanMessage` validáció | KÉSZ | 11-bit max 0x7FF, 29-bit max 0x1FFFFFFF, 11 teszt |
| 1.3 | ✅ Hibakezelés javítás | KÉSZ | ParseCanMessage refaktor: `ParseSpaceSeparated` + `ParseCompact` |
| 1.4 | ✅ CAN monitor parancs | KÉSZ | `canmonitor` – ATMA mód, passzív figyelés |

### Fázis 2: VW TP 2.0 Transport Protocol ✅ KÉSZ
**Cél:** VW-specifikus CAN transport layer a diagnosztikai üzenetekhez

| # | Feladat | Állapot | Leírás |
|---|---------|---------|--------|
| 2.1 | ✅ `Tp20Channel` osztály | KÉSZ | Teljes csatorna lifecycle: Open/Close/SendData/ReceiveData |
| 2.2 | ✅ Csatorna paraméterek | KÉSZ | BS, T1, T3 egyeztetés + timing byte encode/decode |
| 2.3 | ✅ Szegmentálás | KÉSZ | SendData szegmentálás + ReceiveData összerakás (max 7 byte/keret) |
| 2.4 | ✅ ACK kezelés | KÉSZ | WaitForAck/SendAck + block size alapú flow control |
| 2.5 | ✅ Keep-alive | KÉSZ | ConnectionTest (0xA3) küldés + fogadás kezelés |
| 2.6 | ✅ Csatorna bontás | KÉSZ | Disconnect (0xA8) + IDisposable |
| 2.7 | ✅ `cantp` CLI parancs | KÉSZ | TesterPresent + ReadECUIdentification tesztelés |
| 2.8 | ✅ Unit tesztek | KÉSZ | 28 TP 2.0 teszt (timing, enum, PadTo8) |

**VW TP 2.0 protokoll áttekintés:**
```
Tester                          ECU
  |--- Channel Setup Req ------->|  (CAN ID: 0x200 + modul_cím)
  |<-- Channel Setup Resp -------|  (CAN ID: 0x200 + tester logikai cím)
  |                               |
  |--- KWP2000 adat (TP 2.0) --->|  (dinamikus CAN ID-k)
  |<-- KWP2000 válasz (TP 2.0) --|
  |                               |
  |--- Disconnect --------------->|
```

**Kulcs CAN ID-k:**
- `0x200 + controller_address` → channel setup request
- `0x300 + controller_address` → broadcast
- Dinamikus CAN ID-k: a channel setup során egyeztetett TX/RX ID-k

### Fázis 3: KWP2000-over-CAN integráció ✅ KÉSZ
**Cél:** A meglévő `KW2000Dialog` képességek elérhetővé tétele CAN-en keresztül

| # | Feladat | Állapot | Leírás |
|---|---------|---------|--------|
| 3.1 | ✅ `IKwp2000Dialog` interfész | KÉSZ | Transport-agnosztikus KWP2000 absztrakció (6 metódus) |
| 3.2 | ✅ `Kwp2000CanDialog` | KÉSZ | TP 2.0 fölötti KWP2000 üzenetkezelés, IDisposable |
| 3.3 | ✅ `KW2000Dialog` refaktor | KÉSZ | Implementálja az `IKwp2000Dialog` interfészt |
| 3.4 | ✅ `BoschRBxCluster` refaktor | KÉSZ | Konkrét `KW2000Dialog` → `IKwp2000Dialog` absztrakció |
| 3.5 | ✅ `Tester` bővítés | KÉSZ | `Kwp2000CanWakeup(CanInterface)` CAN-alapú wakeup |
| 3.6 | ✅ `Program.cs` integráció | KÉSZ | `cantp` parancs: `Kwp2000CanDialog`-on keresztül |
| 3.7 | ✅ Unit tesztek | KÉSZ | 5 teszt: interfész compliance, method check, BoschRBx ctor |

**Megvalósított architektúra:**
```
Program.cs ("cantp" / "canmulti" parancs)
    ↓
Tester.Kwp2000CanWakeup()  /  Tp20Session (multi-ECU)
    ↓                              ↓
IKwp2000Dialog              Tp20Channel × N db
    ├── KW2000Dialog               ↓
    └── Kwp2000CanDialog      CanRouter (keret-routing)
            ↓                      ↓
        Tp20Channel           CanInterface
            ↓
        CanRouter
            ↓
        CanInterface
```

**Kulcs implementációs döntések:**
- `IKwp2000Dialog` interfész (nem absztrakt osztály) – minimális invazív refaktor
- CAN KWP2000 üzenetek: `[service_byte, ...body]` – nincs format byte, cím, checksum (TP 2.0 kezeli)
- `Kwp2000CanDialog.SendReceive`: `reqCorrectlyRcvdRspPending` (0x78) kezelés retry loop-pal
- `DumpMem`: `_channel.SendKeepAlive()` a TP 2.0 csatorna fenntartásához
- `BoschRBxCluster` immár K-line és CAN transporttal is működik

### Fázis 4: Haladó funkciók (folyamatban)
**Cél:** Teljes diagnosztikai képesség CAN-en

| # | Feladat | Állapot | Leírás |
|---|---------|---------|--------|
| 4.1 | ✅ CAN AutoScan | KÉSZ | Automatikus controller felderítés (0x01–0x7F), KWP2000 + UDS fallback |
| 4.2 | ✅ UDS (ISO 14229) | KÉSZ | UdsCanDialog, UdsService, UdsNrc, canuds CLI parancs |
| 4.3 | ✅ Multi-ECU kommunikáció | KÉSZ | CanRouter frame-routing, Tp20Session multi-csatorna kezelés, canmulti CLI |
| 4.4 | CAN-FD támogatás | - | Ha a HEX-V2 támogatja |

**CAN AutoScan részletek:**
- `canautoscan` CLI parancs – `Program.CanAutoScan(portName, baudRate)`
- 0x01–0x7F VW TP 2.0 címtartomány iteráció
- Minden címre: `Tp20Channel.Open()` → ha sikeres, KWP2000 ident → UDS fallback
- Protokoll detektálás: KWP2000 / UDS / TP2.0 (ha sem KWP sem UDS nem válaszol)
- `GetControllerName()` – ismert VW modul nevek (`ControllerAddress` enum)
- 8 unit teszt a `GetControllerName` logikára

**UDS (ISO 14229) részletek:**
- `Uds/UdsService.cs` – 17 UDS szolgáltatás enum (0x10–0x85)
- `Uds/UdsNrc.cs` – 20 NRC (Negative Response Code) enum érték
- `Uds/NegativeUdsResponseException.cs` – kivétel szolgáltatás + NRC adatokkal
- `Uds/UdsCanDialog.cs` – TP 2.0 fölötti UDS implementáció:
  - `SendReceive()` – ResponsePending (0x78) retry kezelés
  - `DiagnosticSessionControl()`, `ECUReset()`, `TesterPresent()`
  - `ReadDataByIdentifier(ushort did)` – 2 byte-os DID (F190=VIN, F187=PartNumber, stb.)
  - `WriteDataByIdentifier()`, `SecurityAccess()`, `ReadDTCInformation()`, `ClearDiagnosticInformation()`
  - `ReadMemoryByAddress()` – addressAndLengthFormatIdentifier kódolás
  - `RoutineControl()` – sub-function + routineId
- `canuds` CLI parancs – Extended session + VIN + PartNumber + TesterPresent teszt
- 28 unit teszt (enum értékek, exception, IDisposable, metódusok, GetByteSize)

**Multi-ECU kommunikáció részletek:**
- `Interface/CanRouter.cs` – CAN keret routing réteg
  - `ConcurrentDictionary<uint, ConcurrentQueue<CanMessage>>` – csatornánkénti pufferelés
  - `RegisterChannel(uint rxCanId)` / `UnregisterChannel(uint rxCanId)` – csatorna regisztráció
  - `ReceiveMessage(uint rxCanId, int timeoutMs)` – regisztrált csatornáról olvasás, más csatornák pufferelése
  - `ReceiveUnregisteredMessage(int timeoutMs)` – nem-regisztrált keretek fogadása (channel setup fázisban)
  - `SendMessage(CanMessage)` – delegálás a `CanInterface`-hez
- `Kwp2000/Tp20Session.cs` – többcsatornás session kezelő
  - `OpenChannel(byte moduleAddress)` – TP 2.0 csatorna nyitás és regisztráció
  - `GetChannel(byte moduleAddress)` – nyitott csatorna lekérdezés
  - `CloseChannel(byte moduleAddress)` – csatorna bontás és erőforrás felszabadítás
  - `SendKeepAliveAll()` – összes nyitott csatorna keep-alive
  - `ChannelCount` / `OpenAddresses` – állapot lekérdezés
- `Tp20Channel` refaktorálva: `CanRouter`-en keresztül kommunikál (backward compat: `CanInterface` konstruktor is elérhető)
- `canmulti` CLI parancs – 7 ismert VW modul egyidejű csatorna, azonosítás olvasás, keep-alive
- 9 + 8 = 17 unit teszt (CanRouter típus struktúra, Tp20Session típus struktúra)

---

## 3. Technikai megjegyzések

### ELM327 vs HEX-V2
A jelenlegi implementáció ELM327 AT parancsokat használ. A Ross-Tech HEX-V2 adapter
kompatibilis ezekkel, de van néhány HEX-V2-specifikus kiterjesztés is (pl. raw CAN mód),
ami a TP 2.0 implementációhoz szükséges lehet. Az ELM327 protocol handler automatikusan
kezeli az ISO-TP szegmentálást, ami egyszerűsíti az OBD-II kommunikációt, de a VW TP 2.0
protokollhoz raw CAN kereteket kell tudnunk küldeni/fogadni.

### ParseCanMessage ✅ JAVÍTVA
A `ParseCanMessage` immár mindkét formátumot kezeli:
- Szóközös: `"7E8 06 41 00 BE 3E B8 13"`
- Kompakt (ATS0): `"7E80641003EB813"`
Automatikus felismerés az ID hossz heurisztikával.

### K-line vs CAN összehasonlítás
| Szempont | K-line (jelenlegi) | CAN (megvalósítva) |
|----------|-------------------|-----------------|
| Fizikai réteg | Egyvezetékes soros (K/L vonal) | Differenciális (CAN-H/CAN-L) |
| Sebesség | 9600-10400 baud | 250-500 kbps |
| Protokoll | KW1281 / KWP2000 | VW TP 2.0 + KWP2000 |
| Inicializálás | 5-baud wakeup | CAN channel setup (Tp20Channel) |
| Járművek | ~1995-2005 VW/Audi | ~2004+ VW/Audi |

---

## 4. Következő lépések (Fázis 4)

1. **CAN-FD támogatás** – ha a HEX-V2 támogatja a CAN-FD-t
2. **Valós hardveres tesztelés** – CAN AutoScan / KWP2000 / UDS / Multi-ECU helyes működés a HEX-V2-vel
