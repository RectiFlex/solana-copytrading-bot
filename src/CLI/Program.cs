using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using SolanaTradingBot.Common;
using SolanaTradingBot.Discovery;
using SolanaTradingBot.DataVendors;
using SolanaTradingBot.Engine;
using SolanaTradingBot.State;
using Microsoft.EntityFrameworkCore;
using Refit;

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
                    Log.Warning("Live trading mode not yet implemented");
                    Log.Information("Use 'paper' mode for simulation");
                    return 1;
                    
                case "watch":
                    Log.Information("Starting market watch mode...");
                    await RunMarketWatch(host);
                    break;
                    
                case "test":
                    Log.Information("Running system test...");
                    await RunSystemTest(host);
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
        Console.WriteLine("  paper     Run in paper trading mode (simulation)");
        Console.WriteLine("  start     Start live trading (not implemented)");
        Console.WriteLine("  watch     Monitor market discovery events");
        Console.WriteLine("  test      Run system tests");
        Console.WriteLine("  help      Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run -- paper");
        Console.WriteLine("  dotnet run -- watch");
        Console.WriteLine("  dotnet run -- test");
        Console.WriteLine();
        Console.WriteLine("Configuration:");
        Console.WriteLine("  Edit appsettings.json to configure trading parameters");
        Console.WriteLine("  Set API keys in appsettings.Development.json");
        Console.WriteLine();
    }
}
