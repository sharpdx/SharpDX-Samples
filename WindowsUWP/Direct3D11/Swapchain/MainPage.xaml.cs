using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using SharpDX;
using SharpDX.Direct3D11;
using Windows.ApplicationModel;
using System.Text;
using System.Diagnostics;

namespace UWPSwapchain
{


    public sealed partial class MainPage : Page
    {
        SharpDX.Direct3D11.Device device;
        SharpDX.Direct3D11.DeviceContext deviceContext;

        SharpDX.DXGI.SwapChain1 swapchain;
        Texture2D backBufferTexture;
        RenderTargetView backBufferView;

        Stopwatch sw = Stopwatch.StartNew();

        public MainPage()
        {
            this.InitializeComponent();

            bool enableDebug = false;

            DeviceCreationFlags flags = DeviceCreationFlags.BgraSupport;
            if (enableDebug)
                flags |= DeviceCreationFlags.Debug;

            this.device = new SharpDX.Direct3D11.Device(SharpDX.Direct3D.DriverType.Hardware, flags);
            this.deviceContext = this.device.ImmediateContext;
        }

        private void CompositionTarget_Rendering(object sender, object e)
        {
            SharpDX.Color4 c = Color.Red;
            c.Red = (float)Math.Abs(Math.Sin(sw.Elapsed.TotalSeconds));
        
            //This is your render call, perform all render tasks and then call present on the swap chain
            this.deviceContext.ClearRenderTargetView(this.backBufferView, c);

            //Attach render target and set viewport
            this.deviceContext.OutputMerger.SetRenderTargets(this.backBufferView);
            this.deviceContext.Rasterizer.SetViewport(new ViewportF(0.0f, 0.0f, this.backBufferTexture.Description.Width, this.backBufferTexture.Description.Height, 0.0f, 1.0f));

            //perform draw calls here

            this.swapchain.Present(0, PresentFlags.None);
        }

        private void panel_Loaded(object sender, RoutedEventArgs e)
        {
            //This is trigerred when panel is loaded, you can now create your swap chain.

            //Please note : Do not use this.panel.Width, make sure to use render size as width property returns NaN on create
            SwapChainDescription1 swapChainDescription = new SwapChainDescription1()
            {
                AlphaMode = AlphaMode.Ignore,
                BufferCount = 2,
                Format = Format.R8G8B8A8_UNorm,
                Height = (int)(this.panel.RenderSize.Height),
                Width = (int)(this.panel.RenderSize.Width),
                SampleDescription = new SampleDescription(1, 0),
                Scaling = SharpDX.DXGI.Scaling.Stretch,
                Stereo = false,
                SwapEffect = SwapEffect.FlipSequential,
                Usage = Usage.RenderTargetOutput
            };

            using (SharpDX.DXGI.Device3 dxgiDevice3 = this.device.QueryInterface<SharpDX.DXGI.Device3>())
            {
                using (SharpDX.DXGI.Factory3 dxgiFactory3 = dxgiDevice3.Adapter.GetParent<SharpDX.DXGI.Factory3>())
                {
                    SharpDX.DXGI.SwapChain1 swapChain1 = new SharpDX.DXGI.SwapChain1(dxgiFactory3, this.device, ref swapChainDescription);
                    this.swapchain = swapChain1;
                }
            }

            using (SharpDX.DXGI.ISwapChainPanelNative nativeObject = SharpDX.ComObject.As<SharpDX.DXGI.ISwapChainPanelNative>(this.panel))
            {
                nativeObject.SwapChain = this.swapchain;
            }

            this.backBufferTexture = this.swapchain.GetBackBuffer<Texture2D>(0);
            this.backBufferView = new RenderTargetView(this.device, this.backBufferTexture);

            CompositionTarget.Rendering += CompositionTarget_Rendering;
            Application.Current.Suspending += Current_Suspending;
        }

        private void Current_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            //This is the only part where you will receive a "close event", you should serialize any state so app can resume correctly.

            //Please note that this method has a time out (e.SuspendingOperation.Deadline), if you do not return prior to this time, application will terminate instead of being suspended.

            if (this.swapchain != null)
            {
                this.deviceContext.ClearState();
                using (SharpDX.DXGI.Device3 dxgiDevice3 = this.device.QueryInterface<SharpDX.DXGI.Device3>())
                    dxgiDevice3.Trim();
            }


        }

        private void panel_Unloaded(object sender, RoutedEventArgs e)
        {
            //On uwp windows 10 , this is never called, quitting the app does not do anything on it
        }

        private void panel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            /* Resize swap chain, to do it cleanly, we clear state (to ensure resources are not bound to pipeline, and
             * dispose associated resources (eg : render view and texture object) 
             * Once done, we can call resizebuffers, and create direct3d11 objects again */
            if (this.swapchain != null)
            {
                this.deviceContext.ClearState();

                Size2 newSize = new Size2((int)e.NewSize.Width, (int)e.NewSize.Height);

                Utilities.Dispose(ref this.backBufferView);
                Utilities.Dispose(ref this.backBufferTexture);

                this.swapchain.ResizeBuffers(this.swapchain.Description.BufferCount, (int)e.NewSize.Width, (int)e.NewSize.Height, swapchain.Description1.Format, swapchain.Description1.Flags);

                this.backBufferTexture = this.swapchain.GetBackBuffer<Texture2D>(0);
                this.backBufferView = new RenderTargetView(this.device, this.backBufferTexture);

            }
        }
    }
}
