using System;
using System.Text;
using SharpDX;


namespace OcclusionQuery
{
    // Use these namespaces here to override SharpDX.Direct3D11
    using SharpDX.Toolkit;
    using SharpDX.Toolkit.Graphics;
    using SharpDX.Toolkit.Input;

    enum Scenario
    {
        StallPipeline,
        DoWork,
        SkipIfUnavailable
    }

    /// <summary>
    /// A sample on using OcclusionQuery 
    /// </summary>
    public class OcclusionQueryGame : Game
    {
        private GraphicsDeviceManager graphicsDeviceManager;
        private SpriteBatch spriteBatch;
        private SpriteFont arial16Font;

        private Matrix view;
        private Matrix projection;

        private BasicEffect basicEffect;
        private GeometricPrimitive sphere;

        private Texture2D pixelTexture;
        private const float textureWidth = 128;
        private const float textureHeight = 128;

        private MouseManager mouse;
        private MouseState mouseState;

        private OcclusionQuery occlusionQuery;

        private RenderTarget2D offscreenBuffer;

        private Scenario scenario = Scenario.StallPipeline;

        /// <summary>
        /// Initializes a new instance of the <see cref="OcclusionQueryGame" /> class.
        /// </summary>
        public OcclusionQueryGame()
        {
            // Creates a graphics manager. This is mandatory.
            graphicsDeviceManager = new GraphicsDeviceManager(this);

            // Setup the relative directory to the executable directory
            // for loading contents with the ContentManager
            Content.RootDirectory = "Content";

            // Initialize input mouse system
            mouse = new MouseManager(this);
        }

        protected override void Initialize()
        {
            // Modify the title of the window
            Window.Title = "OcclusionQueryGame";

            base.Initialize();
        }

        protected override void LoadContent()
        {
            // Instantiate a SpriteBatch
            spriteBatch = ToDisposeContent(new SpriteBatch(GraphicsDevice));

            // Loads a sprite font
            // The [Arial16.xml] file is defined with the build action [ToolkitFont] in the project
            arial16Font = Content.Load<SpriteFont>("Arial16");

            // Creates a basic effect
            basicEffect = ToDisposeContent(new BasicEffect(GraphicsDevice));
            basicEffect.DiffuseColor = Color.OrangeRed.ToVector4();
            basicEffect.PreferPerPixelLighting = true;
            basicEffect.EnableDefaultLighting();

            // Creates torus primitive
            sphere = ToDisposeContent(GeometricPrimitive.Sphere.New(GraphicsDevice, 1.75f));

            pixelTexture = Texture2D.New(GraphicsDevice, 1, 1, GraphicsDevice.BackBuffer.Format);
            pixelTexture.SetData<Color>(new Color[] { Color.Green });

            occlusionQuery = new OcclusionQuery(GraphicsDevice);

            offscreenBuffer = RenderTarget2D.New(GraphicsDevice, 128, 128, GraphicsDevice.BackBuffer.Format);

            base.LoadContent();
        }

        protected override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Calculates the world and the view based on the model size
            view = Matrix.LookAtRH(new Vector3(0.0f, 0.0f, 7.0f), new Vector3(0, 0.0f, 0), Vector3.UnitY);
            projection = Matrix.PerspectiveFovRH(0.9f, (float)GraphicsDevice.BackBuffer.Width / GraphicsDevice.BackBuffer.Height, 0.1f, 100.0f);

            // Update basic effect for rendering the Primitive
            basicEffect.View = view;
            basicEffect.Projection = projection;

            // Get the current state of the mouse
            mouseState = mouse.GetState();

            if (mouseState.LeftButton.Pressed)
            {
                scenario = scenario == Scenario.SkipIfUnavailable ? Scenario.StallPipeline : ++scenario;
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            // Clears the screen with the Color.CornflowerBlue
            GraphicsDevice.Clear(Color.CornflowerBlue);

            spriteBatch.Begin(0, null, null, GraphicsDevice.DepthStencilStates.Default);

            var textureBounds = new RectangleF(
                mouseState.X * GraphicsDevice.Viewport.Width - textureWidth / 2.0f,
                mouseState.Y * GraphicsDevice.Viewport.Height - textureHeight / 2.0f,
                textureWidth,
                textureHeight);

            spriteBatch.Draw(pixelTexture, textureBounds, Color.White);
            spriteBatch.End();

            occlusionQuery.Begin();
            sphere.Draw(basicEffect);
            occlusionQuery.End();

            var resultString = string.Empty;

            switch (scenario)
            {
                case Scenario.StallPipeline:
                    {
                        // Stall the pipeline by waiting indefinitely for the query to complete.
                        while (!occlusionQuery.IsComplete) ;
                    }
                    break;

                case Scenario.DoWork:
                    {
                        // Do some more GPU work prior attempting to get the result.
                        // NOTE: This rough, hard-coded method does not guarantee that the result will be available.
                        //       Waiting using the above scenario may be necesssary.
                        GraphicsDevice.SetRenderTargets(GraphicsDevice.DepthStencilBuffer, offscreenBuffer);

                        for (int i = 0; i < 1000; i++)
                        {
                            sphere.Draw(basicEffect);
                        }

                        GraphicsDevice.SetRenderTargets(GraphicsDevice.DepthStencilBuffer, GraphicsDevice.BackBuffer);
                    }
                    break;

                case Scenario.SkipIfUnavailable:
                    {
                        // Just don't do anything if the result is not crucial; no guarantees that the query will complete
                    }
                    break;
            };

            resultString = occlusionQuery.IsComplete ? string.Format("Number of sphere's visible pixels: {0}", occlusionQuery.PixelCount) : "Query did not complete; no result available";

            spriteBatch.Begin();
            spriteBatch.DrawString(arial16Font, "Scenario: " + scenario.ToString() + " (left mouse button to cycle through scenarios)\n" + resultString, new Vector2(16, 16), Color.White);
            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
