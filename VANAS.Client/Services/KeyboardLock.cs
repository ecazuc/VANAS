using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VANAS.Client.Services
{
    public enum KeyboardLockMode
    {
        Off,
        /// <summary>Login screen: nothing but the login form is reachable.</summary>
        Locked,
        /// <summary>Customer is playing: Alt+Tab stays free, the Windows key and closing our own window do not.</summary>
        Session
    }

    /// <summary>Low-level keyboard hook that swallows the shortcuts used to escape the kiosk.</summary>
    public static class KeyboardLock
    {
        private const int WH_KEYBOARD_LL = 13;
        private const uint LLKHF_ALTDOWN = 0x20;
        private const int VK_TAB = 0x09, VK_ESCAPE = 0x1B, VK_F4 = 0x73, VK_CONTROL = 0x11, VK_LWIN = 0x5B, VK_RWIN = 0x5C;

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        private static HookProc? _proc; // kept in a field so the GC never collects the callback
        private static IntPtr _hook = IntPtr.Zero;
        private static KeyboardLockMode _mode = KeyboardLockMode.Off;

        /// <summary>Lets Session mode block Alt+F4 only while our own window has the focus (so games can still be closed).</summary>
        public static Func<bool>? OwnWindowIsForeground { get; set; }

        public static void SetMode(KeyboardLockMode mode)
        {
            _mode = mode;
            if (mode == KeyboardLockMode.Off) Uninstall();
            else Install();
        }

        private static void Install()
        {
            if (_hook != IntPtr.Zero) return;
            _proc = Callback;
            using var module = Process.GetCurrentProcess().MainModule!;
            _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(module.ModuleName), 0);
        }

        private static void Uninstall()
        {
            if (_hook == IntPtr.Zero) return;
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
            _proc = null;
        }

        private static IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && ShouldBlock(Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam)))
                return (IntPtr)1;
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private static bool ShouldBlock(KBDLLHOOKSTRUCT key)
        {
            var vk = (int)key.vkCode;
            var alt = (key.flags & LLKHF_ALTDOWN) != 0;
            var ctrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;

            if (vk is VK_LWIN or VK_RWIN) return true;

            return _mode switch
            {
                KeyboardLockMode.Locked => (alt && vk is VK_TAB or VK_ESCAPE or VK_F4) || (ctrl && vk == VK_ESCAPE),
                KeyboardLockMode.Session => alt && vk == VK_F4 && OwnWindowIsForeground?.Invoke() == true,
                _ => false
            };
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
