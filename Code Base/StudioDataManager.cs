using Newtonsoft.Json;
using System;
using System.IO;

namespace Pixel_Simulations.Studio
{
    public class StudioDataManager
    {
        public AnimationStateMachine CurrentStateMachine { get; private set; }
        public CharacterAnimProfile CurrentCharacter { get; private set; }

        public void CreateNewStateMachine()
        {
            CurrentStateMachine = new AnimationStateMachine();
            CurrentStateMachine.States["Idle"] = new AnimState { Name = "Idle", GraphPosition = new Microsoft.Xna.Framework.Vector2(100, 100) };
        }

        public void CreateNewCharacter()
        {
            CurrentCharacter = new CharacterAnimProfile
            {
                CharacterName = "NewHero",
                AtlasName = "",
                StateMachineID = CurrentStateMachine?.ID ?? "Unknown"
            };
        }

        public void SaveAll(string basePath)
        {
            try
            {
                Directory.CreateDirectory(basePath);
                if (CurrentStateMachine != null)
                    File.WriteAllText(Path.Combine(basePath, CurrentStateMachine.ID + ".sm"), JsonConvert.SerializeObject(CurrentStateMachine, Formatting.Indented));

                if (CurrentCharacter != null)
                    File.WriteAllText(Path.Combine(basePath, CurrentCharacter.CharacterName + ".char"), JsonConvert.SerializeObject(CurrentCharacter, Formatting.Indented));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Studio Save Error: {ex.Message}"); }
        }

        public void LoadAll(string smPath, string charPath)
        {
            // SAFE LOAD: State Machine
            if (File.Exists(smPath))
            {
                try
                {
                    string json = File.ReadAllText(smPath);
                    if (!string.IsNullOrWhiteSpace(json)) CurrentStateMachine = JsonConvert.DeserializeObject<AnimationStateMachine>(json);
                }
                catch { }
            }
            if (CurrentStateMachine == null) CreateNewStateMachine(); // Fallback if file was empty/corrupted

            // SAFE LOAD: Character
            if (File.Exists(charPath))
            {
                try
                {
                    string json = File.ReadAllText(charPath);
                    if (!string.IsNullOrWhiteSpace(json)) CurrentCharacter = JsonConvert.DeserializeObject<CharacterAnimProfile>(json);
                }
                catch { }
            }
            if (CurrentCharacter == null) CreateNewCharacter(); // Fallback
        }
    }
}