using System.Text.Json;
using FinCopilot.Api.Tools;

namespace FinCopilot.Tests;

public class CompoundInterestToolTests
{
    [Fact]
    public void Sem_aportes_aplica_a_taxa_anual_composta()
    {
        var result = CompoundInterestTool.Simulate(new(10_000m, 0m, 10m, 2));

        // 10.000 * 1,10^2 = 12.100
        Assert.Equal(12_100.00m, result.FinalBalance);
        Assert.Equal(10_000.00m, result.TotalInvested);
        Assert.Equal(2_100.00m, result.TotalInterest);
    }

    [Fact]
    public void Com_taxa_zero_saldo_e_igual_ao_total_investido()
    {
        var result = CompoundInterestTool.Simulate(new(1_000m, 500m, 0m, 3));

        Assert.Equal(1_000m + 500m * 36, result.FinalBalance);
        Assert.Equal(0m, result.TotalInterest);
        Assert.Equal(3, result.Yearly.Count);
    }

    [Fact]
    public void Aportes_mensais_batem_com_formula_de_serie_uniforme()
    {
        var result = CompoundInterestTool.Simulate(new(0m, 500m, 12m, 10));

        var i = Math.Pow(1.12, 1.0 / 12) - 1;
        var expected = 500 * (Math.Pow(1 + i, 120) - 1) / i;
        Assert.Equal(Math.Round((decimal)expected, 2), result.FinalBalance);
    }

    [Fact]
    public void Inflacao_igual_a_taxa_preserva_o_poder_de_compra()
    {
        var result = CompoundInterestTool.Simulate(new(10_000m, 0m, 5m, 10, AnnualInflationPercent: 5m));

        Assert.Equal(10_000.00m, result.RealFinalBalance);
    }

    [Theory]
    [InlineData(-1, 0, 10, 5)]
    [InlineData(0, -1, 10, 5)]
    [InlineData(0, 100, 10, 0)]
    [InlineData(0, 100, 10, 51)]
    public void Rejeita_entradas_invalidas(decimal initial, decimal monthly, decimal rate, int years)
    {
        Assert.Throws<ToolException>(() => CompoundInterestTool.Simulate(new(initial, monthly, rate, years)));
    }

    [Fact]
    public async Task Entrada_do_modelo_com_tipo_errado_vira_ToolException()
    {
        IAgentTool tool = new CompoundInterestTool();
        var input = new Dictionary<string, JsonElement>
        {
            ["initialAmount"] = JsonSerializer.SerializeToElement("muito"),
            ["monthlyContribution"] = JsonSerializer.SerializeToElement(0),
            ["annualRatePercent"] = JsonSerializer.SerializeToElement(10),
            ["years"] = JsonSerializer.SerializeToElement(1),
        };

        await Assert.ThrowsAsync<ToolException>(() => tool.ExecuteAsync(input, CancellationToken.None));
    }
}
