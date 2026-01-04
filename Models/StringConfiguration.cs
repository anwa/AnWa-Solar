using System;

namespace AnWaSolar.Models;

public class StringConfiguration
{
    // Konfiguration eines MPPT-Strings (ein MPPT-Eingang)
    public int MpptIndex { get; set; }

    public PVModule? SelectedModule { get; set; }

    public int ModuleProString { get; set; } = 10;

    public int ParalleleStrings { get; set; } = 1;

    // Aktivierungszustand des Strings; bei false wird der String in der Berechnung übersprungen
    public bool IsEnabled { get; set; } = true;
}
