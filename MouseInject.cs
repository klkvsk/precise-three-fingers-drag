using System.Runtime.InteropServices;

namespace PreciseThreeFingersDrag
{
    internal class MouseInject
    {
        [DllImport("user32.dll")]
        private static extern void mouse_event(MouseEventFlags dwFlags, int dx = 0, int dy = 0, uint dwData = 1, UIntPtr dwExtraInfo = 0);

        public enum MouseEventFlags : uint
        {
            LEFTDOWN = 0x00000002,
            LEFTUP = 0x00000004,
            MIDDLEDOWN = 0x00000020,
            MIDDLEUP = 0x00000040,
            MOVE = 0x00000001,
            ABSOLUTE = 0x00008000,
            RIGHTDOWN = 0x00000008,
            RIGHTUP = 0x00000010,
            WHEEL = 0x00000800,
            XDOWN = 0x00000080,
            XUP = 0x00000100
        }

        private static bool _leftButtonPressed = false;

        public static bool LeftButtonPressed
        {
            get => _leftButtonPressed;
            set
            {
                mouse_event(value ? MouseEventFlags.LEFTDOWN : MouseEventFlags.LEFTUP);
                _leftButtonPressed = value;
            }
        }

        public static void Move(Point distance)
        {
            mouse_event(MouseEventFlags.MOVE, distance.X, distance.Y, 3);
        }

    }
}
