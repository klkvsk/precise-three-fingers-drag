﻿using Linearstar.Windows.RawInput;
using System.Collections.Concurrent;
using System.Drawing;
using System.Timers;

namespace PreciseThreeFingersDrag
{
    public class TouchProcessor : IDisposable
    {
        public enum CooldownDelay
        {
            None = 1,
            Short = 300,
            Long = 900,
        }

        public enum State
        {
            CLEAR,
            ONE_FINGER_IDLE,
            ONE_FINGER_MOVING,
            TWO_FINGER_IDLE,
            TWO_FINGER_MOVING,
            THREE_FINGER_IDLE,
            THREE_FINGER_MOVING,
            THREE_FINGER_DRAG,
            THREE_FINGER_DRAG_COOLDOWN,
            FOUR_OR_MORE,
        };

        public bool threeFingerClickIsFree = false;

        private State state = State.CLEAR;

        private double accumulatedDistance;
        private readonly System.Timers.Timer dragCooldownTimer;

        public CooldownDelay DragCooldownDelay
        {
            get => dragCooldownTimer.Interval switch
            {
                > 900 => CooldownDelay.Long,
                < 1 => CooldownDelay.None,
                _ => CooldownDelay.Short
            };

            set => dragCooldownTimer.Interval = (double)value;
        }

        private RawInputDigitizerContact[] previousContacts;
        private Point? previousCenterPoint;

        private readonly BlockingCollection<RawInputDigitizerData> updatesQueue;

        public double DragCooldownMilliseconds
        {
            get => dragCooldownTimer.Interval;
            set => dragCooldownTimer.Interval = value;
        }

        public TouchProcessor()
        {
            dragCooldownTimer = new System.Timers.Timer((double)CooldownDelay.Short)
            {
                AutoReset = false
            };
            dragCooldownTimer.Elapsed += DragCooldownTimer_Elapsed;
            updatesQueue = new BlockingCollection<RawInputDigitizerData>(
                new ConcurrentQueue<RawInputDigitizerData>()
            );
            previousContacts = new RawInputDigitizerContact[] { };
        }

        public void Register(IntPtr hwnd)
        {
            RawInputDevice.RegisterDevice(HidUsageAndPage.TouchPad, RawInputDeviceFlags.InputSink, hwnd);
            RawInputDevice.RegisterDevice(HidUsageAndPage.Mouse, RawInputDeviceFlags.InputSink, hwnd);
        }

        public void Update(RawInputMouseData data)
        {
            if (
                data.Mouse.Buttons.HasFlag(Linearstar.Windows.RawInput.Native.RawMouseButtonFlags.LeftButtonUp)
            )
            {
                if (state is State.THREE_FINGER_DRAG or State.THREE_FINGER_DRAG_COOLDOWN)
                {
                    SetState(State.CLEAR);
                    dragCooldownTimer.Stop();
                }

                Thread.Sleep(100);
            }
        }

        public void Update(RawInputDigitizerData data)
        {
            RawInputDigitizerContact[] newContacts = data.Contacts
                .Where(c => c.IsButtonDown.GetValueOrDefault(false) && c.Kind == RawInputDigitizerContactKind.Finger)
                .ToArray();

            // From nothing
            if (state == State.CLEAR)
            {
                SetState(newContacts.Length switch
                {
                    0 => State.CLEAR,
                    1 => State.ONE_FINGER_IDLE,
                    2 => State.TWO_FINGER_IDLE,
                    3 => State.THREE_FINGER_IDLE,
                    _ => State.FOUR_OR_MORE,
                });
                accumulatedDistance = 0;
                previousContacts = newContacts;
                previousCenterPoint = null;
                return;
            }

            // While cooldown - reset on any movement no matter how many fingers registered
            if (state == State.THREE_FINGER_DRAG_COOLDOWN)
            {
                for (int i = 0; i < newContacts.Length && i < previousContacts.Length; i++)
                {
                    Point newPoint = new(newContacts[i].X, newContacts[i].Y);
                    Point previousPoint = new(previousContacts[i].X, previousContacts[i].Y);
                    Point diff = newPoint - (Size)previousPoint;
                    accumulatedDistance += 1 / InvSqrt((diff.X * diff.X) + (diff.Y * diff.Y));
                }

                if (accumulatedDistance > 15)
                {
                    SetState(newContacts.Length switch
                    {
                        0 => State.CLEAR,
                        1 => State.ONE_FINGER_IDLE,
                        2 => State.TWO_FINGER_IDLE,
                        3 => State.THREE_FINGER_IDLE,
                        _ => State.FOUR_OR_MORE,
                    });
                    MouseInject.LeftButtonPressed = false;
                }
            }

            // Same fingers
            if (previousContacts.Length == newContacts.Length)
            {
                Point center = CalculateCenterPoint(newContacts);
                Point diff = new();
                if (previousCenterPoint.HasValue)
                {
                    diff = center - (Size)previousCenterPoint.Value;
                    accumulatedDistance += 1 / InvSqrt((diff.X * diff.X) + (diff.Y * diff.Y));
                }
                previousCenterPoint = center;
                previousContacts = newContacts;

                if (accumulatedDistance > 15)
                {
                    SetState(state switch
                    {
                        State.ONE_FINGER_IDLE => State.ONE_FINGER_MOVING,
                        State.TWO_FINGER_IDLE => State.TWO_FINGER_MOVING,
                        State.THREE_FINGER_IDLE => State.THREE_FINGER_MOVING,
                        _ => state
                    });
                }

                if (state == State.THREE_FINGER_MOVING)
                {
                    MouseInject.LeftButtonPressed = true;
                    SetState(State.THREE_FINGER_DRAG);
                }

                if (state == State.THREE_FINGER_DRAG)
                {
                    MouseInject.Move(diff);
                }

                return;
            }

            // Added fingers
            if (previousContacts.Length < newContacts.Length)
            {
                if (state == State.THREE_FINGER_DRAG_COOLDOWN)
                {
                    if (newContacts.Length == 3)
                    {
                        SetState(State.THREE_FINGER_DRAG);
                        dragCooldownTimer.Stop();
                    }
                    else
                    {
                        // keep this state
                    }
                }
                else if (state == State.THREE_FINGER_DRAG && newContacts.Length == 3)
                {
                    // keep this state
                }
                else if (state == State.TWO_FINGER_MOVING)
                {
                    // keep this state
                }
                else
                {
                    SetState(newContacts.Length switch
                    {
                        0 => State.CLEAR,
                        1 => State.ONE_FINGER_IDLE,
                        2 => State.TWO_FINGER_IDLE,
                        3 => State.THREE_FINGER_IDLE,
                        _ => State.FOUR_OR_MORE,
                    });
                    accumulatedDistance = 0;
                }

                previousContacts = newContacts;
                previousCenterPoint = null;
                return;
            }

            // Removed fingers
            if (previousContacts.Length > newContacts.Length)
            {
                if (state == State.THREE_FINGER_DRAG)
                {
                    if (newContacts.Length == 2)
                    {
                        // keep dragging
                    }
                    else
                    {
                        SetState(State.THREE_FINGER_DRAG_COOLDOWN);
                        dragCooldownTimer.Start();
                        accumulatedDistance = 0;
                    }
                }
                else if (state == State.THREE_FINGER_DRAG_COOLDOWN)
                {

                    // keep this state
                }
                else if (state == State.FOUR_OR_MORE && newContacts.Length == 3)
                {
                    SetState(State.THREE_FINGER_DRAG);
                }
                else if (state == State.TWO_FINGER_MOVING && newContacts.Length >= 2)
                {
                    // keep this state
                }
                else
                {
                    SetState(newContacts.Length switch
                    {
                        0 => State.CLEAR,
                        1 => State.ONE_FINGER_IDLE,
                        2 => State.TWO_FINGER_IDLE,
                        3 => State.THREE_FINGER_IDLE,
                        _ => State.FOUR_OR_MORE,
                    });
                    accumulatedDistance = 0;
                }

                previousContacts = newContacts;
                previousCenterPoint = null;

                return;
            }

        }

        public delegate void StateChangeEvent(object sender, State newState);

        public event StateChangeEvent? StateChange;

        private void SetState(State newState)
        {
            state = newState;
            StateChange?.Invoke(this, newState);
        }

        private float InvSqrt(float x)
        {
            float xhalf = 0.5f * x;
            int i = BitConverter.SingleToInt32Bits(x);
            i = 0x5f3759df - (i >> 1);
            x = BitConverter.Int32BitsToSingle(i);
            x *= 1.5f - (xhalf * x * x);
            return x;
        }

        protected Point CalculateCenterPoint(RawInputDigitizerContact[] contacts)
        {
            if (contacts.Length == 0)
            {
                throw new ArgumentException("empty list of contacts");
            }

            if (contacts.Length == 1)
            {
                return new Point(contacts[0].X, contacts[0].Y);
            }

            Point p = new(0, 0);
            foreach (RawInputDigitizerContact contact in contacts)
            {
                p.X += contact.X;
                p.Y += contact.Y;
            }
            p.X /= contacts.Length;
            p.Y /= contacts.Length;

            return p;
        }

        private void DragCooldownTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            SetState(State.CLEAR);
            if (MouseInject.LeftButtonPressed)
            {
                MouseInject.LeftButtonPressed = false;
            }
        }

        public bool IsDisposed { get; private set; }

        ~TouchProcessor()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                RawInputDevice.UnregisterDevice(HidUsageAndPage.TouchPad);
                RawInputDevice.UnregisterDevice(HidUsageAndPage.Mouse);
            }
        }

    }
}
