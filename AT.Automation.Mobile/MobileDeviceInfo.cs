namespace AT.Automation.Mobile;

/// <summary>Egy ADB-n keresztül látott Android eszköz aktuális állapota — a Mobil nézet
/// eszköz-állapot sávja ebből építi fel a "Csatlakoztatva: Samsung Galaxy S23" jellegű
/// szöveget. IsConnected=false esetén a DeviceModel/SerialNumber üres, nincs mit mutatni.</summary>
public sealed class MobileDeviceInfo
{
    public required bool IsConnected { get; init; }

    /// <summary>Az adb devices által adott nyers azonosító (sorozatszám/emulátor-port) — pl. "R58N70ABCDE".</summary>
    public string? SerialNumber { get; init; }

    /// <summary>A telefon "szép" hardver-modell neve (adb shell getprop ro.product.model) — pl. "SM-S911B" vagy "Pixel 7".
    /// Null, ha a lekérdezés nem sikerült (pl. az eszköz "unauthorized" állapotban van).</summary>
    public string? DeviceModel { get; init; }

    /// <summary>Ha true, az eszköz látszik az adb devices listában, de "unauthorized"/"offline"
    /// állapotban van — csatlakoztatva van, de nem használható (pl. a telefonon még nem
    /// fogadták el az USB-hibakeresési engedélykérést).</summary>
    public bool IsUnauthorizedOrOffline { get; init; }
}
