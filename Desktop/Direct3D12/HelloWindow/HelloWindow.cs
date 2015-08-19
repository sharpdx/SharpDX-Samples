using SharpDX.DXGI;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelloWindow
{
    using SharpDX;
    using SharpDX.Direct3D12;
    using SharpDX.Windows;

    public class HelloWindow : IDisposable
    {
        /// <summary>
        /// Initialise pipeline and assets
        /// </summary>
        /// <param name="form">The form</param>
        public void Initialize(RenderForm form)
        {
            LoadPipeline(form);
            LoadAssets();
        }

        private void LoadPipeline(RenderForm form)
        {
            int width = form.ClientSize.Width;
            int height = form.ClientSize.Height;

#if DEBUG
            // Enable the D3D12 debug layer.
            {
                DebugInterface.Get().EnableDebugLayer();
            }
#endif
            device = new Device(null, SharpDX.Direct3D.FeatureLevel.Level_11_0);
            using (var factory = new Factory4())
            {
                // Describe and create the command queue.
                CommandQueueDescription queueDesc = new CommandQueueDescription(CommandListType.Direct);
                commandQueue = device.CreateCommandQueue(queueDesc);


                // Describe and create the swap chain.
                SwapChainDescription swapChainDesc = new SwapChainDescription()
                {
                    BufferCount = FrameCount,
                    ModeDescription = new ModeDescription(width, height, new Rational(60, 1), Format.R8G8B8A8_UNorm),
                    Usage = Usage.RenderTargetOutput,
                    SwapEffect = SwapEffect.FlipDiscard,
                    OutputHandle = form.Handle,
                    //Flags = SwapChainFlags.None,
                    SampleDescription = new SampleDescription(1, 0),
                    IsWindowed = true
                };

                SwapChain tempSwapChain = new SwapChain(factory, commandQueue, swapChainDesc);
                swapChain = tempSwapChain.QueryInterface<SwapChain3>();
                tempSwapChain.Dispose();
                frameIndex = swapChain.CurrentBackBufferIndex;
            }

            // Create descriptor heaps.
            // Describe and create a render target view (RTV) descriptor heap.
            DescriptorHeapDescription rtvHeapDesc = new DescriptorHeapDescription()
            {
                DescriptorCount = FrameCount,
                Flags = DescriptorHeapFlags.None,
                Type = DescriptorHeapType.RenderTargetView
            };

            renderTargetViewHeap = device.CreateDescriptorHeap(rtvHeapDesc);

            rtvDescriptorSize = device.GetDescriptorHandleIncrementSize(DescriptorHeapType.RenderTargetView);

            // Create frame resources.
            CpuDescriptorHandle rtvHandle = renderTargetViewHeap.CPUDescriptorHandleForHeapStart;
            for (int n = 0; n < FrameCount; n++)
            {
                renderTargets[n] = swapChain.GetBackBuffer<Resource>(n);
                device.CreateRenderTargetView(renderTargets[n], null, rtvHandle);
                rtvHandle += rtvDescriptorSize;
            }

            commandAllocator = device.CreateCommandAllocator(CommandListType.Direct);
        }

        private void LoadAssets()
        {
            // Create the command list.
            commandList = device.CreateCommandList(CommandListType.Direct, commandAllocator, null);

            // Command lists are created in the recording state, but there is nothing
            // to record yet. The main loop expects it to be closed, so close it now.
            commandList.Close();

            // Create synchronization objects.
            fence = device.CreateFence(0, FenceFlags.None);
            fenceValue = 1;

            // Create an event handle to use for frame synchronization.
            fenceEvent = new AutoResetEvent(false);
        }

        private void PopulateCommandList()
        {
            // Command list allocators can only be reset when the associated 
            // command lists have finished execution on the GPU; apps should use 
            // fences to determine GPU execution progress.
            commandAllocator.Reset();

            // However, when ExecuteCommandList() is called on a particular command 
            // list, that command list can then be reset at any time and must be before 
            // re-recording.
            commandList.Reset(commandAllocator, null);

            // Indicate that the back buffer will be used as a render target.
            commandList.ResourceBarrierTransition(renderTargets[frameIndex], ResourceStates.Present, ResourceStates.RenderTarget);

            CpuDescriptorHandle rtvHandle = renderTargetViewHeap.CPUDescriptorHandleForHeapStart;
            rtvHandle += frameIndex * rtvDescriptorSize;

            // Record commands.
            commandList.ClearRenderTargetView(rtvHandle, new Color4(0, 0.2F, 0.4f, 1), 0, null);

            // Indicate that the back buffer will now be used to present.
            commandList.ResourceBarrierTransition(renderTargets[frameIndex], ResourceStates.RenderTarget, ResourceStates.Present);

            commandList.Close();
        }

        /// <summary> 
        /// Wait the previous command list to finish executing. 
        /// </summary> 
        private void WaitForPreviousFrame()
        {
            // WAITING FOR THE FRAME TO COMPLETE BEFORE CONTINUING IS NOT BEST PRACTICE. 
            // This is code implemented as such for simplicity. 

            int fence = fenceValue;
            commandQueue.Signal(this.fence, fence);
            fenceValue++;

            // Wait until the previous frame is finished.
            if (this.fence.CompletedValue < fence)
            {
                this.fence.SetEventOnCompletion(fence, fenceEvent.SafeWaitHandle.DangerousGetHandle());
                fenceEvent.WaitOne();
            }

            frameIndex = swapChain.CurrentBackBufferIndex;
        }

        public void Update()
        {
        }

        public void Render()
        {
            // Record all the commands we need to render the scene into the command list.
            PopulateCommandList();

            // Execute the command list.
            commandQueue.ExecuteCommandList(commandList);

            // Present the frame.
            swapChain.Present(1, 0);

            WaitForPreviousFrame();
        }

        public void Dispose()
        {
            // Wait for the GPU to be done with all resources.
            WaitForPreviousFrame();

            //release all resources
            foreach (var target in renderTargets)
            {
                target.Dispose();
            }
            commandAllocator.Dispose();
            commandQueue.Dispose();
            renderTargetViewHeap.Dispose();
            commandList.Dispose();
            fence.Dispose();
            swapChain.Dispose();
            device.Dispose();
        }

        const int FrameCount = 2;

        // Pipeline objects.
        private SwapChain3 swapChain;
        private Device device;
        private Resource[] renderTargets = new Resource[FrameCount];

        private CommandAllocator commandAllocator;
        private CommandQueue commandQueue;
        private DescriptorHeap renderTargetViewHeap;

        private GraphicsCommandList commandList;
        private int rtvDescriptorSize;

        // Synchronization objects.
        private int frameIndex;
        private AutoResetEvent fenceEvent;

        private Fence fence;
        private int fenceValue;


    }
}
