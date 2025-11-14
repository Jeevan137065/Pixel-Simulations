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
}
