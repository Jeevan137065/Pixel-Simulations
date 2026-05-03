using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Simulations
{

    public class TimeManager
    {
        public int Day { get; private set; }
        public Season CurrentSeason { get; private set; }
        public int Year { get; private set; }

        public event Action OnDayChanged;

        public TimeManager()
        {
            Day = 1;
            CurrentSeason = Season.Spring;
            Year = 1;
        }

        public void AdvanceDay()
        {
            Day++;
            if (Day > 28) // Strict 28-day months
            {
                Day = 1;
                CurrentSeason = (Season)(((int)CurrentSeason + 1) % 6);
                if (CurrentSeason == Season.Spring) Year++;
            }
            OnDayChanged?.Invoke();
        }

        // NEW: Safely jump to a specific season for debugging
        public void DebugSetDate(Season season, int day)
        {
            CurrentSeason = season;
            Day = MathHelper.Clamp(day, 1, 28);
            OnDayChanged?.Invoke();
        }

        // Calculates the Phase based on the exact day
        public SeasonPhase GetCurrentPhase()
        {
            if (Day <= 7) return SeasonPhase.Early;    // Week 1
            if (Day <= 21) return SeasonPhase.Mid;     // Weeks 2 & 3
            return SeasonPhase.Late;                   // Week 4
        }

        public override string ToString() => $"Year {Year}, {CurrentSeason}, Day {Day} ({GetCurrentPhase()})";
    }
    public class TimeKeyframe
    {
        [JsonProperty("hour")] public float Hour { get; set; }
        [JsonProperty("color"), JsonConverter(typeof(HexColorConverter))]
        public Color Color { get; set; }
    }
    public class PhaseLightData
    {
        [JsonProperty("style")] public string Style { get; set; } = "linear";
        [JsonProperty("primary")] public List<TimeKeyframe> Primary { get; set; } = new List<TimeKeyframe>();
        [JsonProperty("secondary")] public List<TimeKeyframe> Secondary { get; set; } = new List<TimeKeyframe>();
    }
    public class DayTimeManager
    {
        public float TimeOfDay { get; private set; } = 12f;
        public float TimeScale { get; set; } = 0.016f;

        public Color CurrentAmbientColorTop { get; private set; } = Color.White;
        public Color CurrentAmbientColorBottom { get; private set; } = Color.White;
        public int CurrentGradientStyle { get; private set; } = 0; // 0=Linear, 1=Reverse, 2=Horiz, 3=Radial, 4=RadialRev

        private Dictionary<string, Dictionary<string, PhaseLightData>> _gradientDatabase;

        public void LoadContent(string jsonPath)
        {
            if (File.Exists(jsonPath))
            {
                try
                {
                    string json = File.ReadAllText(jsonPath);
                    var settings = new JsonSerializerSettings { Converters = { new HexColorConverter() } };
                    _gradientDatabase = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, PhaseLightData>>>(json, settings);
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to load TimeOfDay.json: {ex.Message}"); }
            }
            if (_gradientDatabase == null) _gradientDatabase = new Dictionary<string, Dictionary<string, PhaseLightData>>();
        }

        public void Update(GameTime gameTime, Season currentSeason, SeasonPhase currentPhase)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            TimeOfDay += dt * TimeScale;
            if (TimeOfDay >= 24f) TimeOfDay -= 24f;
            if (TimeOfDay < 0f) TimeOfDay += 24f;

            string sKey = currentSeason.ToString();
            string pKey = currentPhase.ToString();

            if (_gradientDatabase.TryGetValue(sKey, out var seasonDict) && seasonDict.TryGetValue(pKey, out var activeData))
            {
                CurrentAmbientColorTop = EvaluateGradient(TimeOfDay, activeData.Primary);
                CurrentAmbientColorBottom = EvaluateGradient(TimeOfDay, activeData.Secondary);

                // Map string style to int for the shader
                CurrentGradientStyle = activeData.Style switch
                {
                    "linear-reverse" => 1,
                    "linear-horizontal" => 2,
                    "radial" => 3,
                    "radial-reverse" => 4,
                    _ => 0 // default linear
                };
            }
        }

        public void SetTime(float hour) => TimeOfDay = MathHelper.Clamp(hour, 0f, 23.99f);
        public void AddHours(float hours)
        {
            TimeOfDay += hours;
            if (TimeOfDay >= 24f) TimeOfDay -= 24f;
            if (TimeOfDay < 0f) TimeOfDay += 24f;
        }
        private Color EvaluateGradient(float time, List<TimeKeyframe> gradient)
        {
            if (gradient == null || gradient.Count == 0) return Color.White;
            if (gradient.Count == 1) return gradient[0].Color;

            TimeKeyframe before = gradient.LastOrDefault(k => k.Hour <= time) ?? gradient.Last();
            TimeKeyframe after = gradient.FirstOrDefault(k => k.Hour > time) ?? gradient.First();

            if (before == after) return before.Color;

            float timeRange = after.Hour - before.Hour;
            float currentTimePassed = time - before.Hour;

            if (timeRange < 0) { timeRange += 24f; if (currentTimePassed < 0) currentTimePassed += 24f; }

            float lerpFactor = currentTimePassed / timeRange;
            return Color.Lerp(before.Color, after.Color, lerpFactor);
        }

        public string GetDebugInfo()
        {
            int hours = (int)TimeOfDay;
            int minutes = (int)((TimeOfDay - hours) * 60);
            return $"Time: {hours:D2}:{minutes:D2} | Ambient: {CurrentAmbientColorTop.ToVector3():F2}, {CurrentAmbientColorBottom.ToVector3().Y:F2}";
        }
    }
}
