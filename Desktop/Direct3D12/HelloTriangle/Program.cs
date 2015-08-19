using SharpDX.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HelloTriangle
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            var form = new RenderForm("Hello Triangle");
            form.Width = 1280;
            form.Height = 800;
            form.Show();

            using (HelloTriangle app = new HelloTriangle())
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
