namespace Xylocopadream.Audio;

/// <summary>
/// Changes the sample rate of a whole sound, once, when it is loaded: a sinc interpolation with a Blackman window,
/// its cut-off lowered when the rate goes down so that nothing folds back into the audible band.
/// </summary>
public static class Resampler
{
    /// <summary>Samples on each side of the point computed: more is cleaner and slower.</summary>
    private const int HalfWidth = 16;

    public static float[] Resample(ReadOnlySpan<float> input, int fromRate, int toRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fromRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(toRate);
        if (fromRate == toRate || input.IsEmpty)
            return input.ToArray();

        double step = fromRate / (double)toRate;
        // going down: the filter is widened to keep only what the new rate can hold
        double cutoff = Math.Min(1.0, toRate / (double)fromRate);
        int reach = (int)Math.Ceiling(HalfWidth / cutoff);

        int length = (int)Math.Ceiling(input.Length / step);
        float[] output = new float[length];
        for (int i = 0; i < length; i++)
        {
            double position = i * step;
            int center = (int)Math.Floor(position);
            double sum = 0;
            double weights = 0;
            for (int k = center - reach + 1; k <= center + reach; k++)
            {
                double distance = position - k;
                double weight = cutoff * Sinc(cutoff * distance) * Blackman(distance / reach);
                weights += weight;
                if (k >= 0 && k < input.Length)
                    sum += input[k] * weight;
            }

            // the weights are normalized: a constant stays the same constant
            output[i] = weights == 0 ? 0 : (float)(sum / weights);
        }

        return output;
    }

    private static double Sinc(double x) => x == 0 ? 1 : Math.Sin(Math.PI * x) / (Math.PI * x);

    // x from -1 to 1: 1 in the middle, 0 at both ends
    private static double Blackman(double x)
        => Math.Abs(x) >= 1 ? 0 : 0.42 + 0.5 * Math.Cos(Math.PI * x) + 0.08 * Math.Cos(2 * Math.PI * x);
}
