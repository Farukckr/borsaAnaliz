namespace BorsaAnaliz.Web.Services;

public static class IndicatorCalculator
{
    public static IReadOnlyList<decimal?> Sma(IReadOnlyList<decimal> values, int period)
    {
        ValidatePeriod(period);
        var result = new decimal?[values.Count];
        var sum = 0m;

        for (var index = 0; index < values.Count; index++)
        {
            sum += values[index];
            if (index >= period)
            {
                sum -= values[index - period];
            }

            if (index >= period - 1)
            {
                result[index] = sum / period;
            }
        }

        return result;
    }

    public static IReadOnlyList<decimal?> Ema(IReadOnlyList<decimal> values, int period)
    {
        ValidatePeriod(period);
        var result = new decimal?[values.Count];
        if (values.Count < period)
        {
            return result;
        }

        var seed = 0m;
        for (var index = 0; index < period; index++)
        {
            seed += values[index];
        }

        var previous = seed / period;
        result[period - 1] = previous;
        var multiplier = 2m / (period + 1);

        for (var index = period; index < values.Count; index++)
        {
            previous = ((values[index] - previous) * multiplier) + previous;
            result[index] = previous;
        }

        return result;
    }

    public static IReadOnlyList<decimal?> Rsi(IReadOnlyList<decimal> values, int period = 14)
    {
        ValidatePeriod(period);
        var result = new decimal?[values.Count];
        if (values.Count <= period)
        {
            return result;
        }

        var gainTotal = 0m;
        var lossTotal = 0m;
        for (var index = 1; index <= period; index++)
        {
            var change = values[index] - values[index - 1];
            gainTotal += Math.Max(change, 0m);
            lossTotal += Math.Max(-change, 0m);
        }

        var averageGain = gainTotal / period;
        var averageLoss = lossTotal / period;
        result[period] = CalculateRsiValue(averageGain, averageLoss);

        for (var index = period + 1; index < values.Count; index++)
        {
            var change = values[index] - values[index - 1];
            var gain = Math.Max(change, 0m);
            var loss = Math.Max(-change, 0m);
            averageGain = ((averageGain * (period - 1)) + gain) / period;
            averageLoss = ((averageLoss * (period - 1)) + loss) / period;
            result[index] = CalculateRsiValue(averageGain, averageLoss);
        }

        return result;
    }

    public static MacdValues Macd(
        IReadOnlyList<decimal> values,
        int fastPeriod = 12,
        int slowPeriod = 26,
        int signalPeriod = 9)
    {
        ValidatePeriod(fastPeriod);
        ValidatePeriod(slowPeriod);
        ValidatePeriod(signalPeriod);
        if (fastPeriod >= slowPeriod)
        {
            throw new ArgumentException("Hızlı EMA periyodu yavaş EMA periyodundan küçük olmalıdır.");
        }

        var fast = Ema(values, fastPeriod);
        var slow = Ema(values, slowPeriod);
        var macd = new decimal?[values.Count];
        var compactMacd = new List<decimal>();
        var compactIndexes = new List<int>();

        for (var index = 0; index < values.Count; index++)
        {
            if (fast[index] is not decimal fastValue || slow[index] is not decimal slowValue)
            {
                continue;
            }

            var value = fastValue - slowValue;
            macd[index] = value;
            compactMacd.Add(value);
            compactIndexes.Add(index);
        }

        var signal = new decimal?[values.Count];
        var histogram = new decimal?[values.Count];
        var compactSignal = Ema(compactMacd, signalPeriod);
        for (var compactIndex = 0; compactIndex < compactMacd.Count; compactIndex++)
        {
            if (compactSignal[compactIndex] is not decimal signalValue)
            {
                continue;
            }

            var sourceIndex = compactIndexes[compactIndex];
            signal[sourceIndex] = signalValue;
            histogram[sourceIndex] = compactMacd[compactIndex] - signalValue;
        }

        return new MacdValues(macd, signal, histogram);
    }

    public static BollingerValues Bollinger(
        IReadOnlyList<decimal> values,
        int period = 20,
        decimal standardDeviations = 2m)
    {
        ValidatePeriod(period);
        if (standardDeviations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(standardDeviations));
        }

        var middle = Sma(values, period);
        var upper = new decimal?[values.Count];
        var lower = new decimal?[values.Count];

        for (var index = period - 1; index < values.Count; index++)
        {
            var mean = middle[index]!.Value;
            var variance = 0m;
            for (var offset = 0; offset < period; offset++)
            {
                var difference = values[index - offset] - mean;
                variance += difference * difference;
            }

            var standardDeviation = (decimal)Math.Sqrt((double)(variance / period));
            upper[index] = mean + (standardDeviation * standardDeviations);
            lower[index] = mean - (standardDeviation * standardDeviations);
        }

        return new BollingerValues(middle, upper, lower);
    }

    private static decimal CalculateRsiValue(decimal averageGain, decimal averageLoss)
    {
        if (averageGain == 0 && averageLoss == 0)
        {
            return 50m;
        }

        if (averageLoss == 0)
        {
            return 100m;
        }

        var relativeStrength = averageGain / averageLoss;
        return 100m - (100m / (1m + relativeStrength));
    }

    private static void ValidatePeriod(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period));
        }
    }
}

public sealed record MacdValues(
    IReadOnlyList<decimal?> Macd,
    IReadOnlyList<decimal?> Signal,
    IReadOnlyList<decimal?> Histogram);

public sealed record BollingerValues(
    IReadOnlyList<decimal?> Middle,
    IReadOnlyList<decimal?> Upper,
    IReadOnlyList<decimal?> Lower);
