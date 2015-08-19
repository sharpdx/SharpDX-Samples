using System;
using SharpDX.Windows;

namespace HelloTexture
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            var form = new RenderForm("Hello Texture")
            {
                Width = 1280,
                Height = 800
            };
            form.Show();
            
            using (var app = new HelloTexture())
            {
                app.Initialize(form);

                using (var loop = new RenderLoop(form))
                {
                    while (loop.NextFrame())
                    {
                        app.Update();
                        app.Render();
                    }
                }
            }
        }
    }
}
