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
using System.Collections.Generic;

using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Input;

namespace SkeletalAnimation
{
    // Use this namespace here in case we need to use Direct3D11 namespace as well, as this
    // namespace will override the Direct3D11.
    using SharpDX.Toolkit.Graphics;

    public class ModelAnimationController
    {
        private Model model;
        private ModelAnimation animation;
        private float currentTime;
        private int[] channelMap;

        public ModelAnimationController(Model model, ModelAnimation animation)
        {
            this.model = model;
            this.animation = animation;

            channelMap = new int[animation.Channels.Count];

            for (int i = 0; i < animation.Channels.Count; i++)
            {
                channelMap[i] = -1;
                foreach (var bone in model.Bones)
                {
                    if (bone.Name == animation.Channels[i].BoneName)
                    {
                        channelMap[i] = bone.Index;
                        break;
                    }
                }
            }
        }

        public void Update(GameTime gameTime)
        {
            if (MathUtil.IsZero(animation.Duration))
            {
                currentTime = 0.0f;
            }
            else
            {
                currentTime += (float)gameTime.ElapsedGameTime.TotalSeconds;
                currentTime %= animation.Duration;
            }

            for (int i = 0; i < animation.Channels.Count; i++)
            {
                var boneIndex = channelMap[i];
                if (boneIndex < 0)
                {
                    continue;
                }

                var bone = model.Bones[boneIndex];
                var keyFrames = animation.Channels[i].KeyFrames;

                int nextIndex = 0;
                int lastIndex = keyFrames.Count - 1;

                while (nextIndex < lastIndex && currentTime >= keyFrames[nextIndex].Time)
                {
                    nextIndex++;
                }

                var nextKeyFrame = keyFrames[nextIndex];

                if (nextIndex == 0)
                {
                    bone.Transform = (Matrix)nextKeyFrame.Value;
                }
                else
                {
                    var previousKeyFrame = keyFrames[nextIndex - 1];
                    var amount = (currentTime - previousKeyFrame.Time) / (nextKeyFrame.Time - previousKeyFrame.Time);

                    bone.Transform = (Matrix)CompositeTransform.Slerp(previousKeyFrame.Value, nextKeyFrame.Value, amount);
                }
            }
        }
    }

    /// <summary>
    /// Simple SpriteBatchAndFont application using SharpDX.Toolkit.
    /// The purpose of this application is to use SpriteBatch and SpriteFont.
    /// </summary>
    public class SkeletalAnimationGame : Game
    {
        private GraphicsDeviceManager graphicsDeviceManager;

        private Model model;
        private ModelAnimationController animationController;

        private BoundingSphere modelBounds;
        private Matrix world;
        private Matrix view;
        private Matrix projection;

        /// <summary>
        /// Initializes a new instance of the <see cref="SkeletalAnimationGame" /> class.
        /// </summary>
        public SkeletalAnimationGame()
        {
            // Creates a graphics manager. This is mandatory.
            graphicsDeviceManager = new GraphicsDeviceManager(this);
            graphicsDeviceManager.PreferredGraphicsProfile = new FeatureLevel[] { FeatureLevel.Level_9_1, };

            // Setup the relative directory to the executable directory
            // for loading contents with the ContentManager
            Content.RootDirectory = "Content";
        }

        protected override void LoadContent()
        {
            // Load the model with a SkinnedEffectInstaller
            var options = new ModelContentReaderOptions { EffectInstaller = new SkinnedEffectInstaller(GraphicsDevice) };

            model = Content.Load<Model>("Dude", options);
                
            // Enable default lighting for BasicEffect ans SkinnedEffect on model.
            BasicEffect.EnableDefaultLighting(model, true);

            model.ForEach(part =>
                {
                    var effect = part.Effect as SkinnedEffect;
                    if (effect != null)
                    {
                        effect.EnableDefaultLighting();
                    }
                });


            if (model.Animations.Count > 0)
            {
                animationController = new ModelAnimationController(model, model.Animations[0]);
            }

            base.LoadContent();
        }

        protected override void Initialize()
        {
            Window.Title = "Skeletal Animation Demo";
            base.Initialize();
        }

        protected override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Advance the animation
            if (animationController != null)
            {
                animationController.Update(gameTime);
            }

            // Calculate the bounds of this model
            modelBounds = model.CalculateBounds();

            // Calculates the world and the view based on the model size
            const float MaxModelSize = 10.0f;
            var scaling = MaxModelSize / modelBounds.Radius;
            view = Matrix.LookAtRH(new Vector3(0, 0, MaxModelSize * 2.5f), new Vector3(0, 0, 0), Vector3.UnitY);
            projection = Matrix.PerspectiveFovRH(0.9f, (float)GraphicsDevice.BackBuffer.Width / GraphicsDevice.BackBuffer.Height, 0.1f, MaxModelSize * 10.0f);
            world = Matrix.Translation(-modelBounds.Center.X, -modelBounds.Center.Y, -modelBounds.Center.Z) * Matrix.Scaling(scaling) * Matrix.RotationY((float)gameTime.TotalGameTime.TotalSeconds);
        }

        protected override void Draw(GameTime gameTime)
        {
            // Clears the screen with the Color.CornflowerBlue
            GraphicsDevice.Clear(Color.CornflowerBlue);            

            // Draw the model
            model.Draw(GraphicsDevice, world, view, projection);

            // Handle base.Draw
            base.Draw(gameTime);
        }
    }
}
