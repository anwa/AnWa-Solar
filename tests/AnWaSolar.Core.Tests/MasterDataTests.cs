using System.Text.Json;

using AnWaSolar.Core.Services;

using Microsoft.Extensions.Logging.Abstractions;

namespace AnWaSolar.Core.Tests;

/// <summary>
/// Checks the master data files that are shipped with the application.
/// </summary>
public sealed class MasterDataTests
{
    private static JsonDataStore LoadShippedData()
    {
        var store = new JsonDataStore(NullLogger<JsonDataStore>.Instance, new JsonSerializerOptions(), new DataPathProvider());
        store.Load();
        return store;
    }

    [Fact]
    public void Shipped_files_load_completely()
    {
        JsonDataStore store = LoadShippedData();

        Assert.NotEmpty(store.Module);
        Assert.NotEmpty(store.Wechselrichter);
        Assert.Empty(store.Issues);
    }

    [Fact]
    public void Modules_have_consistent_datasheet_values()
    {
        foreach (Models.PVModule module in LoadShippedData().Module)
        {
            string name = module.DisplayName;
            Assert.False(string.IsNullOrWhiteSpace(module.Hersteller), name);
            Assert.False(string.IsNullOrWhiteSpace(module.Model), name);
            Assert.True(module.NominalleistungPmaxWp > 0, name);
            Assert.True(module.LeerlaufspannungUocV > module.SpannungImMppUmppV, $"{name}: UOC must exceed UMPP");
            Assert.True(module.KurzschlusstromIscA > module.StromImMppImppA, $"{name}: ISC must exceed IMPP");
            Assert.True(module.TemperaturkoeffVocProzentProGradC < 0, $"{name}: VOC coefficient must be negative");
        }
    }

    [Fact]
    public void Inverters_have_consistent_datasheet_values()
    {
        foreach (Models.Wechselrichter inverter in LoadShippedData().Wechselrichter)
        {
            string name = inverter.DisplayName;
            Assert.True(inverter.AnzahlDerMpptTrackers > 0, name);
            Assert.True(inverter.MaxStringsProMppt > 0, name);
            Assert.True(inverter.MaxDcEingangsspannungV > 0, name);
            Assert.True(inverter.MaxEingangsKurzschlussstromA >= inverter.MaxBetriebsPvEingangsstromA, name);
            Assert.Matches(@"^\d+-\d+$", inverter.MpptSpannungsbereichV);
        }
    }
}
