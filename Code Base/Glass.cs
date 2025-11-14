using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Pixel_Simulations;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Pixel_Simulations
{

    public class FlaskDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }
    }
    public class Flask
    {
        // Textures
        public Texture2D ColorTexture { get; private set; }
        public Texture2D CollisionTexture { get; private set; }
        private PixelTexture PT;
        private Texture2D pixel;
        // Transform properties
        public Vector2 Position { get; private set; }
        public float Rotation { get; private set; }
        public float Scale { get; private set; }

        // State
        public bool IsHeld { get; private set; }

        // Internal helpers
        private readonly Vector2 _drawOrigin;
        public Vector2 _collisionOrigin { get; }
        private readonly Color[] _collisionData;
        private Matrix _inverseTransform;
        private readonly float _collisionToDrawRatio;
        private Rectangle _axisAlignedBoundingBox;

        public Flask(Texture2D colorTexture, Texture2D collisionTexture, Vector2 initialPosition, GraphicsDevice _gd)
        {
            ColorTexture = colorTexture;
            CollisionTexture = collisionTexture;
            Position = initialPosition;
            PT = new PixelTexture(_gd, 1);
            pixel = PT.GetPixelTexture(false);
            Rotation = 0f;
            Scale = 1.0f;
            IsHeld = false;
            _collisionToDrawRatio = CollisionTexture.Width / (float)ColorTexture.Width;
            _drawOrigin = new Vector2(ColorTexture.Width / 2f, ColorTexture.Height / 2f);
            _collisionOrigin = new Vector2(CollisionTexture.Width / 2f, CollisionTexture.Height / 2f);
            _collisionData = new Color[CollisionTexture.Width * CollisionTexture.Height];
            CollisionTexture.GetData(_collisionData);
            UpdateTransformsAndBounds(); // Renamed UpdateMatrix for clarity
        }

        private void UpdateTransformsAndBounds()
        {
            // Update the inverse matrix for pixel-perfect collision
            _inverseTransform = Matrix.Invert(
                Matrix.CreateTranslation(new Vector3(-_drawOrigin, 0)) *
                Matrix.CreateScale(Scale) *
                Matrix.CreateRotationZ(Rotation) *
                Matrix.CreateTranslation(new Vector3(Position, 0))
            );

            // Update the Axis-Aligned Bounding Box for broad-phase collision
            var corners = GetCollisionBoundsCorners();
            int minX = (int)Math.Min(Math.Min(corners[0].X, corners[1].X), Math.Min(corners[2].X, corners[3].X));
            int minY = (int)Math.Min(Math.Min(corners[0].Y, corners[1].Y), Math.Min(corners[2].Y, corners[3].Y));
            int maxX = (int)Math.Max(Math.Max(corners[0].X, corners[1].X), Math.Max(corners[2].X, corners[3].X));
            int maxY = (int)Math.Max(Math.Max(corners[0].Y, corners[1].Y), Math.Max(corners[2].Y, corners[3].Y));
            _axisAlignedBoundingBox = new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }
        public Vector2[] GetCollisionBoundsCorners()
        {
            Vector2[] corners = new Vector2[4];
            float w = ColorTexture.Width;
            float h = ColorTexture.Height;

            // Define corners relative to the top-left (0,0) of the texture.
            Vector2 tl = new Vector2(0, 0);       // Top-Left
            Vector2 tr = new Vector2(w, 0);       // Top-Right
            Vector2 bl = new Vector2(0, h);       // Bottom-Left
            Vector2 br = new Vector2(w, h);       // Bottom-Right

            // Build the transformation matrix that correctly handles the origin.
            Matrix transform =
                Matrix.CreateTranslation(new Vector3(-_drawOrigin, 0)) * // 1. Move pivot to world origin (0,0)
                Matrix.CreateScale(Scale) *                              // 2. Scale around the origin
                Matrix.CreateRotationZ(Rotation) *                       // 3. Rotate around the origin
                Matrix.CreateTranslation(new Vector3(Position, 0));      // 4. Move to final world position

            // Transform each corner from local space to world space.
            corners[0] = Vector2.Transform(tl, transform);
            corners[1] = Vector2.Transform(tr, transform);
            corners[2] = Vector2.Transform(br, transform);
            corners[3] = Vector2.Transform(bl, transform);

            return corners;
        }

        public void Update(KeyboardState currentKeyboard, Vector2 mouseWorldPos, Rectangle mouseScreenRect, bool isLeftMouseDown, float upscaleFactor)
        {
            if (isLeftMouseDown)
            {
                // We only set IsHeld if it's not already held AND a collision is detected.
                if (IsPointColliding(mouseScreenRect, upscaleFactor))
                {
                    IsHeld = true;
                }
            }
            else
            {
                IsHeld = false;
            }

            if (IsHeld)
            {
                Position = mouseWorldPos;
                if (currentKeyboard.IsKeyDown(Keys.Q)) Rotation -= 0.05f;
                if (currentKeyboard.IsKeyDown(Keys.E)) Rotation += 0.05f;
            }
        }

        public bool IsPointColliding(Rectangle mouseScreenRect, float upscaleFactor)
        {
            // --- 1. BROAD-PHASE CHECK ---
            // Get the flask's bounding box in screen space and check for a simple overlap.
            Rectangle flaskScreenAABB = GetScreenSpaceAABB(upscaleFactor);
            if (!mouseScreenRect.Intersects(flaskScreenAABB))
            {
                return false; // No overlap at all, so we can stop here.
            }

            // --- 2. NARROW-PHASE CHECK ---
            // If the boxes overlap, we check the pixels under the mouse rectangle.

            // Build a matrix that converts a screen coordinate to a local texture coordinate.
            Matrix inverseScreenTransform = GetInverseScreenTransform(upscaleFactor);

            // Find the area of the mouse rectangle in the texture's local space.
            Vector2 mouseTopLeftLocal = Vector2.Transform(new Vector2(mouseScreenRect.Left, mouseScreenRect.Top), inverseScreenTransform);
            Vector2 mouseTopRightLocal = Vector2.Transform(new Vector2(mouseScreenRect.Right, mouseScreenRect.Top), inverseScreenTransform);
            Vector2 mouseBottomLeftLocal = Vector2.Transform(new Vector2(mouseScreenRect.Left, mouseScreenRect.Bottom), inverseScreenTransform);
            Vector2 mouseBottomRightLocal = Vector2.Transform(new Vector2(mouseScreenRect.Right, mouseScreenRect.Bottom), inverseScreenTransform);

            // Find the bounding box of that transformed area on the texture.
            int minX = (int)Math.Min(Math.Min(mouseTopLeftLocal.X, mouseTopRightLocal.X), Math.Min(mouseBottomLeftLocal.X, mouseBottomRightLocal.X));
            int minY = (int)Math.Min(Math.Min(mouseTopLeftLocal.Y, mouseTopRightLocal.Y), Math.Min(mouseBottomLeftLocal.Y, mouseBottomRightLocal.Y));
            int maxX = (int)Math.Ceiling(Math.Max(Math.Max(mouseTopLeftLocal.X, mouseTopRightLocal.X), Math.Max(mouseBottomLeftLocal.X, mouseBottomRightLocal.X)));
            int maxY = (int)Math.Ceiling(Math.Max(Math.Max(mouseTopLeftLocal.Y, mouseTopRightLocal.Y), Math.Max(mouseBottomLeftLocal.Y, mouseBottomRightLocal.Y)));

            // Iterate through only the pixels of the collision texture that are potentially under the mouse.
            for (int y = Math.Max(0, minY); y < Math.Min(CollisionTexture.Height, maxY); y++)
            {
                for (int x = Math.Max(0, minX); x < Math.Min(CollisionTexture.Width, maxX); x++)
                {
                    // Check if the pixel at this coordinate is opaque (alpha > 0).
                    if (_collisionData[x + y * CollisionTexture.Width].A > 0)
                    {
                        return true; // Found an intersecting pixel! It's a collision.
                    }
                }
            }

            return false; // No opaque pixels were found under the mouse rectangle.
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            spriteBatch.Draw(texture: ColorTexture, position: Position, sourceRectangle: null, color: Color.White, rotation: Rotation, origin: _drawOrigin, scale: Scale, effects: SpriteEffects.None, layerDepth: 0f);
        }

        // --- THE CORRECTED METHOD ---
        // Note: It now requires a 'pixelTexture' for drawing lines/rects.
        public void DrawDebug(SpriteBatch spriteBatch, float upscaleFactor)
        {
            spriteBatch.Draw(texture: CollisionTexture, position: Position * upscaleFactor, sourceRectangle: null, color: Color.White * 0.8f, rotation: Rotation, origin: _collisionOrigin, scale: Scale, effects: SpriteEffects.None, layerDepth: 0f);
            Rectangle aabb = GetAxisAlignedBoundingBox();
            Rectangle screenAabb = GetScreenSpaceAABB(upscaleFactor);
            DrawRectangle(spriteBatch, screenAabb, Color.Cyan, 1, pixel);
            var corners = GetCollisionBoundsCorners();
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 start = corners[i] * upscaleFactor;
                Vector2 end = corners[(i + 1) % 4] * upscaleFactor;
                DrawLine(spriteBatch, start, end, Color.Red, 2, pixel);
            }
        }

        // Helper methods now require the pixel texture to be passed in.
        private void DrawRectangle(SpriteBatch spriteBatch, Rectangle rect, Color color, int lineWidth, Texture2D pixel)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, lineWidth), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y + rect.Height - lineWidth, rect.Width, lineWidth), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, lineWidth, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X + rect.Width - lineWidth, rect.Y, lineWidth, rect.Height), color);
        }

        private void DrawLine(SpriteBatch spriteBatch, Vector2 point1, Vector2 point2, Color color, float thickness, Texture2D pixel)
        {
            float distance = Vector2.Distance(point1, point2);
            float angle = (float)Math.Atan2(point2.Y - point1.Y, point2.X - point1.X);
            spriteBatch.Draw(pixel, point1, null, color, angle, Vector2.Zero, new Vector2(distance, thickness), SpriteEffects.None, 0);
        }
        private Matrix GetInverseScreenTransform(float upscaleFactor)
        {
            Vector2 flaskScreenPos = this.Position * upscaleFactor;
            return Matrix.Invert(
                Matrix.CreateTranslation(new Vector3(-_collisionOrigin, 0)) *
                Matrix.CreateScale(this.Scale) *
                Matrix.CreateRotationZ(this.Rotation) *
                Matrix.CreateTranslation(new Vector3(flaskScreenPos, 0))
            );
        }

        // Helper to get the simple AABB of the flask on the screen.
        public Rectangle GetScreenSpaceAABB(float upscaleFactor)
        {
            // This is a simplified version that is good enough for a broad-phase check.
            float worldWidth = ColorTexture.Width * upscaleFactor;
            float worldHeight = ColorTexture.Height * upscaleFactor;

            // Find the maximum possible extent due to rotation (the diagonal).
            float maxExtent = (float)Math.Sqrt(worldWidth * worldWidth + worldHeight * worldHeight);

            Vector2 screenCenter = Position * upscaleFactor;
            int screenRadius = (int)(maxExtent * 0.5f);

            return new Rectangle(
                (int)screenCenter.X - screenRadius,
                (int)screenCenter.Y - screenRadius,
                screenRadius * 2,
                screenRadius * 2
            );
        }
        public Rectangle GetAxisAlignedBoundingBox() => _axisAlignedBoundingBox;
    }

    public class Glassware
    {
        // Atlas Textures
        private readonly Texture2D _colorAtlas;
        private readonly Texture2D _collisionAtlas;
        private readonly Color[] _fullCollisionData;
        private PixelTexture PT;
        private Texture2D pixel;
        // Flask Definitions
        private readonly List<FlaskDefinition> _definitions;
        private int _currentFlaskIndex = 0;

        // Current Flask Properties
        private Rectangle _sourceRectColor;
        private Rectangle _sourceRectCollision;
        private Vector2 _drawOrigin;

        // Transform and State
        public Vector2 Position { get; private set; }
        public float Rotation { get; private set; }
        public float Scale { get; private set; } = 1.0f;
        public bool IsHeld { get; private set; }
        public string CurrentFlaskName => _definitions[_currentFlaskIndex].Name;

        public const int CELL_SIZE = 16; // The size of one grid cell in the atlas in pixels
        private Rectangle _worldSpacePaddedAABB;
        public const int PADDING_IN_WORLD_UNITS = 4;

        public Glassware(Texture2D colorAtlas, Texture2D collisionAtlas, string jsonContent, Vector2 initialPosition, GraphicsDevice _gd)
        {
            _colorAtlas = colorAtlas;
            _collisionAtlas = collisionAtlas;
            Position = initialPosition;
            PT = new PixelTexture(_gd, 1);
            pixel = PT.GetPixelTexture(false);
            // Load all flask definitions from the JSON
            _definitions = JsonSerializer.Deserialize<List<FlaskDefinition>>(jsonContent);

            // Pre-load the entire collision atlas data once for performance
            _fullCollisionData = new Color[_collisionAtlas.Width * _collisionAtlas.Height];
            _collisionAtlas.GetData(_fullCollisionData);

            // Set the initial flask type
            SetFlaskType(0);
        }

        private void SetFlaskType(int index)
        {
            if (index < 0 || index >= _definitions.Count) return;

            _currentFlaskIndex = index;
            FlaskDefinition def = _definitions[index];

            // Calculate the source rectangle for the color atlas
            _sourceRectColor = new Rectangle(
                def.X * CELL_SIZE,
                def.Y * CELL_SIZE,
                def.Width * CELL_SIZE,
                def.Height * CELL_SIZE
            );

            // Calculate the source rectangle for the 4x larger collision atlas
            _sourceRectCollision = new Rectangle(
                _sourceRectColor.X * 4,
                _sourceRectColor.Y * 4,
                _sourceRectColor.Width * 4,
                _sourceRectColor.Height * 4
            );

            // The origin for drawing is the center of the visible part
            _drawOrigin = new Vector2(_sourceRectColor.Width / 2f, _sourceRectColor.Height / 2f);
        }

        public void Update(KeyboardState currentKeyboard, KeyboardState previousKeyboard, Vector2 mouseWorldPos, Rectangle mouseScreenRect, bool isLeftMouseDown, float upscaleFactor)
        {
            // --- Cycle Flask Type ---
            if (currentKeyboard.IsKeyDown(Keys.T) && previousKeyboard.IsKeyUp(Keys.T))
            {
                int nextIndex = (_currentFlaskIndex + 1) % _definitions.Count;
                SetFlaskType(nextIndex);
            }

            // --- Handle Grabbing ---
            if (isLeftMouseDown)
            {
                if (!IsHeld && IsCollidingWith(mouseScreenRect, upscaleFactor))
                {
                    IsHeld = true;
                }
            }
            else
            {
                IsHeld = false;
            }

            // --- Handle Movement & Rotation ---
            if (IsHeld)
            {
                Position = mouseWorldPos;
                // Use E and R for rotation to avoid conflict with Q
                if (currentKeyboard.IsKeyDown(Keys.Q)) Rotation -= 0.05f;
                if (currentKeyboard.IsKeyDown(Keys.E)) Rotation += 0.05f;
            }
            UpdateBounds();
        }

        private bool IsCollidingWith(Rectangle mouseScreenRect, float upscaleFactor)
        {
            Rectangle screenPaddedAABB = new Rectangle(
                (int)(_worldSpacePaddedAABB.X * upscaleFactor),
                (int)(_worldSpacePaddedAABB.Y * upscaleFactor),
                (int)(_worldSpacePaddedAABB.Width * upscaleFactor),
                (int)(_worldSpacePaddedAABB.Height * upscaleFactor)
            );
            if (!GetScreenSpaceAABB(upscaleFactor).Intersects(mouseScreenRect))
                return false;

            Matrix inverseScreenTransform = GetInverseScreenTransform(upscaleFactor);
            Vector2 mouseTopLeftLocal = Vector2.Transform(new Vector2(mouseScreenRect.Left, mouseScreenRect.Top), inverseScreenTransform);
            Vector2 mouseBottomRightLocal = Vector2.Transform(new Vector2(mouseScreenRect.Right, mouseScreenRect.Bottom), inverseScreenTransform);

            int minX = (int)Math.Min(mouseTopLeftLocal.X, mouseBottomRightLocal.X);
            int minY = (int)Math.Min(mouseTopLeftLocal.Y, mouseBottomRightLocal.Y);
            int maxX = (int)Math.Ceiling(Math.Max(mouseTopLeftLocal.X, mouseBottomRightLocal.X));
            int maxY = (int)Math.Ceiling(Math.Max(mouseTopLeftLocal.Y, mouseBottomRightLocal.Y));

            for (int y = Math.Max(0, minY); y < Math.Min(_sourceRectCollision.Height, maxY); y++)
            {
                for (int x = Math.Max(0, minX); x < Math.Min(_sourceRectCollision.Width, maxX); x++)
                {
                    // CRITICAL: Offset the lookup by the source rectangle's position in the atlas
                    int atlasX = _sourceRectCollision.X + x;
                    int atlasY = _sourceRectCollision.Y + y;
                    if (_fullCollisionData[atlasX + atlasY * _collisionAtlas.Width].A > 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void UpdateBounds()
        {
            // 1. Get the precise corners of the rotated sprite in world space.
            Vector2[] corners = GetWorldSpaceCorners();

            // 2. Find the min/max X and Y to create the tightest possible AABB.
            int minX = (int)Math.Min(Math.Min(corners[0].X, corners[1].X), Math.Min(corners[2].X, corners[3].X));
            int minY = (int)Math.Min(Math.Min(corners[0].Y, corners[1].Y), Math.Min(corners[2].Y, corners[3].Y));
            int maxX = (int)Math.Max(Math.Max(corners[0].X, corners[1].X), Math.Max(corners[2].X, corners[3].X));
            int maxY = (int)Math.Max(Math.Max(corners[0].Y, corners[1].Y), Math.Max(corners[2].Y, corners[3].Y));

            Rectangle tightAABB = new Rectangle(minX, minY, maxX - minX, maxY - minY);

            // 3. Inflate the tight AABB with our padding value.
            tightAABB.Inflate(PADDING_IN_WORLD_UNITS, PADDING_IN_WORLD_UNITS);

            // 4. Store this final, padded box for collision and debug drawing.
            _worldSpacePaddedAABB = tightAABB;
        }

        private Matrix GetInverseScreenTransform(float upscaleFactor)
        {
            Vector2 flaskScreenPos = this.Position * upscaleFactor;
            // The collision origin is the center of the current collision source rect
            Vector2 collisionOrigin = new Vector2(_sourceRectCollision.Width / 2f, _sourceRectCollision.Height / 2f);
            return Matrix.Invert(
                Matrix.CreateTranslation(new Vector3(-collisionOrigin, 0)) *
                Matrix.CreateScale(this.Scale) *
                Matrix.CreateRotationZ(this.Rotation) *
                Matrix.CreateTranslation(new Vector3(flaskScreenPos, 0))
            );
        }

        public Rectangle GetScreenSpaceAABB(float upscaleFactor)
        {
            float worldWidth = _sourceRectColor.Width * Scale;
            float worldHeight = _sourceRectColor.Height * Scale;
            float maxExtent = (float)Math.Sqrt(worldWidth * worldWidth + worldHeight * worldHeight);
            Vector2 screenCenter = Position * upscaleFactor;
            int screenRadius = (int)(maxExtent * 0.5f);
            return new Rectangle((int)screenCenter.X - screenRadius, (int)screenCenter.Y - screenRadius, screenRadius * 2, screenRadius * 2);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            spriteBatch.Draw(
                texture: _colorAtlas,
                position: Position,
                sourceRectangle: _sourceRectColor, // Use the calculated source rectangle
                color: Color.White,
                rotation: Rotation,
                origin: _drawOrigin, // Use the calculated origin
                scale: Scale,
                effects: SpriteEffects.None,
                layerDepth: 0f
            );
        }

        public void DrawDebug(SpriteBatch spriteBatch, float upscaleFactor)
        {
            // --- 1. Draw the Collision Texture Overlay (unchanged) ---
            spriteBatch.Draw(texture: _collisionAtlas, position: Position * upscaleFactor, sourceRectangle: _sourceRectCollision, color: Color.White * 0.4f, rotation: Rotation, origin: _drawOrigin * 4, scale: Scale, effects: SpriteEffects.None, layerDepth: 0f);

            // --- 2. Draw the PADDED Screen-Space Bounding Box (Cyan) ---
            // This is the actual box used for the broad-phase collision check.
            Rectangle screenPaddedAABB = new Rectangle(
                (int)(_worldSpacePaddedAABB.X * upscaleFactor),
                (int)(_worldSpacePaddedAABB.Y * upscaleFactor),
                (int)(_worldSpacePaddedAABB.Width * upscaleFactor),
                (int)(_worldSpacePaddedAABB.Height * upscaleFactor)
            );
            DrawRectangle(spriteBatch, screenPaddedAABB, Color.Cyan, 2, pixel);

            // --- 3. Draw the Oriented Bounding Box (Red) (unchanged) ---
            Vector2[] worldCorners = GetWorldSpaceCorners();
            for (int i = 0; i < worldCorners.Length; i++)
            {
                Vector2 start = worldCorners[i] * upscaleFactor;
                Vector2 end = worldCorners[(i + 1) % 4] * upscaleFactor;
                DrawLine(spriteBatch, start, end, Color.Red, 2, pixel);
            }
        }
        private Vector2[] GetWorldSpaceCorners()
        {
            Vector2[] corners = new Vector2[4];
            float w = _sourceRectColor.Width;
            float h = _sourceRectColor.Height;

            Vector2 tl = new Vector2(0, 0), tr = new Vector2(w, 0), bl = new Vector2(0, h), br = new Vector2(w, h);

            Matrix transform =
                Matrix.CreateTranslation(new Vector3(-_drawOrigin, 0)) *
                Matrix.CreateScale(Scale) *
                Matrix.CreateRotationZ(Rotation) *
                Matrix.CreateTranslation(new Vector3(Position, 0));

            corners[0] = Vector2.Transform(tl, transform);
            corners[1] = Vector2.Transform(tr, transform);
            corners[2] = Vector2.Transform(br, transform);
            corners[3] = Vector2.Transform(bl, transform);

            return corners;
        }
        private void DrawRectangle(SpriteBatch spriteBatch, Rectangle rect, Color color, int lineWidth, Texture2D pixel)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, lineWidth), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y + rect.Height - lineWidth, rect.Width, lineWidth), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, lineWidth, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X + rect.Width - lineWidth, rect.Y, lineWidth, rect.Height), color);
        }

        private void DrawLine(SpriteBatch spriteBatch, Vector2 point1, Vector2 point2, Color color, float thickness, Texture2D pixel)
        {
            float distance = Vector2.Distance(point1, point2);
            float angle = (float)Math.Atan2(point2.Y - point1.Y, point2.X - point1.X);
            spriteBatch.Draw(pixel, point1, null, color, angle, Vector2.Zero, new Vector2(distance, thickness), SpriteEffects.None, 0);
        }
    }
}