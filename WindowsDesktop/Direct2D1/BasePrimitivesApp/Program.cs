// Copyright (c) 2010-2013 SharpDX - Julien Vulliet
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
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using SharpDX.Windows;
using System.Windows.Forms;

using D2DFactory = SharpDX.Direct2D1.Factory;
using DWriteFactory = SharpDX.DirectWrite.Factory;

// Very quick overview to show how to draw shapes in Direct2D
namespace BaePrimitivesApp
{
    public class Program
    {        
        private static D2DFactory d2dFactory;
        private static DWriteFactory dwFactory;
        private static RenderForm mainForm;

        private static WindowRenderTarget renderTarget;

        //Various brushes for our example
        private static SolidColorBrush backgroundBrush;
        private static SolidColorBrush borderBrush;

        private static TextFormat textFormat;
        private static StrokeStyle simpleDashedStroke;
            

        [STAThread]
        static void Main(string[] args)
        {
            mainForm = new RenderForm("Advanced Text rendering demo");

            d2dFactory = new D2DFactory();
            dwFactory = new DWriteFactory(SharpDX.DirectWrite.FactoryType.Shared);

            //Simple text format to draw text
            textFormat = new TextFormat(dwFactory, "Arial", 16.0f);

            //Dashed Stroke
            simpleDashedStroke = new StrokeStyle(d2dFactory, new StrokeStyleProperties() { DashStyle = DashStyle.Dash });
            
            CreateResources();

            var bgcolor = new Color4(0.0f,0.0f,0.0f,1.0f);

            RenderLoop.Run(mainForm, () =>
            {
                renderTarget.BeginDraw();
                renderTarget.Clear(bgcolor);

                DrawBasicShapes();

                DrawCurves();

                DrawCompositeGeometry();

                try
                {
                    renderTarget.EndDraw();
                }
                catch
                {
                    CreateResources();
                }
            });

            d2dFactory.Dispose();
            dwFactory.Dispose();

            borderBrush.Dispose();
            backgroundBrush.Dispose();
            renderTarget.Dispose();
        }

        private static void CreateResources()
        {
            if (renderTarget != null) { renderTarget.Dispose(); }
            if (backgroundBrush != null) { backgroundBrush.Dispose(); }
            if (borderBrush != null) { borderBrush.Dispose(); }


            HwndRenderTargetProperties wtp = new HwndRenderTargetProperties();
            wtp.Hwnd = mainForm.Handle;
            wtp.PixelSize = new Size2(mainForm.ClientSize.Width, mainForm.ClientSize.Height);
            wtp.PresentOptions = PresentOptions.Immediately;
            renderTarget = new WindowRenderTarget(d2dFactory, new RenderTargetProperties(), wtp);

            backgroundBrush = new SolidColorBrush(renderTarget, new Color4(0.3f, 0.3f, 0.3f, 1.0f));
            borderBrush = new SolidColorBrush(renderTarget, new Color4(0.9f, 0.9f, 0.9f, 1.0f));

        }

        private static void DrawBasicShapes()
        {
            int x = 20;
            int y = 20;

            //Simple shapes rendering
            renderTarget.DrawText("Shapes", textFormat, new RectangleF(x, y, 100, 30), backgroundBrush);

            y += 40;

            var rectangle = new RectangleF(x, y, 100, 30);

            renderTarget.FillRectangle(rectangle, backgroundBrush);
            renderTarget.DrawRectangle(rectangle, borderBrush);

            y += 40;

            var roundRect = new RoundedRectangle()
            {
                Rect = new RectangleF(x, y, 100, 30),
                RadiusX = 3,
                RadiusY = 3
            };

            renderTarget.FillRoundedRectangle(roundRect, backgroundBrush);
            renderTarget.DrawRoundedRectangle(roundRect, borderBrush);

            y += 40;

            var ellipse = new Ellipse()
            {
                Point = new Vector2(x + 50, y + 10), //In case of ellipse we use center
                RadiusX = 30,
                RadiusY = 10
            };

            renderTarget.FillEllipse(ellipse, backgroundBrush);
            renderTarget.DrawEllipse(ellipse, borderBrush);

            y += 40;

            //Show a few more primitives with different strokes

            rectangle = new RectangleF(x, y, 100, 30);
            renderTarget.DrawRectangle(rectangle, borderBrush, 5.0f); //Large stroke style
            y += 40;

            rectangle = new RectangleF(x, y, 100, 30);
            renderTarget.DrawRectangle(rectangle, borderBrush, 2.0f, simpleDashedStroke);
            y += 40;
        }

        private static void DrawCurves()
        {

            //Now show a few path examples
            int y = 20;
            int x = 200;
            renderTarget.DrawText("Paths", textFormat, new RectangleF(x, y, 100, 30), backgroundBrush);

            //Simple line
            y += 40;
            renderTarget.DrawLine(new Vector2(x, y), new Vector2(x + 50, y + 15), borderBrush);

            y += 40;

            /*To construct bezier we need to build path geometry, here they built every frame, 
             * but you will likely want to cache geometry construction.
             * Geometry does not depend on rendertarget but factory, so it can be reused */

            var quadBezier = new PathGeometry(d2dFactory);

            /*Geometry sink allows us to add curves to our path, please note that we can add as many as we want */
            var geometrySink = quadBezier.Open();

            //Start a curve
            geometrySink.BeginFigure(new Vector2(x, y), FigureBegin.Hollow);
            geometrySink.AddQuadraticBezier(new QuadraticBezierSegment()
            {
                Point1 = new Vector2(x + 20, y + 15), //Control point
                Point2 = new Vector2(x + 60, y + 2) //End point
            });

            geometrySink.EndFigure(FigureEnd.Open);

            geometrySink.Close(); //Finish curve

            renderTarget.DrawGeometry(quadBezier, borderBrush);
            quadBezier.Dispose();


            //Just build a closed quad bezier now
            var quadBezierCLosed = new PathGeometry(d2dFactory);

            /*Geometry sink allows us to add curves to our path, please note that we can add as many as we want */
            var closedGeometrySink = quadBezierCLosed.Open();

            //Start a curve
            closedGeometrySink.BeginFigure(new Vector2(x, y), FigureBegin.Hollow);
            closedGeometrySink.AddQuadraticBezier(new QuadraticBezierSegment()
            {
                Point1 = new Vector2(x + 20, y + 15), //Control point
                Point2 = new Vector2(x + 60, y + 2) //End point
            });

            closedGeometrySink.EndFigure(FigureEnd.Closed); //Here we tell d2d to finish back path to original point

            closedGeometrySink.Close(); //Finish curve

            renderTarget.DrawGeometry(quadBezierCLosed, borderBrush);

            quadBezierCLosed.Dispose();

            y += 80;

            //Now we'll draw a more complex path
            var complexPath = new PathGeometry(d2dFactory);

            var complexSink = complexPath.Open();

            complexSink.BeginFigure(new Vector2(x,y), FigureBegin.Hollow);

            complexSink.AddLine(new Vector2(x + 20, y + 50));
            complexSink.AddLine(new Vector2(x + 10, y + 5));
            complexSink.AddBezier(new BezierSegment()
                {
                    Point1 = new Vector2(x + 50, y + 30),
                    Point2 = new Vector2(x + 20, y + 75),
                    Point3 = new Vector2(x + 30, y + 100)
                });

            complexSink.EndFigure(FigureEnd.Open);
            complexSink.Close();

            renderTarget.DrawGeometry(complexPath, borderBrush);

            complexPath.Dispose();
        }


        private static void DrawCompositeGeometry()
        {
            int x = 400;
            int y = 20;

            //Composite shapes rendering
            renderTarget.DrawText("Composite", textFormat, new RectangleF(x, y, 100, 30), backgroundBrush);

            y += 100;

            var e1 = new Ellipse()
            {
                Point = new Vector2(x+50,y),
                RadiusX = 100,
                RadiusY = 30
            };

            var e2 = new Ellipse()
            {
                Point = new Vector2(x+20,y),
                RadiusX = 50,
                RadiusY = 60
            };


            //Same as per curves, you'll likely want to cache geometry construction, composition can be expensive
            EllipseGeometry ellipse1 = new EllipseGeometry(d2dFactory, e1);
            EllipseGeometry ellipse2 = new EllipseGeometry(d2dFactory, e2);

            PathGeometry combinedGeometry = new PathGeometry(d2dFactory);
            GeometrySink sink = combinedGeometry.Open();

            ellipse1.Combine(ellipse2, CombineMode.Intersect, sink); //You can easily try other combine modes here

            sink.Close();

            renderTarget.FillGeometry(combinedGeometry, backgroundBrush);
            renderTarget.DrawGeometry(combinedGeometry, borderBrush);

            combinedGeometry.Dispose();
            ellipse1.Dispose();
            ellipse2.Dispose();
        }

    }
}
