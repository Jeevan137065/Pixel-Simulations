using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Pixel_Simulations.Studio
{
    public enum VarType { Float, Int, Bool, Trigger, String }
    public enum ConditionOperator { Equals, NotEquals, GreaterThan, LessThan, GreaterOrEqual, LessOrEqual }
    public enum NodePort { Top, Bottom, Left, Right }
    public enum LineStyle { Solid, Dashed, Dotted }
    public enum NodeShape { Rectangle, Pill }

    // ==========================================================
    // 1. STATE MACHINE (The Logic Brain - Shared across characters)
    // ==========================================================
    [JsonObject(MemberSerialization.OptIn)]
    public class AnimVariable
    {
        [JsonProperty] public string Name { get; set; }
        [JsonProperty] public VarType Type { get; set; }
        [JsonProperty] public string DefaultValue { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class TransitionCondition
    {
        [JsonProperty] public string VariableName { get; set; }
        [JsonProperty] public ConditionOperator Operator { get; set; }
        [JsonProperty] public string Value { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class StateTransition
    {
        [JsonProperty] public string ID { get; set; } = Guid.NewGuid().ToString().Substring(0, 8);
        [JsonProperty] public string TargetState { get; set; }
        [JsonProperty] public bool HasExitTime { get; set; } = false;
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<TransitionCondition> Conditions { get; set; } = new List<TransitionCondition>();
        [JsonProperty] public NodePort SourcePort { get; set; } = NodePort.Right;
        [JsonProperty] public NodePort TargetPort { get; set; } = NodePort.Left;
        [JsonProperty] public Color LineColor { get; set; } = Color.Cyan;
        [JsonProperty] public LineStyle Style { get; set; } = LineStyle.Solid;
        [JsonProperty] public string CustomLabel { get; set; } = "";
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class AnimState
    {
        [JsonProperty] public string Name { get; set; }
        // Maps Context (e.g., "Axe", "Default") to an Animation Clip Name (e.g., "Walk_Axe")
        [JsonProperty] public Dictionary<string, string> Animations { get; set; } = new Dictionary<string, string>();

        [JsonProperty] public List<StateTransition> Transitions { get; set; } = new List<StateTransition>();
        [JsonProperty] public Vector2 GraphPosition { get; set; }
        [JsonProperty] public Vector2 Size { get; set; } = new Vector2(160, 45);
        [JsonProperty] public Color NodeColor { get; set; } = new Color(40, 40, 45);
        [JsonProperty] public NodeShape Shape { get; set; } = NodeShape.Rectangle;
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class AnimationStateMachine
    {
        [JsonProperty] public string ID { get; set; } = "New_StateMachine";
        [JsonProperty] public string DefaultState { get; set; } = "Idle";
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Dictionary<string, AnimVariable> Variables { get; set; } = new Dictionary<string, AnimVariable>();
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Dictionary<string, AnimState> States { get; set; } = new Dictionary<string, AnimState>();
        [JsonProperty] public Vector2 GraphPanOffset { get; set; }
        [JsonProperty] public float GraphZoom { get; set; } = 1.0f;
    }

    // ==========================================================
    // 2. CHARACTER PROFILE (The Modular Body)
    // ==========================================================
    [JsonObject(MemberSerialization.OptIn)]
    public class AnimFrame
    {
        // NEW: Maps "Torso", "LeftArm", etc. to their exact source rectangles on the atlas!
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Dictionary<string, Rectangle> Parts { get; set; } = new Dictionary<string, Rectangle>();
        [JsonProperty] public Vector2 Pivot { get; set; } = new Vector2(16, 32);
        [JsonProperty] public float DurationMultiplier { get; set; } = 1.0f;
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class AnimationClip
    {
        [JsonProperty] public string Name { get; set; }
        [JsonProperty] public int FPS { get; set; } = 12;
        [JsonProperty] public bool IsLooping { get; set; } = true;
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<AnimFrame> Frames { get; set; } = new List<AnimFrame>();
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class CharacterAnimProfile
    {
        [JsonProperty] public string CharacterName { get; set; } = "Hero";
        [JsonProperty] public string AtlasName { get; set; } = "";
        [JsonProperty] public string StateMachineID { get; set; } = "";
        [JsonProperty] public Point FrameSize { get; set; } = new Point(32, 64);

        // Define the available parts for this character
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<string> BodyParts { get; set; } = new List<string> { "Torso", "Head", "LeftArm", "RightArm", "LeftLeg", "RightLeg" };

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Dictionary<string, Dictionary<string, int>> DrawOrders { get; set; } = new Dictionary<string, Dictionary<string, int>>();

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Dictionary<string, AnimationClip> Clips { get; set; } = new Dictionary<string, AnimationClip>();
    }
}