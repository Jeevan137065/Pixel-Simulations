using Microsoft.Xna.Framework;
using Pixel_Simulations.Data;
using Pixel_Simulations.UI;
using System.Linq;

namespace Pixel_Simulations.Studio
{
    public static class StudioUIBuilder
    {
        public static void Build(StudioState state, Rectangle windowBounds, UITheme theme)
        {
            state.UI.Root.Size = new Vector2(windowBounds.Width, windowBounds.Height);
            state.UI.Theme = theme;

            int inspectorWidth = 350;
            int topBarHeight = 40;
            int bottomBarHeight = 100;

            state.InspectorContainer = new UIStackPanel { Direction = StackDirection.Vertical, LocalPosition = new Vector2(windowBounds.Width - inspectorWidth, 0), Size = new Vector2(inspectorWidth, windowBounds.Height), AutoSize = false, PanelBackground = new Color(20, 25, 30), BorderColor = Color.Black, Padding = 15f, Spacing = 10f, ClipToBounds = true };
            state.UI.Root.AddChild(state.InspectorContainer);

            // --- TOP BAR ---
            var topBar = new UIStackPanel { Direction = StackDirection.Horizontal, LocalPosition = Vector2.Zero, Size = new Vector2(windowBounds.Width - inspectorWidth, topBarHeight), AutoSize = false, PanelBackground = new Color(15, 15, 15), BorderColor = Color.White, Padding = 4f, Spacing = 10f };
            topBar.AddChild(new UILabel { Text = "Studio", TextColor = Color.Orange });
            topBar.AddChild(new UIPanel { Size = new Vector2(2, 32), BackgroundColor = Color.Gray });
            topBar.AddChild(new UIButton { Size = new Vector2(32, 32), IconName = "Icon_SM", BackgroundColor = Color.Transparent });
            topBar.AddChild(new UIButton { Size = new Vector2(32, 32), IconName = "Icon_New", Command = new NewStudioDataCommand { IsCharacter = false } });
            topBar.AddChild(new UIPanel { Size = new Vector2(2, 32), BackgroundColor = Color.Gray });
            topBar.AddChild(new UIButton { Size = new Vector2(32, 32), IconName = "Icon_Char", BackgroundColor = Color.Transparent });
            topBar.AddChild(new UIButton { Size = new Vector2(32, 32), IconName = "Icon_New", Command = new NewStudioDataCommand { IsCharacter = true } });
            topBar.AddChild(new UIPanel { Size = new Vector2(2, 32), BackgroundColor = Color.Gray });
            topBar.AddChild(new UIButton { Size = new Vector2(32, 32), IconName = "Icon_Save", Command = new SaveStudioCommand(), BackgroundColor = Color.DarkGreen });
            topBar.AddChild(new UIButton { Size = new Vector2(32, 32), IconName = "Icon_Load", Command = new LoadStudioCommand(), BackgroundColor = Color.DarkGoldenrod });
            state.UI.Root.AddChild(topBar);

            // --- BOTTOM CONTROL BAR ---
            state.ControlContainer = new UIPanel { LocalPosition = new Vector2(0, windowBounds.Height - bottomBarHeight), Size = new Vector2(windowBounds.Width - inspectorWidth, bottomBarHeight), BackgroundColor = new Color(10, 10, 10), BorderColor = Color.White };
            state.UI.Root.AddChild(state.ControlContainer);

            // --- VIEWPORT ---
            state.ViewportContainer = new UIPanel { LocalPosition = new Vector2(0, topBarHeight), Size = new Vector2(windowBounds.Width - inspectorWidth, windowBounds.Height - topBarHeight - bottomBarHeight), BackgroundColor = Color.Black, ClipToBounds = true };
            state.UI.Root.AddChild(state.ViewportContainer);

            SwitchMode(state, StudioMode.Animator);
        }

        private static UIButton CreateModeBtn(StudioState state, string icon, StudioMode mode)
        {
            var btn = new UIButton { Size = new Vector2(40, 40), IconName = icon, BackgroundColor = state.CurrentMode == mode ? Color.Cyan * 0.4f : Color.DarkSlateGray };
            btn.OnClick = () => SwitchMode(state, mode);
            return btn;
        }

        public static void SwitchMode(StudioState state, StudioMode newMode)
        {
            state.CurrentMode = newMode;
            state.ViewportContainer.ClearChildren();
            state.IsPlaying = false;

            if (newMode == StudioMode.Character)
                state.ViewportContainer.AddChild(new UINodeCanvas(state) { Size = state.ViewportContainer.Size });
            else if (newMode == StudioMode.Animator)
            {
                float splitWidth = state.ViewportContainer.Size.X * 0.40f;
                state.ViewportContainer.AddChild(new UISpriteCanvas(state) { Size = new Vector2(splitWidth, state.ViewportContainer.Size.Y) });
                state.ViewportContainer.AddChild(new UINodeCanvas(state) { LocalPosition = new Vector2(splitWidth, 0), Size = new Vector2(state.ViewportContainer.Size.X - splitWidth, state.ViewportContainer.Size.Y) });
                state.ViewportContainer.AddChild(new UIPanel { LocalPosition = new Vector2(splitWidth, 0), Size = new Vector2(2, state.ViewportContainer.Size.Y), BackgroundColor = Color.White });
            }

            RebuildInspector(state);
            RebuildTimeline(state);
        }

        public static void RebuildTimeline(StudioState state)
        {
            state.ControlContainer.ClearChildren();

            // Mode Toggles (Always present)
            var toggleBar = new UIStackPanel { Direction = StackDirection.Horizontal, LocalPosition = new Vector2(10, state.ControlContainer.Size.Y - 50), PanelBackground = Color.Transparent, Spacing = 10f };
            toggleBar.AddChild(CreateModeBtn(state, "Icon_SM", StudioMode.Character));
            toggleBar.AddChild(CreateModeBtn(state, "Icon_Char", StudioMode.Animator));
            toggleBar.AddChild(CreateModeBtn(state, "Icon_Cutscene", StudioMode.Cutscene));
            state.ControlContainer.AddChild(toggleBar);

            // Timeline Controls
            if (state.CurrentMode == StudioMode.Animator && !string.IsNullOrEmpty(state.SelectedClipName))
            {
                var character = state.DataManager.CurrentCharacter;
                if (character != null && character.Clips.TryGetValue(state.SelectedClipName, out var clip) && clip.Frames.Count > 0)
                {
                    var playControls = new UIStackPanel { Direction = StackDirection.Horizontal, LocalPosition = new Vector2(250, state.ControlContainer.Size.Y - 50), Spacing = 10f, PanelBackground = Color.Transparent };

                    // Safe Clamp
                    int totalFrames = clip.Frames.Count;
                    if (state.CurrentFrameIndex >= totalFrames) state.CurrentFrameIndex = System.Math.Max(0, totalFrames - 1);

                    playControls.AddChild(new UIButton { Size = new Vector2(40, 40), IconName = "Icon_AddFrame", BackgroundColor = Color.DarkGreen, OnClick = () => { clip.Frames.Insert(state.CurrentFrameIndex + 1, new AnimFrame()); state.CurrentFrameIndex++; RebuildTimeline(state); } });
                    playControls.AddChild(new UIButton { Size = new Vector2(40, 40), IconName = "Icon_Prev", BackgroundColor = Color.DarkSlateGray, OnClick = () => { state.CurrentFrameIndex = System.Math.Max(0, state.CurrentFrameIndex - 1); RebuildTimeline(state); } });
                    playControls.AddChild(new UIButton { Size = new Vector2(40, 40), IconName = state.IsPlaying ? "Icon_Pause" : "Icon_Play", BackgroundColor = state.IsPlaying ? Color.DarkGoldenrod : Color.DarkGreen, OnClick = () => { state.IsPlaying = !state.IsPlaying; RebuildTimeline(state); } });
                    playControls.AddChild(new UIButton { Size = new Vector2(40, 40), IconName = "Icon_Next", BackgroundColor = Color.DarkSlateGray, OnClick = () => { state.CurrentFrameIndex = System.Math.Min(totalFrames - 1, state.CurrentFrameIndex + 1); RebuildTimeline(state); } });
                    playControls.AddChild(new UIButton { Size = new Vector2(40, 40), IconName = "Icon_DelFrame", BackgroundColor = Color.DarkRed, OnClick = () => { if (clip.Frames.Count > 1) { clip.Frames.RemoveAt(state.CurrentFrameIndex); state.CurrentFrameIndex = System.Math.Max(0, state.CurrentFrameIndex - 1); RebuildTimeline(state); } } });

                    playControls.AddChild(new UILabel { Text = $"  Frame {state.CurrentFrameIndex + 1}/{totalFrames}  ", TextColor = Color.Yellow });

                    playControls.AddChild(new UILabel { Text = "Duration (x): ", TextColor = Color.Gray });
                    var durBox = new UITextBox { Size = new Vector2(40, 30), Text = clip.Frames[state.CurrentFrameIndex].DurationMultiplier.ToString() };
                    durBox.OnSubmit = (t) => { if (float.TryParse(t, out float d)) clip.Frames[state.CurrentFrameIndex].DurationMultiplier = d; };
                    playControls.AddChild(durBox);

                    playControls.AddChild(new UIButton { Size = new Vector2(80, 40), Text = clip.IsLooping ? "Looping" : "Once", BackgroundColor = clip.IsLooping ? Color.DarkCyan : Color.DarkSlateGray, OnClick = () => { clip.IsLooping = !clip.IsLooping; RebuildTimeline(state); } });

                    state.ControlContainer.AddChild(playControls);
                }
            }
        }

        public static void RebuildInspector(StudioState state)
        {
            state.InspectorContainer.ClearChildren();
            var sm = state.DataManager.CurrentStateMachine;
            var character = state.DataManager.CurrentCharacter;

            // =======================================================
            // MODE: ANIMATOR (Character Profile)
            // =======================================================
            if (state.CurrentMode == StudioMode.Animator)
            {
                if (character == null) return;
                state.InspectorContainer.AddChild(new UILabel { Text = "CHARACTER ANIMATOR", TextColor = Color.Yellow });
                state.InspectorContainer.AddChild(new UIPanel { Size = new Vector2(320, 2), BackgroundColor = Color.Gray });

                var charNameBox = new UITextBox { Size = new Vector2(320, 30), Text = character.CharacterName };
                charNameBox.OnSubmit = (t) => character.CharacterName = t;
                state.InspectorContainer.AddChild(charNameBox);

                var atlasBox = new UITextBox { Size = new Vector2(320, 30), Text = character.AtlasName, Placeholder = "Atlas Texture (e.g. BodySheet)" };
                atlasBox.OnSubmit = (t) => character.AtlasName = t;
                state.InspectorContainer.AddChild(atlasBox);

                var sizeRow = new UIStackPanel { Direction = StackDirection.Horizontal, Spacing = 5f, PanelBackground = Color.Transparent };
                sizeRow.AddChild(new UILabel { Text = "Grid Size:", TextColor = Color.Gray });
                var wBox = new UITextBox { Size = new Vector2(50, 30), Text = character.FrameSize.X.ToString() };
                wBox.OnSubmit = (t) => { if (int.TryParse(t, out int w)) character.FrameSize = new Point(w, character.FrameSize.Y); };
                var hBox = new UITextBox { Size = new Vector2(50, 30), Text = character.FrameSize.Y.ToString() };
                hBox.OnSubmit = (t) => { if (int.TryParse(t, out int h)) character.FrameSize = new Point(character.FrameSize.X, h); };
                sizeRow.AddChild(wBox); sizeRow.AddChild(hBox);
                state.InspectorContainer.AddChild(sizeRow);

                state.InspectorContainer.AddChild(new UIPanel { Size = new Vector2(320, 2), BackgroundColor = Color.Gray });

                if (state.SelectedNodeName != null && sm != null && sm.States.ContainsKey(state.SelectedNodeName))
                {
                    state.InspectorContainer.AddChild(new UILabel { Text = $"Node: {state.SelectedNodeName}", TextColor = Color.Cyan });

                    var dirRow = new UIStackPanel { Direction = StackDirection.Horizontal, Spacing = 5f, PanelBackground = Color.Transparent };
                    string[] dirs = { "South", "North", "East", "West" };
                    foreach (var d in dirs)
                    {
                        var dBtn = new UIButton { Size = new Vector2(75, 30), Text = d, BackgroundColor = state.ActiveDirection == d ? Color.DarkGreen : Color.DarkSlateGray };
                        string localD = d;
                        dBtn.OnClick = () => {
                            state.ActiveDirection = localD; state.AssigningBodyPart = null;
                            state.SelectedClipName = $"{state.SelectedNodeName}_{localD}";
                            state.CurrentFrameIndex = 0;
                            RebuildInspector(state); RebuildTimeline(state);
                        };
                        dirRow.AddChild(dBtn);
                    }
                    state.InspectorContainer.AddChild(dirRow);

                    if (!character.DrawOrders.ContainsKey(state.ActiveDirection)) character.DrawOrders[state.ActiveDirection] = new System.Collections.Generic.Dictionary<string, int>();

                    state.InspectorContainer.AddChild(new UILabel { Text = "Parts       |      Z-Index", TextColor = Color.Yellow });
                    var partsList = new UIStackPanel { Direction = StackDirection.Vertical, Size = new Vector2(320, 300), PanelBackground = Color.Black * 0.4f, Spacing = 5f, Padding = 5f };

                    foreach (var part in character.BodyParts)
                    {
                        string localPart = part;
                        bool isAssigning = state.AssigningBodyPart == localPart;
                        var partRow = new UIStackPanel { Direction = StackDirection.Horizontal, Spacing = 10f, PanelBackground = Color.Transparent };

                        var partBtn = new UIButton { Size = new Vector2(200, 30), Text = localPart, BackgroundColor = isAssigning ? Color.Goldenrod : Color.Black * 0.5f };
                        partBtn.OnClick = () => { state.AssigningBodyPart = localPart; RebuildInspector(state); };

                        int zIndex = character.DrawOrders[state.ActiveDirection].ContainsKey(localPart) ? character.DrawOrders[state.ActiveDirection][localPart] : 0;
                        var zInput = new UITextBox { Size = new Vector2(50, 30), Text = zIndex.ToString() };
                        zInput.OnSubmit = (t) => { if (int.TryParse(t, out int val)) character.DrawOrders[state.ActiveDirection][localPart] = val; };

                        partRow.AddChild(partBtn); partRow.AddChild(zInput);
                        partsList.AddChild(partRow);
                    }
                    state.InspectorContainer.AddChild(partsList);
                }
                else state.InspectorContainer.AddChild(new UILabel { Text = "Select a Node to assign sprites.", TextColor = Color.Gray });
            }
            // =======================================================
            // MODE: CHARACTER (State Machine Logic)
            // =======================================================
            else
            {
                if (sm == null) return;

                if (state.SelectedTransitionID != null)
                {
                    StateTransition selectedTrans = null; AnimState parentNode = null;
                    foreach (var n in sm.States.Values) { selectedTrans = n.Transitions.FirstOrDefault(t => t.ID == state.SelectedTransitionID); if (selectedTrans != null) { parentNode = n; break; } }

                    if (selectedTrans != null)
                    {
                        state.InspectorContainer.AddChild(new UILabel { Text = "TRANSITION", TextColor = Color.LimeGreen });
                        state.InspectorContainer.AddChild(new UIPanel { Size = new Vector2(320, 2), BackgroundColor = Color.Gray });
                        state.InspectorContainer.AddChild(new UILabel { Text = $"{parentNode.Name} -> {selectedTrans.TargetState}", TextColor = Color.White });

                        var exitBtn = new UIButton { Size = new Vector2(320, 30), Text = $"Has Exit Time: {selectedTrans.HasExitTime}", BackgroundColor = selectedTrans.HasExitTime ? Color.DarkGreen : Color.DarkRed };
                        exitBtn.OnClick = () => { selectedTrans.HasExitTime = !selectedTrans.HasExitTime; RebuildInspector(state); };
                        state.InspectorContainer.AddChild(exitBtn);

                        state.InspectorContainer.AddChild(new UILabel { Text = "Conditions:", TextColor = Color.Yellow });
                        var condList = new UIStackPanel { Direction = StackDirection.Vertical, Size = new Vector2(320, 200), PanelBackground = Color.Black * 0.5f, Spacing = 5f, Padding = 5f };
                        foreach (var cond in selectedTrans.Conditions)
                        {
                            var row = new UIStackPanel { Direction = StackDirection.Horizontal, Spacing = 5f, PanelBackground = Color.Transparent };
                            row.AddChild(new UILabel { Text = $"{cond.VariableName} {cond.Operator} {cond.Value}", TextColor = Color.White });
                            var delC = new UIButton { Size = new Vector2(30, 20), Text = "X", BackgroundColor = Color.DarkRed };
                            delC.OnClick = () => { selectedTrans.Conditions.Remove(cond); RebuildInspector(state); };
                            row.AddChild(delC);
                            condList.AddChild(row);
                        }
                        state.InspectorContainer.AddChild(condList);

                        var addCondBtn = new UIButton { Size = new Vector2(320, 30), Text = "Add Condition", BackgroundColor = Color.DarkSlateGray };
                        addCondBtn.OnClick = () => { selectedTrans.Conditions.Add(new TransitionCondition { VariableName = "Speed", Operator = ConditionOperator.GreaterThan, Value = "0" }); RebuildInspector(state); };
                        state.InspectorContainer.AddChild(addCondBtn);

                        var delBtn = new UIButton { Size = new Vector2(320, 30), Text = "Delete Transition", BackgroundColor = Color.DarkRed };
                        delBtn.OnClick = () => { parentNode.Transitions.Remove(selectedTrans); state.SelectedTransitionID = null; RebuildInspector(state); };
                        state.InspectorContainer.AddChild(delBtn);
                    }
                }
                else if (state.SelectedNodeName != null && sm.States.TryGetValue(state.SelectedNodeName, out var node))
                {
                    state.InspectorContainer.AddChild(new UILabel { Text = "STATE NODE", TextColor = Color.Cyan });
                    state.InspectorContainer.AddChild(new UIPanel { Size = new Vector2(320, 2), BackgroundColor = Color.Gray });

                    var nameBox = new UITextBox { Size = new Vector2(320, 30), Text = node.Name };
                    nameBox.OnSubmit = (newName) => {
                        if (string.IsNullOrWhiteSpace(newName)) return;
                        string oldName = node.Name;
                        if (oldName == newName || sm.States.ContainsKey(newName)) return;

                        sm.States.Remove(oldName);
                        node.Name = newName;
                        sm.States[newName] = node;
                        if (sm.DefaultState == oldName) sm.DefaultState = newName;
                        foreach (var s in sm.States.Values) foreach (var t in s.Transitions) if (t.TargetState == oldName) t.TargetState = newName;

                        state.SelectedNodeName = newName;
                        RebuildInspector(state);
                    };
                    state.InspectorContainer.AddChild(nameBox);

                    var defBtn = new UIButton { Size = new Vector2(320, 30), Text = "Make Default State", BackgroundColor = sm.DefaultState == node.Name ? Color.DarkGoldenrod : Color.DarkSlateGray };
                    defBtn.OnClick = () => { sm.DefaultState = node.Name; RebuildInspector(state); };
                    state.InspectorContainer.AddChild(defBtn);

                    // --- SAFE DELETE NODE ---
                    var delNodeBtn = new UIButton { Size = new Vector2(320, 30), Text = "Delete Node", BackgroundColor = Color.DarkRed };
                    delNodeBtn.OnClick = () => {
                        sm.States.Remove(node.Name);
                        foreach (var s in sm.States.Values) s.Transitions.RemoveAll(t => t.TargetState == node.Name);

                        // Clear all active selections so we don't crash trying to draw a dead node
                        state.SelectedNodeName = null;
                        state.SelectedClipName = null;
                        state.AssigningBodyPart = null;
                        state.CurrentFrameIndex = 0;

                        RebuildInspector(state);
                        RebuildTimeline(state);
                    };
                    state.InspectorContainer.AddChild(delNodeBtn);
                }
                else
                {
                    state.InspectorContainer.AddChild(new UILabel { Text = "GLOBAL VARIABLES", TextColor = Color.Orange });
                    state.InspectorContainer.AddChild(new UIPanel { Size = new Vector2(320, 2), BackgroundColor = Color.Gray });
                    var varList = new UIStackPanel { Direction = StackDirection.Vertical, Size = new Vector2(320, 300), PanelBackground = Color.Black * 0.5f, Spacing = 5f, Padding = 5f };
                    foreach (var v in sm.Variables.Values.ToList())
                    {
                        var row = new UIStackPanel { Direction = StackDirection.Horizontal, Spacing = 5f, PanelBackground = Color.Transparent };
                        row.AddChild(new UILabel { Text = $"[{v.Type}] {v.Name} = {v.DefaultValue}", TextColor = Color.White });
                        var delV = new UIButton { Size = new Vector2(30, 20), Text = "X", BackgroundColor = Color.DarkRed };
                        delV.OnClick = () => { sm.Variables.Remove(v.Name); RebuildInspector(state); };
                        row.AddChild(delV);
                        varList.AddChild(row);
                    }
                    state.InspectorContainer.AddChild(varList);
                    var addVarBtn = new UIButton { Size = new Vector2(320, 30), Text = "Add Test Variable", BackgroundColor = Color.DarkCyan };
                    addVarBtn.OnClick = () => { sm.Variables["Speed"] = new AnimVariable { Name = "Speed", Type = VarType.Float, DefaultValue = "0" }; RebuildInspector(state); };
                    state.InspectorContainer.AddChild(addVarBtn);
                }
            }
        }
    }
}