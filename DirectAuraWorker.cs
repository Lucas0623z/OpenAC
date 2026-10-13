using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32.SafeHandles;

// Native Windows HID adapter; no ASUS SDK, HAL, service, or kernel driver.
// Protocol reference: dvalfrid/rigstats, AuraUsb.cs (MIT, Daniel Valfridsson).
// Cross-check: OpenRGB AsusAuraUSBController / AsusAuraMainboardController.
// Only EC82/ECB0 are sent during scan. Never sends SetGen1 or flash commit.
internal static class DirectAuraProgram
{
    private static int Main(string[] args)
    {
        Console.InputEncoding = new UTF8Encoding(false);
        Console.OutputEncoding = new UTF8Encoding(false);
        if (args.Length == 1 && args[0] == "--self-test") return DirectAuraWorker.SelfTest();
        using (DirectAuraWorker worker = new DirectAuraWorker()) return worker.Run();
    }
}

internal sealed class DirectAuraWorker : IDisposable
{
    private readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 65536, RecursionLimit = 16 };
    private readonly List<Controller> controllers = new List<Controller>();
    private readonly List<string> warnings = new List<string>();
    private bool controlled;
    private string active;
    private bool stop;

    internal int Run()
    {
        string line;
        while (!stop && (line = Console.ReadLine()) != null)
        {
            object id = null;
            Dictionary<string, object> result = new Dictionary<string, object>();
            try
            {
                Dictionary<string, object> request = json.Deserialize<Dictionary<string, object>>(line);
                if (request == null) throw new ArgumentException("Request must be a JSON object.");
                request.TryGetValue("id", out id);
                string op = Text(request, "op", true);
                if (op == "scan") Scan();
                else if (op == "status") { }
                else if (op == "apply") Apply(request);
                else if (op == "release") Release();
                else if (op == "shutdown") { Release(); stop = true; }
                else throw new ArgumentException("Unknown op. Expected scan, status, apply, release, or shutdown.");
                result["ok"] = true;
                result["error"] = null;
            }
            catch (Exception ex) { result["ok"] = false; result["error"] = ex.Message; }
            result["id"] = id;
            result["devices"] = Descriptions();
            result["controlled"] = controlled;
            result["effect"] = active;
            result["animationError"] = null;
            result["deviceType"] = 0x00010000;
            result["backend"] = "native-hid";
            result["warnings"] = warnings.ToArray();
            result["releaseRestoresFirmware"] = false;
            result["speedSupported"] = false;
            result["brightnessSupported"] = active != "cycle";
            result["restartRequired"] = AuraBoundedIo.RestartRequired;
            Console.WriteLine(json.Serialize(result));
            Console.Out.Flush();
            // A defective driver can fail to complete CancelIoEx. Keep its buffers
            // alive, return a useful error, then let process teardown own cleanup.
            if (AuraBoundedIo.RestartRequired) Environment.Exit(4);
        }
        return 0;
    }

    private void Scan()
    {
        // Releasing the handle does not revert the controller's retained effect.
        DisposeControllers();
        warnings.Clear();
        controlled = false;
        active = null;
        int candidates = 0;
        foreach (HidInfo info in NativeHid.EnumerateAura())
        {
            candidates++;
            NativeHid device = null;
            try
            {
                device = new NativeHid(info);
                byte[] fw = device.Query(0x82, 0x02);
                byte[] cfg = device.Query(0xB0, 0x30);
                byte[] table = ParseConfig(cfg);
                string firmware = ParseFirmware(fw);
                List<Zone> zones = ParseZones(table);
                if (zones.Count == 0) throw new InvalidOperationException("Controller reported no lighting zones.");
                Controller controller = new Controller { Info = info, Hid = device, Firmware = firmware,
                    Config = table, Zones = zones, Id = Identity(info.Path), Name = "ASUS Aura motherboard (native USB)" };
                controllers.Add(controller);
                device = null;
            }
            catch (Exception ex)
            {
                warnings.Add("0B05:" + info.ProductId.ToString("X4") + ": " + ex.Message);
                if (AuraBoundedIo.RestartRequired) throw;
            }
            finally { if (device != null) device.Dispose(); }
        }
        if (candidates > 0 && controllers.Count == 0)
            throw new InvalidOperationException("The ASUS Aura HID controller did not return valid firmware/configuration. " + String.Join("; ", warnings));
    }

    private void Apply(Dictionary<string, object> request)
    {
        string effect = Text(request, "effect", true).ToLowerInvariant();
        byte mode = Mode(effect); // Reject unsupported effects before any I/O.
        string color = Text(request, "color", false) ?? "#FF0000";
        int rgb;
        if (color.Length != 7 || color[0] != '#' || !Int32.TryParse(color.Substring(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out rgb))
            throw new ArgumentException("color must be #RRGGBB.");
        double brightness = Number(request, "brightness", 100);
        if (Double.IsNaN(brightness) || Double.IsInfinity(brightness)) throw new ArgumentException("brightness must be finite.");
        brightness = Math.Max(0, Math.Min(100, brightness)) / 100.0;
        byte red = effect == "off" ? (byte)0 : (byte)Math.Round(((rgb >> 16) & 255) * brightness);
        byte green = effect == "off" ? (byte)0 : (byte)Math.Round(((rgb >> 8) & 255) * brightness);
        byte blue = effect == "off" ? (byte)0 : (byte)Math.Round((rgb & 255) * brightness);
        object selected;
        if (!request.TryGetValue("devices", out selected) || !(selected is IEnumerable) || selected is string || selected is IDictionary)
            throw new ArgumentException("devices must be a nonempty array of IDs from scan.");
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (object item in (IEnumerable)selected)
        {
            string key = item as string;
            if (String.IsNullOrEmpty(key) || !controllers.Exists(delegate(Controller c) { return c.Id == key; }))
                throw new ArgumentException("Unknown device ID. Scan again before applying lighting.");
            ids.Add(key);
        }
        if (ids.Count == 0) throw new ArgumentException("Select at least one device before applying lighting.");
        // Prepare and validate every packet before the first hardware write.
        Dictionary<Controller, List<byte[]>> packets = new Dictionary<Controller, List<byte[]>>();
        foreach (Controller c in controllers)
            if (ids.Contains(c.Id)) packets.Add(c, EffectPackets(c.Zones, mode, red, green, blue));
        foreach (KeyValuePair<Controller, List<byte[]>> item in packets)
            foreach (byte[] packet in item.Value) item.Key.Hid.Write(packet);
        controlled = true;
        active = effect;
        warnings.Clear();
        warnings.Add("Hardware effect uses the controller's default speed. Closing or releasing does not restore the previous color; the last hardware effect is retained.");
        if (effect == "cycle") warnings.Add("Spectrum cycle ignores color and brightness on this controller.");
    }

    private void Release()
    {
        // This protocol has no proven read-back/restore operation. No packet is sent.
        DisposeControllers();
        controlled = false;
        active = null;
        warnings.Clear();
        warnings.Add("Native USB writes have stopped. The controller retains the last effect; previous firmware lighting is not restored.");
    }

    private object[] Descriptions()
    {
        List<object> result = new List<object>();
        foreach (Controller controller in controllers)
        {
            List<object> zones = new List<object>();
            int total = 0;
            foreach (Zone z in controller.Zones) { total += z.Leds; zones.Add(new { name = z.Name, channel = z.Channel, leds = z.Leds, addressable = z.Addressable }); }
            result.Add(new Dictionary<string, object> {
                { "id", controller.Id }, { "name", controller.Name }, { "type", 0x00010000 }, { "leds", total },
                { "firmware", controller.Firmware }, { "vendorId", controller.Info.VendorId.ToString("X4") },
                { "productId", controller.Info.ProductId.ToString("X4") }, { "usagePage", controller.Info.UsagePage.ToString("X4") },
                { "inputReportLength", controller.Info.InputLength }, { "outputReportLength", controller.Info.OutputLength },
                { "configTable", Hex(controller.Config) }, { "zones", zones.ToArray() },
                { "effects", new[] { "static", "breathe", "cycle", "off" } }, { "speedSupported", false },
                { "releaseRestoresFirmware", false }, { "backend", "native-hid" }, { "ledCountIsLogical", true },
                { "zoneCount", controller.Zones.Count }, { "effectWriteValidated", false }
            });
        }
        return result.ToArray();
    }

    internal static byte[] Report(params byte[] body)
    {
        byte[] report = new byte[65]; report[0] = 0xEC; Array.Copy(body, 0, report, 1, body.Length); return report;
    }
    internal static string ParseFirmware(byte[] packet)
    {
        if (packet == null || packet.Length < 18 || packet[0] != 0xEC || packet[1] != 0x02)
            throw new InvalidOperationException("Invalid firmware reply.");
        return Encoding.ASCII.GetString(packet, 2, 16).TrimEnd('\0', ' ');
    }
    internal static byte[] ParseConfig(byte[] packet)
    {
        if (packet == null || packet.Length < 64 || packet[0] != 0xEC || packet[1] != 0x30)
            throw new InvalidOperationException("Invalid configuration reply.");
        byte[] table = new byte[60]; Array.Copy(packet, 4, table, 0, 60); return table;
    }
    private static List<Zone> ParseZones(byte[] config)
    {
        if (config.Length != 60) throw new InvalidOperationException("Invalid configuration length.");
        List<Zone> zones = new List<Zone>();
        int channel = 0;
        if (config[0x1B] > 0) zones.Add(new Zone { Name = "Motherboard", Channel = channel++, Leds = config[0x1B] });
        if (config[2] > 8) throw new InvalidOperationException("Unrecognized configuration: too many ARGB headers.");
        for (int i = 0; i < config[2]; i++) zones.Add(new Zone { Name = "ARGB header " + (i + 1), Channel = channel++, Leds = 1, Addressable = true });
        return zones;
    }
    internal static byte Mode(string effect)
    {
        if (effect == "off") return 0;
        if (effect == "static") return 1;
        if (effect == "breathe") return 2;
        if (effect == "cycle") return 4;
        throw new ArgumentException("Native USB supports static, breathe, cycle, and off. Rainbow is not implemented.");
    }
    private static List<byte[]> EffectPackets(List<Zone> zones, byte mode, byte red, byte green, byte blue)
    {
        int total = 0;
        foreach (Zone z in zones) total += z.Leds;
        if (total < 1 || total > 16) throw new InvalidOperationException("Lighting configuration exceeds the proven 16-LED effect mask. No write was sent.");
        List<byte[]> result = new List<byte[]>();
        int start = 0;
        foreach (Zone z in zones)
        {
            result.Add(Report(0x35, (byte)z.Channel, 0, 0, mode));
            ushort mask = (ushort)(((1 << z.Leds) - 1) << start);
            byte[] color = Report(0x36, (byte)(mask >> 8), (byte)mask, 0);
            for (int i = 0; i < z.Leds; i++) { int at = 5 + 3 * (start + i); color[at] = red; color[at + 1] = green; color[at + 2] = blue; }
            result.Add(color); start += z.Leds;
        }
        return result;
    }
    private static string Text(Dictionary<string, object> request, string key, bool required)
    {
        object value;
        if (!request.TryGetValue(key, out value) || value == null) { if (!required) return null; throw new ArgumentException(key + " is required."); }
        string text = value as string;
        if (text == null || (required && text.Length == 0)) throw new ArgumentException(key + " must be a string.");
        return text;
    }
    private static double Number(Dictionary<string, object> request, string key, double fallback)
    {
        object value; if (!request.TryGetValue(key, out value)) return fallback;
        if (value is string || value == null || value is bool) throw new ArgumentException(key + " must be a number.");
        return Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }
    private static string Identity(string path)
    {
        using (SHA256 sha = SHA256.Create()) return "native-aura-" + Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(path.ToLowerInvariant()))).Substring(0, 16).ToLowerInvariant();
    }
    private static string Hex(byte[] data) { return BitConverter.ToString(data).Replace("-", ""); }
    private void DisposeControllers() { foreach (Controller c in controllers) c.Hid.Dispose(); controllers.Clear(); }
    public void Dispose() { DisposeControllers(); }
    private sealed class Controller { internal HidInfo Info; internal NativeHid Hid; internal string Id, Name, Firmware; internal byte[] Config; internal List<Zone> Zones; }
    private sealed class Zone { internal string Name; internal int Channel, Leds; internal bool Addressable; }

    internal static int SelfTest()
    {
        int count = 0;
        Action<bool, string> check = delegate(bool ok, string message) { if (!ok) throw new Exception(message); count++; };
        byte[] request = Report(0xB0); check(request.Length == 65 && request[0] == 0xEC && request[1] == 0xB0 && request[64] == 0, "Query framing");
        byte[] reply = Report(0x30); reply[6] = 3; reply[31] = 4;
        byte[] cfg = ParseConfig(reply); check(cfg.Length == 60 && cfg[2] == 3 && cfg[0x1B] == 4, "Configuration offsets");
        List<Zone> zones = ParseZones(cfg); check(zones.Count == 4 && zones[0].Leds == 4 && zones[3].Channel == 3, "Zone ordering");
        List<byte[]> packets = EffectPackets(zones, Mode("static"), 0x11, 0x22, 0x33);
        check(packets.Count == 8 && packets[0][1] == 0x35 && packets[0][5] == 1, "Effect command");
        check(packets[1][2] == 0 && packets[1][3] == 0x0F && packets[1][5] == 0x11 && packets[1][7] == 0x33, "Mainboard mask and RGB");
        check(packets[3][3] == 0x10 && packets[3][17] == 0x11 && packets[3][5] == 0, "ARGB mask and global RGB offset");
        check(Mode("off") == 0 && Mode("breathe") == 2 && Mode("cycle") == 4, "Hardware mode mapping");
        bool rejected = false; try { Mode("rainbow"); } catch (ArgumentException) { rejected = true; } check(rejected, "Unsupported effect rejected");
        rejected = false; try { ParseConfig(new byte[63]); } catch (InvalidOperationException) { rejected = true; } check(rejected, "Short reply rejected");
        zones[0].Leds = 16; rejected = false; try { EffectPackets(zones, 1, 1, 1, 1); } catch (InvalidOperationException) { rejected = true; } check(rejected, "Oversized mask rejected before I/O");
        AuraBoundedIoTests.Run(check);
        check(NativeHid.MutexName("TEST-HID-Path") == NativeHid.MutexName("test-hid-path"), "HID ownership key is case independent");
        check(NativeHid.MutexName("test-hid-path") != NativeHid.MutexName("another-path"), "Different HID interfaces have distinct ownership keys");
        check(NativeHid.MutexName("test-hid-path").StartsWith("Local\\RogControl.Hid.") && NativeHid.MutexName("test-hid-path").Length == 85, "Owned HID workers share the full SHA256 key convention");
        Console.WriteLine("Direct Aura protocol tests passed: " + count + "; hardware writes performed: 0; HID devices opened: 0"); return 0;
    }
}

internal sealed class HidInfo
{
    internal string Path;
    internal ushort VendorId, ProductId, UsagePage;
    internal int InputLength, OutputLength;
}

internal sealed class NativeHid : IDisposable
{
    private static readonly List<NativeHid> RetainedPoisoned = new List<NativeHid>();
    private SafeFileHandle handle;
    private Mutex ownership;
    private bool ownsMutex;
    private readonly HidInfo info;
    internal NativeHid(HidInfo descriptor)
    {
        info = descriptor;
        ownership = new Mutex(false, MutexName(info.Path));
        try
        {
            try { ownsMutex = ownership.WaitOne(0); }
            catch (AbandonedMutexException) { ownsMutex = true; }
            if (!ownsMutex) throw new IOException("Aura HID interface is already in use by another ROG Control worker. Close that session before scanning again.");
            handle = CreateFile(info.Path, 0xC0000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error, "Cannot open Aura HID collection.");
            }
        }
        catch { Dispose(); throw; }
    }
    internal void Write(byte[] packet)
    {
        Write(packet, 1000);
    }
    private void Write(byte[] packet, int timeoutMs)
    {
        if (packet.Length != 65 || info.OutputLength < 65) throw new InvalidOperationException("Unexpected Aura output report size.");
        byte[] buffer = new byte[info.OutputLength]; Array.Copy(packet, buffer, packet.Length);
        int sent = Transfer(true, buffer, timeoutMs);
        if (sent != buffer.Length) throw new IOException("Aura HID write returned a partial report. Its hardware effect cannot be confirmed.");
    }
    internal byte[] Query(byte command, byte expected)
    {
        Stopwatch deadline = Stopwatch.StartNew();
        try
        {
            // One absolute budget covers the write and every unrelated input report.
            // FlushQueue has no cancellation contract and could steal another app's
            // response, so it is deliberately not used here.
            Write(DirectAuraWorker.Report(command), Remaining(deadline, 1200));
            while (true)
            {
                byte[] buffer = new byte[info.InputLength];
                int got = Transfer(false, buffer, Remaining(deadline, 1200));
                if (got == 0) throw new IOException("Aura HID device disconnected.");
                if (got >= 2 && buffer[0] == 0xEC && buffer[1] == expected) { Array.Resize(ref buffer, got); return buffer; }
            }
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException("Aura query EC" + command.ToString("X2") + " timed out within its 1200 ms budget. Another application or the device may be holding the response. " + ex.Message, ex);
        }
    }
    private static int Remaining(Stopwatch elapsed, int budget)
    {
        int remaining = budget - (int)Math.Min(Int32.MaxValue, elapsed.ElapsedMilliseconds);
        if (remaining <= AuraBoundedIo.CancelReserveMs) throw new TimeoutException("The HID transaction deadline expired.");
        return remaining;
    }
    private int Transfer(bool write, byte[] buffer, int timeoutMs)
    {
        if (handle == null || handle.IsClosed || handle.IsInvalid) throw new ObjectDisposedException("NativeHid");
        bool retained = false;
        try
        {
            handle.DangerousAddRef(ref retained);
            return AuraBoundedIo.Transfer(handle.DangerousGetHandle(), write, buffer, timeoutMs, null);
        }
        finally { if (retained) handle.DangerousRelease(); }
    }
    internal static string MutexName(string path)
    {
        using (SHA256 sha = SHA256.Create())
            return "Local\\RogControl.Hid." + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(path.ToLowerInvariant()))).Replace("-", "");
    }
    public void Dispose()
    {
        // Never wait on or recycle a handle whose cancellation is unconfirmed.
        // Run() returns the JSON failure and immediately terminates this child.
        if (AuraBoundedIo.RestartRequired)
        {
            // Keep SafeFileHandle and Mutex objects rooted so their finalizers
            // cannot close them between the JSON failure and Environment.Exit.
            if (!RetainedPoisoned.Contains(this)) RetainedPoisoned.Add(this);
            return;
        }
        if (handle != null) { handle.Dispose(); handle = null; }
        if (ownership != null)
        {
            if (ownsMutex) { ownership.ReleaseMutex(); ownsMutex = false; }
            ownership.Dispose(); ownership = null;
        }
    }

    internal static List<HidInfo> EnumerateAura()
    {
        Guid guid; HidD_GetHidGuid(out guid);
        IntPtr set = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, 0x12);
        if (set == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot enumerate HID devices.");
        List<HidInfo> found = new List<HidInfo>();
        try
        {
            DeviceInterface iface = new DeviceInterface { Size = Marshal.SizeOf(typeof(DeviceInterface)) };
            for (uint i = 0; SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref iface); i++)
            {
                uint needed; SetupDiGetDeviceInterfaceDetail(set, ref iface, IntPtr.Zero, 0, out needed, IntPtr.Zero);
                if (needed == 0) continue;
                IntPtr detail = Marshal.AllocHGlobal((int)needed);
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(set, ref iface, detail, needed, out needed, IntPtr.Zero)) continue;
                    string path = Marshal.PtrToStringUni(IntPtr.Add(detail, 4));
                    using (SafeFileHandle handle = CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
                    {
                        if (handle.IsInvalid) continue;
                        Attributes attributes = new Attributes { Size = Marshal.SizeOf(typeof(Attributes)) };
                        if (!HidD_GetAttributes(handle, ref attributes) || attributes.VendorId != 0x0B05 || !KnownMotherboard(attributes.ProductId)) continue;
                        IntPtr preparsed;
                        if (!HidD_GetPreparsedData(handle, out preparsed)) continue;
                        try
                        {
                            Caps caps;
                            if (HidP_GetCaps(preparsed, out caps) != 0x00110000 || caps.UsagePage != 0xFF72 || caps.OutputLength < 65 || caps.InputLength < 64) continue;
                            found.Add(new HidInfo { Path = path, VendorId = attributes.VendorId, ProductId = attributes.ProductId,
                                UsagePage = caps.UsagePage, InputLength = caps.InputLength, OutputLength = caps.OutputLength });
                        }
                        finally { HidD_FreePreparsedData(preparsed); }
                    }
                }
                finally { Marshal.FreeHGlobal(detail); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return found;
    }
    private static bool KnownMotherboard(ushort pid) { return pid == 0x18F3 || pid == 0x1939 || pid == 0x19AF || pid == 0x1AA6 || pid == 0x1BED; }
    [StructLayout(LayoutKind.Sequential)] private struct DeviceInterface { public int Size; public Guid Guid; public int Flags; public IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)] private struct Attributes { public int Size; public ushort VendorId, ProductId, Version; }
    [StructLayout(LayoutKind.Sequential)] private struct Caps
    {
        public ushort Usage, UsagePage, InputLength, OutputLength, FeatureLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort LinkNodes, InputButtons, InputValues, InputIndices, OutputButtons, OutputValues, OutputIndices, FeatureButtons, FeatureValues, FeatureIndices;
    }
    [DllImport("hid.dll")] private static extern void HidD_GetHidGuid(out Guid guid);
    [DllImport("hid.dll", SetLastError = true)] private static extern bool HidD_GetAttributes(SafeFileHandle handle, ref Attributes attributes);
    [DllImport("hid.dll", SetLastError = true)] private static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr data);
    [DllImport("hid.dll")] private static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")] private static extern int HidP_GetCaps(IntPtr data, out Caps caps);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr SetupDiGetClassDevs(ref Guid guid, IntPtr enumerator, IntPtr hwnd, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)] private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr device, ref Guid guid, uint index, ref DeviceInterface iface);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref DeviceInterface iface, IntPtr detail, uint size, out uint needed, IntPtr device);
    [DllImport("setupapi.dll")] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
}

// Injecting this small API keeps cancellation, completion races, and buffer
// lifetime testable without opening a device or loading an ASUS component.
internal interface IAuraIoApi
{
    IntPtr CreateEvent();
    void CloseEvent(IntPtr eventHandle);
    int Start(IntPtr handle, bool write, IntPtr buffer, int length, IntPtr overlapped);
    uint Wait(IntPtr eventHandle, uint milliseconds);
    bool Cancel(IntPtr handle, IntPtr overlapped, out int error);
    int Finish(IntPtr handle, IntPtr overlapped, out uint transferred);
}

internal static class AuraBoundedIo
{
    internal const int CancelReserveMs = 100;
    internal static bool RestartRequired { get; private set; }
    private const int Pending = 997, Incomplete = 996, Aborted = 995;
    private const uint Signaled = 0;
    private static readonly IAuraIoApi Windows = new WindowsAuraIoApi();

    [StructLayout(LayoutKind.Sequential)]
    private struct Overlapped
    {
        internal IntPtr Internal, InternalHigh;
        internal uint Offset, OffsetHigh;
        internal IntPtr Event;
    }

    internal static int Transfer(IntPtr handle, bool write, byte[] buffer, int timeoutMs, IAuraIoApi injected)
    {
        if (RestartRequired) throw new IOException("The HID worker requires a restart after an unconfirmed I/O cancellation.");
        if (buffer == null || buffer.Length == 0) throw new ArgumentException("A nonempty HID report is required.");
        if (timeoutMs <= CancelReserveMs || timeoutMs > 60000) throw new ArgumentOutOfRangeException("timeoutMs", "The bounded HID deadline must include its 100 ms cancellation reserve.");
        IAuraIoApi api = injected ?? Windows;
        Stopwatch deadline = Stopwatch.StartNew();
        IntPtr eventHandle = api.CreateEvent();
        if (eventHandle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot create the HID completion event.");
        IntPtr nativeBuffer = IntPtr.Zero, overlapped = IntPtr.Zero;
        bool started = false, terminal = true;
        try
        {
            nativeBuffer = Marshal.AllocHGlobal(buffer.Length);
            Marshal.Copy(buffer, 0, nativeBuffer, buffer.Length);
            overlapped = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Overlapped)));
            Marshal.StructureToPtr(new Overlapped { Event = eventHandle }, overlapped, false);
            int error = api.Start(handle, write, nativeBuffer, buffer.Length, overlapped);
            if (error != 0 && error != Pending) throw new Win32Exception(error, "Cannot start the Aura HID " + (write ? "write" : "read") + ".");
            started = true;
            terminal = false;
            uint transferred;
            if (error == 0)
            {
                error = api.Finish(handle, overlapped, out transferred);
                if (error == 0)
                {
                    terminal = true;
                    return Complete(write, buffer, nativeBuffer, transferred);
                }
                if (error != Incomplete)
                {
                    terminal = true;
                    throw new Win32Exception(error, "The Aura HID transfer failed.");
                }
            }
            int workBudget = timeoutMs - (int)deadline.ElapsedMilliseconds - CancelReserveMs;
            uint wait = workBudget > 0 ? api.Wait(eventHandle, (uint)workBudget) : 258;
            if (wait == Signaled)
            {
                error = api.Finish(handle, overlapped, out transferred);
                if (error == 0)
                {
                    terminal = true;
                    return Complete(write, buffer, nativeBuffer, transferred);
                }
                if (error != Incomplete)
                {
                    terminal = true;
                    throw new Win32Exception(error, "The Aura HID transfer failed.");
                }
            }
            // Cancel only this exact operation; never cancel another report or
            // assume CancelIoEx returning true means the driver finished with it.
            int cancelError;
            api.Cancel(handle, overlapped, out cancelError);
            error = api.Finish(handle, overlapped, out transferred);
            if (error == Incomplete)
            {
                int remaining = timeoutMs - (int)deadline.ElapsedMilliseconds;
                if (remaining > 0) api.Wait(eventHandle, (uint)Math.Min(CancelReserveMs, remaining));
                error = api.Finish(handle, overlapped, out transferred);
            }
            if (error != Incomplete)
            {
                terminal = true;
                string ending = error == 0 ? "The operation completed while cancellation was being requested; a write may already have reached the device."
                    : error == Aborted ? "The pending operation was cancelled." : "The pending operation completed with error " + error + ".";
                throw new TimeoutException("Aura HID " + (write ? "write" : "read") + " exceeded its bounded deadline. " + ending);
            }
            RestartRequired = true;
            throw new TimeoutException("Aura HID " + (write ? "write" : "read") + " exceeded its bounded deadline and its cancellation did not complete. The worker will stop and must be restarted.");
        }
        finally
        {
            if (terminal || !started)
            {
                if (overlapped != IntPtr.Zero) Marshal.FreeHGlobal(overlapped);
                if (nativeBuffer != IntPtr.Zero) Marshal.FreeHGlobal(nativeBuffer);
                api.CloseEvent(eventHandle);
            }
            else
            {
                // Kernel I/O may still reference all three native allocations.
                // Intentionally retain them until child process teardown. Freeing
                // or reusing this memory would be a use-after-free in the driver.
                RestartRequired = true;
            }
        }
    }

    private static int Complete(bool write, byte[] buffer, IntPtr nativeBuffer, uint transferred)
    {
        if (transferred > buffer.Length) throw new IOException("The Aura HID driver returned an invalid transfer length.");
        if (!write && transferred > 0) Marshal.Copy(nativeBuffer, buffer, 0, (int)transferred);
        return (int)transferred;
    }

    // Synthetic tests own their fake pending operations; they use this only
    // after fake completion. A JSON request cannot clear the poisoned state.
    internal static void ResetForSyntheticTests() { RestartRequired = false; }

    private sealed class WindowsAuraIoApi : IAuraIoApi
    {
        public IntPtr CreateEvent() { return CreateEventW(IntPtr.Zero, true, false, null); }
        public void CloseEvent(IntPtr eventHandle) { CloseHandle(eventHandle); }
        public int Start(IntPtr handle, bool write, IntPtr buffer, int length, IntPtr overlapped)
        {
            uint transferred;
            bool ok = write ? WriteFile(handle, buffer, (uint)length, out transferred, overlapped)
                : ReadFile(handle, buffer, (uint)length, out transferred, overlapped);
            return ok ? 0 : Marshal.GetLastWin32Error();
        }
        public uint Wait(IntPtr eventHandle, uint milliseconds) { return WaitForSingleObject(eventHandle, milliseconds); }
        public bool Cancel(IntPtr handle, IntPtr overlapped, out int error)
        {
            bool ok = CancelIoEx(handle, overlapped);
            error = ok ? 0 : Marshal.GetLastWin32Error();
            return ok;
        }
        public int Finish(IntPtr handle, IntPtr overlapped, out uint transferred)
        {
            bool ok = GetOverlappedResult(handle, overlapped, out transferred, false);
            return ok ? 0 : Marshal.GetLastWin32Error();
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateEventW(IntPtr security, [MarshalAs(UnmanagedType.Bool)] bool manualReset, [MarshalAs(UnmanagedType.Bool)] bool initialState, string name);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReadFile(IntPtr handle, IntPtr buffer, uint count, out uint transferred, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WriteFile(IntPtr handle, IntPtr buffer, uint count, out uint transferred, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetOverlappedResult(IntPtr handle, IntPtr overlapped, out uint transferred, [MarshalAs(UnmanagedType.Bool)] bool wait);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CancelIoEx(IntPtr handle, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    }
}

internal static class AuraBoundedIoTests
{
    internal static void Run(Action<bool, string> check)
    {
        byte[] report = new byte[65];
        FakeIo fake = new FakeIo { StartResult = 0 };
        int got = AuraBoundedIo.Transfer(new IntPtr(7), false, report, 1200, fake);
        check(got == 65 && report[0] == 0xEC && report[1] == 0x30, "Immediate native read copies only completed data");
        check(fake.Closes == 1 && fake.Waits.Count == 0 && fake.Cancels == 0, "Immediate completion frees its event without waiting or cancelling");

        fake = new FakeIo();
        got = AuraBoundedIo.Transfer(new IntPtr(7), false, report, 1200, fake);
        check(got == 65 && fake.Closes == 1 && fake.Waits.Count == 1 && fake.Cancels == 0, "Pending read waits for completion then releases native storage");
        check(fake.Waits[0] > 0 && fake.Waits[0] <= 1100, "Working deadline reserves cancellation time");

        report[4] = 0xAC;
        fake = new FakeIo { StartResult = 0 };
        got = AuraBoundedIo.Transfer(new IntPtr(7), true, report, 1000, fake);
        check(got == 65 && fake.Output[4] == 0xAC, "Native write uses a stable unmanaged copy of the validated report");

        fake = new FakeIo { StartResult = 5 };
        bool rejected = false;
        try { AuraBoundedIo.Transfer(new IntPtr(7), false, report, 1200, fake); } catch (Win32Exception) { rejected = true; }
        check(rejected && fake.Closes == 1 && fake.Finishes == 0 && fake.Cancels == 0, "Failure to start an operation releases allocation safely");

        fake = new FakeIo(); fake.WaitResults.Enqueue(258); fake.FinishResults.Enqueue(995);
        rejected = false;
        try { AuraBoundedIo.Transfer(new IntPtr(7), false, report, 1200, fake); } catch (TimeoutException) { rejected = true; }
        check(rejected && fake.Cancels == 1 && fake.Closes == 1 && !AuraBoundedIo.RestartRequired, "Timed out read verifies aborted completion before freeing its buffer");
        check(fake.Waits.Count == 1 && fake.Finishes == 1, "Already completed cancellation needs no second wait");

        fake = new FakeIo(); fake.WaitResults.Enqueue(258); fake.WaitResults.Enqueue(0);
        fake.FinishResults.Enqueue(996); fake.FinishResults.Enqueue(995);
        rejected = false;
        try { AuraBoundedIo.Transfer(new IntPtr(7), false, report, 1200, fake); } catch (TimeoutException) { rejected = true; }
        check(rejected && fake.Closes == 1 && fake.Waits.Count == 2 && !AuraBoundedIo.RestartRequired, "Delayed cancellation waits for terminal completion");
        check(fake.Waits[1] <= AuraBoundedIo.CancelReserveMs, "Cancellation cleanup has a bounded reserve");

        fake = new FakeIo { CancelError = 1168 }; fake.WaitResults.Enqueue(258); fake.FinishResults.Enqueue(0);
        string error = null;
        try { AuraBoundedIo.Transfer(new IntPtr(7), true, report, 1000, fake); } catch (TimeoutException ex) { error = ex.Message; }
        check(error != null && error.Contains("may already have reached") && fake.Closes == 1, "Write completing during cancellation is reported as an uncertain hardware outcome");
        check(!AuraBoundedIo.RestartRequired, "A cancellation completion race leaves no pending I/O");

        fake = new FakeIo(); fake.FinishResults.Enqueue(5);
        rejected = false;
        try { AuraBoundedIo.Transfer(new IntPtr(7), false, report, 1200, fake); } catch (Win32Exception) { rejected = true; }
        check(rejected && fake.Closes == 1 && fake.Cancels == 0, "A terminal device error releases native storage");

        fake = new FakeIo { Transferred = 100 };
        rejected = false;
        try { AuraBoundedIo.Transfer(new IntPtr(7), false, report, 1200, fake); } catch (IOException) { rejected = true; }
        check(rejected && fake.Closes == 1, "Invalid driver length cannot copy past the validated report");

        fake = new FakeIo(); fake.FinishResults.Enqueue(996); fake.FinishResults.Enqueue(995);
        rejected = false;
        try { AuraBoundedIo.Transfer(new IntPtr(7), false, report, 1200, fake); } catch (TimeoutException) { rejected = true; }
        check(rejected && fake.Cancels == 1 && fake.Closes == 1, "A signaled event with incomplete I/O is cancelled rather than freeing a pending buffer");

        // This fake models a driver that ignores cancellation. There is no real
        // device I/O, so the test can safely release its retained memory itself.
        fake = new FakeIo(); fake.WaitResults.Enqueue(258); fake.WaitResults.Enqueue(258);
        fake.FinishResults.Enqueue(996); fake.FinishResults.Enqueue(996);
        rejected = false;
        try { AuraBoundedIo.Transfer(new IntPtr(7), false, report, 1200, fake); } catch (TimeoutException) { rejected = true; }
        check(rejected && AuraBoundedIo.RestartRequired && fake.Closes == 0, "Unconfirmed cancellation preserves buffer/OVERLAPPED/event and poisons the worker");
        FakeIo forbidden = new FakeIo();
        rejected = false;
        try { AuraBoundedIo.Transfer(new IntPtr(7), false, report, 1200, forbidden); } catch (IOException) { rejected = true; }
        check(rejected && forbidden.Creates == 0, "A poisoned worker rejects every subsequent native operation");
        fake.FreeSyntheticRetainedMemory();
        AuraBoundedIo.ResetForSyntheticTests();

        fake = new FakeIo();
        rejected = false;
        try { AuraBoundedIo.Transfer(new IntPtr(7), false, report, 100, fake); } catch (ArgumentOutOfRangeException) { rejected = true; }
        check(rejected && fake.Creates == 0, "An exhausted deadline is rejected before event creation or hardware I/O");
        rejected = false;
        try { AuraBoundedIo.Transfer(new IntPtr(7), false, new byte[0], 1200, fake); } catch (ArgumentException) { rejected = true; }
        check(rejected && fake.Creates == 0, "Empty reports are rejected before any I/O");
        check(!AuraBoundedIo.RestartRequired, "Synthetic pending state cannot leak into later tests");
    }

    private sealed class FakeIo : IAuraIoApi
    {
        internal int StartResult = 997, CancelError, Creates, Closes, Cancels, Finishes;
        internal uint Transferred = 65;
        internal byte[] Output;
        internal readonly Queue<uint> WaitResults = new Queue<uint>();
        internal readonly Queue<int> FinishResults = new Queue<int>();
        internal readonly List<uint> Waits = new List<uint>();
        private IntPtr memory, operation;
        public IntPtr CreateEvent() { Creates++; return new IntPtr(123); }
        public void CloseEvent(IntPtr eventHandle) { Closes++; }
        public int Start(IntPtr handle, bool write, IntPtr buffer, int length, IntPtr overlapped)
        {
            memory = buffer; operation = overlapped;
            if (write) { Output = new byte[length]; Marshal.Copy(buffer, Output, 0, length); }
            else { byte[] reply = new byte[length]; reply[0] = 0xEC; reply[1] = 0x30; Marshal.Copy(reply, 0, buffer, length); }
            return StartResult;
        }
        public uint Wait(IntPtr eventHandle, uint milliseconds)
        {
            Waits.Add(milliseconds); return WaitResults.Count == 0 ? 0 : WaitResults.Dequeue();
        }
        public bool Cancel(IntPtr handle, IntPtr overlapped, out int error) { Cancels++; error = CancelError; return error == 0; }
        public int Finish(IntPtr handle, IntPtr overlapped, out uint transferred)
        {
            Finishes++; transferred = Transferred; return FinishResults.Count == 0 ? 0 : FinishResults.Dequeue();
        }
        internal void FreeSyntheticRetainedMemory()
        {
            if (Closes != 0) throw new Exception("Cannot release a fake allocation twice.");
            Marshal.FreeHGlobal(operation); Marshal.FreeHGlobal(memory); Closes++;
        }
    }
}
