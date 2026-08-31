using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace Klucznik.Services;

public sealed class RawInputRfidScanner : IDisposable
{
    private const int WM_INPUT = 0x00FF;

    private const uint RID_INPUT = 0x10000003;
    private const uint RIDI_DEVICENAME = 0x20000007;

    private const int RIM_TYPEKEYBOARD = 1;

    private const int RIDEV_INPUTSINK = 0x00000100;

    private const ushort HID_USAGE_PAGE_GENERIC = 0x01;
    private const ushort HID_USAGE_GENERIC_KEYBOARD = 0x06;

    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_SYSKEYDOWN = 0x0104;

    private const ushort VK_BACK = 0x08;
    private const ushort VK_RETURN = 0x0D;
    private const ushort VK_ESCAPE = 0x1B;

    private static readonly TimeSpan ScanGapReset = TimeSpan.FromMilliseconds(250);

    private readonly Window _window;
    private readonly string _vid;
    private readonly string _pid;
    private readonly StringBuilder _scanBuffer = new();
    private readonly Dictionary<IntPtr, bool> _deviceMatchCache = new();

    private HwndSource? _source;
    private DateTime _lastScanCharAt = DateTime.MinValue;
    private bool _disposed;

    public event EventHandler<string>? CodeScanned;

    public bool IsRegistered { get; private set; }

    public RawInputRfidScanner(Window window, string vid, string pid)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _vid = NormalizeHardwareIdPart(vid);
        _pid = NormalizeHardwareIdPart(pid);

        _window.SourceInitialized += Window_SourceInitialized;
        _window.Closed += Window_Closed;

        TryAttachAndRegister();
    }

    private static string NormalizeHardwareIdPart(string value)
    {
        return (value ?? string.Empty).Trim().ToUpperInvariant();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        TryAttachAndRegister();
    }

    private void TryAttachAndRegister()
    {
        if (_disposed || IsRegistered)
            return;

        var helper = new WindowInteropHelper(_window);
        var hwnd = helper.Handle;

        if (hwnd == IntPtr.Zero)
            return;

        _source = HwndSource.FromHwnd(hwnd);
        _source?.RemoveHook(WndProc);
        _source?.AddHook(WndProc);

        var devices = new[]
        {
            new RAWINPUTDEVICE
            {
                usUsagePage = HID_USAGE_PAGE_GENERIC,
                usUsage = HID_USAGE_GENERIC_KEYBOARD,
                dwFlags = RIDEV_INPUTSINK,
                hwndTarget = hwnd
            }
        };

        IsRegistered = RegisterRawInputDevices(
            devices,
            (uint)devices.Length,
            (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        Dispose();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_INPUT)
        {
            ProcessRawInput(lParam);
        }

        return IntPtr.Zero;
    }

    private void ProcessRawInput(IntPtr rawInputHandle)
    {
        uint size = 0;

        var result = GetRawInputData(
            rawInputHandle,
            RID_INPUT,
            IntPtr.Zero,
            ref size,
            (uint)Marshal.SizeOf<RAWINPUTHEADER>());

        if (result == uint.MaxValue || size == 0)
            return;

        var buffer = Marshal.AllocHGlobal((int)size);

        try
        {
            result = GetRawInputData(
                rawInputHandle,
                RID_INPUT,
                buffer,
                ref size,
                (uint)Marshal.SizeOf<RAWINPUTHEADER>());

            if (result == uint.MaxValue || result != size)
                return;

            var raw = Marshal.PtrToStructure<RAWINPUT>(buffer);

            if (raw.header.dwType != RIM_TYPEKEYBOARD)
                return;

            if (!IsMatchingReaderDevice(raw.header.hDevice))
                return;

            if (raw.keyboard.Message != WM_KEYDOWN && raw.keyboard.Message != WM_SYSKEYDOWN)
                return;

            ProcessVirtualKey(raw.keyboard.VKey);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private bool IsMatchingReaderDevice(IntPtr deviceHandle)
    {
        if (deviceHandle == IntPtr.Zero)
            return false;

        if (_deviceMatchCache.TryGetValue(deviceHandle, out var cached))
            return cached;

        var deviceName = GetDeviceName(deviceHandle);

        var isMatch = deviceName.Contains(_vid, StringComparison.OrdinalIgnoreCase)
            && deviceName.Contains(_pid, StringComparison.OrdinalIgnoreCase);

        _deviceMatchCache[deviceHandle] = isMatch;
        return isMatch;
    }

    private static string GetDeviceName(IntPtr deviceHandle)
    {
        uint size = 0;

        var result = GetRawInputDeviceInfo(
            deviceHandle,
            RIDI_DEVICENAME,
            null,
            ref size);

        if (result == uint.MaxValue || size == 0)
            return string.Empty;

        var builder = new StringBuilder((int)size);

        result = GetRawInputDeviceInfo(
            deviceHandle,
            RIDI_DEVICENAME,
            builder,
            ref size);

        if (result == uint.MaxValue)
            return string.Empty;

        return builder.ToString();
    }

    private void ProcessVirtualKey(ushort virtualKey)
    {
        if (virtualKey == VK_RETURN)
        {
            CompleteScan();
            return;
        }

        if (virtualKey == VK_ESCAPE)
        {
            ResetScanBuffer();
            return;
        }

        if (virtualKey == VK_BACK)
        {
            if (_scanBuffer.Length > 0)
                _scanBuffer.Length -= 1;

            return;
        }

        var text = MapVirtualKeyToScannerText(virtualKey);

        if (string.IsNullOrEmpty(text))
            return;

        var now = DateTime.Now;

        if (_lastScanCharAt != DateTime.MinValue && now - _lastScanCharAt > ScanGapReset)
        {
            _scanBuffer.Clear();
        }

        _lastScanCharAt = now;
        _scanBuffer.Append(text);
    }

    private void CompleteScan()
    {
        var code = _scanBuffer.ToString().Trim();
        ResetScanBuffer();

        if (string.IsNullOrWhiteSpace(code))
            return;

        CodeScanned?.Invoke(this, code);
    }

    private void ResetScanBuffer()
    {
        _scanBuffer.Clear();
        _lastScanCharAt = DateTime.MinValue;
    }

    private static string? MapVirtualKeyToScannerText(ushort virtualKey)
    {
        if (virtualKey >= 0x30 && virtualKey <= 0x39)
            return ((char)virtualKey).ToString();

        if (virtualKey >= 0x60 && virtualKey <= 0x69)
            return ((char)('0' + virtualKey - 0x60)).ToString();

        if (virtualKey >= 0x41 && virtualKey <= 0x5A)
            return ((char)virtualKey).ToString();

        return null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        IsRegistered = false;

        _window.SourceInitialized -= Window_SourceInitialized;
        _window.Closed -= Window_Closed;

        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }

        _deviceMatchCache.Clear();
        ResetScanBuffer();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public int dwFlags;
        public IntPtr hwndTarget;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTHEADER
    {
        public int dwType;
        public int dwSize;
        public IntPtr hDevice;
        public IntPtr wParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWKEYBOARD
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUT
    {
        public RAWINPUTHEADER header;
        public RAWKEYBOARD keyboard;
    }

    [DllImport("User32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(
        [In] RAWINPUTDEVICE[] pRawInputDevices,
        uint uiNumDevices,
        uint cbSize);

    [DllImport("User32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        IntPtr hRawInput,
        uint uiCommand,
        IntPtr pData,
        ref uint pcbSize,
        uint cbSizeHeader);

    [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern uint GetRawInputDeviceInfo(
        IntPtr hDevice,
        uint uiCommand,
        StringBuilder? pData,
        ref uint pcbSize);
}
