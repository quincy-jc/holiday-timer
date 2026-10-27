using System;
using HolidayTimer;

internal static class MusicTests
{
    private static int checks;
    private static int Main()
    {
        try
        {
            TestBands(); TestFormats(); TestStereo(); TestSilenceAndDiscontinuity();
            TestTransients(); TestInvalidData(); TestSignalBounds();
            Console.WriteLine("PASS " + checks + " audio analysis checks."); return 0;
        }
        catch (Exception error) { Console.WriteLine("FAIL: " + error.Message); return 1; }
    }

    private static void Check(bool condition, string description)
    { checks++; if (!condition) throw new Exception(description); }

    private static void Reject(Action action, string description)
    {
        bool rejected = false;
        try { action(); } catch (ArgumentException) { rejected = true; } catch (NotSupportedException) { rejected = true; }
        Check(rejected, description);
    }

    private static AudioSampleFormat Format(int bits, bool floating, int channels)
    { return new AudioSampleFormat(48000, channels, bits, floating, channels * (bits / 8)); }

    private static AudioSignal Tone(int bits, bool floating, int channels, double amplitude, double frequency, bool opposite)
    {
        AudioSampleFormat format = Format(bits, floating, channels);
        AudioAnalyzer analyzer = new AudioAnalyzer(format);
        return analyzer.Process(Samples(format, 48000, amplitude, frequency, opposite), 48000, false, false);
    }

    private static byte[] Samples(AudioSampleFormat format, int frames, double amplitude, double frequency, bool opposite)
    {
        byte[] bytes = new byte[frames * format.BlockAlign];
        for (int frame = 0; frame < frames; frame++)
            for (int channel = 0; channel < format.Channels; channel++)
            {
                double sample = Math.Sin(frame * frequency * 2 * Math.PI / format.SampleRate) * amplitude;
                if (opposite && channel % 2 == 1) sample = -sample;
                int offset = frame * format.BlockAlign + channel * (format.BitsPerSample / 8);
                if (format.IsFloat)
                {
                    byte[] encoded = format.BitsPerSample == 32 ? BitConverter.GetBytes((float)sample) : BitConverter.GetBytes(sample);
                    Buffer.BlockCopy(encoded, 0, bytes, offset, encoded.Length);
                }
                else if (format.BitsPerSample == 8) bytes[offset] = (byte)Math.Round(sample * 127 + 128);
                else
                {
                    long maximum = format.BitsPerSample == 32 ? 2147483647L : (1L << (format.BitsPerSample - 1)) - 1;
                    long encoded = (long)Math.Round(sample * maximum);
                    for (int part = 0; part < format.BitsPerSample / 8; part++) bytes[offset + part] = (byte)(encoded >> (part * 8));
                }
            }
        return bytes;
    }

    private static void Normalized(AudioSignal signal, string label)
    {
        foreach (double value in new double[] { signal.Level, signal.Bass, signal.Mid, signal.High, signal.Beat })
            Check(!Double.IsNaN(value) && !Double.IsInfinity(value) && value >= 0 && value <= 1, label + " is a finite normalized snapshot");
    }

    private static void TestBands()
    {
        AudioSignal bass = Tone(32, true, 2, .18, 80, false);
        AudioSignal middle = Tone(32, true, 2, .18, 1000, false);
        AudioSignal treble = Tone(32, true, 2, .18, 8000, false);
        Check(bass.Bass > bass.Mid * 1.25 && bass.Bass > bass.High * 2, "80 Hz predominantly drives bass");
        Check(middle.Mid > middle.Bass * 1.2 && middle.Mid > middle.High * 1.2, "1 kHz predominantly drives midrange");
        Check(treble.High > treble.Bass * 1.5 && treble.High > treble.Mid * 1.5, "8 kHz predominantly drives treble");
        Normalized(bass, "Bass tone"); Normalized(middle, "Mid tone"); Normalized(treble, "Treble tone");
    }

    private static void TestFormats()
    {
        AudioSignal reference = Tone(32, true, 1, .2, 1000, false);
        int[] bits = { 8, 16, 24, 32, 32, 64 };
        bool[] floating = { false, false, false, false, true, true };
        for (int index = 0; index < bits.Length; index++)
        {
            AudioSignal signal = Tone(bits[index], floating[index], 1, .2, 1000, false);
            string name = (floating[index] ? "Float" : "PCM") + bits[index];
            Check(Math.Abs(signal.Level - reference.Level) < .012, name + " preserves amplitude across sample encodings");
            Check(Math.Abs(signal.Mid - reference.Mid) < .012, name + " preserves band energy across sample encodings");
            Normalized(signal, name);
        }
        AudioSignal quiet = Tone(16, false, 1, .005, 1000, false);
        AudioSignal loud = Tone(16, false, 1, .25, 1000, false);
        Check(loud.Level > quiet.Level * 3, "Louder audio produces a meaningfully stronger response");
        Check(Tone(32, true, 1, 1, 1000, false).Level <= 1, "Full-scale audio remains bounded");
    }

    private static void TestStereo()
    {
        AudioSignal same = Tone(32, true, 2, .2, 1000, false);
        AudioSignal opposite = Tone(32, true, 2, .2, 1000, true);
        AudioSignal mono = Tone(32, true, 1, .2, 1000, false);
        Check(Math.Abs(same.Level - opposite.Level) < .0001, "Opposite-phase stereo does not cancel the output level");
        Check(Math.Abs(same.Mid - opposite.Mid) < .0001, "Opposite-phase stereo preserves spectral response");
        Check(Math.Abs(same.Level - mono.Level) < .0001, "Identical channels do not inflate the output level");
        AudioSampleFormat padded = new AudioSampleFormat(48000, 1, 16, false, 4);
        AudioAnalyzer analyzer = new AudioAnalyzer(padded);
        AudioSignal padding = analyzer.Process(Samples(padded, 48000, .2, 1000, false), 48000, false, false);
        Check(Math.Abs(padding.Level - mono.Level) < .002, "Frame padding is excluded from sample energy");
    }

    private static void TestSilenceAndDiscontinuity()
    {
        AudioSampleFormat format = Format(32, true, 2);
        AudioAnalyzer analyzer = new AudioAnalyzer(format);
        Check(analyzer.Process(null, 480, true, false).Level == 0, "A WASAPI silent packet needs no readable buffer");
        Check(analyzer.Decay(1).Beat == 0, "Silence does not fabricate beats");
        byte[] music = Samples(format, 48000, .25, 80, false);
        analyzer.Process(music, 48000, false, false);
        AudioSignal previous = analyzer.Signal;
        AudioSignal release = analyzer.Process(null, 4800, true, false);
        Check(release.Level < previous.Level && release.Level > 0, "A brief audio pause releases smoothly");
        AudioSignal silent = analyzer.Decay(10);
        Check(silent.Level == 0 && silent.Bass == 0 && silent.Mid == 0 && silent.High == 0 && silent.Beat == 0,
            "Prolonged silence settles to exact zero in every band");
        Check(analyzer.Process(new byte[format.BlockAlign * 480], 480, false, false).Level == 0,
            "Real zero samples remain silent after the filters settle");
        analyzer.Process(music, 48000, false, false);
        AudioSignal discontinuity = analyzer.Process(null, 480, true, true);
        Check(discontinuity.Level == 0 && discontinuity.Beat == 0, "A discontinuity clears the envelope and transient history");
        analyzer.Process(music, 48000, false, false); analyzer.Reset();
        Check(analyzer.Signal.Level == 0 && analyzer.Signal.Bass == 0, "Explicit reset clears the signal");
        Check(analyzer.Process(null, 0, false, false).Level == 0, "An empty packet keeps the current silent state");
    }

    private static void TestTransients()
    {
        AudioSampleFormat format = Format(32, true, 1);
        AudioAnalyzer analyzer = new AudioAnalyzer(format);
        byte[] quiet = Samples(format, 480, .005, 1000, false);
        byte[] loud = Samples(format, 480, .8, 1000, false);
        Check(analyzer.Process(loud, 480, false, false).Beat == 0, "Opening mid-song does not invent a first-frame beat");
        analyzer.Reset();
        for (int index = 0; index < 60; index++) analyzer.Process(quiet, 480, false, false);
        for (int index = 0; index < 10; index++) analyzer.Process(loud, 480, false, false);
        Check(analyzer.Signal.Beat > .2, "A clear audio onset generates a transient response");
        for (int index = 0; index < 500; index++) analyzer.Process(loud, 480, false, false);
        Check(analyzer.Signal.Beat < .01, "An unchanging tone does not keep generating beats");
        analyzer.Decay(10);
        Check(analyzer.Signal.Beat == 0, "Beat energy settles completely when playback stops");
    }

    private static void TestInvalidData()
    {
        Reject(delegate { Format(12, false, 1); }, "Unsupported PCM sizes are rejected");
        Reject(delegate { Format(16, true, 1); }, "Unsupported floating-point sizes are rejected");
        Reject(delegate { new AudioSampleFormat(0, 1, 16, false, 2); }, "Invalid sample rates are rejected");
        Reject(delegate { new AudioSampleFormat(48000, 0, 16, false, 0); }, "Invalid channel counts are rejected");
        Reject(delegate { new AudioSampleFormat(48000, 2, 16, false, 2); }, "Incomplete frame alignment is rejected");
        Reject(delegate { new AudioAnalyzer(null); }, "A null sample format is rejected");
        AudioAnalyzer analyzer = new AudioAnalyzer(Format(32, true, 1));
        Reject(delegate { analyzer.Process(null, 20, false, false); }, "A missing nonsilent buffer is rejected");
        Reject(delegate { analyzer.Process(new byte[4], 20, false, false); }, "A truncated nonsilent packet is rejected");
        Reject(delegate { analyzer.Process(null, -1, true, false); }, "Negative packet frames are rejected");
        Reject(delegate { analyzer.Process(null, Int32.MaxValue, true, false); }, "Packet size overflow is rejected");
        Reject(delegate { analyzer.Decay(-1); }, "Negative silence duration is rejected");
        Reject(delegate { analyzer.Decay(Double.NaN); }, "Nonfinite silence duration is rejected");
        foreach (float value in new float[] { Single.NaN, Single.PositiveInfinity, Single.NegativeInfinity })
        {
            analyzer.Reset(); byte[] data = BitConverter.GetBytes(value);
            AudioSignal result = analyzer.Process(data, 1, false, false);
            Check(result.Level == 0 && result.Bass == 0 && result.High == 0, "Nonfinite audio samples are treated as silence");
        }
    }

    private static void TestSignalBounds()
    {
        AudioSignal snapshot = new AudioSignal(-1, 2, Double.NaN, Double.PositiveInfinity, .5);
        Check(snapshot.Level == 0 && snapshot.Bass == 1 && snapshot.Mid == 0 && snapshot.High == 0 && snapshot.Beat == .5,
            "Published signal values remain safe normalized scalars");
        foreach (string name in new string[] { "Level", "Bass", "Mid", "High", "Beat" })
            Check(typeof(AudioSignal).GetProperty(name).GetSetMethod() == null, name + " has no public setter");
    }
}
