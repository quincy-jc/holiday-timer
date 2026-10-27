using System;

namespace HolidayTimer
{
    public sealed class AudioSignal
    {
        public static readonly AudioSignal Zero = new AudioSignal(0, 0, 0, 0, 0);
        public double Level { get; private set; }
        public double Bass { get; private set; }
        public double Mid { get; private set; }
        public double High { get; private set; }
        public double Beat { get; private set; }

        public AudioSignal(double level, double bass, double mid, double high, double beat)
        {
            Level = Bound(level); Bass = Bound(bass); Mid = Bound(mid); High = Bound(high); Beat = Bound(beat);
        }

        private static double Bound(double value)
        {
            return Double.IsNaN(value) || Double.IsInfinity(value) ? 0 : Math.Max(0, Math.Min(1, value));
        }
    }

    internal sealed class AudioSampleFormat
    {
        public int SampleRate { get; private set; }
        public int Channels { get; private set; }
        public int BitsPerSample { get; private set; }
        public bool IsFloat { get; private set; }
        public int BlockAlign { get; private set; }

        public AudioSampleFormat(int sampleRate, int channels, int bitsPerSample, bool isFloat, int blockAlign)
        {
            if (sampleRate < 8000 || sampleRate > 384000 || channels < 1 || channels > 32)
                throw new NotSupportedException("This audio output format is not supported.");
            if (isFloat ? bitsPerSample != 32 && bitsPerSample != 64 :
                bitsPerSample != 8 && bitsPerSample != 16 && bitsPerSample != 24 && bitsPerSample != 32)
                throw new NotSupportedException("This audio output format is not supported.");
            if (blockAlign < channels * (bitsPerSample / 8) || blockAlign > 4096)
                throw new NotSupportedException("This audio output format is not supported.");
            SampleRate = sampleRate; Channels = channels; BitsPerSample = bitsPerSample;
            IsFloat = isFloat; BlockAlign = blockAlign;
        }
    }

    // Only short-lived sample blocks are examined. Neither this analyzer nor the capture
    // service writes audio to disk, and samples are overwritten on the next capture pass.
    internal sealed class AudioAnalyzer
    {
        private readonly AudioSampleFormat format;
        private readonly LowPass[] lows, middles;
        private AudioSignal signal = AudioSignal.Zero;
        private double baseline, elapsed;
        public AudioSignal Signal { get { return signal; } }

        public AudioAnalyzer(AudioSampleFormat format)
        {
            if (format == null) throw new ArgumentNullException("format");
            this.format = format;
            lows = new LowPass[format.Channels]; middles = new LowPass[format.Channels];
            for (int channel = 0; channel < format.Channels; channel++)
            {
                lows[channel] = new LowPass(220, format.SampleRate);
                middles[channel] = new LowPass(Math.Min(3000, format.SampleRate * .35), format.SampleRate);
            }
        }

        public AudioSignal Process(byte[] data, int frames, bool silent, bool discontinuity)
        {
            if (frames < 0 || frames > Int32.MaxValue / format.BlockAlign)
                throw new ArgumentOutOfRangeException("frames");
            if (discontinuity) Reset();
            if (frames == 0) return signal;
            double seconds = frames / (double)format.SampleRate;
            if (silent) return Decay(seconds);
            if (data == null || data.Length < frames * format.BlockAlign)
                throw new ArgumentException("The audio packet is incomplete.", "data");

            double total = 0, lowTotal = 0, midTotal = 0, highTotal = 0;
            int bytes = format.BitsPerSample / 8;
            for (int frame = 0; frame < frames; frame++)
            {
                int row = frame * format.BlockAlign;
                for (int channel = 0; channel < format.Channels; channel++)
                {
                    double sample = Read(data, row + channel * bytes);
                    double low = lows[channel].Filter(sample);
                    double middle = middles[channel].Filter(sample);
                    double mid = middle - low, high = sample - middle;
                    total += sample * sample; lowTotal += low * low;
                    midTotal += mid * mid; highTotal += high * high;
                }
            }
            double count = (double)frames * format.Channels;
            double rms = Math.Sqrt(total / count);
            if (rms < .00008) return Decay(seconds);
            double level = Normalize(rms);
            double bass = Normalize(Math.Sqrt(lowTotal / count));
            double midBand = Normalize(Math.Sqrt(midTotal / count));
            double highBand = Normalize(Math.Sqrt(highTotal / count));
            // A transient needs an established energy floor. Starting capture in the
            // middle of a song therefore does not fabricate a first-frame beat.
            double transient = elapsed >= .18 ? Math.Max(0, level - baseline - .045) * 4 : 0;
            baseline += (level - baseline) * (1 - Math.Exp(-seconds / .65));
            elapsed += seconds;
            signal = new AudioSignal(Smooth(signal.Level, level, seconds, .035, .28),
                Smooth(signal.Bass, bass, seconds, .045, .32),
                Smooth(signal.Mid, midBand, seconds, .04, .25),
                Smooth(signal.High, highBand, seconds, .025, .18),
                Smooth(signal.Beat, Math.Min(1, transient), seconds, .018, .16));
            return signal;
        }

        public AudioSignal Decay(double seconds)
        {
            if (Double.IsNaN(seconds) || Double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException("seconds");
            seconds = Math.Min(seconds, 10);
            baseline *= Math.Exp(-seconds / .65); elapsed += seconds;
            signal = new AudioSignal(Cut(signal.Level * Math.Exp(-seconds / .28)),
                Cut(signal.Bass * Math.Exp(-seconds / .32)),
                Cut(signal.Mid * Math.Exp(-seconds / .25)),
                Cut(signal.High * Math.Exp(-seconds / .18)),
                Cut(signal.Beat * Math.Exp(-seconds / .16)));
            if (signal.Level == 0)
                for (int channel = 0; channel < format.Channels; channel++) { lows[channel].Reset(); middles[channel].Reset(); }
            return signal;
        }

        public void Reset()
        {
            signal = AudioSignal.Zero; baseline = elapsed = 0;
            for (int channel = 0; channel < format.Channels; channel++) { lows[channel].Reset(); middles[channel].Reset(); }
        }

        private static double Normalize(double rms) { return Math.Min(1, Math.Sqrt(Math.Max(0, rms)) * 1.45); }
        private static double Cut(double value) { return value < .0005 ? 0 : value; }
        private static double Smooth(double previous, double current, double seconds, double attack, double release)
        {
            return previous + (current - previous) * (1 - Math.Exp(-seconds / (current > previous ? attack : release)));
        }

        private double Read(byte[] bytes, int offset)
        {
            double sample;
            if (format.IsFloat)
                sample = format.BitsPerSample == 32 ? BitConverter.ToSingle(bytes, offset) : BitConverter.ToDouble(bytes, offset);
            else if (format.BitsPerSample == 8) sample = (bytes[offset] - 128) / 128.0;
            else if (format.BitsPerSample == 16) sample = (short)(bytes[offset] | bytes[offset + 1] << 8) / 32768.0;
            else if (format.BitsPerSample == 24)
            {
                int raw = bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16;
                if ((raw & 0x800000) != 0) raw |= unchecked((int)0xff000000);
                sample = raw / 8388608.0;
            }
            else sample = (bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16 | bytes[offset + 3] << 24) / 2147483648.0;
            return Double.IsNaN(sample) || Double.IsInfinity(sample) ? 0 : Math.Max(-1, Math.Min(1, sample));
        }

        private sealed class LowPass
        {
            private readonly double b0, b1, b2, a1, a2;
            private double x1, x2, y1, y2;
            public LowPass(double cutoff, int rate)
            {
                double omega = 2 * Math.PI * cutoff / rate, cosine = Math.Cos(omega);
                double alpha = Math.Sin(omega) / Math.Sqrt(2), inverse = 1 / (1 + alpha);
                b0 = (1 - cosine) * .5 * inverse; b1 = (1 - cosine) * inverse; b2 = b0;
                a1 = -2 * cosine * inverse; a2 = (1 - alpha) * inverse;
            }
            public double Filter(double sample)
            {
                double result = b0 * sample + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = sample; y2 = y1; y1 = result;
                return result;
            }
            public void Reset() { x1 = x2 = y1 = y2 = 0; }
        }
    }
}
