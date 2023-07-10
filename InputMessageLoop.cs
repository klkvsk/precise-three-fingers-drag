using System.Diagnostics;
using System.Runtime.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.User32;

namespace PreciseThreeFingersDrag
{
    public class InputMessageLoop : IDisposable
    {
        internal string WindowId { get; private set; }
        public Thread? HwndThread { get; private set; }

        public bool IsCreated => Handle != 0;
        public nint Handle { get; private set; }

        public InputMessageLoop(InputMessageEvent handler)
        {
            WindowId = "PTFD_" + Guid.NewGuid();
            InputMessageReceived += handler;
        }

        protected unsafe nint WndProc(HWND hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            return DefWindowProc(hwnd, msg, wParam, lParam);
        }

        public delegate void InputMessageEvent(IntPtr lParam);
        public event InputMessageEvent InputMessageReceived;

        public nint Create()
        {
            ManualResetEvent mutHwnd = new(false);

            HwndThread = new Thread(() =>
            {
                WNDCLASS wndClass = new()
                {
                    lpfnWndProc = new WindowProc(WndProc),
                    lpszClassName = WindowId,
                };

                ushort result = RegisterClass(wndClass);
                if (result == 0)
                {
                    throw new Exception(Marshal.GetLastPInvokeErrorMessage());
                }

                nint hwnd = CreateWindow(lpClassName: WindowId).DangerousGetHandle();
                if (hwnd == 0)
                {
                    throw new Exception(Marshal.GetLastPInvokeErrorMessage());
                }

                Handle = hwnd;

                _ = mutHwnd.Set();

                RunMessageLoop();

                _ = DestroyWindow(hwnd);
            });

            HwndThread.Start();
            _ = mutHwnd.WaitOne();
            mutHwnd.Dispose();

            return Handle;
        }

        private void RunMessageLoop()
        {
            Debug.WriteLine("InputHwnd: message loop started");
            bool quit = false;
            while (!quit)
            {
                _ = new MSG();
                int result = GetMessage(out MSG msg, IntPtr.Zero, 0, 0);
                if (result is 0 or (-1))
                {
                    break;
                }

                if (msg.message == (uint)WindowMessage.WM_QUIT)
                {
                    Debug.WriteLine("InputHwnd: got WM_QUIT");
                    quit = true;
                }
                if (msg.message == (uint)WindowMessage.WM_INPUT)
                {
                    InputMessageReceived.Invoke(msg.lParam);
                }

                _ = TranslateMessage(msg);
                _ = DispatchMessage(msg);
            }
            Debug.WriteLine("InputHwnd: message loop exited");
        }

        public void StopMessageLoop()
        {
            if (Handle != 0)
            {
                Debug.WriteLine("sent quit");
                _ = PostMessage((HWND)Handle, (uint)WindowMessage.WM_QUIT, 0, 0);
                Handle = nint.Zero;
            }
        }

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                StopMessageLoop();
                IsDisposed = true;
            }
        }

        ~InputMessageLoop()
        {
            Dispose();
        }

    }
}
