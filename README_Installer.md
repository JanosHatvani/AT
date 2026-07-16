# AT Studio — Telepítőcsomag készítése (installer + portable ZIP)

Ez az útmutató egyetlen, önálló `.exe` telepítőt (AT Studio Setup) és egy portable
ZIP-csomagot hoz létre a programból — a célgépen **nem kell külön .NET runtime-ot
telepíteni**, minden szükséges függőség a csomagba kerül (self-contained deployment).


---

## Előfeltételek (egyszeri beállítás)

1. **.NET 9 SDK** — ha Visual Studio-val dolgozol, ez valószínűleg már megvan.
2. **Inno Setup** — töltsd le és telepítsd innen: https://jrsoftware.org/isdl.php
   (a sima "Inno Setup" elég, nem kell a QuickStart Pack, bár az sem árt).

---

## 1. lépés — Fájlok elhelyezése a solution-ben

Az alábbi 3 fájl megléte a solutionban `Installer` mappában:

```
AT\                             
├── AT.App\
│   ├── AT.App.csproj
│   └── Properties\
│       └── PublishProfiles\
│           └── SelfContainedWinX64.pubxml    
├── AT.Infrastructure\
├── AT.Automation.Mobile\
├── AT.Core\
├── ...
└── Installer\                                 
    ├── ATStudioSetup.iss                       
    └── create-portable-zip.bat                 
```

---

## 2. lépés — Self-contained publish futtatása

Nyiss egy **Developer PowerShell**-t (vagy sima PowerShell-t/cmd-t, ha a `dotnet`
parancssorban ), navigálj a solution gyökerébe (ahol az `AT.sln` van) és futtasd:



```powershell

publish előtt obj, debug mappa törlés:
Get-ChildItem -Path . -Include bin,obj -Recurse -Directory | Remove-Item -Recurse -Force
dotnet publish AT.App\AT.App.csproj -c Release -p:PublishProfile=AT.App\Properties\PublishProfiles\SelfContainedWinX64.pubxml -v:detailed > publish-log.txt

dotnet publish AT.App\AT.App.csproj -c Release -p:PublishProfile=AT.App\Properties\PublishProfiles\SelfContainedWinX64.pubxml
```

Ez néhány percig tarthat (a teljes .NET runtime bekerül a kimenetbe). A végeredmény itt lesz:

```
AT.App\bin\Release\net9.0-windows\win-x64\publish\AT.App.exe
```

## 3. lépés — Installer (.exe telepítő) elkészítése

1. Nyisd meg az **Inno Setup Compilert**
2. File → Open → válaszd ki az `Installer\ATStudioSetup.iss` fájlt.
3. **Fontos, egyszeri lépés:** generálj egy saját, egyedi GUID-ot: Tools menü →
   Generate GUID, majd másold be az `ATStudioSetup.iss` `AppId={{...}` sorába ha még nincs
4. Nyomj **Ctrl+F9** (vagy Build → Compile).
5. Ha minden rendben, a telepítő itt jön létre:
   ```
   Installer\Output\AT-Studio-Setup-1.0.0.exe
   ```

Ezt az egy `.exe`-t adhatod tovább — dupla kattintásra telepíti a programot (Start
Menü-bejegyzéssel, opcionális Asztali ikonnal, Eltávolítás a Vezérlőpultból is
elérhető lesz).

**Mit csinál ez a telepítő:**
- Bemásolja a self-contained publish teljes kimenetét (`AT.App.exe` + a .NET
  runtime + az `AppiumRuntime` mappa, ami már eleve a `.csproj`
  CopyToOutputDirectory-jával bekerül a publish-kimenetbe).
- Ellenőrzi (registry-alapon), van-e telepített böngésző (Chrome/Firefox/Edge) a
  gépen — ha nincs, egy tájékoztató, nem blokkoló üzenetet mutat, mert a
  Desktop modul böngésző nélkül is teljesen működik.
- NEM ellenőrzi és NEM telepíti az Android SDK-t — az a program saját feladata
  (lásd lent).

---

## 4. lépés — Portable ZIP elkészítése (opcionális, a "másik" csomagforma)

A `create-portable-zip.bat` fájlt futtasd a solution gyökeréből (dupla kattintás,
vagy parancssorból):

```powershell
Installer\create-portable-zip.bat
```

Ez létrehoz egy `AT-Studio-Portable-1.0.0.zip` fájlt a solution gyökerében — ezt
kicsomagolva az `AT.App.exe` telepítés nélkül, azonnal futtatható.

---

## 5. Verziószám frissítése új kiadásnál

Ha a jövőben új verziót adsz ki, 2 helyen kell módosítani a verziószámot:

1. `ATStudioSetup.iss` — a `#define MyAppVersion "1.0.0"` sor.
2. `create-portable-zip.bat` — a `set VERSION=1.0.0` sor.

Az `AppId` GUID-ot NE változtasd meg — az installer ez alapján ismeri fel, hogy
ugyanannak a programnak egy újabb verzióját telepíted (és lecseréli a régit, nem
telepíti kettőt egymás mellé).

---

## Natív / külső függőségek — mit tartalmaz a csomag, és mit kell külön telepíteni

| Modul | Technológia | Csomagban van? | Külön telepítendő a célgépen |
|---|---|---|---|
| Desktop | FlaUI (UIA3), tiszta NuGet-csomag, nincs külső folyamat | Igen, teljesen | Semmi — a Desktop modul önmagában, extra függőség nélkül működik |
| Web | Selenium + beépített Selenium Manager | A driver-bináris (chromedriver.exe stb.) automatikusan letöltődik első használatkor | Böngésző (Chrome, Firefox vagy Edge) — ezt nem telepíti semmi automatikusan |
| Mobil | Appium, becsomagolt Node.js runtime (AppiumRuntime mappa) | Igen, a .csproj már CopyToOutputDirectory-val bemásolja | Android SDK — a program-belüli telepítő intézi (lásd lent) |



## Kód-aláírás (code signing) 

Jelenleg a telepítő és az `.exe` nincs digitálisan aláírva — emiatt Windows SmartScreen
egy "Ismeretlen kiadó" figyelmeztetést fog mutatni telepítéskor.

**Fontos, 2024 óta érvényes fejlemény:** Microsoft megszüntette azt a korábbi
gyakorlatot, hogy a drágább EV-tanúsítvánnyal aláírt szoftver azonnal átugorja a
SmartScreen-figyelmeztetést. Ma az OV és EV tanúsítványok egyenrangúak SmartScreen
szempontjából — mindkettő "reputáció-építésen" megy át (hetekig-hónapokig tarthat).

**Ajánlott irányok (WPF asztali app esetén, nem driver):**
1. OV (Organization Validation) tanúsítvány — kb. $226–$386/év (Sectigo/DigiCert),
   1–3 munkanapos igénylés, 2023 óta kötelező hardveres (FIPS 140-2) tokenen tárolni
   a privát kulcsot.
2. Microsoft Azure Trusted Signing — felhő-alapú, havidíjas, nincs fizikai token —
   ha a vevő EU-ban bejegyzett cég, ez a legegyszerűbb út.

Az EV-tanúsítványt nem javasolt külön megvenni, mert a plusz költség (kb. dupla ár)
2026-ban már nem jár arányos extra haszonnal egy WPF alkalmazásnál.

---

