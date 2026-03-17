using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Pixel_Simulations.Data;
using Pixel_Simulations.UI;
using System;
using System.Collections.Generic;


namespace Pixel_Simulations
{
    public class UIManager
    {
        public UIElement Root { get; }
        public UITheme Theme { get; set; }
        public UIElement FocusedElement { get; private set; }
        public bool IsMouseOverUI { get; private set; }
        public UIElement HoveredElement { get; private set; }
        public UIManager()
        {
            Root = new UIPanel
            {
                Size = Vector2.Zero, // Will be resized by layout manager if needed
                //PanelBackground = Color.Transparent,
                //BorderColor = Color.Transparent
            };
            Theme = new UITheme();
        }

        public void Update(EditorInputState input, EventBus bus)
        {
            HoveredElement = null; // Reset every frame

            if (input.IsNewLeftClick)
            {
                var clickedElement = FindElementAt(Root, input.MouseWindowPosition);

                if (FocusedElement != null && FocusedElement != clickedElement)
                    FocusedElement.IsFocused = false;

                FocusedElement = clickedElement;
                if (FocusedElement != null)
                {
                    FocusedElement.IsFocused = true;
                    FocusedElement.OnGotFocus?.Invoke(); // Trigger focus event
                }
            }

            // Get the hovered element for debugging
            HoveredElement = FindElementAt(Root, input.MouseWindowPosition);

            IsMouseOverUI = Root.Update(input, bus);
        }

        public void Draw(SpriteBatch sb, EditorUI ui)
        {
            Root.Draw(sb, ui, Theme);
        }

        // Recursive helper to find the deepest UIElement under the mouse
        private UIElement FindElementAt(UIElement element, Vector2 mousePos)
        {
            if (!element.IsVisible || !element.AbsoluteBounds.Contains(mousePos))
                return null;

            // Search children backwards (top-most first)
            for (int i = element.Children.Count - 1; i >= 0; i--)
            {
                var hit = FindElementAt(element.Children[i], mousePos);
                if (hit != null) return hit;
            }

            return element; // If no children were hit, return this element
        }
    }
}
