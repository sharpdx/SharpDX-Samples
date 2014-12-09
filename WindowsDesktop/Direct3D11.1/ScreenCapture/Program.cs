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
using System.Drawing.Imaging;
using System.IO;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace ScreenCapture
{
    /// <summary>
    ///   Screen capture of the desktop using DXGI OutputDuplication.
    /// </summary>
    internal static class Program
    {

        [STAThread]
        private static void Main()
        {
            // # of graphics card adapter
            const int numAdapter = 0;

            // # of output device (i.e. monitor)
            const int numOutput = 0;

            const string outputFileName = "ScreenCapture.png";

            // Create DXGI Factory1
            using (var factory = new Factory1())
            // Get adapt from factory
            using (var adapter = factory.GetAdapter1(numAdapter))
            // Create device from Adapter
            using (var device = new Device(adapter))
            // Get DXGI.Output
            using (var output = adapter.GetOutput(numOutput))
            // "cast" to DXGI.Output1 by using QueryInterface
            using (var output1 = output.QueryInterface<Output1>())
            {

                // Width/Height of desktop to capture
                int width = output.Description.DesktopBounds.Width;
                int height = output.Description.DesktopBounds.Height;

                // Create Staging texture CPU-accessible
                var texture2DDescription = new Texture2DDescription
                {
                    CpuAccessFlags = CpuAccessFlags.Read,
                    BindFlags = BindFlags.None,
                    Format = Format.B8G8R8A8_UNorm,
                    Width = width,
                    Height = height,
                    OptionFlags = ResourceOptionFlags.None,
                    MipLevels = 1,
                    ArraySize = 1,
                    SampleDescription = { Count = 1, Quality = 0 },
                    Usage = ResourceUsage.Staging
                };

                using (var screenTexture = new Texture2D(device, texture2DDescription))
                // Duplicate the output
                using (var duplicatedOutput = output1.DuplicateOutput(device))
                {
                    bool captureDone = false;
                    SharpDX.DXGI.Resource screenResource = null;
                    for (int i = 0; !captureDone; i++)
                    {
                        try
                        {
                            OutputDuplicateFrameInformation duplicateFrameInformation;
                            // Try to get duplicated frame within given time
                            duplicatedOutput.AcquireNextFrame(10000, out duplicateFrameInformation, out screenResource);

                            // Ignore first call, this always seems to return a black frame
                            if (i == 0)
                            {
                                continue;
                            }

                            // copy resource into memory that can be accessed by the CPU
                            using (var screenTexture2D = screenResource.QueryInterface<Texture2D>())
                            {
                                device.ImmediateContext.CopyResource(screenTexture2D, screenTexture);
                            }

                            // Get the desktop capture texture
                            var mapSource = device.ImmediateContext.MapSubresource(screenTexture, 0, MapMode.Read, MapFlags.None);
                            var boundsRect = new System.Drawing.Rectangle(0, 0, width, height);
                            // Create Drawing.Bitmap
                            using (var bitmap = new System.Drawing.Bitmap(width, height, PixelFormat.Format32bppArgb))
                            {
                                // Copy pixels from screen capture Texture to GDI bitmap
                                var bitmapData = bitmap.LockBits(boundsRect, ImageLockMode.WriteOnly, bitmap.PixelFormat);
                                var sourcePtr = mapSource.DataPointer;
                                var destinationPtr = bitmapData.Scan0;
                                for (int y = 0; y < height; y++)
                                {
                                    // Copy a single line 
                                    Utilities.CopyMemory(destinationPtr, sourcePtr, width * 4);

                                    // Advance pointers
                                    sourcePtr = IntPtr.Add(sourcePtr, mapSource.RowPitch);
                                    destinationPtr = IntPtr.Add(destinationPtr, bitmapData.Stride);
                                }

                                // Release source and dest locks
                                bitmap.UnlockBits(bitmapData);

                                device.ImmediateContext.UnmapSubresource(screenTexture, 0);

                                // Save the output
                                bitmap.Save(outputFileName);
                            }

                            // Capture done
                            captureDone = true;

                        }
                        catch (SharpDXException e)
                        {
                            if (e.ResultCode.Code != SharpDX.DXGI.ResultCode.WaitTimeout.Result.Code)
                            {
                                throw;
                            }
                        }
                        finally
                        {
                            // Dispose manually
                            if (screenResource != null)
                            {
                                screenResource.Dispose();
                            }
                            duplicatedOutput.ReleaseFrame();
                        }
                    }
                }
            }

            // Display the texture using system associated viewer
            System.Diagnostics.Process.Start(Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, outputFileName)));
        }
    }
}