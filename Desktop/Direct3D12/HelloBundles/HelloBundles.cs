using SharpDX.DXGI;
using System.Threading;
using System;

namespace HelloBundles
{
    using SharpDX;
    using SharpDX.Direct3D12;
    using SharpDX.Windows;

    public class HelloBundles : IDisposable
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

            viewport.Width = width;
            viewport.Height = height;
            viewport.MaxDepth = 1.0f;

            scissorRect.Right = width;
            scissorRect.Bottom = height;

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
                var queueDesc = new CommandQueueDescription(CommandListType.Direct);
                commandQueue = device.CreateCommandQueue(queueDesc);


                // Describe and create the swap chain.
                var swapChainDesc = new SwapChainDescription()
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

                var tempSwapChain = new SwapChain(factory, commandQueue, swapChainDesc);
                swapChain = tempSwapChain.QueryInterface<SwapChain3>();
                tempSwapChain.Dispose();
                frameIndex = swapChain.CurrentBackBufferIndex;
            }

            // Create descriptor heaps.
            // Describe and create a render target view (RTV) descriptor heap.
            var rtvHeapDesc = new DescriptorHeapDescription()
            {
                DescriptorCount = FrameCount,
                Flags = DescriptorHeapFlags.None,
                Type = DescriptorHeapType.RenderTargetView
            };

            renderTargetViewHeap = device.CreateDescriptorHeap(rtvHeapDesc);

            rtvDescriptorSize = device.GetDescriptorHandleIncrementSize(DescriptorHeapType.RenderTargetView);

            // Create frame resources.
            var rtvHandle = renderTargetViewHeap.CPUDescriptorHandleForHeapStart;
            for (int n = 0; n < FrameCount; n++)
            {
                renderTargets[n] = swapChain.GetBackBuffer<Resource>(n);
                device.CreateRenderTargetView(renderTargets[n], null, rtvHandle);
                rtvHandle += rtvDescriptorSize;
            }

            commandAllocator = device.CreateCommandAllocator(CommandListType.Direct);
            bundleAllocator = device.CreateCommandAllocator(CommandListType.Bundle);
        }

        private void LoadAssets()
        {
            // Create an empty root signature.
            var rootSignatureDesc = new RootSignatureDescription(RootSignatureFlags.AllowInputAssemblerInputLayout);
            rootSignature = device.CreateRootSignature(rootSignatureDesc.Serialize());

            // Create the pipeline state, which includes compiling and loading shaders.

#if DEBUG
            var vertexShader = new ShaderBytecode(SharpDX.D3DCompiler.ShaderBytecode.CompileFromFile("shaders.hlsl", "VSMain", "vs_5_0", SharpDX.D3DCompiler.ShaderFlags.Debug));
#else
            var vertexShader = new ShaderBytecode(SharpDX.D3DCompiler.ShaderBytecode.CompileFromFile("shaders.hlsl", "VSMain", "vs_5_0"));
#endif

#if DEBUG
            var pixelShader = new ShaderBytecode(SharpDX.D3DCompiler.ShaderBytecode.CompileFromFile("shaders.hlsl", "PSMain", "ps_5_0", SharpDX.D3DCompiler.ShaderFlags.Debug));
#else
            var pixelShader = new ShaderBytecode(SharpDX.D3DCompiler.ShaderBytecode.CompileFromFile("shaders.hlsl", "PSMain", "ps_5_0"));
#endif

            // Define the vertex input layout.
            var inputElementDescs = new []
            {
                    new InputElement("POSITION",0,Format.R32G32B32_Float,0,0),
                    new InputElement("COLOR",0,Format.R32G32B32A32_Float,12,0)
            };

            // Describe and create the graphics pipeline state object (PSO).
            var psoDesc = new GraphicsPipelineStateDescription()
            {
                InputLayout = new InputLayoutDescription(inputElementDescs),
                RootSignature = rootSignature,
                VertexShader = vertexShader,
                PixelShader = pixelShader,
                RasterizerState = RasterizerStateDescription.Default(),
                BlendState = BlendStateDescription.Default(),
                DepthStencilFormat = SharpDX.DXGI.Format.D32_Float,
                DepthStencilState = new DepthStencilStateDescription() { IsDepthEnabled = false, IsStencilEnabled = false },
                SampleMask = int.MaxValue,
                PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
                RenderTargetCount = 1,
                Flags = PipelineStateFlags.None,
                SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                StreamOutput = new StreamOutputDescription()
            };
            psoDesc.RenderTargetFormats[0] = SharpDX.DXGI.Format.R8G8B8A8_UNorm;

            pipelineState = device.CreateGraphicsPipelineState(psoDesc);

            // Create the command list.
            commandList = device.CreateCommandList(CommandListType.Direct, commandAllocator, pipelineState);

            // Command lists are created in the recording state, but there is nothing
            // to record yet. The main loop expects it to be closed, so close it now.
            commandList.Close();

            // Create the vertex buffer.
            float aspectRatio = viewport.Width / viewport.Height;

            // Define the geometry for a triangle.
            var triangleVertices = new []
            {
                    new Vertex() {Position=new Vector3(0.0f, 0.25f * aspectRatio, 0.0f ),Color=new Vector4(1.0f, 0.0f, 0.0f, 1.0f ) },
                    new Vertex() {Position=new Vector3(0.25f, -0.25f * aspectRatio, 0.0f),Color=new Vector4(0.0f, 1.0f, 0.0f, 1.0f) },
                    new Vertex() {Position=new Vector3(-0.25f, -0.25f * aspectRatio, 0.0f),Color=new Vector4(0.0f, 0.0f, 1.0f, 1.0f ) },
            };
            
            int vertexBufferSize = Utilities.SizeOf(triangleVertices);

            // Note: using upload heaps to transfer static data like vert buffers is not 
            // recommended. Every time the GPU needs it, the upload heap will be marshalled 
            // over. Please read up on Default Heap usage. An upload heap is used here for 
            // code simplicity and because there are very few verts to actually transfer.
            vertexBuffer = device.CreateCommittedResource(new HeapProperties(HeapType.Upload), HeapFlags.None, ResourceDescription.Buffer(vertexBufferSize), ResourceStates.GenericRead);
            
            // Copy the triangle data to the vertex buffer.
            IntPtr pVertexDataBegin = vertexBuffer.Map(0);
            Utilities.Write(pVertexDataBegin, triangleVertices, 0, triangleVertices.Length);
            vertexBuffer.Unmap(0);

            // Initialize the vertex buffer view.
            vertexBufferView = new VertexBufferView();
            vertexBufferView.BufferLocation = vertexBuffer.GPUVirtualAddress;
            vertexBufferView.StrideInBytes = Utilities.SizeOf<Vertex>();
            vertexBufferView.SizeInBytes = vertexBufferSize;
            
            // Create and record the bundle.
            bundle = device.CreateCommandList(0, CommandListType.Bundle, bundleAllocator, pipelineState);
            bundle.SetGraphicsRootSignature(rootSignature);
            bundle.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;
            bundle.SetVertexBuffer(0, vertexBufferView);
            bundle.DrawInstanced(3, 1, 0, 0);
            bundle.Close();

            // Create synchronization objects.
            {
                fence = device.CreateFence(0, FenceFlags.None);
                fenceValue = 1;

                // Create an event handle to use for frame synchronization.
                fenceEvent = new AutoResetEvent(false);
            }
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
            commandList.Reset(commandAllocator, pipelineState);


            // Set necessary state.
            commandList.SetGraphicsRootSignature(rootSignature);
            commandList.SetViewport(viewport);
            commandList.SetScissorRectangles(scissorRect);

            // Indicate that the back buffer will be used as a render target.
            commandList.ResourceBarrierTransition(renderTargets[frameIndex], ResourceStates.Present, ResourceStates.RenderTarget);
            
            var rtvHandle = renderTargetViewHeap.CPUDescriptorHandleForHeapStart;
            rtvHandle += frameIndex * rtvDescriptorSize;
            commandList.SetRenderTargets(1, rtvHandle, false, null);

            // Record commands.
            commandList.ClearRenderTargetView(rtvHandle, new Color4(0, 0.2F, 0.4f, 1), 0, null);

            commandList.ExecuteBundle(bundle);
            
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

            int localFence = fenceValue;
            commandQueue.Signal(this.fence, localFence);
            fenceValue++;

            // Wait until the previous frame is finished.
            if (this.fence.CompletedValue < localFence)
            {
                this.fence.SetEventOnCompletion(localFence, fenceEvent.SafeWaitHandle.DangerousGetHandle());
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

        /// <summary>
        /// Dispose
        /// </summary>
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
            bundleAllocator.Dispose();
            commandQueue.Dispose();
            rootSignature.Dispose();
            renderTargetViewHeap.Dispose();
            pipelineState.Dispose();
            commandList.Dispose();
            bundle.Dispose();
            vertexBuffer.Dispose();
            fence.Dispose();
            swapChain.Dispose();
            device.Dispose();
        }


        struct Vertex
        {
            public Vector3 Position;
            public Vector4 Color;
        };

        private const int FrameCount = 2;

        private ViewportF viewport;
        private Rectangle scissorRect;
        // Pipeline objects.
        private SwapChain3 swapChain;
        private Device device;
        private readonly Resource[] renderTargets = new Resource[FrameCount];
        private CommandAllocator commandAllocator;
        private CommandAllocator bundleAllocator;
        private CommandQueue commandQueue;
        private RootSignature rootSignature;
        private DescriptorHeap renderTargetViewHeap;
        private PipelineState pipelineState;
        private GraphicsCommandList commandList;
        private GraphicsCommandList bundle;
        private int rtvDescriptorSize;

        // App resources.
        Resource vertexBuffer;
        VertexBufferView vertexBufferView;


        // Synchronization objects.
        private int frameIndex;
        private AutoResetEvent fenceEvent;

        private Fence fence;
        private int fenceValue;
        
    }
}
