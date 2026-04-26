using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Pixel_Simulations.Data;
using Pixel_Simulations.Editor;

namespace Pixel_Simulations.Studio
{


    public class StudioController
    {
        private readonly StudioState _state;
        private readonly EventBus _bus;
        public readonly HistoryController History;

        private string _lastSelectedNode = null;
        private string _lastSelectedTrans = null;
        private string _lastSelectedClip = null;

        public StudioController(StudioState state, EventBus bus)
        {
            _state = state;
            _bus = bus;
            History = new HistoryController(_bus, new HistoryState());

            _bus.Subscribe<SaveStudioCommand>(cmd => _state.DataManager.SaveAll(System.IO.Path.Combine(PathHelper.GetAssetsPath(), "Animations")));

            _bus.Subscribe<LoadStudioCommand>(cmd => {
                _state.DataManager.LoadAll(
                    System.IO.Path.Combine(PathHelper.GetAssetsPath(), "Animations", "New_StateMachine.sm"),
                    System.IO.Path.Combine(PathHelper.GetAssetsPath(), "Animations", "Hero.char"));
                StudioUIBuilder.RebuildInspector(_state);
                StudioUIBuilder.RebuildTimeline(_state);
            });

            _bus.Subscribe<NewStudioDataCommand>(cmd => {
                if (cmd.IsCharacter) _state.DataManager.CreateNewCharacter();
                else _state.DataManager.CreateNewStateMachine();
                StudioUIBuilder.RebuildInspector(_state);
                StudioUIBuilder.RebuildTimeline(_state);
            });
        }

        public void Update(GameTime gameTime, KeyboardState kbs, MouseState ms)
        {
            _state.Input.PreviousKeyboard = _state.Input.CurrentKeyboard;
            _state.Input.PreviousMouse = _state.Input.CurrentMouse;
            _state.Input.CurrentKeyboard = kbs;
            _state.Input.CurrentMouse = ms;
            _state.Input.MouseWindowPosition = ms.Position.ToVector2();
            _state.Input.Update(gameTime);

            // --- TIMELINE PLAYBACK LOGIC ---
            if (_state.IsPlaying && _state.CurrentMode == StudioMode.Animator)
            {
                var character = _state.DataManager.CurrentCharacter;
                if (character != null && !string.IsNullOrEmpty(_state.SelectedClipName) && character.Clips.TryGetValue(_state.SelectedClipName, out var clip) && clip.Frames.Count > 0)
                {
                    _state.PlaybackTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

                    float frameDuration = (1f / clip.FPS) * clip.Frames[_state.CurrentFrameIndex].DurationMultiplier;

                    if (_state.PlaybackTimer >= frameDuration)
                    {
                        _state.PlaybackTimer -= frameDuration;
                        _state.CurrentFrameIndex++;

                        if (_state.CurrentFrameIndex >= clip.Frames.Count)
                        {
                            if (clip.IsLooping) _state.CurrentFrameIndex = 0;
                            else { _state.CurrentFrameIndex = clip.Frames.Count - 1; _state.IsPlaying = false; }
                        }
                        StudioUIBuilder.RebuildTimeline(_state); // Refresh UI for playhead
                    }
                }
            }

            bool isTyping = _state.UI.FocusedElement is Pixel_Simulations.UI.UITextBox;
            if (!isTyping)
            {
                if (kbs.IsKeyDown(Keys.LeftControl) && kbs.IsKeyDown(Keys.Z) && _state.Input.PreviousKeyboard.IsKeyUp(Keys.Z)) _bus.Publish(new MenuActionCommand { ActionName = "Undo" });
                if (kbs.IsKeyDown(Keys.LeftControl) && kbs.IsKeyDown(Keys.S) && _state.Input.PreviousKeyboard.IsKeyUp(Keys.S)) _bus.Publish(new SaveStudioCommand());
            }

            _state.UI.Update(_state.Input, _bus);

            // --- FIX: ALWAYS SYNC UI IF SELECTION CHANGES ---
            if (_state.SelectedNodeName != _lastSelectedNode || _state.SelectedTransitionID != _lastSelectedTrans || _state.SelectedClipName != _lastSelectedClip)
            {
                StudioUIBuilder.RebuildInspector(_state);
                StudioUIBuilder.RebuildTimeline(_state); // Ensures timeline appears instantly!

                _lastSelectedNode = _state.SelectedNodeName;
                _lastSelectedTrans = _state.SelectedTransitionID;
                _lastSelectedClip = _state.SelectedClipName;
            }
        }
    }
}