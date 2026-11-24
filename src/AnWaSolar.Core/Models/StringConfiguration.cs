namespace AnWaSolar.Core.Models;

/// <summary>String configuration of one MPPT input.</summary>
public class StringConfiguration
{
    /// <summary>MPPT number, starting at 1.</summary>
    public int MpptIndex { get; set; }

    public PVModule? SelectedModule { get; set; }

    public int ModulesPerString { get; set; } = 10;

    public int ParallelStrings { get; set; } = 1;

    /// <summary>A disabled string keeps its values but is skipped by the calculation.</summary>
    public bool IsEnabled { get; set; } = true;
}
