using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Simulations
{
    public class TimeManager
    {
        public int Day { get; private set; }
        public Season CurrentSeason { get; private set; }
        public int Year { get; private set; }

        // Event that fires whenever a new day begins.
        // Other systems (like plant growth) can subscribe to this.
        public event Action OnDayChanged;

        public TimeManager()
        {
            // Start the game on the first day of Spring, Year 1.
            Day = 1;
            CurrentSeason = Season.Spring;
            Year = 1;
        }

        public void AdvanceDay()
        {
            Day++;

            int daysInCurrentSeason = GetDaysInSeason(CurrentSeason, Year);

            if (Day > daysInCurrentSeason)
            {
                // Move to the next season
                Day = 1;

                // Cycle through the seasons. The modulo operator (%) makes it wrap around.
                CurrentSeason = (Season)(((int)CurrentSeason + 1) % Enum.GetValues(typeof(Season)).Length);

                // If we've wrapped back to Spring, a new year has begun.
                if (CurrentSeason == Season.Spring)
                {
                    Year++;
                }
            }

            // Notify any subscribers that the day has changed.
            OnDayChanged?.Invoke();
        }

        private int GetDaysInSeason(Season season, int year)
        {
            if (season == Season.Spring && IsLeapYear(year))
            {
                return 31; // Spring has an extra day in a leap year
            }
            return 30; // All other seasons have 30 days
        }

        private bool IsLeapYear(int year)
        {
            // A simple leap year calculation is fine for a game.
            return year % 4 == 0;
        }

        public override string ToString()
        {
            string s = $"Year {Year}, {CurrentSeason} , Day {Day}";
            return s;
        }
    }
    public class TimeKeyframe
    {
        public float Hour { get; }
        public Vector3 Color { get; }

        public TimeKeyframe(float hour, Vector3 color)
        {
            Hour = hour;
            Color = color;
        }
    }
    public class DayTimeManager
    {
        public float TimeOfDay { get; private set; } = 12f; // Starts at Noon (12.0)

        // 1 real second = X game hours. 
        // e.g., 0.016 means 1 real minute = 1 game hour (a 24-minute day cycle).
        public float TimeScale { get; set; } = 0.016f;

        public Vector3 CurrentAmbientColor { get; private set; }

        public DayTimeManager()
        {

        }

        public void Update(GameTime gameTime, Season currentSeason, SeasonPhase currentPhase)
        {
            // 1. Advance Time
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            TimeOfDay += dt * TimeScale;

            // Wrap around at midnight
            if (TimeOfDay >= 24f) TimeOfDay -= 24f;
            if (TimeOfDay < 0f) TimeOfDay += 24f;

            // 2. Calculate Color based on Season
            List<TimeKeyframe> activeGradient = GetSeasonalGradient(currentSeason, currentPhase);
            CurrentAmbientColor = EvaluateGradient(TimeOfDay, activeGradient);
        }

        // Method for manual testing via keyboard
        public void AddHours(float hours)
        {
            TimeOfDay += hours;
            if (TimeOfDay >= 24f) TimeOfDay -= 24f;
            if (TimeOfDay < 0f) TimeOfDay += 24f;
        }

        private List<TimeKeyframe> GetSeasonalGradient(Season season, SeasonPhase phase)
        {
            // Define colors: (R, G, B) from 0.0 to 1.0
            Vector3 night = new Vector3(0.15f, 0.2f, 0.4f);  // Dark blue
            Vector3 deepNight = new Vector3(0.08f, 0.1f, 0.25f); // Pitch black/blue
            Vector3 sunrise = new Vector3(1.0f, 0.6f, 0.4f);   // Orange/Pink
            Vector3 noon = new Vector3(1.0f, 1.0f, 1.0f);   // Pure white
            Vector3 sunset = new Vector3(1.0f, 0.4f, 0.2f);   // Deep Red/Orange

            // We adjust the HOURS of sunrise/sunset based on the season!
            float sunriseHour = 6f;
            float sunsetHour = 18f;

            if (season == Season.Summer || season == Season.Monsoon)
            {
                sunriseHour = 5f;   // Early sunrise
                sunsetHour = 20.5f; // Late sunset
                noon = new Vector3(1.0f, 1.0f, 0.9f); // Slightly warmer/yellow noon
            }
            else if (season == Season.Winter || season == Season.Fall)
            {
                sunriseHour = 7.5f; // Late sunrise
                sunsetHour = 16.5f; // Early sunset
                noon = new Vector3(0.9f, 0.95f, 1.0f); // Slightly colder/blue noon
            }

            return new List<TimeKeyframe>
            {
                new TimeKeyframe(0.0f, deepNight),
                new TimeKeyframe(sunriseHour - 1f, night),
                new TimeKeyframe(sunriseHour, sunrise),
                new TimeKeyframe(sunriseHour + 1.5f, noon),
                new TimeKeyframe(12.0f, noon),
                new TimeKeyframe(sunsetHour - 1.5f, noon),
                new TimeKeyframe(sunsetHour, sunset),
                new TimeKeyframe(sunsetHour + 1f, night),
                new TimeKeyframe(24.0f, deepNight) // Loop back
            };
        }

        private Vector3 EvaluateGradient(float time, List<TimeKeyframe> gradient)
        {
            // Ensure sorted
            gradient = gradient.OrderBy(k => k.Hour).ToList();

            // Find the keyframes we are between
            TimeKeyframe before = gradient.LastOrDefault(k => k.Hour <= time) ?? gradient.Last();
            TimeKeyframe after = gradient.FirstOrDefault(k => k.Hour > time) ?? gradient.First();

            if (before == after) return before.Color;

            // Handle midnight wrap-around math
            float timeRange = after.Hour - before.Hour;
            float currentTimePassed = time - before.Hour;

            if (timeRange < 0) // e.g., wrapping from 23:00 to 01:00
            {
                timeRange += 24f;
                if (currentTimePassed < 0) currentTimePassed += 24f;
            }

            float lerpFactor = currentTimePassed / timeRange;
            return Vector3.Lerp(before.Color, after.Color, lerpFactor);
        }

        public string GetDebugInfo()
        {
            int hours = (int)TimeOfDay;
            int minutes = (int)((TimeOfDay - hours) * 60);
            return $"Time: {hours:D2}:{minutes:D2} | Ambient: {CurrentAmbientColor.X:F2}, {CurrentAmbientColor.Y:F2}, {CurrentAmbientColor.Z:F2}";
        }
    }
}
