using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Simulations.Data
{
    public class EventBus
    {
        public readonly Dictionary<Type, List<Action<ICommand>>> _subscribers;
        public ICommand LastPublishedCommand { get; private set; }
        public int CommandsProcessed = 0;

        public EventBus()
        {
            _subscribers = new Dictionary<Type, List<Action<ICommand>>>();
        }
        public void Subscribe<T>(Action<T> handler) where T : ICommand
        {
            Type commandType = typeof(T);
            if (!_subscribers.ContainsKey(commandType))
            {
                _subscribers[commandType] = new List<Action<ICommand>>();
            }
            // We store the handler as a generic Action<ICommand> by wrapping it.
            _subscribers[commandType].Add(cmd => handler((T)cmd));
        }
        public void Publish<T>(T command) where T : ICommand
        {
            if (command == null) return;
            CommandsProcessed++;
            Type commandType = command.GetType(); // Get the command's ACTUAL runtime type
            LastPublishedCommand = command;

            System.Diagnostics.Debug.WriteLine($"EVENT BUS PUBLISHED: {commandType.Name}");

            var typesToNotify = commandType.GetInterfaces().Concat(new[] { commandType });

            // 2. Loop through each type (e.g., PlaceTileCommand, IUndoableCommand, ICommand)
            foreach (var type in typesToNotify)
            {
                // 3. If we have subscribers for that type, notify them.
                if (_subscribers.ContainsKey(type))
                {
                    foreach (var handler in _subscribers[type])
                    {
                        // The handler is an Action<ICommand>, so it can accept any command.
                        // The wrapper we created in Subscribe() will handle the cast.
                        handler(command);
                    }
                }
            }
        }
    }
}
