using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SolanaTradingBot.Common;
using SolanaTradingBot.Execution;
using System.Text.Json;

namespace SolanaTradingBot.Tests;

public class WalletConfigTests
{
    [Fact]
    public void WalletConfig_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var config = new WalletConfig();

        // Assert
        Assert.Equal(WalletSource.File, config.Source);
        Assert.Equal("", config.KeyFile);
        Assert.Equal("", config.PrivateKeyJson);
        Assert.Equal(5_000_000, config.MinSolLamports);
    }

    [Theory]
    [InlineData(WalletSource.Env)]
    [InlineData(WalletSource.File)]
    public void WalletConfig_CanSetSource(WalletSource source)
    {
        // Arrange & Act
        var config = new WalletConfig { Source = source };

        // Assert
        Assert.Equal(source, config.Source);
    }

    [Fact]
    public void WalletConfig_CanSetMinSolLamports()
    {
        // Arrange & Act
        var config = new WalletConfig { MinSolLamports = 10_000_000 };

        // Assert
        Assert.Equal(10_000_000, config.MinSolLamports);
    }

    [Fact]
    public void TradingConfig_IncludesWalletConfig()
    {
        // Arrange & Act
        var config = new TradingConfig();

        // Assert
        Assert.NotNull(config.Wallet);
        Assert.IsType<WalletConfig>(config.Wallet);
    }
}

public class ExecutionContextTests
{
    [Fact]
    public void ExecutionContext_CanBeCreated()
    {
        // Arrange
        var candidate = new SolanaTradingBot.Discovery.PoolCandidate
        {
            TokenMint = "test-mint",
            PoolAddress = "test-pool"
        };

        // Act
        var context = new SolanaTradingBot.Execution.ExecutionContext
        {
            Candidate = candidate,
            Sleeve = "Scalps",
            SizeSOL = 0.1m,
            SlippageBps = 100
        };

        // Assert
        Assert.Equal(candidate, context.Candidate);
        Assert.Equal("Scalps", context.Sleeve);
        Assert.Equal(0.1m, context.SizeSOL);
        Assert.Equal(100, context.SlippageBps);
    }

    [Fact]
    public void ExecutionContext_CanSetOptionalProperties()
    {
        // Arrange
        var candidate = new SolanaTradingBot.Discovery.PoolCandidate
        {
            TokenMint = "test-mint",
            PoolAddress = "test-pool"
        };

        // Act
        var context = new SolanaTradingBot.Execution.ExecutionContext
        {
            Candidate = candidate,
            Sleeve = "Momentum",
            SizeSOL = 0.5m,
            SlippageBps = 150,
            SimulationSignature = "sim-123",
            TransactionSignature = "tx-456",
            ErrorMessage = "Test error"
        };

        // Assert
        Assert.Equal("sim-123", context.SimulationSignature);
        Assert.Equal("tx-456", context.TransactionSignature);
        Assert.Equal("Test error", context.ErrorMessage);
    }
}

public class ExecuteResultTests
{
    [Theory]
    [InlineData(ExecuteResult.Success)]
    [InlineData(ExecuteResult.Failed)]
    [InlineData(ExecuteResult.Rejected)]
    public void ExecuteResult_AllValuesExist(ExecuteResult result)
    {
        // This test ensures all enum values are defined
        Assert.True(Enum.IsDefined(typeof(ExecuteResult), result));
    }
}