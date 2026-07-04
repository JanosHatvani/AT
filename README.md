# AT Framework — új projekt váz

Ez a solution a régi `ATold` projekt (JanosHatvani/AT) modernizált újraírásának
első lépése: a design-rendszer és a UI-shell. Automatizálási logika (Web,
Desktop, Mobile) még nincs bekötve — azok külön fázisokban kerülnek bele.

## Megnyitás Visual Studio-ban

1. Nyisd meg a `AT.sln` fájlt Visual Studio 2022-ben (17.8+, .NET 9 SDK szükséges).
2. Első megnyitáskor a VS automatikusan visszaállítja (restore) a NuGet csomagokat
   (`CommunityToolkit.Mvvm`, `Microsoft.Extensions.Hosting`).
3. Állítsd az **AT.App**-ot induló projektnek (jobb klikk a Solution Explorer-ben → *Set as Startup Project*), ha nem az lenne alapból.
4. F5 — a shell elindul: oldalsáv navigáció, 5 placeholder oldal, működő toast-értesítés demóval.

## Projektek

| Projekt | Cél | Állapot |
|---|---|---|
| `AT.Core` | Közös szerződések (`IAutomationDriver`) és modellek | Kész (váz) |
| `AT.Infrastructure` | Config, logging, adattárolás | Placeholder |
| `AT.Automation.Web` | Selenium-alapú webteszt | **Kész ebben a körben** |
| `AT.Automation.Desktop` | FlaUI-alapú desktop teszt (Winium leváltása) | Placeholder |
| `AT.Automation.Mobile` | Appium-alapú Android/iOS teszt | Placeholder |
| `AT.App` | WPF UI, MVVM, navigáció, design-rendszer | **Kész ebben a körben** |

## Web tesztelés modul — előfeltételek

- Telepített **Chrome, Firefox vagy Edge** böngésző (választható a UI-n) a gépen.
- Első futtatáskor a Selenium 4.6+ beépített *Selenium Manager*-je automatikusan
  letölti a böngésző verziójához illő drivert (internet-kapcsolat kell hozzá első
  alkalommal) — nem kell kézzel driver .exe-t kezelni, nincs verzió-egyeztetés.
- Elérhető műveletek: Navigate, Click, DoubleClick, RightClick, SendKeys, Clear,
  Hover, SelectByText, SelectByValue, DragAndDrop, Wait, és 10 különböző
  wait-feltétel (láthatóság, kattinthatóság, jelenlét, szöveg/attribútum/class/
  CSS-érték egyezés stb.) — a régi `WebMethods.cs` összes egyedi műveletét lefedi.


## Következő lépések

1. Android/iOS modul (a régi `AppMethods.cs` logika átemelése, tisztítva)
2. a képernyőkép-mentés (`PrtScFolderPath`, `CaptureScreenshots`)
  és a futási statisztika (`testName`, `LastFindElementDuration`) — ezek az
  Infrastructure/riportozási fázisban kerülnek be, mert adatréteget igényelnek.
