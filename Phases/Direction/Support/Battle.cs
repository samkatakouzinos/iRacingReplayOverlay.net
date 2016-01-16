﻿// This file is part of iRacingReplayOverlay.
//
// Copyright 2014 Dean Netherton
// https://github.com/vipoo/iRacingReplayOverlay.net
//
// iRacingReplayOverlay is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// iRacingReplayOverlay is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with iRacingReplayOverlay.  If not, see <http://www.gnu.org/licenses/>.

using iRacingSDK;
using iRacingSDK.Support;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace iRacingReplayOverlay.Phases.Direction.Support
{
    public static class Battle
    {
        static readonly Random random = new Random();
        static long[] preferredCarIdxs = null;

        public struct GapMetric
        {
            public int CarIdx;
            public double Time;
            public int Position;
            public int LeaderCarIdx;
            public double Factor;

            public override string ToString()
            {
                return "CarIdx: {0}, Time: {1}, Position: {2}, LeaderCarIdx: {3}".F(CarIdx, Time, Position, LeaderCarIdx);
            }
        }
        
        public static Car Find(DataSample data, TimeSpan battleGap, double factor, IEnumerable<string> preferredDrivers)
        {
            preferredCarIdxs = GetPreferredCarIdxs(data, preferredDrivers);

            var allBattles = All(data, battleGap, factor);

            allBattles = ApplyFactors(data, factor, allBattles);

            return SelectABattle(data, allBattles, random.Next(10100) / 100.0);
        }

        public static GapMetric[] FindAll(DataSample data, TimeSpan battleGap, double factor, IEnumerable<string> preferredDrivers)
        {
            preferredCarIdxs = GetPreferredCarIdxs(data, preferredDrivers);

            return All(data, battleGap, factor);
        }

        internal static long[] GetPreferredCarIdxs(DataSample data, IEnumerable<string> preferredDrivers)
        {
            preferredDrivers = preferredDrivers.Select(d => d.ToLower()).ToList();

            return data.SessionData.DriverInfo.CompetingDrivers.Where(x => preferredDrivers.Contains(x.UserName.ToLower())).Select(x => x.CarIdx).ToArray();
        }

        internal static GapMetric[] All(DataSample data, TimeSpan battleGap, double factor)
        {
            var distances = data.Telemetry.CarIdxDistance
                    .Select((d, i) => new { CarIdx = i, Distance = d })
                    .Skip(1)
                    .Where(d => data.Telemetry.CarIdxTrackSurface[d.CarIdx] == TrackLocation.OnTrack)
                    .Where(d => d.Distance > 0)
                    .OrderByDescending(d => d.Distance)
                    .ToList();

            if (distances.Count == 0)
                return new GapMetric[0];

            var gap = Enumerable.Range(1, distances.Count - 1)
                    .Select(i => new
                    {
                        LeaderCarIdx = distances[i - 1].CarIdx,
                        CarIdx = distances[i].CarIdx,
                        Distance = distances[i - 1].Distance - distances[i].Distance,
                        Position = i + 1
                    });

            var timeGap = gap.Select(g => new GapMetric
            {
                CarIdx = g.CarIdx,
                LeaderCarIdx = g.LeaderCarIdx,
                Time = g.Distance * data.Telemetry.Session.ResultsAverageLapTime,
                Position = g.Position
            });

            var r = timeGap
                .Where(d => d.Time < battleGap.TotalSeconds);

            if (Settings.Default.FocusOnPreferedDriver)
                r = r.Where(d => IsAPerferredDriver(d))
                     .OrderBy(d => d.Position);
            else
                r = r
                .OrderBy(d => IsAPerferredDriver(d) ? 1 : 2)
                .ThenBy(d => d.Position);

            return r.ToArray();
        }

        private static GapMetric[] ApplyFactors(DataSample data, double factor, GapMetric[] r)
        {
            double[] floor = { 0.0 };
            var factors = Enumerable.Range(1, r.Count()).Select(index => Math.Pow(factor, index)).ToArray();
            var totalFactors = factors.Sum();
            var ratio = 100.0 / totalFactors;
            var factorsTo100 = factors.Select(f => f * ratio).Reverse().Concat(floor).ToArray();

            //TraceInfo.WriteLine("total: {0}, ratio: {1}", totalFactors, ratio);

            //for(var i = 0; i < factors.Length; i++)
            //    TraceInfo.WriteLine("Factor: {0}, ratio: {1}", factors[i], factorsTo100[i]);

            var currentFactor = 0.0;
            r = r.Select((d, index) =>
            {
                currentFactor += factorsTo100[index];
                var gapMetric = new GapMetric
                {
                    CarIdx = d.CarIdx,
                    Factor = currentFactor,
                    LeaderCarIdx = d.LeaderCarIdx,
                    Position = d.Position,
                    Time = d.Time
                };
                return gapMetric;
            }).ToArray();

            TraceInfo.WriteLine("Battles:");
            foreach (var d in r)
                TraceInfo.WriteLine("{0}: {1} -> {2}, Time: {3}, Pos: {4}",
                    d.Factor,
                    data.Telemetry.Cars[d.LeaderCarIdx].Details.Driver.UserName,
                    data.Telemetry.Cars[d.CarIdx].Details.Driver.UserName,
                    d.Time, d.Position);

            return r;
        }

        static bool IsAPerferredDriver(GapMetric d)
        {
            return preferredCarIdxs.Contains(d.CarIdx) || preferredCarIdxs.Contains(d.LeaderCarIdx);
        }

        internal static Car SelectABattle(DataSample data, IEnumerable<GapMetric> all, double dice)
        {
            if (all.Count() == 0)
                return null;

            foreach (var battle in all)
            {
                if (dice < battle.Factor)
                {
                    var driver = data.Telemetry.Cars[battle.CarIdx];
                    var leaderDriver = data.Telemetry.Cars[battle.LeaderCarIdx];
                    TraceInfo.WriteLine("{0} Found battle follower: {1}, leader: {2}, by chance {3}", data.Telemetry.SessionTimeSpan, driver.Details.UserName, leaderDriver.Details.UserName, dice);
                    return driver;
                }
            }

            Trace.WriteLine("WARNING!  did not find battle by chance!", "DEBUG");
            return null;
        }

        internal static bool IsInBattle(DataSample data, TimeSpan battleGap, CarDetails follower, CarDetails leader)
        {
            var leaderCar = leader.Car(data);
            var followerCar = follower.Car(data);

            if (Settings.Default.FocusOnPreferedDriver)
            {
                if (!(preferredCarIdxs.Contains(leaderCar.CarIdx) || preferredCarIdxs.Contains(followerCar.CarIdx)))
                {
                    Trace.WriteLine("Current race battle does not include drivers within preferred list");
                    return false;
                }
            }

            if (leaderCar.Position == followerCar.Position + 1)
                return false;

            var timeGap = (leaderCar.TotalDistance - followerCar.TotalDistance) * data.Telemetry.Session.ResultsAverageLapTime;
            return timeGap < battleGap.TotalSeconds;
        }
    }
}