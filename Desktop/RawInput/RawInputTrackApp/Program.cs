// Copyright (c) 2010-2013 SharpDX - Alexandre Mutel
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Concurrent;
using System.Windows.Forms;

using SharpDX.Multimedia;
using SharpDX.RawInput;

namespace MouseTrackApp
{
    /// <summary>
    /// Show how to use 
    /// </summary>
    static class Program
    {
        private static TextBox textBox;
        private static readonly ConcurrentDictionary<IntPtr, string> DeviceNameCache = new ConcurrentDictionary<IntPtr, string>();

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {

            var form = new Form { Width = 800, Height = 600};
            textBox = new TextBox() { Dock = DockStyle.Fill, Multiline = true, Text = "Interact with the mouse or the keyboard...\r\n", ReadOnly = true};
            form.Controls.Add(textBox);
            form.Visible = true;

            // setup the device
            Device.RegisterDevice(UsagePage.Generic, UsageId.GenericMouse, DeviceFlags.None);
            Device.MouseInput += (sender, args) => textBox.Invoke(new UpdateTextCallback(UpdateMouseText), args);

            Device.RegisterDevice(UsagePage.Generic, UsageId.GenericKeyboard, DeviceFlags.None);
            Device.KeyboardInput += (sender, args) => textBox.Invoke(new UpdateTextCallback(UpdateKeyboardText), args);

            Application.Run(form);
        }

        /// <summary>
        /// Updates the mouse text.
        /// </summary>
        /// <param name="rawArgs">The <see cref="SharpDX.RawInput.RawInputEventArgs"/> instance containing the event data.</param>
        static void UpdateMouseText(RawInputEventArgs rawArgs)
        {
            const string sep = "        ";
            var args = (MouseInputEventArgs)rawArgs;
            var devName = GetDeviceName(args.Device);
            textBox.AppendText(
                $"Mouse: {devName} {sep} Coords: {args.X},{args.Y} {sep} Buttons: {args.ButtonFlags} {sep} State: {args.Mode} {sep} Wheel: {args.WheelDelta}\r\n");
        }

        static string GetDeviceName(IntPtr devPtr)
        {
            if (DeviceNameCache.ContainsKey(devPtr))
            {
                return DeviceNameCache[devPtr];
            }
            var devices = Device.GetDevices();
            var deviceName = devPtr.ToString();
            foreach (var dev in devices)
            {
                if (dev.Handle != devPtr) continue;
                deviceName = dev.DeviceName.Split('#')[1];
                break;
            }
            DeviceNameCache.TryAdd(devPtr, deviceName);
            return deviceName;
        }

        /// <summary>
        /// Updates the keyboard text.
        /// </summary>
        /// <param name="rawArgs">The <see cref="SharpDX.RawInput.RawInputEventArgs"/> instance containing the event data.</param>
        static void UpdateKeyboardText(RawInputEventArgs rawArgs)
        {
            const string sep = "        ";
            var args = (KeyboardInputEventArgs)rawArgs;
            var devName = GetDeviceName(args.Device);
            textBox.AppendText($"Keyboard: {devName} {sep} Key: {args.Key} ({(int)args.Key}) {sep} State: {args.State} {sep} ScanCodeFlags: {args.ScanCodeFlags}\r\n");
        }

        /// <summary>
        /// Delegate use for printing events
        /// </summary>
        /// <param name="args">The <see cref="SharpDX.RawInput.RawInputEventArgs"/> instance containing the event data.</param>
        public delegate void UpdateTextCallback(RawInputEventArgs args);
    }
}
