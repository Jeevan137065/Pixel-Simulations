using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Pixel_Simulations.Data;
using Pixel_Simulations.UI;
using System.Net.Mime;

namespace Pixel_Simulations.Studio
{
    public enum StudioMode { Character, Animator, Cutscene }

    public class StudioState
    {
        public StudioDataManager DataManager { get; } = new StudioDataManager();
        public EditorLibrary AssetLibrary { get; set; }
        public StudioUI UI { get; } = new StudioUI();
        public EditorInputState Input { get; } = new EditorInputState();
        public int CurrentFrameIndex { get; set; } = 0;
        public bool IsPlaying { get; set; } = false;
        public float PlaybackTimer { get; set; } = 0f;
        public StudioMode CurrentMode { get; set; } = StudioMode.Character;

        // Selection State
        public string SelectedNodeName { get; set; }
        public string SelectedTransitionID { get; set; }
        public string SelectedClipName { get; set; } // NEW: For Animator Mode
        public string ActiveDirection { get; set; } = "South"; // "South", "North", "East", "West"
        public string AssigningBodyPart { get; set; } // The part currently selected in the Inspector to assign a sprite to
        public UIPanel ViewportContainer { get; set; }
        public UIStackPanel InspectorContainer { get; set; }
        public UIPanel ControlContainer { get; set; }

        public void LoadContent(ContentManager content, GraphicsDevice gd)
        {
            AssetLibrary = new EditorLibrary(content);
            // Load whatever spritesheets you have
            AssetLibrary.LoadAtlas("BodySheet", AtlasType.Universal);

            // Load or create safe defaults!
            string smPath = System.IO.Path.Combine(PathHelper.GetAssetsPath(), "Animations", "BasicHumanoid.sm");
            string charPath = System.IO.Path.Combine(PathHelper.GetAssetsPath(), "Animations", "Hero.char");
            DataManager.LoadAll(smPath, charPath);

            UI.LoadContent(content);
        }

    }
}