using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Pixel_Simulations.Editor;
using Pixel_Simulations.UI;
using System.Collections.Generic;

namespace Pixel_Simulations.Data
{
    public class Button
    {
        public Rectangle Bounds { get; }
        public ICommand CommandToPublish { get; }
        public string IconName { get; set; }
        public bool IsHovered { get; private set; }

        public Button(Rectangle bounds, ICommand command, string iconName)
        {
            Bounds = bounds;
            CommandToPublish = command;
            IconName = iconName;
        }

        public bool Update(InputState input)// Returns true if mouse is on this Button
        {
            return IsHovered = Bounds.Contains(input.MouseWindowPosition);
        }

        public void Draw(SpriteBatch sb, EditorUI editorUI)
        {
            Color tint = IsHovered ? Color.Yellow : Color.White;
            if (IsHovered) sb.FillRectangle(Bounds, Color.White * 0.2f);
            sb.DrawRectangle(Bounds, tint);
            editorUI.DrawIcon(sb, Bounds, IconName, tint);
        }


    }
    public class PopupWindow
    {
        public Rectangle Bounds { get; }
        public string Title { get; }
        public List<string> Items { get; }
        public string SelectedItem { get; private set; }

        public PopupWindow(string title, List<string> items, LayoutManager layout)
        {
            Title = title;
            Items = items;

            // Center the popup in the main viewport
            int width = 300;
            int height = 200;
            Bounds = new Rectangle(
                layout.ViewportPanel.X + (layout.ViewportPanel.Width - width) / 2,
                layout.ViewportPanel.Y + (layout.ViewportPanel.Height - height) / 2,
                width, height);
        }

        public void Update(InputState input)
        {
            SelectedItem = null;
            if (!Bounds.Contains(input.MouseWindowPosition)) return;

            if (input.IsNewLeftClick)
            {
                int index = (int)((input.MouseWindowPosition.Y - Bounds.Y - 30) / 20); // 30px for title
                if (index >= 0 && index < Items.Count)
                {
                    SelectedItem = Items[index];
                }
            }
        }

        public void Draw(SpriteBatch sb, SpriteFont font)
        {
            // Draw a semi-transparent overlay behind the popup
            sb.FillRectangle(new Rectangle(0, 0, 2000, 2000), Color.Black * 0.5f);

            // Draw the popup window
            sb.FillRectangle(Bounds, Color.DarkSlateGray);
            sb.DrawRectangle(Bounds, Color.Black, 2);
            sb.DrawString(font, Title, Bounds.Location.ToVector2() + new Vector2(5, 5), Color.White);

            // Draw the list of items
            int yOffset = 30;
            foreach (var item in Items)
            {
                var pos = new Vector2(Bounds.X + 10, Bounds.Y + yOffset);
                sb.DrawString(font, item, pos, Color.White);
                yOffset += 20;
            }
        }
    }
    public abstract class BasePanel : IPanel
    {
        protected Rectangle Area { get; }
        protected EditorUI _editorUI { get; }
        protected EditorState _editorState { get; }

        protected BasePanel(Rectangle area, EditorUI editorUI, EditorState editorState)
        {
            Area = area;
            _editorUI = editorUI;
            _editorState = editorState;
        }

        public abstract void Update(InputState input, EventBus bus);
        public abstract void Draw(SpriteBatch spriteBatch);
    }
}
