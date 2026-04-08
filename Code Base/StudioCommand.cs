using Microsoft.Xna.Framework;
using Pixel_Simulations.Data;

namespace Pixel_Simulations.Studio
{
    // --- BASIC ACTIONS ---
    public struct SaveStudioCommand : ICommand { }
    public struct LoadStudioCommand : ICommand { }
    public struct LoadAtlasCommand : ICommand { }

    // --- UNDOABLE ACTIONS (The Graph) ---
    public class AddAnimNodeCommand : IUndoableCommand
    {
        private readonly AnimationStateMachine _sm;
        private readonly AnimState _node;

        public AddAnimNodeCommand(AnimationStateMachine sm, AnimState node)
        {
            _sm = sm;
            _node = node;
        }

        public void Execute() { _sm.States[_node.Name] = _node; }
        public void Undo() { _sm.States.Remove(_node.Name); }
    }

    public class MoveAnimNodeCommand : IUndoableCommand
    {
        private readonly AnimState _node;
        private readonly Vector2 _oldPos;
        private readonly Vector2 _newPos;

        public MoveAnimNodeCommand(AnimState node, Vector2 oldPos, Vector2 newPos)
        {
            _node = node;
            _oldPos = oldPos;
            _newPos = newPos;
        }

        public void Execute() { _node.EditorVisuals.Position = _newPos; }
        public void Undo() { _node.EditorVisuals.Position = _oldPos; }
    }

    public class AddAnimLinkCommand : IUndoableCommand
    {
        private readonly AnimState _source;
        private readonly StateTransition _transition;

        public AddAnimLinkCommand(AnimState source, StateTransition transition)
        {
            _source = source;
            _transition = transition;
        }

        public void Execute() { _source.Transitions.Add(_transition); }
        public void Undo() { _source.Transitions.Remove(_transition); }
    }
}