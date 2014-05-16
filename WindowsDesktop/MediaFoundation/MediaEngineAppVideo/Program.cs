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
using System.Threading;
using System.Windows.Forms;

using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.MediaFoundation;

using DXDevice = SharpDX.Direct3D11.Device;
using SharpDX.DXGI;
using SharpDX.Windows;

namespace MediaEngineApp
{
    /// <summary>
    /// Demonstrates simple usage of MediaEngine on Windows by playing a video and audio selected by a file dialog.
    /// Note that this sample is not "Dispose" safe and is not releasing any COM resources.
    /// Also please note that this sample might not work on NVidia Cards (due to video engine broken on many drivers),
    /// This has been tested and working on Intel and ATI.
    /// </summary>
    class Program
    {
        /// <summary>
        /// The event raised when MediaEngine is ready to play the music.
        /// </summary>
        private static readonly ManualResetEvent eventReadyToPlay = new ManualResetEvent(false);

        /// <summary>
        /// Set when the music is stopped.
        /// </summary>
        private static bool isMusicStopped;

        /// <summary>
        /// The instance of MediaEngineEx
        /// </summary>
        private static MediaEngineEx mediaEngineEx;

        /// <summary>
        /// Our dx11 device
        /// </summary>
        private static DXDevice device;

        /// <summary>
        /// Our SwapChain
        /// </summary>
        private static SwapChain swapChain;

        /// <summary>
        /// DXGI Manager
        /// </summary>
        private static DXGIDeviceManager dxgiManager;



        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
        /// <param name="args">The args.</param>
        [STAThread]
        static void Main(string[] args)
        {
            // Select a File to play
            var openFileDialog = new OpenFileDialog { Title = "Select a file", Filter = "Media Files(*.WMV;*.MP4;*.AVI)|*.WMV;*.MP4;*.AVI" };
            var result = openFileDialog.ShowDialog();
            if (result == DialogResult.Cancel)
            {
                return;
            }

            // Initialize MediaFoundation
            MediaManager.Startup();

            var renderForm = new SharpDX.Windows.RenderForm();

            device = CreateDeviceForVideo(out dxgiManager);

            // Creates the MediaEngineClassFactory
            var mediaEngineFactory = new MediaEngineClassFactory();
            
            //Assign our dxgi manager, and set format to bgra
            MediaEngineAttributes attr = new MediaEngineAttributes();
            attr.VideoOutputFormat = (int)SharpDX.DXGI.Format.B8G8R8A8_UNorm;
            attr.DxgiManager = dxgiManager;

            // Creates MediaEngine for AudioOnly 
            var mediaEngine = new MediaEngine(mediaEngineFactory, attr, MediaEngineCreateFlags.None);

            // Register our PlayBackEvent
            mediaEngine.PlaybackEvent += OnPlaybackCallback;

            // Query for MediaEngineEx interface
            mediaEngineEx = mediaEngine.QueryInterface<MediaEngineEx>();

            // Opens the file
            var fileStream = openFileDialog.OpenFile();
            
            // Create a ByteStream object from it
            var stream = new ByteStream(fileStream);

            // Creates an URL to the file
            var url = new Uri(openFileDialog.FileName, UriKind.RelativeOrAbsolute);

            // Set the source stream
            mediaEngineEx.SetSourceFromByteStream(stream, url.AbsoluteUri);

            // Wait for MediaEngine to be ready
            if (!eventReadyToPlay.WaitOne(1000))
            {
                Console.WriteLine("Unexpected error: Unable to play this file");
            }

            //Create our swapchain
            swapChain = CreateSwapChain(device, renderForm.Handle);

            //Get DXGI surface to be used by our media engine
            var texture = Texture2D.FromSwapChain<Texture2D>(swapChain, 0);
            var surface = texture.QueryInterface<SharpDX.DXGI.Surface>();

            //Get our video size
            int w, h;
            mediaEngine.GetNativeVideoSize(out w, out h);

            // Play the music
            mediaEngineEx.Play();

            long ts;

            RenderLoop.Run(renderForm, () =>
            {
                //Transfer frame if a new one is available
                if (mediaEngine.OnVideoStreamTick(out ts))
                {
                    mediaEngine.TransferVideoFrame(surface, null, new SharpDX.Rectangle(0, 0, w, h), null);
                }

                swapChain.Present(1, SharpDX.DXGI.PresentFlags.None);
            });

            mediaEngine.Shutdown();
            swapChain.Dispose();
            device.Dispose();
        }

        /// <summary>
        /// Called when [playback callback].
        /// </summary>
        /// <param name="playEvent">The play event.</param>
        /// <param name="param1">The param1.</param>
        /// <param name="param2">The param2.</param>
        private static void OnPlaybackCallback(MediaEngineEvent playEvent, long param1, int param2)
        {
            switch (playEvent)
            {
                case MediaEngineEvent.CanPlay:
                    eventReadyToPlay.Set();
                    break;
                case MediaEngineEvent.TimeUpdate:
                    break;
                case MediaEngineEvent.Error:
                case MediaEngineEvent.Abort:
                case MediaEngineEvent.Ended:
                    isMusicStopped = true;
                    break;
            }
        }

        /// <summary>
        /// Creates device with necessary flags for video processing
        /// </summary>
        /// <param name="manager">DXGI Manager, used to create media engine</param>
        /// <returns>Device with video support</returns>
        private static DXDevice CreateDeviceForVideo(out DXGIDeviceManager manager)
        {
            //Device need bgra and video support
            var device = new DXDevice(SharpDX.Direct3D.DriverType.Hardware, DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport);

            //Add multi thread protection on device
            DeviceMultithread mt = device.QueryInterface<DeviceMultithread>();
            mt.SetMultithreadProtected(true);

            //Reset device
            manager = new DXGIDeviceManager();
            manager.ResetDevice(device);

            return device;
        }

        /// <summary>
        /// Creates swap chain ready to use for video output
        /// </summary>
        /// <param name="dxdevice">DirectX11 device</param>
        /// <param name="handle">RenderForm Handle</param>
        /// <returns>SwapChain</returns>
        private static SwapChain CreateSwapChain(DXDevice dxdevice, IntPtr handle)
        {
            //Walk up device to retrieve Factory, necessary to create SwapChain
            var dxgidevice = dxdevice.QueryInterface<SharpDX.DXGI.Device>();           
            var adapter =  dxgidevice.Adapter.QueryInterface<Adapter>();
            var factory = adapter.GetParent<Factory1>();

            /*To be allowed to be used as video, texture must be of the same format (eg: bgra), and needs to be bindable are render target.
             * you do not need to create render target view, only the flag*/
            SwapChainDescription sd = new SwapChainDescription()
            {
                BufferCount = 1,
                ModeDescription = new ModeDescription(0, 0, new Rational(60, 1), Format.B8G8R8A8_UNorm),
                IsWindowed = true,
                OutputHandle = handle,
                SampleDescription = new SampleDescription(1,0),
                SwapEffect = SwapEffect.Discard,
                Usage = Usage.RenderTargetOutput,
                Flags = SwapChainFlags.None
            };

           return new SwapChain(factory, dxdevice, sd);
        }
    }
}
