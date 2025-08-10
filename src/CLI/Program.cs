using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using SolanaTradingBot.Common;
using SolanaTradingBot.Discovery;
using SolanaTradingBot.DataVendors;
using SolanaTradingBot.Engine;
using SolanaTradingBot.Execution;
using SolanaTradingBot.State;
using Microsoft.EntityFrameworkCore;
using Refit;
using Solnet.Rpc;

namespace SolanaTradingBot.CLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            var command = args.Length > 0 ? args[0].ToLower() : "help";

            Log.Information("Solana Autonomous Trading Bot - Command: {Command}", command);

            var host = CreateHostBuilder(args).Build();

            switch (command)
            {
                case "paper":
                    Log.Information("Starting paper trading mode...");
                    await RunPaperTrading(host);
                    break;
                    
                case "start":
                    Log.Information("Starting live trading mode...");
                    await RunLiveTrading(host, args);
                    break;
                    
                case "watch":
                    Log.Information("Starting market watch mode...");
                    await RunMarketWatch(host);
                    break;
                    
                case "test":
                    Log.Information("Running system test...");
                    await RunSystemTest(host);
                    break;
                    
                case "show-balances":
                    Log.Information("Showing wallet balances...");
                    await ShowBalances(host);
                    break;
                    
                case "ensure-atas":
                    Log.Information("Ensuring associated token accounts...");
                    await EnsureATAs(host);
                    break;
                    
                case "canary":
                    Log.Information("Running canary trade...");
                    await RunCanary(host);
                    break;
                    
                case "help":
                case "--help":
                case "-h":
                    ShowHelp();
                    return 0;
                    
                default:
                    Log.Error("Unknown command: {Command}", command);
                    ShowHelp();
                    return 1;
            }

            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                // Configuration
                services.Configure<TradingConfig>(context.Configuration);

                // Database
                services.AddDbContext<TradingDbContext>(options =>
                    options.UseSqlite("Data Source=trading.db"));
                
                services.AddScoped<IRepository, Repository>();

                // HTTP clients
                services.AddHttpClient();
                
                // RPC Client for Solana
                services.AddSingleton<IRpcClient>(provider =>
                {
                    var config = provider.GetRequiredService<IOptions<TradingConfig>>();
                    return ClientFactory.GetClient(config.Value.Helius.Http);
                });
                
                // API clients
                services.AddRefitClient<IJupiterApi>()
                    .ConfigureHttpClient((provider, client) =>
                    {
                        var config = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<TradingConfig>>();
                        client.BaseAddress = new Uri(config.Value.Jupiter.BaseUrl);
                    });
                    
                services.AddRefitClient<IBirdeyeApi>()
                    .ConfigureHttpClient(client =>
                    {
                        client.BaseAddress = new Uri("https://public-api.birdeye.so");
                    });

                // Data vendors
                services.AddScoped<IJupiterClient, JupiterClient>();
                services.AddScoped<IBirdeyeClient, BirdeyeClient>();

                // Execution
                services.AddScoped<IWalletService, WalletService>();
                services.AddScoped<IExecutor, Executor>();

                // Engine
                services.AddScoped<IGuards, Guards>();
                services.AddScoped<IStrategy, Strategy>();

                // Discovery
                services.AddSingleton<IPoolDetector, PoolDetector>();
                services.AddSingleton<IFlowScanner, FlowScanner>();
                services.AddSingleton<ICandidateSelector, CandidateSelector>();

                // Background services
                services.AddHostedService<PoolDetector>();
                services.AddHostedService<FlowScanner>();
                services.AddHostedService<CandidateSelector>();
            });

    static async Task RunPaperTrading(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var candidateSelector = scope.ServiceProvider.GetRequiredService<ICandidateSelector>();
        var strategy = scope.ServiceProvider.GetRequiredService<IStrategy>();

        logger.LogInformation("Paper trading mode started. Monitoring for trading opportunities...");
        logger.LogInformation("This is a simulation - no real trades will be executed.");

        // Subscribe to candidate events
        candidateSelector.CandidateSelected += async (candidate) =>
        {
            try
            {
                logger.LogInformation("Evaluating candidate: {TokenMint} from {Source}", 
                    candidate.TokenMint, candidate.Source);

                var result = await strategy.EvaluateEntryAsync(candidate);
                
                if (result.Signal == StrategySignal.Buy)
                {
                    logger.LogInformation("PAPER BUY: {TokenMint} - {Sleeve} - Size: {Size:F3} SOL - Reason: {Reason}",
                        candidate.TokenMint, result.Sleeve, result.SuggestedSizeSOL, result.Reason);
                }
                else
                {
                    logger.LogDebug("No entry signal for {TokenMint}: {Reason}", 
                        candidate.TokenMint, result.Reason);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error evaluating candidate {TokenMint}", candidate.TokenMint);
            }
        };

        // Start the host
        await host.StartAsync();

        logger.LogInformation("Press Ctrl+C to stop paper trading...");

        // Wait for cancellation
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await Task.Delay(-1, cts.Token);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Paper trading stopped.");
        }

        await host.StopAsync();
    }

    static async Task RunMarketWatch(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var candidateSelector = scope.ServiceProvider.GetRequiredService<ICandidateSelector>();

        logger.LogInformation("Market watch mode started. Monitoring discovery events...");

        // Subscribe to candidate events
        candidateSelector.CandidateSelected += (candidate) =>
        {
            logger.LogInformation("CANDIDATE: {TokenMint} | Source: {Source} | Score: {Score:F1} | Type: {Type}",
                candidate.TokenMint, candidate.Source, candidate.Score, candidate.Type);
        };

        // Start the host
        await host.StartAsync();

        logger.LogInformation("Press Ctrl+C to stop market watch...");

        // Wait for cancellation
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await Task.Delay(-1, cts.Token);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Market watch stopped.");
        }

        await host.StopAsync();
    }

    static async Task RunSystemTest(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var jupiterClient = scope.ServiceProvider.GetRequiredService<IJupiterClient>();
        var birdeyeClient = scope.ServiceProvider.GetRequiredService<IBirdeyeClient>();
        var guards = scope.ServiceProvider.GetRequiredService<IGuards>();

        logger.LogInformation("Running system tests...");

        try
        {
            // Test Jupiter client
            logger.LogInformation("Testing Jupiter client...");
            var price = await jupiterClient.GetTokenPriceAsync("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");
            logger.LogInformation("USDC price: ${Price}", price);

            // Test Birdeye client
            logger.LogInformation("Testing Birdeye client...");
            var trending = await birdeyeClient.GetTrendingTokensAsync(5);
            logger.LogInformation("Found {Count} trending tokens", trending.Count);

            // Test Guards
            logger.LogInformation("Testing Guards system...");
            if (trending.Any())
            {
                var testToken = trending.First().Address;
                var guardResults = await guards.RunAllGuardsAsync(testToken);
                var passed = guardResults.Count(g => g.Result == GuardResult.Pass);
                var failed = guardResults.Count(g => g.Result == GuardResult.Fail);
                logger.LogInformation("Guard results for {Token}: {Passed} passed, {Failed} failed", 
                    testToken, passed, failed);
            }

            logger.LogInformation("System test completed successfully!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "System test failed");
            throw;
        }
    }

    static void ShowHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Solana Autonomous Trading Bot");
        Console.WriteLine("============================");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  paper          Run in paper trading mode (simulation)");
        Console.WriteLine("  start [--yes]  Start live trading (requires wallet setup)");
        Console.WriteLine("  watch          Monitor market discovery events");
        Console.WriteLine("  test           Run system tests");
        Console.WriteLine("  show-balances  Display wallet SOL and token balances");
        Console.WriteLine("  ensure-atas    Ensure associated token accounts exist");
        Console.WriteLine("  canary         Run a small test trade to validate pipeline");
        Console.WriteLine("  help           Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run -- paper");
        Console.WriteLine("  dotnet run -- watch");
        Console.WriteLine("  dotnet run -- start --yes");
        Console.WriteLine("  dotnet run -- show-balances");
        Console.WriteLine();
        Console.WriteLine("Configuration:");
        Console.WriteLine("  Set environment variables or edit appsettings.json");
        Console.WriteLine("  See docs/Helius-and-env-setup.md for details");
        Console.WriteLine();
    }

    static async Task RunLiveTrading(IHost host, string[] args)
    {
        using var scope = host.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var candidateSelector = scope.ServiceProvider.GetRequiredService<ICandidateSelector>();
        var strategy = scope.ServiceProvider.GetRequiredService<IStrategy>();
        var executor = scope.ServiceProvider.GetRequiredService<IExecutor>();
        var walletService = scope.ServiceProvider.GetRequiredService<IWalletService>();

        // Safety check - require --yes flag to bypass prompt
        bool skipPrompt = args.Contains("--yes");
        
        if (!skipPrompt)
        {
            Console.WriteLine();
            Console.WriteLine("⚠️  WARNING: LIVE TRADING MODE ⚠️");
            Console.WriteLine("This will execute real trades with real money!");
            Console.WriteLine("Ensure you have:");
            Console.WriteLine("  - Configured your wallet properly");
            Console.WriteLine("  - Set appropriate risk limits");
            Console.WriteLine("  - Tested with paper trading first");
            Console.WriteLine();
            Console.Write("Type 'yes' to continue: ");
            
            var confirmation = Console.ReadLine();
            if (confirmation?.ToLower() != "yes")
            {
                logger.LogInformation("Live trading cancelled by user");
                return;
            }
        }

        try
        {
            // Validate wallet and configuration
            logger.LogInformation("Validating wallet and configuration...");
            await walletService.ValidateMinimumBalanceAsync();
            var balance = await walletService.GetSolBalanceAsync();
            logger.LogInformation("Wallet validation passed. SOL balance: {Balance} lamports", balance);

            logger.LogInformation("Live trading mode started. REAL TRADES WILL BE EXECUTED!");

            // Subscribe to candidate events
            candidateSelector.CandidateSelected += async (candidate) =>
            {
                try
                {
                    logger.LogInformation("Evaluating candidate: {TokenMint} from {Source}", 
                        candidate.TokenMint, candidate.Source);

                    var result = await strategy.EvaluateEntryAsync(candidate);
                    
                    if (result.Signal == StrategySignal.Buy)
                    {
                        logger.LogInformation("LIVE BUY SIGNAL: {TokenMint} - {Sleeve} - Size: {Size:F3} SOL - Reason: {Reason}",
                            candidate.TokenMint, result.Sleeve, result.SuggestedSizeSOL, result.Reason);

                        var executionContext = new SolanaTradingBot.Execution.ExecutionContext
                        {
                            Candidate = candidate,
                            Sleeve = result.Sleeve.ToString(),
                            SizeSOL = result.SuggestedSizeSOL,
                            SlippageBps = 100 // TODO: Calculate based on sleeve config
                        };

                        var executeResult = await executor.ExecuteBuyAsync(executionContext);
                        
                        if (executeResult == ExecuteResult.Success)
                        {
                            logger.LogInformation("LIVE BUY EXECUTED: {TokenMint} - Signature: {Signature}",
                                candidate.TokenMint, executionContext.TransactionSignature);
                        }
                        else
                        {
                            logger.LogError("LIVE BUY FAILED: {TokenMint} - Reason: {Error}",
                                candidate.TokenMint, executionContext.ErrorMessage);
                        }
                    }
                    else
                    {
                        logger.LogDebug("No entry signal for {TokenMint}: {Reason}", 
                            candidate.TokenMint, result.Reason);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error evaluating candidate {TokenMint}", candidate.TokenMint);
                }
            };

            // Start the host
            await host.StartAsync();

            logger.LogInformation("Press Ctrl+C to stop live trading...");

            // Wait for cancellation
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            try
            {
                await Task.Delay(-1, cts.Token);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Live trading stopped.");
            }

            await host.StopAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Live trading failed");
            throw;
        }
    }

    static async Task ShowBalances(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var walletService = scope.ServiceProvider.GetRequiredService<IWalletService>();

        try
        {
            logger.LogInformation("Wallet Public Key: {PublicKey}", walletService.PublicKey);
            
            var solBalance = await walletService.GetSolBalanceAsync();
            logger.LogInformation("SOL Balance: {Balance} lamports ({SOL:F6} SOL)", 
                solBalance, solBalance / 1_000_000_000.0m);

            // Check common token balances
            var commonTokens = new Dictionary<string, string>
            {
                { "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v", "USDC" },
                { "So11111111111111111111111111111111111111112", "WSOL" }
            };

            foreach (var (mint, symbol) in commonTokens)
            {
                try
                {
                    var balance = await walletService.GetTokenBalanceAsync(mint);
                    if (balance > 0)
                    {
                        logger.LogInformation("{Symbol} Balance: {Balance}", symbol, balance);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug("Could not get {Symbol} balance: {Error}", symbol, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to show balances");
            throw;
        }
    }

    static async Task EnsureATAs(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var walletService = scope.ServiceProvider.GetRequiredService<IWalletService>();

        try
        {
            var commonTokens = new[]
            {
                "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v", // USDC
                "So11111111111111111111111111111111111111112"  // WSOL
            };

            logger.LogInformation("Ensuring ATAs for common tokens...");
            await walletService.EnsureAssociatedTokenAccountsAsync(commonTokens);
            logger.LogInformation("ATA creation completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ensure ATAs");
            throw;
        }
    }

    static async Task RunCanary(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var executor = scope.ServiceProvider.GetRequiredService<IExecutor>();

        try
        {
            logger.LogInformation("Running canary trade (SOL<>USDC validation)...");
            logger.LogInformation("This would execute a small test trade to validate the pipeline");
            logger.LogInformation("⚠️ Canary mode not fully implemented - would execute real trade!");
            
            // For now, just log that we would execute a canary
            // In a real implementation, this would:
            // 1. Execute a very small SOL->USDC swap
            // 2. Immediately swap back USDC->SOL
            // 3. Log the signatures and slippage results
            // 4. Validate the pipeline works end-to-end
            
            await Task.Delay(100);
            logger.LogInformation("Canary trade simulation completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Canary trade failed");
            throw;
        }
    }
}
