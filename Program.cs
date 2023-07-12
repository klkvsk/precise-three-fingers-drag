using Linearstar.Windows.RawInput;
using Linearstar.Windows.RawInput.Native;
using System.Configuration;

///ref
namespace PreciseThreeFingersDrag
{
    internal static class Program
    {
        private static TrayIcon? ui;
        private static InputMessageLoop? input;
        private static TouchProcessor? touch;

        public static Configuration GetConfig()
        {
            return ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        }


        [STAThread]
        private static void Main()
        {
            ui = new TrayIcon();
            input = new InputMessageLoop(OnInputEvent);
            touch = new TouchProcessor();

            var createdNew = false;
            new Mutex(true, "precise-three-fingers-drag", out createdNew);
            if (!createdNew)
            {
                return;
            }

            var hwnd = input.Create();

            touch.Register(hwnd);
            touch.StateChange += Touch_StateChange;
            ui.Created += Ui_Created;
            ui.QuitMenuItem.Click += Quit;
            ui.UseStandardTooltip = true;
            ui.Create();

            input.HwndThread?.Join();
        }

        private static void Ui_Created(object? sender, EventArgs e)
        {
            if (ui == null)
            {
                throw new NullReferenceException();
            }

            ui.AutoStartMenuItem.Checked = AutostartHelper.IsEnabled;
            ui.AutoStartMenuItem.Click += Ui_AutostartToggle;

            ui.DelayChanged += Ui_DelayChanged;

            Configuration config = GetConfig();
            KeyValueConfigurationElement delayCfg = config.AppSettings.Settings["delay"];
            TouchProcessor.CooldownDelay delay = TouchProcessor.CooldownDelay.Long;
            if (delayCfg == null)
            {
                config.AppSettings.Settings.Add(new KeyValueConfigurationElement("delay", ((uint)delay).ToString()));
                config.Save();
            }
            else
            {
                if (Enum.TryParse(delayCfg.Value, out TouchProcessor.CooldownDelay storedDelay))
                {
                    delay = storedDelay;
                }
                else
                {
                    // clean up unparseable value
                    config.AppSettings.Settings.Remove("delay");
                    config.Save();
                }
            }

            if (touch != null)
            {
                touch.DragCooldownDelay = delay;
            }

            ui.SetCheckedDelay(delay);
        }

        private static void Ui_DelayChanged(TouchProcessor.CooldownDelay delay)
        {
            if (touch != null)
            {
                touch.DragCooldownDelay = delay;
            }

            Configuration config = GetConfig();
            KeyValueConfigurationElement? delayCfg = config.AppSettings.Settings["delay"];
            if (delayCfg == null)
            {
                delayCfg = new KeyValueConfigurationElement("delay", ((uint)delay).ToString());
                config.AppSettings.Settings.Add(delayCfg);
            }
            else
            {
                delayCfg.Value = ((uint)delay).ToString();
            }
            config.Save();
        }

        private static void Ui_AutostartToggle(object? sender, EventArgs e)
        {
            if (ui == null)
            {
                return;
            }

            if (AutostartHelper.IsEnabled)
            {
                AutostartHelper.Disable();
                ui.ShowNotification(
                    "Autostart is OFF",
                    "Precise Three Fingers Drag is removed from autostart.",
                    timeout: TimeSpan.FromMilliseconds(1500)
                );
            }
            else
            {
                AutostartHelper.Enable();
                ui.ShowNotification(
                    "Autostart is ON",
                    "Precise Three Fingers Drag will start with your system.",
                    timeout: TimeSpan.FromMilliseconds(1500)
                );
            }

            ui.AutoStartMenuItem.Checked = AutostartHelper.IsEnabled;
        }

        private static void Quit(object? sender, EventArgs e)
        {
            input?.Dispose();

            touch?.Dispose();
        }

        private static void OnInputEvent(nint lParam)
        {
            if (touch == null)
            {
                return;
            }

            RawInputData data = RawInputData.FromHandle(lParam);
            if (data is RawInputDigitizerData digitizerData)
            {
                touch.Update(digitizerData);
            }
            if (data is RawInputMouseData mouseData)
            {
                touch.Update(mouseData);
            }
        }

        private static void Touch_StateChange(object sender, TouchProcessor.State newState)
        {
            //Debug.WriteLine(newState);
        }
    }

}