namespace FinCopilot.Api.Tools;

public sealed record CompoundInterestInput(
    decimal InitialAmount,
    decimal MonthlyContribution,
    decimal AnnualRatePercent,
    int Years,
    decimal? AnnualInflationPercent = null);

public sealed record YearSnapshot(int Year, decimal TotalInvested, decimal Balance, decimal? RealBalance);

public sealed record CompoundInterestResult(
    decimal FinalBalance,
    decimal TotalInvested,
    decimal TotalInterest,
    decimal MonthlyRatePercent,
    decimal? RealFinalBalance,
    IReadOnlyList<YearSnapshot> Yearly,
    string Notes);

/// <summary>
/// Simulação determinística de juros compostos. Contas ficam aqui, não no LLM:
/// o modelo decide os parâmetros, o código garante a aritmética.
/// </summary>
public sealed class CompoundInterestTool : AgentTool<CompoundInterestInput>
{
    public override string Name => "simulate_compound_interest";

    public override string Description =>
        "Simula a evolução de um investimento com juros compostos e aportes mensais no fim de cada mês. " +
        "Use SEMPRE esta ferramenta para projeções de patrimônio em vez de calcular de cabeça. " +
        "A taxa anual é convertida em taxa mensal equivalente. Não considera imposto de renda, taxas de " +
        "administração nem variação da taxa ao longo do tempo. Se a inflação anual for informada, " +
        "também retorna o saldo em valores de hoje (poder de compra).";

    public override IReadOnlyDictionary<string, object> Properties => new Dictionary<string, object>
    {
        ["initialAmount"] = new { type = "number", description = "Valor inicial investido em reais (>= 0)." },
        ["monthlyContribution"] = new { type = "number", description = "Aporte mensal em reais (>= 0)." },
        ["annualRatePercent"] = new { type = "number", description = "Rentabilidade anual em %, ex.: 10.5 para 10,5% a.a." },
        ["years"] = new { type = "integer", description = "Prazo em anos, de 1 a 50." },
        ["annualInflationPercent"] = new { type = "number", description = "Opcional. Inflação anual esperada em %, para calcular o valor real." },
    };

    public override IReadOnlyList<string> Required => ["initialAmount", "monthlyContribution", "annualRatePercent", "years"];

    protected override Task<object> ExecuteAsync(CompoundInterestInput input, CancellationToken ct) =>
        Task.FromResult<object>(Simulate(input));

    public static CompoundInterestResult Simulate(CompoundInterestInput input)
    {
        if (input.InitialAmount < 0 || input.MonthlyContribution < 0)
            throw new ToolException("Valores inicial e de aporte não podem ser negativos.");
        if (input.Years is < 1 or > 50)
            throw new ToolException("O prazo deve estar entre 1 e 50 anos.");
        if (input.AnnualRatePercent is <= -100 or > 1000)
            throw new ToolException("Taxa anual fora do intervalo aceito.");

        var monthlyRate = MonthlyEquivalent(input.AnnualRatePercent);
        var monthlyInflation = input.AnnualInflationPercent is { } inf ? MonthlyEquivalent(inf) : (double?)null;

        var balance = (double)input.InitialAmount;
        var invested = (double)input.InitialAmount;
        var inflationIndex = 1.0;
        var yearly = new List<YearSnapshot>(input.Years);

        for (var month = 1; month <= input.Years * 12; month++)
        {
            balance = balance * (1 + monthlyRate) + (double)input.MonthlyContribution;
            invested += (double)input.MonthlyContribution;
            if (monthlyInflation is { } mi) inflationIndex *= 1 + mi;

            if (month % 12 == 0)
            {
                yearly.Add(new YearSnapshot(
                    month / 12,
                    Money(invested),
                    Money(balance),
                    monthlyInflation is null ? null : Money(balance / inflationIndex)));
            }
        }

        return new CompoundInterestResult(
            FinalBalance: Money(balance),
            TotalInvested: Money(invested),
            TotalInterest: Money(balance - invested),
            MonthlyRatePercent: Math.Round((decimal)(monthlyRate * 100), 4),
            RealFinalBalance: monthlyInflation is null ? null : Money(balance / inflationIndex),
            Yearly: yearly,
            Notes: "Valores brutos, sem IR e sem custos. Aportes no fim de cada mês.");
    }

    private static double MonthlyEquivalent(decimal annualPercent) =>
        Math.Pow(1 + (double)annualPercent / 100, 1.0 / 12) - 1;

    private static decimal Money(double value) => Math.Round((decimal)value, 2);
}
