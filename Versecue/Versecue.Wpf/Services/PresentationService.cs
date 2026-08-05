using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Versecue.Application.Interfaces;
using Versecue.Domain.ValueObjects;
using Versecue.Wpf.Views;

namespace Versecue.Wpf.Services;

public sealed class PresentationService : IPresentationService
{
    private PresenterWindow? _presenterWindow;

    // Win32 API structures for monitor enumeration
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFOEX
    {
        public int Size;
        public RECT Monitor;
        public RECT Work;
        public int Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

    private const int MONITORINFOF_PRIMARY = 0x00000001;

    public Task<IReadOnlyList<Display>> GetDisplaysAsync(CancellationToken ct = default)
    {
        var list = new List<Display>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
        {
            var info = new MONITORINFOEX();
            info.Size = Marshal.SizeOf(typeof(MONITORINFOEX));
            if (GetMonitorInfo(hMonitor, ref info))
            {
                var isPrimary = (info.Flags & MONITORINFOF_PRIMARY) != 0;
                var width = info.Monitor.Right - info.Monitor.Left;
                var height = info.Monitor.Bottom - info.Monitor.Top;
                var displayName = isPrimary ? "Primary Display" : $"Secondary Display ({info.DeviceName})";

                list.Add(new Display(
                    info.DeviceName,
                    displayName,
                    width,
                    height,
                    isPrimary
                ));
            }
            return true;
        }, IntPtr.Zero);

        // Fallback if no screens returned
        if (list.Count == 0)
        {
            list.Add(new Display("Primary", "Primary Display", 1920, 1080, true));
        }

        return Task.FromResult<IReadOnlyList<Display>>(list);
    }

    public Task ShowScriptureAsync(Display display, string text, string referenceDisplay, CancellationToken ct = default)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (_presenterWindow == null)
            {
                _presenterWindow = new PresenterWindow();
            }

            _presenterWindow.UpdateContent(text, referenceDisplay);

            // Locate and position window on target display
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                var info = new MONITORINFOEX();
                info.Size = Marshal.SizeOf(typeof(MONITORINFOEX));
                if (GetMonitorInfo(hMonitor, ref info))
                {
                    if (info.DeviceName == display.DeviceId)
                    {
                        _presenterWindow.Left = info.Monitor.Left;
                        _presenterWindow.Top = info.Monitor.Top;
                        _presenterWindow.Width = info.Monitor.Right - info.Monitor.Left;
                        _presenterWindow.Height = info.Monitor.Bottom - info.Monitor.Top;
                        
                        _presenterWindow.WindowStyle = WindowStyle.None;
                        _presenterWindow.WindowState = WindowState.Maximized;
                        _presenterWindow.Topmost = true;
                        _presenterWindow.Show();
                    }
                }
                return true;
            }, IntPtr.Zero);
        });

        return Task.CompletedTask;
    }

    public Task HideAsync(CancellationToken ct = default)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (_presenterWindow != null)
            {
                _presenterWindow.Hide();
            }
        });

        return Task.CompletedTask;
    }
}
