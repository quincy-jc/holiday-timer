using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace HolidayTimer
{
    public sealed class AudioOutputDevice
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public AudioOutputDevice(string id, string name) { Id = id; Name = name; }
        public override string ToString() { return Name; }
    }

    public enum MusicCaptureState { Off, Opening, Listening, WaitingForAudio, Unavailable }

    internal interface IMusicCapture : IDisposable
    {
        AudioSignal Signal { get; }
        MusicCaptureState State { get; }
        string StatusMessage { get; }
        bool IsRunning { get; }
        void Start(string deviceId);
        void Stop();
    }

    // Listening describes a connected output, so a short pause between notes or
    // words should not make its status appear to disconnect. The actual signal
    // still decays normally and never keeps the artwork moving through silence.
    internal sealed class MusicActivityStatus
    {
        private double lastAudible = Double.NegativeInfinity;
        public MusicCaptureState Update(AudioSignal value, double seconds)
        {
            if (value.Level > .006) lastAudible = seconds;
            return seconds - lastAudible <= 1.25 ? MusicCaptureState.Listening : MusicCaptureState.WaitingForAudio;
        }
    }

    // WASAPI render loopback observes the selected speaker/headphone output. No capture
    // endpoint (microphone), third-party account, recording file, or network is involved.
    public sealed class MusicCapture : IMusicCapture
    {
        private readonly object gate = new object();
        private volatile CaptureRun current;
        private volatile AudioSignal signal = AudioSignal.Zero;
        private volatile MusicCaptureState state = MusicCaptureState.Off;
        private volatile string status = "Music response is off.";
        private bool disposed;
        public AudioSignal Signal { get { return signal; } }
        public MusicCaptureState State { get { return state; } }
        public string StatusMessage { get { return status; } }
        public bool IsRunning { get { return RunIsAlive(current); } }

        public static IList<AudioOutputDevice> GetOutputDevices()
        {
            List<AudioOutputDevice> outputs = new List<AudioOutputDevice>();
            int apartment = Native.CoInitializeEx(IntPtr.Zero, 0);
            if (apartment < 0 && apartment != unchecked((int)0x80010106)) return outputs;
            Native.IMMDeviceEnumerator enumerator = null;
            Native.IMMDeviceCollection collection = null;
            try
            {
                enumerator = Native.Enumerator();
                Native.Check(enumerator.EnumAudioEndpoints(0, 1, out collection));
                uint count; Native.Check(collection.GetCount(out count));
                for (uint index = 0; index < count; index++)
                {
                    Native.IMMDevice device = null;
                    try
                    {
                        Native.Check(collection.Item(index, out device));
                        outputs.Add(new AudioOutputDevice(Native.DeviceId(device), Native.FriendlyName(device)));
                    }
                    catch (COMException) { /* An endpoint may disappear during enumeration. */ }
                    finally { Native.Release(device); }
                }
                outputs.Sort(delegate(AudioOutputDevice left, AudioOutputDevice right)
                    { return StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name); });
            }
            catch (COMException) { }
            finally
            {
                Native.Release(collection); Native.Release(enumerator);
                if (apartment >= 0) Native.CoUninitialize();
            }
            return outputs.AsReadOnly();
        }

        public void Start(string deviceId)
        {
            CaptureRun previous;
            lock (gate)
            {
                if (disposed) throw new ObjectDisposedException("MusicCapture");
                string requested = String.IsNullOrEmpty(deviceId) ? null : deviceId;
                if (current != null && current.DeviceId == requested && RunIsAlive(current)) return;
                previous = CancelRun();
                CaptureRun run = new CaptureRun(requested);
                current = run; signal = AudioSignal.Zero;
                state = MusicCaptureState.Opening; status = "Opening audio output…";
                run.Thread = new Thread(delegate() { Capture(run); });
                run.Thread.IsBackground = true; run.Thread.Name = "Holiday Timer audio output";
                run.Thread.SetApartmentState(ApartmentState.MTA); run.Thread.Start();
            }
            JoinRun(previous);
        }

        public void Stop()
        {
            CaptureRun previous;
            lock (gate)
            {
                previous = CancelRun(); signal = AudioSignal.Zero; state = MusicCaptureState.Off;
                status = "Music response is off.";
            }
            JoinRun(previous);
        }

        public void Dispose()
        {
            CaptureRun previous;
            lock (gate)
            {
                if (disposed) return;
                disposed = true; previous = CancelRun(); signal = AudioSignal.Zero;
                state = MusicCaptureState.Off; status = "Music response is off.";
            }
            JoinRun(previous);
        }

        // Called under gate; detach and cancel before another source can become current.
        private CaptureRun CancelRun()
        {
            CaptureRun previous = current; current = null;
            if (previous != null) previous.Cancel();
            return previous;
        }

        // Never join under gate: the worker may be inside Publish. A slow audio driver
        // has only a short grace period; its canceled worker retains cleanup ownership.
        private static void JoinRun(CaptureRun previous)
        {
            if (previous != null && previous.Thread != null && previous.Thread != Thread.CurrentThread)
                previous.Thread.Join(40);
        }

        private static bool RunIsAlive(CaptureRun run)
        {
            return run != null && !run.IsCanceled && run.Thread != null && run.Thread.IsAlive;
        }

        private void Publish(CaptureRun run, AudioSignal value, MusicCaptureState next, string message)
        {
            lock (gate)
            {
                if (!ReferenceEquals(current, run) || run.IsCanceled) return;
                signal = value; state = next; status = message;
            }
        }

        private void Capture(CaptureRun run)
        {
            int apartment = Native.CoInitializeEx(IntPtr.Zero, 0);
            try
            {
                Native.Check(apartment);
                int failures = 0;
                while (!run.IsCanceled)
                {
                    try
                    {
                        ReadOutput(run);
                        failures = 0;
                    }
                    catch (Exception error)
                    {
                        if (run.IsCanceled) break;
                        if (!(error is COMException) && !(error is NotSupportedException) && !(error is InvalidOperationException))
                        {
                            Publish(run, AudioSignal.Zero, MusicCaptureState.Unavailable, "Audio output could not be opened.");
                            break;
                        }
                        failures = Math.Min(failures + 1, 4);
                        Publish(run, AudioSignal.Zero, MusicCaptureState.Unavailable,
                            "Audio unavailable. Check your output device.");
                        if (run.Wait(Math.Min(4000, failures * 750))) break;
                    }
                }
            }
            catch (COMException)
            {
                Publish(run, AudioSignal.Zero, MusicCaptureState.Unavailable, "Windows audio is unavailable.");
            }
            finally { if (apartment >= 0) Native.CoUninitialize(); run.Finish(); }
        }

        private void ReadOutput(CaptureRun run)
        {
            Native.IMMDeviceEnumerator enumerator = null;
            Native.IMMDevice device = null;
            Native.IAudioClient audio = null;
            Native.IAudioCaptureClient capture = null;
            IntPtr formatPointer = IntPtr.Zero;
            byte[] samples = null;
            bool started = false;
            try
            {
                enumerator = Native.Enumerator();
                Native.Check(run.DeviceId == null ? enumerator.GetDefaultAudioEndpoint(0, 1, out device) :
                    enumerator.GetDevice(run.DeviceId, out device));
                string activeId = Native.DeviceId(device);
                uint deviceState; Native.Check(device.GetState(out deviceState));
                if ((deviceState & 1) == 0) throw new InvalidOperationException("The output device is disconnected.");
                Guid iid = typeof(Native.IAudioClient).GUID; object service;
                Native.Check(device.Activate(ref iid, 23, IntPtr.Zero, out service));
                audio = (Native.IAudioClient)service;
                Native.Check(audio.GetMixFormat(out formatPointer));
                AudioSampleFormat format = Native.SampleFormat(formatPointer);
                Guid session = Guid.Empty;
                Native.Check(audio.Initialize(0, 0x00020000, 2000000, 0, formatPointer, ref session));
                iid = typeof(Native.IAudioCaptureClient).GUID;
                Native.Check(audio.GetService(ref iid, out service)); capture = (Native.IAudioCaptureClient)service;
                AudioAnalyzer analyzer = new AudioAnalyzer(format);
                MusicActivityStatus activity = new MusicActivityStatus();
                samples = new byte[Math.Max(4096, format.BlockAlign * format.SampleRate / 10)];
                Native.Check(audio.Start()); started = true;
                Publish(run, AudioSignal.Zero, MusicCaptureState.WaitingForAudio, "Output connected · waiting for audio.");
                Stopwatch clock = Stopwatch.StartNew();
                double published = 0, lastPacket = 0, decayedTo = 0, checkedDevice = 0;
                while (!run.IsCanceled)
                {
                    bool received = false;
                    uint available; Native.Check(capture.GetNextPacketSize(out available));
                    int packets = 0;
                    while (available != 0 && !run.IsCanceled && packets++ < 128)
                    {
                        IntPtr data; uint frames, flags; ulong position, performance;
                        Native.Check(capture.GetBuffer(out data, out frames, out flags, out position, out performance));
                        if (frames == 0) break;
                        try
                        {
                            bool silent = (flags & 2) != 0;
                            int length = checked((int)frames * format.BlockAlign);
                            if (!silent)
                            {
                                if (data == IntPtr.Zero) throw new InvalidOperationException("The audio packet is unavailable.");
                                if (samples.Length < length)
                                {
                                    Array.Clear(samples, 0, samples.Length); samples = new byte[length];
                                }
                                Marshal.Copy(data, samples, 0, length);
                            }
                            try { analyzer.Process(samples, checked((int)frames), silent, (flags & 1) != 0); }
                            finally { if (!silent) Array.Clear(samples, 0, length); }
                            received = true;
                        }
                        finally { Native.Check(capture.ReleaseBuffer(frames)); }
                        Native.Check(capture.GetNextPacketSize(out available));
                    }
                    double now = clock.Elapsed.TotalSeconds;
                    if (received) lastPacket = decayedTo = now;
                    else if (now - lastPacket > .08)
                    {
                        analyzer.Decay(Math.Max(0, now - decayedTo)); decayedTo = now;
                    }
                    if (now - published >= 1.0 / 30)
                    {
                        AudioSignal value = analyzer.Signal;
                        MusicCaptureState activityState = activity.Update(value, now);
                        Publish(run, value, activityState, activityState == MusicCaptureState.Listening ?
                            "Responding to your audio output." : "Output connected · waiting for audio.");
                        published = now;
                    }
                    if (now - checkedDevice >= 2)
                    {
                        Native.Check(device.GetState(out deviceState));
                        if ((deviceState & 1) == 0) throw new InvalidOperationException("The output device is disconnected.");
                        if (run.DeviceId == null)
                        {
                            Native.IMMDevice next = null;
                            try
                            {
                                Native.Check(enumerator.GetDefaultAudioEndpoint(0, 1, out next));
                                if (Native.DeviceId(next) != activeId) return;
                            }
                            finally { Native.Release(next); }
                        }
                        checkedDevice = now;
                    }
                    if (run.Wait(10)) break;
                }
            }
            finally
            {
                if (started && audio != null) audio.Stop();
                if (samples != null) Array.Clear(samples, 0, samples.Length);
                if (formatPointer != IntPtr.Zero) Marshal.FreeCoTaskMem(formatPointer);
                Native.Release(capture); Native.Release(audio); Native.Release(device); Native.Release(enumerator);
            }
        }

        private sealed class CaptureRun
        {
            private readonly object finishGate = new object();
            private readonly ManualResetEvent cancellation = new ManualResetEvent(false);
            private bool finished;
            public readonly string DeviceId;
            public Thread Thread;
            public volatile bool IsCanceled;
            public CaptureRun(string id) { DeviceId = id; }
            public bool Wait(int milliseconds) { return cancellation.WaitOne(milliseconds); }
            public void Cancel() { lock (finishGate) { IsCanceled = true; if (!finished) cancellation.Set(); } }
            public void Finish() { lock (finishGate) { IsCanceled = true; finished = true; cancellation.Dispose(); } }
        }

        private static class Native
        {
            [DllImport("ole32.dll")] internal static extern int CoInitializeEx(IntPtr reserved, uint apartment);
            [DllImport("ole32.dll")] internal static extern void CoUninitialize();
            [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant value);
            // Keep native failures in the retryable audio-error contract. Mapping
            // HRESULTs to managed ArgumentException, etc. can otherwise terminate
            // capture after an endpoint disappears or its identifier becomes stale.
            internal static void Check(int result)
            { if (result < 0) throw new COMException("Windows audio operation failed.", result); }
            internal static void Release(object value)
            {
                if (value != null && Marshal.IsComObject(value))
                    try { Marshal.FinalReleaseComObject(value); } catch (InvalidComObjectException) { }
            }
            internal static IMMDeviceEnumerator Enumerator()
            {
                return (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")));
            }
            internal static string DeviceId(IMMDevice device)
            {
                IntPtr value = IntPtr.Zero;
                try { Check(device.GetId(out value)); return Marshal.PtrToStringUni(value); }
                finally { if (value != IntPtr.Zero) Marshal.FreeCoTaskMem(value); }
            }
            internal static string FriendlyName(IMMDevice device)
            {
                IPropertyStore properties = null; PropVariant value = new PropVariant();
                try
                {
                    Check(device.OpenPropertyStore(0, out properties));
                    PropertyKey key = new PropertyKey { Format = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), Id = 14 };
                    Check(properties.GetValue(ref key, out value));
                    string name = value.Type == 31 ? Marshal.PtrToStringUni(value.Pointer) : null;
                    return String.IsNullOrEmpty(name) ? "Audio output" : name;
                }
                finally { PropVariantClear(ref value); Release(properties); }
            }
            internal static AudioSampleFormat SampleFormat(IntPtr pointer)
            {
                if (pointer == IntPtr.Zero) throw new NotSupportedException("No audio output format was provided.");
                ushort tag = unchecked((ushort)Marshal.ReadInt16(pointer, 0));
                int channels = unchecked((ushort)Marshal.ReadInt16(pointer, 2));
                int rate = Marshal.ReadInt32(pointer, 4);
                int align = unchecked((ushort)Marshal.ReadInt16(pointer, 12));
                int bits = unchecked((ushort)Marshal.ReadInt16(pointer, 14));
                bool floating = tag == 3;
                if (tag == 0xfffe)
                {
                    if (unchecked((ushort)Marshal.ReadInt16(pointer, 16)) < 22)
                        throw new NotSupportedException("The audio output format is incomplete.");
                    Guid subtype = (Guid)Marshal.PtrToStructure(IntPtr.Add(pointer, 24), typeof(Guid));
                    floating = subtype == new Guid("00000003-0000-0010-8000-00AA00389B71");
                    if (!floating && subtype != new Guid("00000001-0000-0010-8000-00AA00389B71"))
                        throw new NotSupportedException("The audio output format is not PCM.");
                }
                else if (tag != 1 && tag != 3) throw new NotSupportedException("The audio output format is not PCM.");
                return new AudioSampleFormat(rate, channels, bits, floating, align);
            }

            [StructLayout(LayoutKind.Sequential)] internal struct PropertyKey { internal Guid Format; internal uint Id; }
            [StructLayout(LayoutKind.Explicit, Size = 24)] internal struct PropVariant
            { [FieldOffset(0)] internal ushort Type; [FieldOffset(8)] internal IntPtr Pointer; }

            [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            internal interface IMMDeviceEnumerator
            {
                [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out IMMDeviceCollection devices);
                [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
                [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
                [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr callback);
                [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr callback);
            }
            [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            internal interface IMMDeviceCollection
            {
                [PreserveSig] int GetCount(out uint count);
                [PreserveSig] int Item(uint index, out IMMDevice device);
            }
            [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            internal interface IMMDevice
            {
                [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object service);
                [PreserveSig] int OpenPropertyStore(uint access, out IPropertyStore properties);
                [PreserveSig] int GetId(out IntPtr id);
                [PreserveSig] int GetState(out uint state);
            }
            [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            internal interface IPropertyStore
            {
                [PreserveSig] int GetCount(out uint count);
                [PreserveSig] int GetAt(uint index, out PropertyKey key);
                [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
                [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
                [PreserveSig] int Commit();
            }
            [ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            internal interface IAudioClient
            {
                [PreserveSig] int Initialize(int mode, uint flags, long duration, long periodicity, IntPtr format, ref Guid session);
                [PreserveSig] int GetBufferSize(out uint frames);
                [PreserveSig] int GetStreamLatency(out long latency);
                [PreserveSig] int GetCurrentPadding(out uint frames);
                [PreserveSig] int IsFormatSupported(int mode, IntPtr format, out IntPtr closest);
                [PreserveSig] int GetMixFormat(out IntPtr format);
                [PreserveSig] int GetDevicePeriod(out long normal, out long minimum);
                [PreserveSig] int Start();
                [PreserveSig] int Stop();
                [PreserveSig] int Reset();
                [PreserveSig] int SetEventHandle(IntPtr handle);
                [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
            }
            [ComImport, Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            internal interface IAudioCaptureClient
            {
                [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong position, out ulong performance);
                [PreserveSig] int ReleaseBuffer(uint frames);
                [PreserveSig] int GetNextPacketSize(out uint frames);
            }
        }
    }
}
