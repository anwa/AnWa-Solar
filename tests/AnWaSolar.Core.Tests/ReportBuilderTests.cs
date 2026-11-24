using AnWaSolar.Core.Calculation;
using AnWaSolar.Core.Models;

using static AnWaSolar.Core.Tests.StringCalculatorTests;

namespace AnWaSolar.Core.Tests;

public sealed class ReportBuilderTests
{
    private static readonly DateTime s_createdAt = new(2026, 11, 24, 12, 0, 0, DateTimeKind.Local);

    [Fact]
    public void Without_inverter_the_report_asks_for_one()
    {
        string report = ReportBuilder.Build(null, Parameters(), s_createdAt);

        Assert.Contains("Created on 2026-11-24 12:00", report, StringComparison.Ordinal);
        Assert.Contains("Please select an inverter first", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Disabled_mppt_is_not_evaluated_in_the_report()
    {
        StringConfiguration disabled = Aiko(2, 30, 5);
        disabled.IsEnabled = false;
        StringCalculationResult result = StringCalculator.Calculate(Deye18K(), [Aiko(1, 11), disabled], Parameters());

        string report = ReportBuilder.Build(result, Parameters(), s_createdAt);
        string mppt2 = report[report.IndexOf("## MPPT 2", StringComparison.Ordinal)..];

        Assert.Contains("Disabled", mppt2, StringComparison.Ordinal);
        Assert.DoesNotContain("### Results", mppt2, StringComparison.Ordinal);
        Assert.DoesNotContain("exceeded", mppt2, StringComparison.Ordinal);
    }

    [Fact]
    public void Report_lists_too_many_parallel_strings()
    {
        StringCalculationResult result = StringCalculator.Calculate(Deye18K(), [Aiko(1, 11, 3), Aiko(2, 11)], Parameters());

        string report = ReportBuilder.Build(result, Parameters(), s_createdAt);

        Assert.Contains("- Max. strings per MPPT exceeded: 3 > 2.", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Live_text_and_report_show_the_same_violations()
    {
        StringCalculationResult result = StringCalculator.Calculate(Deye18K(), [Aiko(1, 16, 3), Aiko(2, 4)], Parameters());

        string text = CalculationTextFormatter.Format(result);
        string report = ReportBuilder.Build(result, Parameters(), s_createdAt);

        foreach (string marker in new[] { "Max. DC voltage", "MPPT upper bound", "MPPT lower bound", "Start voltage not reached", "Input current limit", "Max. strings per MPPT" })
        {
            Assert.Contains(marker, text, StringComparison.Ordinal);
            Assert.Contains(marker, report, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Report_names_the_vmpp_coefficient_and_its_approximation()
    {
        StringCalculationResult result = StringCalculator.Calculate(Deye18K(), [Aiko(1, 11), Aiko(2, 11)], Parameters());

        string report = ReportBuilder.Build(result, Parameters(), s_createdAt);

        Assert.Contains("(approximation: Pmax coefficient)", report, StringComparison.Ordinal);
        Assert.Contains("Cell temperature range", report, StringComparison.Ordinal);
    }
}
