using SolanaTradingBot.DataVendors;

namespace SolanaTradingBot.Engine;

public static class Indicators
{
    // Exponential Moving Average
    public static decimal CalculateEMA(IEnumerable<decimal> prices, int period)
    {
        var priceList = prices.ToList();
        if (priceList.Count < period)
            return 0m;

        var multiplier = 2m / (period + 1);
        var ema = priceList.Take(period).Average();

        for (int i = period; i < priceList.Count; i++)
        {
            ema = (priceList[i] * multiplier) + (ema * (1 - multiplier));
        }

        return ema;
    }

    // Volume Weighted Average Price
    public static decimal CalculateVWAP(IEnumerable<OhlcvItem> candles, int window)
    {
        var candleList = candles.TakeLast(window).ToList();
        if (!candleList.Any())
            return 0m;

        var totalVolumePrice = candleList.Sum(c => c.Close * c.Volume);
        var totalVolume = candleList.Sum(c => c.Volume);

        return totalVolume > 0 ? totalVolumePrice / totalVolume : 0m;
    }

    // Average True Range (volatility indicator)
    public static decimal CalculateATR(IEnumerable<OhlcvItem> candles, int period)
    {
        var candleList = candles.ToList();
        if (candleList.Count < 2)
            return 0m;

        var trueRanges = new List<decimal>();
        
        for (int i = 1; i < candleList.Count; i++)
        {
            var current = candleList[i];
            var previous = candleList[i - 1];
            
            var highLow = current.High - current.Low;
            var highClose = Math.Abs(current.High - previous.Close);
            var lowClose = Math.Abs(current.Low - previous.Close);
            
            var trueRange = Math.Max(highLow, Math.Max(highClose, lowClose));
            trueRanges.Add(trueRange);
        }

        return trueRanges.TakeLast(period).Average();
    }

    // Buyer to Seller ratio from candle data
    public static decimal CalculateBuyerSellerRatio(IEnumerable<OhlcvItem> candles, int window)
    {
        var candleList = candles.TakeLast(window).ToList();
        if (!candleList.Any())
            return 0m;

        var totalBuyVolume = candleList.Sum(c => c.VolumeBuy);
        var totalSellVolume = candleList.Sum(c => c.VolumeSell);

        return totalSellVolume > 0 ? totalBuyVolume / totalSellVolume : 0m;
    }

    // Net inflow calculation
    public static decimal CalculateNetInflow(IEnumerable<OhlcvItem> candles, int window)
    {
        var candleList = candles.TakeLast(window).ToList();
        if (!candleList.Any())
            return 0m;

        var totalBuyVolume = candleList.Sum(c => c.VolumeBuy);
        var totalSellVolume = candleList.Sum(c => c.VolumeSell);

        return totalBuyVolume - totalSellVolume;
    }

    // Price momentum (rate of change)
    public static decimal CalculateMomentum(IEnumerable<decimal> prices, int period)
    {
        var priceList = prices.ToList();
        if (priceList.Count < period + 1)
            return 0m;

        var currentPrice = priceList.Last();
        var pastPrice = priceList[^(period + 1)];

        return pastPrice > 0 ? ((currentPrice - pastPrice) / pastPrice) * 100 : 0m;
    }

    // RSI (Relative Strength Index)
    public static decimal CalculateRSI(IEnumerable<decimal> prices, int period = 14)
    {
        var priceList = prices.ToList();
        if (priceList.Count < period + 1)
            return 50m; // Neutral RSI

        var gains = new List<decimal>();
        var losses = new List<decimal>();

        for (int i = 1; i < priceList.Count; i++)
        {
            var change = priceList[i] - priceList[i - 1];
            gains.Add(change > 0 ? change : 0);
            losses.Add(change < 0 ? Math.Abs(change) : 0);
        }

        var avgGain = gains.TakeLast(period).Average();
        var avgLoss = losses.TakeLast(period).Average();

        if (avgLoss == 0)
            return 100m;

        var rs = avgGain / avgLoss;
        return 100m - (100m / (1 + rs));
    }

    // Bollinger Bands
    public static (decimal Upper, decimal Middle, decimal Lower) CalculateBollingerBands(
        IEnumerable<decimal> prices, int period = 20, decimal stdDevMultiplier = 2m)
    {
        var priceList = prices.TakeLast(period).ToList();
        if (priceList.Count < period)
            return (0m, 0m, 0m);

        var sma = priceList.Average();
        var variance = priceList.Sum(p => (p - sma) * (p - sma)) / period;
        var stdDev = (decimal)Math.Sqrt((double)variance);

        var upper = sma + (stdDevMultiplier * stdDev);
        var lower = sma - (stdDevMultiplier * stdDev);

        return (upper, sma, lower);
    }

    // Check if price is trending up based on moving averages
    public static bool IsTrendingUp(IEnumerable<decimal> prices, int shortPeriod = 9, int longPeriod = 20)
    {
        var shortEMA = CalculateEMA(prices, shortPeriod);
        var longEMA = CalculateEMA(prices, longPeriod);

        return shortEMA > longEMA && shortEMA > 0 && longEMA > 0;
    }

    // Check for consecutive green candles
    public static bool HasConsecutiveGreenCandles(IEnumerable<OhlcvItem> candles, int count)
    {
        var candleList = candles.TakeLast(count).ToList();
        if (candleList.Count < count)
            return false;

        return candleList.All(c => c.Close > c.Open);
    }

    // Check for rising volume
    public static bool IsVolumeRising(IEnumerable<OhlcvItem> candles, int count)
    {
        var candleList = candles.TakeLast(count).ToList();
        if (candleList.Count < count)
            return false;

        for (int i = 1; i < candleList.Count; i++)
        {
            if (candleList[i].Volume <= candleList[i - 1].Volume)
                return false;
        }

        return true;
    }

    // Calculate price change percentage
    public static decimal CalculatePriceChangePercent(decimal currentPrice, decimal previousPrice)
    {
        if (previousPrice <= 0)
            return 0m;

        return ((currentPrice - previousPrice) / previousPrice) * 100m;
    }

    // Check if price is above VWAP
    public static bool IsPriceAboveVWAP(decimal currentPrice, IEnumerable<OhlcvItem> candles, int window)
    {
        var vwap = CalculateVWAP(candles, window);
        return currentPrice > vwap && vwap > 0;
    }

    // Calculate volatility as percentage of average price
    public static decimal CalculateVolatility(IEnumerable<decimal> prices, int period)
    {
        var priceList = prices.TakeLast(period).ToList();
        if (priceList.Count < 2)
            return 0m;

        var returns = new List<decimal>();
        for (int i = 1; i < priceList.Count; i++)
        {
            if (priceList[i - 1] > 0)
            {
                returns.Add((priceList[i] - priceList[i - 1]) / priceList[i - 1]);
            }
        }

        if (!returns.Any())
            return 0m;

        var meanReturn = returns.Average();
        var variance = returns.Sum(r => (r - meanReturn) * (r - meanReturn)) / returns.Count;
        
        return (decimal)Math.Sqrt((double)variance) * 100m; // Return as percentage
    }
}