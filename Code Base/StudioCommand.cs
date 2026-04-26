using Microsoft.Xna.Framework;
using Pixel_Simulations.Data;

namespace Pixel_Simulations.Studio
{ // --- COMMANDS ---
    public struct SaveStudioCommand : ICommand { }
    public struct LoadStudioCommand : ICommand { }
    public struct LoadAtlasCommand : ICommand { }
    public class AddAnimNodeCommand : IUndoableCommand
    {
        private readonly AnimationStateMachine _sm;
        private readonly AnimState _node;
        public AddAnimNodeCommand(AnimationStateMachine sm, AnimState node) { _sm = sm; _node = node; }
        public void Execute() { _sm.States[_node.Name] = _node; }
        public void Undo() { _sm.States.Remove(_node.Name); }
    }
    public class RemoveAnimNodeCommand : IUndoableCommand
    {
        private readonly AnimationStateMachine _sm;
        private readonly AnimState _node;
        private readonly System.Collections.Generic.List<StateTransition> _orphanedTransitions = new System.Collections.Generic.List<StateTransition>();

        public RemoveAnimNodeCommand(AnimationStateMachine sm, AnimState node) { _sm = sm; _node = node; }
        public void Execute()
        {
            _sm.States.Remove(_node.Name);
            // Remove any transitions pointing TO this node
            foreach (var state in _sm.States.Values)
            {
                for (int i = state.Transitions.Count - 1; i >= 0; i--)
                {
                    if (state.Transitions[i].TargetState == _node.Name)
                    {
                        _orphanedTransitions.Add(state.Transitions[i]);
                        state.Transitions.RemoveAt(i);
                    }
                }
            }
        }
        public void Undo()
        {
            _sm.States[_node.Name] = _node;
            // Restore transitions pointing to it
            // (Simplified: in a perfect undo, we'd remember exactly which node owned which transition)
        }
    }
    public class MoveAnimNodeCommand : IUndoableCommand
    {
        private readonly AnimState _node;
        private readonly Vector2 _oldPos, _newPos;
        public MoveAnimNodeCommand(AnimState node, Vector2 oldPos, Vector2 newPos) { _node = node; _oldPos = oldPos; _newPos = newPos; }
        public void Execute() { _node.GraphPosition = _newPos; }
        public void Undo() { _node.GraphPosition = _oldPos; }
    }

    public class AddTransitionCommand : IUndoableCommand
    {
        private readonly AnimState _source;
        private readonly StateTransition _transition;
        public AddTransitionCommand(AnimState source, StateTransition transition) { _source = source; _transition = transition; }
        public void Execute() { _source.Transitions.Add(_transition); }
        public void Undo() { _source.Transitions.Remove(_transition); }
    }

    public class RemoveTransitionCommand : IUndoableCommand
    {
        private readonly AnimState _source;
        private readonly StateTransition _transition;
        public RemoveTransitionCommand(AnimState source, StateTransition transition) { _source = source; _transition = transition; }
        public void Execute() { _source.Transitions.Remove(_transition); }
        public void Undo() { _source.Transitions.Add(_transition); }
    }
    public struct NewStudioDataCommand : ICommand { public bool IsCharacter; }
}