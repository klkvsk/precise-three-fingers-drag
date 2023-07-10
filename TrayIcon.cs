using H.NotifyIcon.Core;
using System.Diagnostics;

namespace PreciseThreeFingersDrag
{
    internal class TrayIcon : TrayIconWithContextMenu
    {
        public PopupMenuItem TitleMenuItem { get; private set; }
        public PopupMenuItem DelayNoneMenuItem { get; private set; }
        public PopupMenuItem DelayShortMenuItem { get; private set; }
        public PopupMenuItem DelayLongMenuItem { get; private set; }
        public PopupMenuItem AutoStartMenuItem { get; private set; }
        public PopupMenuItem QuitMenuItem { get; private set; }

        public delegate void DelayChangeEvent(TouchProcessor.CooldownDelay delay);
        public event DelayChangeEvent? DelayChanged;

        public TrayIcon()
        {
            Icon = Properties.Resources.AppIcon.Handle;
            UpdateName(ToolTip = "Precise Three Finger Drag");

            var version = Process.GetCurrentProcess().MainModule?.FileVersionInfo.ProductVersion;

            ContextMenu = new PopupMenu
            {
                Items =
                {
                    (TitleMenuItem = new PopupMenuItem(
                        $"Precise Three Fingers Drag ({version})",
                        (_, _) => System.Diagnostics.Process.Start(
                            new System.Diagnostics.ProcessStartInfo()
                            {
                                FileName = "https://github.com/klkvsk/precise-three-fingers-drag",
                                UseShellExecute = true
                            })
                    )),
                    new PopupMenuSeparator(),
                    (AutoStartMenuItem = new PopupMenuItem() { Text = "Auto start on login" }),
                    new PopupSubMenu("Delay to keep dragging")
                    {
                        Items =
                        {
                            (DelayNoneMenuItem = new PopupMenuItem("None", DelayItem_OnClick) ),
                            (DelayShortMenuItem = new PopupMenuItem("Short", DelayItem_OnClick)),
                            (DelayLongMenuItem = new PopupMenuItem("Long", DelayItem_OnClick)),
                        }
                    },
                    new PopupMenuSeparator(),
                    (QuitMenuItem = new PopupMenuItem("Exit", (_, _) => Dispose()))
                },
            };
        }

        private void DelayItem_OnClick(object? sender, EventArgs e)
        {
            if (sender == null || DelayChanged == null)
            {
                return;
            }

            TouchProcessor.CooldownDelay newDelay;
            if (sender == DelayNoneMenuItem)
            {
                newDelay = TouchProcessor.CooldownDelay.None;
            }
            else if (sender == DelayShortMenuItem)
            {
                newDelay = TouchProcessor.CooldownDelay.Short;
            }
            else if (sender == DelayLongMenuItem)
            {
                newDelay = TouchProcessor.CooldownDelay.Long;
            }
            else
            {
                return;
            }

            DelayChanged.Invoke(newDelay);
            SetCheckedDelay(newDelay);
        }

        public void SetCheckedDelay(TouchProcessor.CooldownDelay delay)
        {
            DelayNoneMenuItem.Checked = delay == TouchProcessor.CooldownDelay.None;
            DelayShortMenuItem.Checked = delay == TouchProcessor.CooldownDelay.Short;
            DelayLongMenuItem.Checked = delay == TouchProcessor.CooldownDelay.Long;
        }
    }
}
