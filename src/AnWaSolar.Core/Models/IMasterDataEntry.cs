namespace AnWaSolar.Core.Models;

/// <summary>Common part of PV modules and inverters in the master data.</summary>
public interface IMasterDataEntry
{
    string Manufacturer { get; set; }

    string Model { get; set; }

    /// <summary>Imported datasheet, relative to the user data folder; null when there is none.</summary>
    string? DatasheetFile { get; set; }

    /// <summary>True for an entry from the own master data in the user data folder.</summary>
    bool IsUserEntry { get; set; }
}
