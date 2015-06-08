// Copyright (c) 2010-2015 SharpDX - Alexandre Mutel
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
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.DXGI;

namespace HelloWorld
{
    using SharpDX.Direct3D12;

    /// <summary>
    /// HelloWorldD3D12 sample demonstrating clearing the screen. with D3D12 API.
    /// </summary>
    public class HelloWorld : IDisposable
    {
        private const int SwapBufferCount = 2;
        private int width;
        private int height;
        private Device device;
        private CommandAllocator commandListAllocator;
        private CommandQueue commandQueue;
        private SwapChain swapChain;
        private DescriptorHeap descriptorHeap;
        private GraphicsCommandList commandList;
        private Resource renderTarget;
        private Rectangle scissorRectangle;
        private ViewportF viewPort;
        private AutoResetEvent eventHandle;
        private Fence fence;
        private long currentFence;
        private int indexLastSwapBuf;
        private readonly Stopwatch clock;

        /// <summary>
        /// Constructor.
        /// </summary>
        public HelloWorld()
        {
            clock = Stopwatch.StartNew();
        }

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        /// <param name="form">The form.</param>
        public void Initialize(Form form)
        {
            width = form.ClientSize.Width;
            height = form.ClientSize.Height;

            LoadPipeline(form);
            LoadAssets();
        }

        /// <summary>
        /// Updates this instance.
        /// </summary>
        public void Update()
        {
        }

        /// <summary>
        /// Render scene
        /// </summary>
        public void Render()
        {
            // record all the commands we need to render the scene into the command list
            PopulateCommandLists();

            // execute the command list
            commandQueue.ExecuteCommandList(commandList);

            // swap the back and front buffers
            swapChain.Present(1, 0);
            indexLastSwapBuf = (indexLastSwapBuf + 1) % SwapBufferCount;
            Utilities.Dispose(ref renderTarget);
            renderTarget = swapChain.GetBackBuffer<Resource>(indexLastSwapBuf);
            device.CreateRenderTargetView(renderTarget, null, descriptorHeap.CPUDescriptorHandleForHeapStart);

            // wait and reset EVERYTHING
            WaitForPrevFrame();
        }

        /// <summary>
        /// Cleanup allocations
        /// </summary>
        public void Dispose()
        {
            // wait for the GPU to be done with all resources
            WaitForPrevFrame();

            swapChain.SetFullscreenState(false, null);
            
            eventHandle.Close();

            // asset objects
            Utilities.Dispose(ref commandList);

            // pipeline objects
            Utilities.Dispose(ref descriptorHeap);
            Utilities.Dispose(ref renderTarget);
            Utilities.Dispose(ref commandListAllocator);
            Utilities.Dispose(ref commandQueue);
            Utilities.Dispose(ref device);
            Utilities.Dispose(ref swapChain);
        }

        /// <summary>
        /// Creates the rendering pipeline.
        /// </summary>
        /// <param name="form">The form.</param>
        private void LoadPipeline(Form form)
        {
            // create swap chain descriptor
            var swapChainDescription = new SwapChainDescription()
            {
                BufferCount = SwapBufferCount,
                ModeDescription = new ModeDescription(Format.R8G8B8A8_UNorm),
                Usage = Usage.RenderTargetOutput,
                OutputHandle = form.Handle,
                SwapEffect = SwapEffect.FlipDiscard,
                SampleDescription = new SampleDescription(1, 0),
                IsWindowed = true
            };

            // create the device
            try
            {
                device = CreateDeviceWithSwapChain(DriverType.Hardware, FeatureLevel.Level_11_0, swapChainDescription, out swapChain, out commandQueue);
            }
            catch(SharpDXException)
            {
                device = CreateDeviceWithSwapChain(DriverType.Warp, FeatureLevel.Level_11_0, swapChainDescription, out swapChain, out commandQueue);
            }

            // create command queue and allocator objects
            commandListAllocator = device.CreateCommandAllocator(CommandListType.Direct);
        }

        /// <summary>
        /// Setup resources for rendering
        /// </summary>
        private void LoadAssets()
        {
            // Create the descriptor heap for the render target view
            descriptorHeap = device.CreateDescriptorHeap(new DescriptorHeapDescription()
            {
                Type = DescriptorHeapType.RenderTargetView,
                DescriptorCount = 1
            });

            // Create the main command list
            commandList = device.CreateCommandList(CommandListType.Direct, commandListAllocator, null);

            // Get the backbuffer and creates the render target view
            renderTarget = swapChain.GetBackBuffer<Resource>(0);
            device.CreateRenderTargetView(renderTarget, null, descriptorHeap.CPUDescriptorHandleForHeapStart);

            // Create the viewport
            viewPort = new ViewportF(0, 0, width, height);

            // Create the scissor
            scissorRectangle = new Rectangle(0, 0, width, height);

            // Create a fence to wait for next frame
            fence = device.CreateFence(0, FenceFlags.None);
            currentFence = 1;

            // Close command list
            commandList.Close();

            // Create an event handle use for VTBL
            eventHandle = new AutoResetEvent(false);

            // Wait the command list to complete
            WaitForPrevFrame();
        }

        /// <summary>
        /// Fill the command list with commands
        /// </summary>
        private void PopulateCommandLists()
        {
            commandListAllocator.Reset();

            commandList.Reset(commandListAllocator, null);

            // setup viewport and scissors
            commandList.SetViewport(viewPort);
            commandList.SetScissorRectangles(scissorRectangle);

	        // Use barrier to notify that we are using the RenderTarget to clear it
            commandList.ResourceBarrierTransition(renderTarget, ResourceStates.Present, ResourceStates.RenderTarget);

	        // Clear the RenderTarget
            var time = clock.Elapsed.TotalSeconds;
	        commandList.ClearRenderTargetView(descriptorHeap.CPUDescriptorHandleForHeapStart, new Color4((float)Math.Sin(time) * 0.25f + 0.5f, (float)Math.Sin(time * 0.5f) * 0.4f + 0.6f, 0.4f, 1.0f), 0,  null);

	        // Use barrier to notify that we are going to present the RenderTarget
            commandList.ResourceBarrierTransition(renderTarget, ResourceStates.RenderTarget, ResourceStates.Present);

	        // Execute the command
            commandList.Close();
        }

        /// <summary>
        /// Wait the previous command list to finish executing.
        /// </summary>
        private void WaitForPrevFrame()
        {
	        // WAITING FOR THE FRAME TO COMPLETE BEFORE CONTINUING IS NOT BEST PRACTICE.
	        // This is code implemented as such for simplicity.
            long localFence = currentFence;
            commandQueue.Signal(fence, localFence);
            currentFence++;

            if (fence.CompletedValue < localFence)
            {                
                fence.SetEventOnCompletion(localFence, eventHandle.SafeWaitHandle.DangerousGetHandle());
                eventHandle.WaitOne();
            }
        }
        
        private static Device CreateDeviceWithSwapChain(DriverType driverType, FeatureLevel level,
            SwapChainDescription swapChainDescription,
            out SwapChain swapChain, out CommandQueue queue)
        {
#if DEBUG
            // Enable the D3D12 debug layer.
            // DebugInterface.Get().EnableDebugLayer();
#endif
            using (var factory = new Factory4())
            {
                var adapter = driverType == DriverType.Hardware ? null : factory.GetWarpAdapter();
                var device = new Device(adapter, level);
                queue = device.CreateCommandQueue(new CommandQueueDescription(CommandListType.Direct));

                swapChain = new SwapChain(factory, queue, swapChainDescription);
                return device;
            }
        }
    }
}