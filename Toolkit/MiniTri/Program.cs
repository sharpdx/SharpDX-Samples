// Copyright (c) 2010-2012 SharpDX - Alexandre Mutel
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
using System.IO;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Content;

namespace MiniTri
{
    // Use Toolkit namespace inside your namepsace in order to make a priority over Direct3D11/DXGI namespaces.
    using SharpDX.Toolkit.Graphics;

    /// <summary>
    /// Simple HelloWorld application using SharpDX.Toolkit.
    /// </summary>
    class Program : Game
    {
        private GraphicsDeviceManager graphicsDeviceManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="Program" /> class.
        /// </summary>
        public Program()
        {
            // Creates a graphics manager
            graphicsDeviceManager = new GraphicsDeviceManager(this);
        }

        protected override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            // Clears the screen
            GraphicsDevice.Clear(GraphicsDevice.BackBuffer, Color.CornflowerBlue);
        }

        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            var program = new Program();
            program.Run();
        }
    }
}
