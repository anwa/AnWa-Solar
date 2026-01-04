using System;

namespace AnWaSolar.Models;

public class StringConfiguration
{
    // Konfiguration eines MPPT-Strings (ein MPPT-Eingang)
    public int MpptIndex { get; set; }

    public PVModule? SelectedModule { get; set; }

    public int ModuleProString { get; set; } = 10;

    public int ParalleleStrings { get; set; } = 1;
}
