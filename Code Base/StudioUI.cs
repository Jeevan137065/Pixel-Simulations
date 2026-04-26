using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using Newtonsoft.Json;
using Pixel_Simulations.Data;
using Pixel_Simulations.UI;
using System.Collections.Generic;
using System.IO;

namespace Pixel_Simulations.Studio
{
    public class StudioUI : IUIContext
    {
        // The master container that holds all panels, canvas, and inspectors
        public UIElement Root { get; }

        // Visual theme settings (Colors, Fonts)
        public UITheme Theme { get; set; }

        // Tracks which UI element currently has keyboard/mouse focus (like a TextBox)
        public UIElement FocusedElement { get; private set; }

        // True if the mouse is hovering over any UI element
        public bool IsMouseOverUI { get; private set; }
        private Texture2D _iconsTexture;
        public Dictionary<string, Rectangle> IconSources { get; private set; } = new Dictionary<string, Rectangle>();
        public StudioUI()
        {
            // Initialize an invisible root panel that spans the whole screen
            Root = new UIPanel
            {
                Size = Vector2.Zero,
                BackgroundColor = Color.Transparent,
                BorderColor = Color.Transparent
            };
            Theme = new UITheme();
        }
        public void LoadContent(ContentManager content)
        {
            try
            {
                _iconsTexture = content.Load<Texture2D>("StudioUI");
                string json = File.ReadAllText("Content/StudioIcons.json");
                var defFile = JsonConvert.DeserializeObject<dynamic>(json);
                int iconSize = defFile.icon_size;
                var icons = defFile.icons.ToObject<Dictionary<string, IconDefinition>>();
                foreach (var iconPair in icons)
                {
                    IconSources[iconPair.Key] = new Rectangle(iconPair.Value.x, iconPair.Value.y, iconSize, iconSize);
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load Studio Icons: {ex.Message}");
            }
        }
        public void Update(EditorInputState input, EventBus bus)
        {
            IsMouseOverUI = false;

            if (input.IsNewLeftClick) CheckFocusClick(Root, input);

            // --- NEW: GLOBAL SCROLLING LOGIC ---
            int scrollDelta = input.CurrentMouse.ScrollWheelValue - input.PreviousMouse.ScrollWheelValue;
            if (scrollDelta != 0)
            {
                // Find what panel the mouse is over and scroll it
                var hovered = FindElementAt(Root, input.MouseWindowPosition);
                while (hovered != null)
                {
                    if (hovered is UIStackPanel stack && stack.ClipToBounds)
                    {
                        // Scroll down/up and clamp to 0
                        stack.ScrollOffset = new Vector2(0, System.Math.Max(0, stack.ScrollOffset.Y - scrollDelta * 0.5f));
                        break;
                    }
                    hovered = hovered.Parent;
                }
            }

            IsMouseOverUI = Root.Update(input, bus);
        }

        public void Draw(SpriteBatch sb)
        {
            // Recursively draw all UI elements starting from the Root
            Root.Draw(sb, this, Theme);
        }

        // ==========================================
        // IUIContext Implementation
        // ==========================================

        public void SetFocus(UIElement element)
        {
            if (FocusedElement != null)
                FocusedElement.IsFocused = false;

            FocusedElement = element;

            if (FocusedElement != null)
                FocusedElement.IsFocused = true;
        }

        public void CheckFocusClick(UIElement root, EditorInputState input)
        {
            var hit = FindElementAt(root, input.MouseWindowPosition);

            // Only TextBoxes actually "hold" focus for typing right now.
            if (hit is UITextBox)
                SetFocus(hit);
            else if (hit == null || !(hit is UIButton))
                SetFocus(null);
        }

        private UIElement FindElementAt(UIElement element, Vector2 mousePos)
        {
            if (!element.IsVisible || !element.AbsoluteBounds.Contains(mousePos))
                return null;

            // Search backwards so elements drawn on TOP are clicked first
            for (int i = element.Children.Count - 1; i >= 0; i--)
            {
                var hit = FindElementAt(element.Children[i], mousePos);
                if (hit != null) return hit;
            }
            return element;
        }

        public void DrawIcon(SpriteBatch sb, Rectangle destination, string iconName, Color color)
        {
            if (_iconsTexture != null && IconSources.TryGetValue(iconName, out var sourceRect))
            {
                sb.Draw(_iconsTexture, destination, sourceRect, color);
            }
            else
            {
                // Fallback: draw a colored box if icon is missing
                sb.FillRectangle(destination, color * 0.5f);
            }
        }
    }
}