﻿using SharpDX.DXGI;
using System.Threading;
using System;

namespace HelloWPF
{
    using Microsoft.Wpf.Interop.DirectX;
    using SharpDX;
    using SharpDX.Direct3D12;
    using System.Runtime.InteropServices;
    using System.Windows;

    public class AppWPF : IDisposable
    {
        /// <summary>
        /// Initialise pipeline and assets
        /// </summary>
        /// <param name="form">The form</param>
        public void Initialize(int width, int height, IntPtr handle)
        {

            LoadPipeline(width, height, handle);
            LoadAssets();
        }

        private void LoadPipeline(int width, int height, IntPtr handle)
        {
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

                var flags = SharpDX.Direct3D11.DeviceCreationFlags.BgraSupport;
#if DEBUG
                flags |=  SharpDX.Direct3D11.DeviceCreationFlags.Debug;
#endif

                d3d11Device = SharpDX.Direct3D11.Device.CreateFromDirect3D12(device, flags, new[] { SharpDX.Direct3D.FeatureLevel.Level_11_0 }, null, commandQueue);
                d3d11On12Device = d3d11Device.QueryInterface<SharpDX.Direct3D11.Device11On12>();

                // Describe and create the swap chain.
                var swapChainDesc = new SwapChainDescription()
                {
                    BufferCount = FrameCount,
                    ModeDescription = new ModeDescription(width, height, new Rational(60, 1), Format.R8G8B8A8_UNorm),
                    Usage = Usage.RenderTargetOutput,
                    SwapEffect = SwapEffect.FlipDiscard,
                    OutputHandle = handle,
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

            var srvHeapDesc = new DescriptorHeapDescription()
            {
                DescriptorCount = 1,
                Flags = DescriptorHeapFlags.ShaderVisible,
                Type = DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView
            };

            shaderRenderViewHeap = device.CreateDescriptorHeap(srvHeapDesc);

            rtvDescriptorSize = device.GetDescriptorHandleIncrementSize(DescriptorHeapType.RenderTargetView);

            // Create frame resources.
            var rtvHandle = renderTargetViewHeap.CPUDescriptorHandleForHeapStart;
            for (int n = 0; n < FrameCount; n++)
            {
                renderTargets[n] = swapChain.GetBackBuffer<Resource>(n);
                device.CreateRenderTargetView(renderTargets[n], null, rtvHandle);

                d3d11On12Device.CreateWrappedResource(renderTargets[n], new SharpDX.Direct3D11.D3D11ResourceFlags { BindFlags = (int)SharpDX.Direct3D11.BindFlags.RenderTarget, CPUAccessFlags = 0, MiscFlags = 0, StructureByteStride = 0 }, (int)ResourceStates.RenderTarget, (int)ResourceStates.Present, typeof(SharpDX.Direct3D11.Texture2D).GUID, out d3d11BackBuffers[n]);

                rtvHandle += rtvDescriptorSize;
            }

            commandAllocator = device.CreateCommandAllocator(CommandListType.Direct);
        }

        internal void UpdateD3D11Image(IntPtr surface) {
            var unk = new ComObject(surface);
            var dxgiRes = unk.QueryInterface<SharpDX.DXGI.Resource>();

            var tempRes = d3d11Device.OpenSharedResource<SharpDX.Direct3D11.Resource>(dxgiRes.SharedHandle);
            var backBuffer = tempRes.QueryInterface<SharpDX.Direct3D11.Texture2D>();
            var d3d11BackBuffer = d3d11BackBuffers[frameIndex];

            d3d11On12Device.AcquireWrappedResources(new[] { d3d11BackBuffer }, 1);
            d3d11Device.ImmediateContext.CopyResource(d3d11BackBuffer, backBuffer);
            d3d11Device.ImmediateContext.Flush();
            d3d11On12Device.ReleaseWrappedResources(new[] { d3d11BackBuffer }, 1);
        }

        private void LoadAssets()
        {
            // Create the root signature description.
            var rootSignatureDesc = new RootSignatureDescription(RootSignatureFlags.AllowInputAssemblerInputLayout,
                // Root Parameters
                new[]
                {
                    new RootParameter(ShaderVisibility.Pixel,
                        new DescriptorRange()
                        {
                            RangeType = DescriptorRangeType.ShaderResourceView,
                            DescriptorCount = 1,
                            OffsetInDescriptorsFromTableStart = int.MinValue,
                            BaseShaderRegister = 0
                        })
                },
                // Samplers
                new[]
                {
                    new StaticSamplerDescription(ShaderVisibility.Pixel, 0, 0)
                    {
                        Filter = Filter.MinimumMinMagMipPoint,
                        AddressUVW = TextureAddressMode.Border,
                    }
                });

            rootSignature = device.CreateRootSignature(0, rootSignatureDesc.Serialize());

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
                    new InputElement("TEXCOORD",0,Format.R32G32_Float,12,0)
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

            // Create the vertex buffer.
            float aspectRatio = viewport.Width / viewport.Height;

            // Define the geometry for a triangle.
            var triangleVertices = new []
            {
                new Vertex() { Position = new Vector3(0.0f, 0.25f * aspectRatio, 0.0f ),  TexCoord = new Vector2(0.5f, 0.0f) },
                new Vertex() { Position = new Vector3(0.25f, -0.25f * aspectRatio, 0.0f), TexCoord = new Vector2(1.0f, 1.0f) },
                new Vertex() { Position = new Vector3(-0.25f, -0.25f * aspectRatio, 0.0f),TexCoord = new Vector2(0.0f, 1.0f) },
            };

            int vertexBufferSize = Utilities.SizeOf(triangleVertices);

            // Note: using upload heaps to transfer static data like vert buffers is not 
            // recommended. Every time the GPU needs it, the upload heap will be marshalled 
            // over. Please read up on Default Heap usage. An upload heap is used here for 
            // code simplicity and because there are very few verts to actually transfer.
            vertexBuffer = device.CreateCommittedResource(new HeapProperties(HeapType.Upload), HeapFlags.None, ResourceDescription.Buffer(vertexBufferSize), ResourceStates.GenericRead);

            // Copy the triangle data to the vertex buffer.
            var pVertexDataBegin = vertexBuffer.Map(0);
            Utilities.Write(pVertexDataBegin, triangleVertices, 0, triangleVertices.Length);
            vertexBuffer.Unmap(0);

            // Initialize the vertex buffer view.
            vertexBufferView = new VertexBufferView();
            vertexBufferView.BufferLocation = vertexBuffer.GPUVirtualAddress;
            vertexBufferView.StrideInBytes = Utilities.SizeOf<Vertex>();
            vertexBufferView.SizeInBytes = vertexBufferSize;

            // Create the texture.
            // Describe and create a Texture2D.
            var textureDesc = ResourceDescription.Texture2D(Format.R8G8B8A8_UNorm, TextureWidth, TextureHeight);
            texture = device.CreateCommittedResource(new HeapProperties(HeapType.Default), HeapFlags.None, textureDesc, ResourceStates.CopyDestination);

            long uploadBufferSize = GetRequiredIntermediateSize(this.texture, 0, 1);

            // Create the GPU upload buffer.
            var textureUploadHeap = device.CreateCommittedResource(new HeapProperties(CpuPageProperty.WriteBack, MemoryPool.L0), HeapFlags.None, ResourceDescription.Texture2D(Format.R8G8B8A8_UNorm, TextureWidth, TextureHeight), ResourceStates.GenericRead);

            // Copy data to the intermediate upload heap and then schedule a copy 
            // from the upload heap to the Texture2D.
            byte[] textureData = GenerateTextureData();

            var handle = GCHandle.Alloc(textureData, GCHandleType.Pinned);
            var ptr = Marshal.UnsafeAddrOfPinnedArrayElement(textureData, 0);
            textureUploadHeap.WriteToSubresource(0, null, ptr, TexturePixelSize * TextureWidth, textureData.Length);
            handle.Free();

            commandList.CopyTextureRegion(new TextureCopyLocation(texture, 0), 0, 0, 0, new TextureCopyLocation(textureUploadHeap, 0), null);

            commandList.ResourceBarrierTransition(this.texture, ResourceStates.CopyDestination, ResourceStates.PixelShaderResource);

            // Describe and create a SRV for the texture.
            var srvDesc = new ShaderResourceViewDescription
            {
                Shader4ComponentMapping = D3DXUtilities.DefaultComponentMapping(),
                Format = textureDesc.Format,
                Dimension = ShaderResourceViewDimension.Texture2D,
                Texture2D = {MipLevels = 1},
            };

            device.CreateShaderResourceView(this.texture, srvDesc, shaderRenderViewHeap.CPUDescriptorHandleForHeapStart);

            // Command lists are created in the recording state, but there is nothing
            // to record yet. The main loop expects it to be closed, so close it now.
            commandList.Close();

            commandQueue.ExecuteCommandList(commandList);

            // Create synchronization objects.
            fence = device.CreateFence(0, FenceFlags.None);
            fenceValue = 1;

            // Create an event handle to use for frame synchronization.
            fenceEvent = new AutoResetEvent(false);

            WaitForPreviousFrame();

            //release temp texture
            textureUploadHeap.Dispose();
        }

        byte[] GenerateTextureData()
        {
            int rowPitch = TextureWidth * TexturePixelSize;
            int cellPitch = rowPitch >> 3;       // The width of a cell in the checkboard texture.
            int cellHeight = TextureWidth >> 3;  // The height of a cell in the checkerboard texture.
            int textureSize = rowPitch * TextureHeight;
            byte[] data = new byte[textureSize];

            for (int n = 0; n < textureSize; n += TexturePixelSize)
            {
                int x = n % rowPitch;
                int y = n / rowPitch;
                int i = x / cellPitch;
                int j = y / cellHeight;

                if (i % 2 == j % 2)
                {
                    data[n] = 0x00;     // R
                    data[n + 1] = 0x00; // G
                    data[n + 2] = 0x00; // B
                    data[n + 3] = 0xff; // A
                }
                else
                {
                    data[n] = 0xff;     // R
                    data[n + 1] = 0xff; // G
                    data[n + 2] = 0xff; // B
                    data[n + 3] = 0xff; // A
                }
            }

            return data;
        }

        private long GetRequiredIntermediateSize(Resource destinationResource, int firstSubresource, int NumSubresources)
        {
            var desc = destinationResource.Description;
            long requiredSize;
            device.GetCopyableFootprints(ref desc, firstSubresource, NumSubresources, 0, null, null, null, out requiredSize);
            return requiredSize;
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

            commandList.SetDescriptorHeaps(1, new DescriptorHeap[] { shaderRenderViewHeap });

            commandList.SetGraphicsRootDescriptorTable(0, shaderRenderViewHeap.GPUDescriptorHandleForHeapStart);

            commandList.SetViewport(viewport);
            commandList.SetScissorRectangles(scissorRect);

            // Indicate that the back buffer will be used as a render target.
            commandList.ResourceBarrierTransition(renderTargets[frameIndex], ResourceStates.Present, ResourceStates.RenderTarget);
            
            var rtvHandle = renderTargetViewHeap.CPUDescriptorHandleForHeapStart;
            rtvHandle += frameIndex * rtvDescriptorSize;
            commandList.SetRenderTargets(rtvHandle, null);

            // Record commands.
            commandList.ClearRenderTargetView(rtvHandle, new Color4(0, 0.2F, 0.4f, 1), 0, null);

            commandList.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;
            commandList.SetVertexBuffer(0, vertexBufferView);
            commandList.DrawInstanced(3, 1, 0, 0);

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
            rootSignature.Dispose();
            renderTargetViewHeap.Dispose();
            shaderRenderViewHeap.Dispose();
            pipelineState.Dispose();
            commandList.Dispose();
            vertexBuffer.Dispose();
            texture.Dispose();
            fence.Dispose();
            swapChain.Dispose();
            device.Dispose();
        }


        struct Vertex
        {
            public Vector3 Position;

            public Vector2 TexCoord;
        };

        const int TextureWidth = 256;
        const int TextureHeight = 256;
        const int TexturePixelSize = 4;	// The number of bytes used to represent a pixel in the texture.


        const int FrameCount = 2;

        private ViewportF viewport;
        private Rectangle scissorRect;
        // Pipeline objects.
        private SwapChain3 swapChain;
        private Device device;
        private SharpDX.Direct3D11.Device d3d11Device;
        private SharpDX.Direct3D11.Device11On12 d3d11On12Device;
        private SharpDX.Direct3D11.Resource[] d3d11BackBuffers = new SharpDX.Direct3D11.Resource[FrameCount];
        private Resource[] renderTargets = new Resource[FrameCount];
        private CommandAllocator commandAllocator;
        private CommandQueue commandQueue;
        private RootSignature rootSignature;
        private DescriptorHeap renderTargetViewHeap;
        private DescriptorHeap shaderRenderViewHeap;
        private PipelineState pipelineState;
        private GraphicsCommandList commandList;
        private int rtvDescriptorSize;

        // App resources.
        Resource vertexBuffer;
        VertexBufferView vertexBufferView;
        Resource texture;

        // Synchronization objects.
        private int frameIndex;
        private AutoResetEvent fenceEvent;

        private Fence fence;
        private int fenceValue;


    }
}
