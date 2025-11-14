using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;


namespace Pixel_Simulations
{
    
    public class UIManager
    {
        private Texture2D _hotbarSlotTexture;
        private Dictionary<Tool, Rectangle> _iconSources;
        private Texture2D _cropsGrowthTexture;
        private Texture2D _cropsIconTexture;
        private const int SlotSize = 48;
        private const int SlotMargin = 2;

        public void LoadContent(ContentManager content, GraphicsDevice graphicsDevice)
        {
            _hotbarSlotTexture = new Texture2D(graphicsDevice, 1, 1);
            _hotbarSlotTexture.SetData(new[] { Color.White });

            _cropsIconTexture = content.Load<Texture2D>("crops_icons");

            _iconSources = new Dictionary<Tool,Rectangle>();
        }

        public void RegisterCropIcons(Dictionary<Tool, CropData> cropData)
        {
            _iconSources.Clear();
            foreach (var crop in cropData.Values)
            {
                if (crop.SeedTool != Tool.None && crop.SeedIcon != null)
                {
                    var iconData = crop.SeedIcon;
                    var rect = new Rectangle(
                        iconData.X * GameConstants.GridCellSize,
                        iconData.Y * GameConstants.GridCellSize,
                        iconData.WidthInCells * GameConstants.GridCellSize,
                        iconData.HeightInCells * GameConstants.GridCellSize
                    );
                    // Seed icons come from the growth sheet
                    _iconSources.Add(crop.SeedTool, rect);
                }
                // You can add logic here for harvested items later
                // if (crop.HarvestTool != Tool.None && crop.HarvestIcon != null) { ... }
            }
        }

        public void DrawHotbar(SpriteBatch spriteBatch, Inventory inventory, GraphicsDevice graphicsDevice)
        {
            int totalWidth = (SlotSize + SlotMargin) * Inventory.HotbarSize;
            int startX = (graphicsDevice.Viewport.Width - totalWidth) / 2;
            int startY = graphicsDevice.Viewport.Height - SlotSize - 20;

            for (int i = 0; i < Inventory.HotbarSize; i++)
            {
                int x = startX + i * (SlotSize + SlotMargin);
                var destRect = new Rectangle(x, startY, SlotSize, SlotSize);

                Color color = (i == inventory.SelectedSlot) ? Color.Yellow : Color.Gray;
                spriteBatch.Draw(_hotbarSlotTexture, destRect, color * 0.5f);

                // Draw the item icon inside the slot
                Tool item = inventory.Hotbar[i];

                // This line gets the Rectangle from the dictionary and puts it into the 'iconInfo' variable.
                if (item != Tool.None && _iconSources.TryGetValue(item, out var iconInfo))
                {
                    // --- THIS IS THE CORRECTED LINE ---
                    // Use 'iconInfo' here, which is the Rectangle we just got.
                    DrawIconCentered(spriteBatch, _cropsIconTexture, iconInfo, destRect);
                }
            }
        }

        private void DrawIconCentered(SpriteBatch spriteBatch, Texture2D texture, Rectangle sourceRect, Rectangle destRect)
        {
            float scaleX = (float)destRect.Width / sourceRect.Width;
            float scaleY = (float)destRect.Height / sourceRect.Height;
            float scale = Math.Min(scaleX, scaleY); // Use the smaller scale to fit the whole icon

            float scaledWidth = sourceRect.Width * scale;
            float scaledHeight = sourceRect.Height * scale;

            Vector2 position = new Vector2(
                destRect.X + (destRect.Width - scaledWidth) / 2,
                destRect.Y + (destRect.Height - scaledHeight) / 2
            );

            spriteBatch.Draw(texture, position, sourceRect, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
