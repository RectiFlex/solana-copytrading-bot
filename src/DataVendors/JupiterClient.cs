using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SolanaTradingBot.Engine;

namespace SolanaTradingBot.DataVendors;

public interface IJupiterClient
{
    Task<JupiterQuoteResponse?> GetQuoteAsync(string inputMint, string outputMint, decimal amountSOL, int slippageBps);
    Task<JupiterQuoteResponse?> GetQuoteAsync(string inputMint, string outputMint, string amountLamports, int slippageBps);
    Task<JupiterSwapResponse?> GetSwapTransactionAsync(JupiterQuoteResponse quote, string userPublicKey, long? priorityFeeLamports = null);
    Task<decimal> GetTokenPriceAsync(string tokenMint, string vsCurrency = "USDC");
    Task<bool> SimulateSwapAsync(JupiterQuoteResponse quote, string userPublicKey);
}

public class JupiterClient : IJupiterClient
{
    private readonly IJupiterApi _api;
    private readonly ILogger<JupiterClient> _logger;
    private readonly TradingConfig _config;

    // SOL mint address
    private const string SOL_MINT = "So11111111111111111111111111111111111111112";
    
    // USDC mint address  
    private const string USDC_MINT = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";
    
    // Lamports per SOL
    private const long LAMPORTS_PER_SOL = 1_000_000_000L;

    public JupiterClient(IJupiterApi api, ILogger<JupiterClient> logger, IOptions<TradingConfig> config)
    {
        _api = api;
        _logger = logger;
        _config = config.Value;
    }

    public async Task<JupiterQuoteResponse?> GetQuoteAsync(string inputMint, string outputMint, decimal amountSOL, int slippageBps)
    {
        // Convert SOL amount to lamports
        var lamports = (long)(amountSOL * LAMPORTS_PER_SOL);
        return await GetQuoteAsync(inputMint, outputMint, lamports.ToString(), slippageBps);
    }

    public async Task<JupiterQuoteResponse?> GetQuoteAsync(string inputMint, string outputMint, string amountLamports, int slippageBps)
    {
        try
        {
            _logger.LogDebug("Getting Jupiter quote: {InputMint} -> {OutputMint}, Amount: {Amount}, Slippage: {Slippage}bps",
                inputMint, outputMint, amountLamports, slippageBps);

            var quote = await _api.GetQuoteAsync(
                inputMint: inputMint,
                outputMint: outputMint,
                amount: amountLamports,
                slippageBps: slippageBps,
                onlyDirectRoutes: false,
                maxAccounts: _config.Jupiter.MaxRouteHops);

            if (quote != null)
            {
                _logger.LogDebug("Quote received: In={InAmount}, Out={OutAmount}, PriceImpact={PriceImpact}%",
                    quote.InAmount, quote.OutAmount, quote.PriceImpactPct);
            }

            return quote;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Jupiter quote for {InputMint} -> {OutputMint}", inputMint, outputMint);
            return null;
        }
    }

    public async Task<JupiterSwapResponse?> GetSwapTransactionAsync(JupiterQuoteResponse quote, string userPublicKey, long? priorityFeeLamports = null)
    {
        try
        {
            _logger.LogDebug("Getting swap transaction for quote: {InputMint} -> {OutputMint}", 
                quote.InputMint, quote.OutputMint);

            var request = new JupiterSwapRequest
            {
                QuoteResponse = quote,
                UserPublicKey = userPublicKey,
                WrapAndUnwrapSol = true,
                UseSharedAccounts = true,
                AsLegacyTransaction = false,
                PrioritizationFeeLamports = priorityFeeLamports?.ToString()
            };

            var response = await _api.GetSwapTransactionAsync(request);

            if (response?.SimulationError != null)
            {
                _logger.LogWarning("Swap simulation failed: {Code} - {Message}", 
                    response.SimulationError.Code, response.SimulationError.Message);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get swap transaction for {InputMint} -> {OutputMint}", 
                quote.InputMint, quote.OutputMint);
            return null;
        }
    }

    public async Task<decimal> GetTokenPriceAsync(string tokenMint, string vsCurrency = "USDC")
    {
        try
        {
            _logger.LogDebug("Getting token price for {TokenMint} vs {VsCurrency}", tokenMint, vsCurrency);

            var response = await _api.GetPricesAsync(tokenMint, vsCurrency);
            
            if (response.Data.TryGetValue(tokenMint, out var priceData))
            {
                return priceData.Price;
            }

            _logger.LogWarning("Price not found for token {TokenMint}", tokenMint);
            return 0m;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get price for token {TokenMint}", tokenMint);
            return 0m;
        }
    }

    public async Task<bool> SimulateSwapAsync(JupiterQuoteResponse quote, string userPublicKey)
    {
        if (!_config.Jupiter.Simulate)
        {
            return true; // Skip simulation if disabled
        }

        try
        {
            var swapResponse = await GetSwapTransactionAsync(quote, userPublicKey);
            return swapResponse?.SimulationError == null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Swap simulation failed for {InputMint} -> {OutputMint}", 
                quote.InputMint, quote.OutputMint);
            return false;
        }
    }

    public static decimal CalculatePriceImpact(JupiterQuoteResponse quote)
    {
        if (decimal.TryParse(quote.PriceImpactPct, out var impact))
        {
            return Math.Abs(impact);
        }
        return 0m;
    }

    public static decimal ConvertLamportsToSOL(string lamports)
    {
        if (long.TryParse(lamports, out var amount))
        {
            return (decimal)amount / LAMPORTS_PER_SOL;
        }
        return 0m;
    }

    public static long ConvertSOLToLamports(decimal sol)
    {
        return (long)(sol * LAMPORTS_PER_SOL);
    }
}