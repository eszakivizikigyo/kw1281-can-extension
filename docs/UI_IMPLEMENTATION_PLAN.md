# KW1281Test Unified UI — Implementációs Terv

## 1. Összefoglaló

Grafikus felhasználói felület tervezése, amely **mind a K-line (KW1281/KWP2000), mind a CAN bus (KWP2000/UDS)** meglévő diagnosztikai funkcióit egységes keretben teszi elérhetővé. A cél egy cross-platform (Windows + Linux + macOS) asztali alkalmazás, amely a jelenlegi CLI-logikát UI-ból vezérli.

---

## 2. Technológia választás

### 2.1 UI Framework: **Avalonia UI**

| Szempont | Avalonia UI | WPF | MAUI |
|----------|-------------|-----|------|
| Cross-platform | ✅ Win/Linux/macOS | ❌ Win only | ⚠️ Win/macOS/Android/iOS (Linux kísérleti) |
| .NET 9+ támogatás | ✅ | ✅ | ✅ |
| MVVM támogatás | ✅ (CommunityToolkit.Mvvm) | ✅ | ✅ |
| Teljesítmény | ✅ (Skia-alapú rendering) | ✅ | ⚠️ Nagyobb overhead |
| Méret / footprint | Kicsi (~10 MB) | Közepes | Nagy (~50+ MB) |
| Linux használhatóság | ✅ Natív | ❌ | ❌ Stabil |

**Indoklás**: A projekt már támogatja Linuxot (LinuxInterface.cs), cross-platform a cél. Avalonia a legérettebb cross-platform .NET UI framework asztali alkalmazásokhoz.

### 2.2 Architektúra: **MVVM** (Model-View-ViewModel)

| Réteg | Felelősség | Technológia |
|-------|-----------|-------------|
| **Model** | Meglévő üzleti logika (Tester, KW1281Dialog, CanInterface, stb.) | Jelenlegi kód — változatlanul |
| **ViewModel** | UI állapot, parancsok, adatkötés | CommunityToolkit.Mvvm |
| **View** | AXAML nézetek (Avalonia XAML) | Avalonia Controls |
| **Service** | Szálkezelés, port management, logging | Háttérszálak, ILog integráció |

### 2.3 NuGet csomagok

```xml
<PackageReference Include="Avalonia" Version="11.*" />
<PackageReference Include="Avalonia.Desktop" Version="11.*" />
<PackageReference Include="Avalonia.Themes.Fluent" Version="11.*" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
<PackageReference Include="Avalonia.ReactiveUI" Version="11.*" />  <!-- opcionális -->
```

---

## 3. Projekt struktúra

```
kw1281test.sln
├── kw1281test/                    ← Meglévő library (átnevezve Class Library-ra)
│   ├── Interface/
│   ├── Kwp2000/
│   ├── Uds/
│   ├── Cluster/
│   ├── Blocks/
│   ├── Logging/
│   ├── Tester.cs
│   ├── KW1281Dialog.cs
│   └── ...
│
├── kw1281test.Cli/                ← CLI frontend (jelenlegi Program.cs)
│   └── Program.cs
│
├── kw1281test.Ui/                 ← Új Avalonia UI projekt
│   ├── App.axaml / App.axaml.cs
│   ├── ViewLocator.cs
│   │
│   ├── Models/
│   │   ├── ConnectionSettings.cs          # Port, baud, controller address
│   │   ├── DiagnosticResult.cs            # Általános eredmény wrapper
│   │   └── EcuInfo.cs                     # ECU azonosítási adatok
│   │
│   ├── ViewModels/
│   │   ├── MainWindowViewModel.cs         # Fő ablak, navigáció
│   │   ├── ConnectionViewModel.cs         # Port/CAN választás, connect/disconnect
│   │   ├── DashboardViewModel.cs          # ECU info összefoglaló
│   │   │
│   │   ├── KLine/
│   │   │   ├── FaultCodesViewModel.cs     # Hibakód olvasás/törlés
│   │   │   ├── AdaptationViewModel.cs     # Adaptáció R/W/Test
│   │   │   ├── ActuatorTestViewModel.cs   # Beavatkozó tesztek
│   │   │   ├── GroupReadViewModel.cs      # Mérőértékek
│   │   │   ├── EepromViewModel.cs         # EEPROM dump/load/map
│   │   │   ├── MemoryViewModel.cs         # RAM/ROM dump
│   │   │   ├── CodingViewModel.cs         # Kódolás
│   │   │   └── ClusterViewModel.cs        # SKC, unlock, cluster-specifikus
│   │   │
│   │   ├── Can/
│   │   │   ├── CanMonitorViewModel.cs     # CAN forgalom figyelés
│   │   │   ├── CanAutoScanViewModel.cs    # Modul keresés
│   │   │   ├── CanDiagViewModel.cs        # KWP2000/UDS műveletek
│   │   │   └── CanMultiEcuViewModel.cs    # Multi-ECU dashboard
│   │   │
│   │   └── Shared/
│   │       ├── LogViewModel.cs            # Élő log/trace nézet
│   │       └── HexEditorViewModel.cs      # Hex nézegető (dump-okhoz)
│   │
│   ├── Views/
│   │   ├── MainWindow.axaml
│   │   ├── ConnectionView.axaml
│   │   ├── DashboardView.axaml
│   │   │
│   │   ├── KLine/
│   │   │   ├── FaultCodesView.axaml
│   │   │   ├── AdaptationView.axaml
│   │   │   ├── ActuatorTestView.axaml
│   │   │   ├── GroupReadView.axaml
│   │   │   ├── EepromView.axaml
│   │   │   ├── MemoryView.axaml
│   │   │   ├── CodingView.axaml
│   │   │   └── ClusterView.axaml
│   │   │
│   │   ├── Can/
│   │   │   ├── CanMonitorView.axaml
│   │   │   ├── CanAutoScanView.axaml
│   │   │   ├── CanDiagView.axaml
│   │   │   └── CanMultiEcuView.axaml
│   │   │
│   │   └── Shared/
│   │       ├── LogView.axaml
│   │       └── HexEditorView.axaml
│   │
│   ├── Services/
│   │   ├── IConnectionService.cs          # Kapcsolat absztrakció
│   │   ├── ConnectionService.cs           # Port megnyitás/zárás
│   │   ├── IDiagnosticService.cs          # Diagnosztikai műveletek
│   │   ├── KLineDiagnosticService.cs      # K-line impl
│   │   ├── CanDiagnosticService.cs        # CAN impl
│   │   └── UiLogAdapter.cs               # ILog → UI log bridge
│   │
│   └── Converters/
│       ├── FaultCodeConverter.cs
│       ├── HexValueConverter.cs
│       └── BoolToColorConverter.cs
│
└── kw1281test.Tests/              ← Meglévő tesztek (változatlan)
```

---

## 4. UI terv — Nézetek és funkcionalitás

### 4.1 Fő ablak elrendezés

```
┌──────────────────────────────────────────────────────────────────┐
│  KW1281Test Diagnostics                               [_][□][X] │
├──────────┬───────────────────────────────────────────────────────┤
│          │  ┌─────────────────────────────────────────────────┐  │
│ ▼ Kapcso │  │  [ConnectionView]                               │  │
│   lat    │  │  Port: [COM3 ▼]  Baud: [10400]                 │  │
│          │  │  Mód: (●) K-Line  (○) CAN                      │  │
│ ▼ K-Line │  │  Cím: [0x17 - Cluster ▼]                       │  │
│   Hibakó │  │  [ Csatlakozás ]  [ Leválás ]                  │  │
│   Adapta │  │  Státusz: ● Csatlakozva — VDO 1J0920927C       │  │
│   Beavat │  └─────────────────────────────────────────────────┘  │
│   Mérőér │                                                       │
│   EEPROM │  ┌─────────────────────────────────────────────────┐  │
│   Memóri │  │  [Aktív nézet tartalma]                         │  │
│   Kódolá │  │                                                 │  │
│   Cluste │  │  Pl. FaultCodesView:                            │  │
│          │  │  ┌──────────────────────────────────────────┐   │  │
│ ▼ CAN    │  │  │ # │ DTC kód  │ Leírás         │ Státusz│   │  │
│   Monitor│  │  │ 1 │ 00668    │ Supply volt... │ Aktív  │   │  │
│   AutoSca│  │  │ 2 │ 01312    │ Engine Cont... │ Tárolt │   │  │
│   Diagnos│  │  └──────────────────────────────────────────┘   │  │
│   Multi-E│  │  [ Hibakódok olvasása ] [ Hibakódok törlése ]  │  │
│          │  └─────────────────────────────────────────────────┘  │
│ ▼ Log    │                                                       │
│          │  ┌─────────────────────────────────────────────────┐  │
│          │  │ [LogView] — valós idejű kommunikációs log       │  │
│          │  │ > TX: 06 01 09 00 03  (ReadFaultCodes)          │  │
│          │  │ < RX: 0F 01 FC 02 A4 01 00 28 01 ...           │  │
│          │  └─────────────────────────────────────────────────┘  │
└──────────┴───────────────────────────────────────────────────────┘
```

**Elrendezés**: 3 oszlopos / sáv

| Terület | Tartalom |
|---------|----------|
| **Bal oldali panel** (200px, összecsukható) | TreeView navigáció — K-Line / CAN / Log csoportok |
| **Fő tartalom** (flex) | Az aktuálisan kiválasztott nézet (View) |
| **Alsó panel** (150px, összecsukható) | Kommunikációs log (TX/RX raw bytes + dekódolt) |

### 4.2 Kapcsolat nézet (ConnectionView)

Minden művelet előfeltétele. A felső sávban mindig látható.

| Mező | Típus | Értékek |
|------|-------|---------|
| Port | ComboBox | Elérhető COM portok (auto-detect) + frissítés gomb |
| Baud Rate | ComboBox / TextBox | 10400 (KW1281 default), 9600, 19200, 115200 |
| Mód | RadioButton | K-Line / CAN |
| Controller cím | ComboBox | ControllerAddress enum minden tagja: 0x01 ECU, 0x17 Cluster, stb. |
| Csatlakozás / Leválás | Button | Async kapcsolódás, státusz kijelzés |

**K-Line mód**: Port + Baud + Controller cím szükséges → `Tester.Kwp1281Wakeup()`
**CAN mód**: Csak Port szükséges (115200 fix baud) → `CanInterface.Initialize()`

### 4.3 K-Line nézetek

#### 4.3.1 Hibakódok (FaultCodesView)

| Elem | Funkció |
|------|---------|
| DataGrid: DTC kód, leírás, státusz | Fault code lista |
| [Olvasás] gomb | `Tester.ReadFaultCodes()` |
| [Törlés] gomb | `Tester.ClearFaultCodes()` (megerősítő dialógus) |
| Export CSV/TXT | Eredmények mentése |

#### 4.3.2 Adaptáció (AdaptationView)

| Elem | Funkció |
|------|---------|
| Channel: NumericUpDown (0–255) | Adaptációs csatorna |
| Value: NumericUpDown (0–65535) | Érték |
| Login: NumericUpDown (opcionális) | Bejelentkezési kód |
| [Olvasás] | `AdaptationRead(channel)` |
| [Teszt] | `AdaptationTest(channel, value)` |
| [Mentés] | `AdaptationSave(channel, value)` — megerősítő dialógus |
| Eredmény: Label | Aktuális csatorna-érték |

#### 4.3.3 Beavatkozó tesztek (ActuatorTestView)

| Elem | Funkció |
|------|---------|
| Aktuális teszt neve | Label (pl. "Idle Speed Stabilization") |
| [Következő] gomb | `ActuatorTest()` — ciklikus |
| [Stop] gomb | Kilépés a tesztből |
| Teszt napló | ListBox az eddigi tesztekről |

#### 4.3.4 Mérőértékek (GroupReadView)

| Elem | Funkció |
|------|---------|
| Group: NumericUpDown (0–255) | Csoportszám |
| [Olvasás] | `GroupRead(group)` |
| Eredmény: DataGrid (4 oszlop) | SensorValue-k megjelenítése |
| Mód: RadioButton | Normál / Basic Setting |
| [Folyamatos olvasás] toggle | Ismétlődő lekérdezés (500ms intervallum) |

#### 4.3.5 EEPROM (EepromView)

| Elem | Funkció |
|------|---------|
| Cím: Hex TextBox | Kezdőcím |
| Hossz: NumericUpDown | Olvasandó byte-ok száma |
| [Dump] | `DumpEeprom(addr, length, filename)` |
| [Load] | `LoadEeprom(addr, filename)` — megerősítés! |
| [Map] | `MapEeprom()` — elérhető címek térképe |
| Hex nézet | HexEditorView beágyazva |

#### 4.3.6 Memória (MemoryView)

| Elem | Funkció |
|------|---------|
| Típus: RadioButton | RAM / ROM / EEPROM |
| Cím: Hex TextBox | Kezdőcím (0x-prefix) |
| Hossz: NumericUpDown | Byte-ok |
| [Dump] | Megfelelő DumpRam/Rom/Eeprom |
| [Single Read] | Egy byte olvasás |
| [Single Write] | Egy byte írás (EEPROM only, megerősítés!) |
| Hex nézet | HexEditorView |

#### 4.3.7 Kódolás (CodingView)

| Elem | Funkció |
|------|---------|
| Coding: NumericUpDown (0–32767) | Kódolási érték |
| Workshop Code: NumericUpDown (0–99999) | Műhelykód |
| [Beállítás] | `SetSoftwareCoding()` — megerősítés! |
| Aktuális kódolás | Megjelenítés ReadIdent-ből |

#### 4.3.8 Cluster műveletek (ClusterView)

| Elem | Funkció |
|------|---------|
| Cluster típus | Auto-detect (VDO/Marelli/Bosch/Motometer/AudiC5) |
| [SKC kinyerés] | `GetSkc()` |
| [Cluster ID] | `GetClusterId()` |
| [NEC ROM dump] | `DumpClusterNecRom()` |
| [Marelli dump] | `DumpMarelliMem(addr, len)` |
| [RBx dump] | `DumpRBxMem(addr, len)` |
| [RB4 mód váltás] | `ToggleRB4Mode()` |
| [Reset] | `ResetCluster()` |
| Eredmény panel | SKC, dump fájl, státusz |

### 4.4 CAN nézetek

#### 4.4.1 CAN Monitor (CanMonitorView)

| Elem | Funkció |
|------|---------|
| [Start] / [Stop] | ATMA parancs indítás/leállítás |
| DataGrid (élő) | CAN ID, DLC, Data (hex), Timestamp |
| Szűrő: TextBox | CAN ID szűrés (pl. "0x200-0x2FF") |
| [Rögzítés fájlba] toggle | Log mentés CSV/CAN-log formátumban |
| Számláló | Összesen/szűrt üzenetek |

#### 4.4.2 CAN AutoScan (CanAutoScanView)

| Elem | Funkció |
|------|---------|
| [Indítás] | `CanAutoScan()` — 0x01–0x7F címek |
| ProgressBar | Aktuális cím / 127 |
| DataGrid: Cím, Protokoll, Part#, Szoftver | Talált modulok |
| Státusz: Label | "Scanning 0x17..." / "Kész: 12 modul" |
| [Részletek] | Kiválasztott modul részletes info |
| [Export] | CSV/JSON mentés |

#### 4.4.3 CAN Diagnosztika (CanDiagView)

Közös nézet KWP2000 és UDS műveletekhez CAN-on.

| Elem | Funkció |
|------|---------|
| Protokoll: RadioButton | KWP2000 / UDS |
| Modul cím: ComboBox | 0x01–0x7F (vagy AutoScan-ból) |
| [Csatlakozás] | TP 2.0 channel nyitás |
| --- KWP2000 műveletek --- | |
| [Read ECU ID] | ReadEcuIdentification (0x1A) |
| [Read Memory] | ReadMemoryByAddress — cím + hossz input |
| [Security Access] | Seed-key folyamat |
| [Tester Present] | Keep-alive |
| --- UDS műveletek --- | |
| [Read DID] | ReadDataByIdentifier — DID combobox (F190=VIN, F187=Part#, stb.) |
| [Read DTCs] | ReadDTCInformation |
| [Clear DTCs] | ClearDiagnosticInformation — megerősítés! |
| [Diagnostic Session] | SessionControl — Default/Extended/Programming |
| [Routine Control] | RoutineControl — routine ID + subfunc |
| Eredmény panel | Válasz hex + dekódolt |

#### 4.4.4 Multi-ECU Dashboard (CanMultiEcuView)

| Elem | Funkció |
|------|---------|
| Modul lista: CheckedListBox | Megnyitandó modulok (0x01–0x7F checkboxok) |
| [Összes megnyitása] | `Tp20Session.OpenChannel()` × N |
| Dashboard Grid | Kártyák minden csatlakozott modulhoz |
| Kártya tartalma | Cím, Part#, Státusz, [Részletek] gomb |
| [Keep-alive] auto toggle | `SendKeepAliveAll()` háttérben |
| [Mind bezárása] | Tp20Session.Dispose() |

### 4.5 Közös nézetek

#### 4.5.1 Log nézet (LogView)

| Elem | Funkció |
|------|---------|
| Auto-scroll toggle | Élő log követés |
| Szűrő: TX/RX/Error | Szint szerinti szűrés |
| [Törlés] | Log ürítés |
| [Mentés] | Log fájlba exportálás |
| Színkódolás | TX=kék, RX=zöld, Error=piros |

#### 4.5.2 Hex nézet (HexEditorView)

Beágyazott komponens EEPROM/RAM/ROM dump-ok megjelenítéséhez.

| Elem | Funkció |
|------|---------|
| Cím oszlop | 8 hex cím |
| Hex adat | 16 byte soronként |
| ASCII nézet | Jobb oldali oszlop |
| Kijelölés + keresés | Byte keresés, értékkiemelés |
| [Módosítás] mód | Inline byte szerkesztés (write-back szükséges) |

---

## 5. Implementációs fázisok

### Fázis 0: Projekt előkészítés (1 hét)

| Feladat | Részletek |
|---------|-----------|
| Meglévő kód refaktor | Program.cs üzleti logikáját Tester/Dialog-ba mozgatni, ahol még nincs ott |
| Class Library átkonvertálás | `kw1281test.csproj` → library, CLI külön projekt |
| Solution struktúra | `kw1281test.sln` → 3 projekt: Lib + CLI + UI |
| NuGet-ek | Avalonia, CommunityToolkit.Mvvm hozzáadása |
| CI/CD alapok | Build script mindhárom projektre |

### Fázis 1: Alapváz + Kapcsolat (1.5 hét)

| Feladat | Részletek |
|---------|-----------|
| MainWindow + navigáció | Bal panel TreeView + ContentControl |
| ConnectionView/ViewModel | Port felismerés, K-Line/CAN mód választás |
| ConnectionService | IInterface / CanInterface beburkolása |
| UiLogAdapter | ILog → ObservableCollection bridge (UI thread-safe) |
| LogView | Élő log panel alul |
| Csatlakozás/Leválás | Async connect, UI feedback |

### Fázis 2: K-Line nézetek (2 hét)

| Feladat | Prioritás |
|---------|-----------|
| FaultCodesView | Magas — leghasznosabb funkció |
| GroupReadView | Magas — valós idejű mérés |
| AdaptationView | Magas |
| CodingView | Közepes |
| ActuatorTestView | Közepes |
| EepromView + HexEditor | Közepes |
| MemoryView | Alacsony |
| ClusterView | Alacsony (speciális) |

### Fázis 3: CAN nézetek (1.5 hét)

| Feladat | Prioritás |
|---------|-----------|
| CanMonitorView | Magas — debugging |
| CanAutoScanView | Magas — modul felfedezés |
| CanDiagView (KWP2000 + UDS) | Magas |
| CanMultiEcuView | Közepes |

### Fázis 4: Közös nézetek + polish (1 hét)

| Feladat | Részletek |
|---------|-----------|
| HexEditorView | Inline szerkesztés, keresés |
| Téma támogatás | Light/Dark |
| Beállítások mentése | Utolsó port, utolsó cím |
| Ablak állapot | Méret/pozíció mentése |
| Állapot jelző | Alsó státuszsáv (csatlakozási idő, ECU info) |
| Error handling | Globális exception kezelés, user-friendly hibák |

### Fázis 5: Tesztelés + csomagolás (1 hét)

| Feladat | Részletek |
|---------|-----------|
| ViewModel unit tesztek | Minden ViewModel parancsra |
| Integrációs tesztek | Mock IInterface / CanInterface |
| Publish profilek | Windows x64, Linux x64, macOS arm64 |
| Installer | MSIX (Windows), AppImage (Linux), DMG (macOS) |
| README frissítés | Használati útmutató UI-hoz |

---

## 6. Szálkezelés — Kritikus tervezési elem

A soros kommunikáció blokkoló — az UI thread-et védeni kell.

```
┌──────────┐     Command      ┌──────────────┐
│  UI      │ ──────────────→  │  ViewModel   │
│  Thread  │                  │  (bindings)  │
└──────────┘                  └──────┬───────┘
                                     │ Task.Run / async
                              ┌──────▼───────┐
                              │  Background  │
                              │  Thread      │
                              │  (Service)   │
                              └──────┬───────┘
         Dispatcher.Post             │ IInterface / CanInterface
┌──────────┐    ◄────────────────────┤ (blokkoló I/O)
│  UI      │    ObservableCollection │
│  Update  │                  ┌──────▼───────┐
└──────────┘                  │  Serial Port │
                              └──────────────┘
```

**Szabályok:**
1. Minden diagnosztikai művelet `Task.Run()`-on belül fut
2. Eredmények `Dispatcher.UIThread.InvokeAsync()`-kal térnek vissza
3. `CancellationToken` minden hosszú művelethez (dump, scan, monitor)
4. ProgressBar update `IProgress<T>` pattern-nel
5. Egyidejű hozzáférés védelem: egy port = egy művelet (semaphore)

---

## 7. Service réteg — Model és ViewModel közötti híd

```csharp
// Kapcsolat absztrakció
public interface IConnectionService
{
    ConnectionState State { get; }
    ConnectionMode Mode { get; }  // KLine / CAN
    
    Task<ControllerInfo> ConnectKLineAsync(string port, int baud, int address, CancellationToken ct);
    Task ConnectCanAsync(string port, CancellationToken ct);
    Task DisconnectAsync();
    
    Tester? Tester { get; }             // K-line műveletek
    CanInterface? CanInterface { get; }  // CAN műveletek
}

// Diagnosztikai műveletek
public interface IDiagnosticService
{
    // K-Line
    Task<List<FaultCode>> ReadFaultCodesAsync(CancellationToken ct);
    Task ClearFaultCodesAsync(CancellationToken ct);
    Task<AdaptationResult> AdaptationReadAsync(byte channel, CancellationToken ct);
    Task<GroupReadResult> GroupReadAsync(byte group, CancellationToken ct);
    Task<byte[]> DumpEepromAsync(int addr, int length, IProgress<int> progress, CancellationToken ct);
    // ...
    
    // CAN
    Task<List<CanMessage>> MonitorCanAsync(CancellationToken ct);
    Task<List<EcuInfo>> AutoScanAsync(IProgress<int> progress, CancellationToken ct);
    Task<byte[]> ReadDidAsync(ushort did, CancellationToken ct);
    // ...
}
```

---

## 8. Log integráció

Jelenlegi ILog interfész bridge-elése az UI-ba:

```csharp
public class UiLogAdapter : ILog
{
    private readonly ObservableCollection<LogEntry> _entries;
    private readonly Dispatcher _dispatcher;
    
    public void WriteLine(string message)
    {
        var entry = new LogEntry(DateTime.Now, LogLevel.Info, message);
        _dispatcher.UIThread.Post(() => _entries.Add(entry));
    }
    
    // TX/RX specifikus színezéssel
    public void WriteRaw(byte[] data, Direction direction) { ... }
}
```

---

## 9. Követelmények és kockázatok

### Követelmények

| # | Követelmény | Prioritás |
|---|-------------|-----------|
| R1 | Minden CLI parancs elérhető UI-ból | Magas |
| R2 | K-Line és CAN egyidejűleg nem szükséges | Közepes |
| R3 | Cross-platform (Win + Linux + macOS) | Magas |
| R4 | Valós idejű log megjelenítés | Magas |
| R5 | Dump fájlok hex megjelenítése | Közepes |
| R6 | ECU azonosítás megjelenítése csatlakozáskor | Magas |
| R7 | Megerősítő dialógusok destruktív műveleteknél | Magas |
| R8 | Beállítások perzisztens mentése | Alacsony |

### Kockázatok

| Kockázat | Valószínűség | Hatás | Mitigáció |
|----------|-------------|-------|-----------|
| Serial port cross-platform eltérések | Közepes | Magas | Meglévő IInterface absztrakció lefedi |
| Avalonia tanulási görbe | Alacsony | Közepes | WPF-hez nagyon hasonló XAML |
| Szálkezelési deadlock | Közepes | Magas | Strict async pattern, CancellationToken mindenütt |
| Cluster-specifikus unlock nem működik UI-ból | Alacsony | Alacsony | Tester már kezeli — csak wrapper kell |
| CAN keep-alive timeout multi-ECU közben | Közepes | Közepes | Háttér timer `SendKeepAliveAll()` |

---

## 10. Első lépés — Proof of Concept

A leggyorsabb validáció egy minimális Avalonia alkalmazás:

1. **Avalonia projekt létrehozás**: `dotnet new avalonia.app -n kw1281test.Ui`
2. **ConnectionView**: Port lista + Connect gomb
3. **FaultCodesView**: Hibakód olvasás + DataGrid
4. **LogView**: Raw kommunikáció log

Ez a 3 nézet elegendő az architektúra és szálkezelés validálásához, mielőtt a teljes UI-t felépítenénk.

---

## 11. UI mockup — Teljes alkalmazás

```
┌──────────────────────────────────────────────────────────────────────┐
│  ⚙ KW1281Test Diagnostics                   [Theme ☀/🌙] [_][□][X] │
├──────────────────────────────────────────────────────────────────────┤
│  Port: [COM3 ▼] [↻]  Mód: [● K-Line ○ CAN]  Cím: [0x17 Cluster ▼]│
│  [ 🔌 Csatlakozás ]     Státusz: ● Online — VDO 1J0920927C         │
├──────────┬───────────────────────────────────────────────────────────┤
│ K-Line   │                                                           │
│  ├ Hibakódok ◄──────── Aktív nézet jelölése                         │
│  ├ Adaptáció  │                                                      │
│  ├ Beavatkozó │   ┌───────────────────────────────────────────┐      │
│  ├ Mérőértékek│   │  Hibakódok — 3 db                        │      │
│  ├ EEPROM     │   │                                           │      │
│  ├ Memória    │   │  ┌─────┬──────────┬───────────┬────────┐ │      │
│  ├ Kódolás    │   │  │  #  │ DTC      │ Leírás    │ Státusz│ │      │
│  └ Cluster    │   │  ├─────┼──────────┼───────────┼────────┤ │      │
│               │   │  │  1  │ 00668    │ Supply V  │ Aktív  │ │      │
│ CAN           │   │  │  2  │ 01312    │ Eng. Con  │ Tárolt │ │      │
│  ├ Monitor    │   │  │  3  │ 65535    │ No code   │ Tárolt │ │      │
│  ├ AutoScan   │   │  └─────┴──────────┴───────────┴────────┘ │      │
│  ├ Diagnoszt. │   │                                           │      │
│  └ Multi-ECU  │   │  [ 📋 Olvasás ]  [ 🗑 Törlés ]  [ 💾 E. ]│      │
│               │   └───────────────────────────────────────────┘      │
│ ─────────     │                                                      │
│ Log           │                                                      │
├──────────┴───────────────────────────────────────────────────────────┤
│ Log  [Auto-scroll ✓]  [Szűrő: Összes ▼]  [Törlés]  [Mentés]        │
│ 14:23:01.123  TX  06 01 09 00 03                                     │
│ 14:23:01.456  RX  0F 01 FC 02 A4 01 28 00 FF FF 00 03               │
│ 14:23:01.789  TX  03 01 09 03                                        │
│ 14:23:02.012  RX  06 02 09 00 03                                     │
├──────────────────────────────────────────────────────────────────────┤
│ ● Csatlakozva  │  0x17 Cluster  │  VDO 1J0920927C  │  Idő: 00:02:34│
└──────────────────────────────────────────────────────────────────────┘
```

---

*Dokumentum verzió: 1.0*
*Utolsó frissítés: 2025*
*Projekt: kw1281test — VW/Audi Diagnosztikai Tool*
