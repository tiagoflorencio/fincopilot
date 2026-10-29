namespace FinCopilot.Tests;

/// <summary>Relógio fixo para testes que dependem de "hoje".</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
