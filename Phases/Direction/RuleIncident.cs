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

using iRacingReplayOverlay.Phases.Analysis;
using iRacingReplayOverlay.Phases.Capturing;
using iRacingReplayOverlay.Support;
using iRacingSDK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace iRacingReplayOverlay.Phases.Direction
{
    public class RuleIncident
    {
        enum IncidentPosition { Started, Inside, Finished, Outside };

        readonly RemovalEdits removalEdits;
        readonly IEnumerator<Incidents.Incident> nextIncident;
        readonly TrackCamera TV2;

        double pitBoxStartTime = 0;
        bool isInside = false;

        public RuleIncident(TrackCamera[] cameras, RemovalEdits removalEdits, Incidents incidents)
        {
            this.removalEdits = removalEdits;

            nextIncident = incidents.GetEnumerator();
            nextIncident.MoveNext();
            if(nextIncident.Current != null)
                Trace.WriteLine("First incident at {0}".F(nextIncident.Current.StartSessionTimeSpan), "INFO");
            TV2 = cameras.First(tc => tc.CameraName == "TV2");
        }
        
        public bool Process(DataSample data)
        {
            var position = GetIncidentPosition(data);

            switch( position)
            {
                case IncidentPosition.Started:
                    removalEdits.InterestingThingHappend(data);
                    SwitchToIncident(data);
                    return true;

                case IncidentPosition.Inside:
                    removalEdits.InterestingThingHappend(data);
                    return true;

                case IncidentPosition.Finished:
                    WatchForNextIncident(data);
                    removalEdits.InterestingThingHappend(data);
                    return true;

                case IncidentPosition.Outside:
                    return false;
            }

            throw new Exception("Invalid Incident Position {0}".F(position));
        }

        IncidentPosition GetIncidentPosition(DataSample data)
        {
            if (nextIncident.Current == null)
                return IncidentPosition.Outside;

            if (!isInside)
                SkipMissedIncidents(data);

            if (nextIncident.Current == null)
                return IncidentPosition.Outside;

            var incident = nextIncident.Current;

            if (!isInside && incident.IsInside(data.Telemetry.SessionTime))
            {
                isInside = true;
                return IncidentPosition.Started;
            }

            if (isInside && (!incident.IsInside(data.Telemetry.SessionTime) || IsInPits(data)))
            {
                isInside = false;
                return IncidentPosition.Finished;
            }

            return isInside ?  IncidentPosition.Inside : IncidentPosition.Outside;
        }

        bool IsInPits(DataSample data)
        {
            if (data.Telemetry.CamCar.TrackSurface == TrackLocation.InPitStall && pitBoxStartTime == 0)
            {
                Trace.WriteLine("{0} Incident car is in pit stall".F(data.Telemetry.SessionTimeSpan));
                pitBoxStartTime = data.Telemetry.SessionTime;
            }

            if (data.Telemetry.CamCar.TrackSurface == TrackLocation.InPitStall && pitBoxStartTime + 2 < data.Telemetry.SessionTime)
            {
                Trace.WriteLine("{0} Finishing showing incident as car is in pit stall".F(data.Telemetry.SessionTime));
                return true;
            }

            return false;
        }

        void WatchForNextIncident(DataSample data)
        {
            Trace.WriteLine("{0} Finishing incident from {1}".F(data.Telemetry.SessionTimeSpan, nextIncident.Current.StartSessionTimeSpan), "INFO");

            nextIncident.MoveNext();

            if(nextIncident.Current != null)
                Trace.WriteLine("{0} (Move) Next incident at {1}".F(data.Telemetry.SessionTimeSpan, nextIncident.Current.StartSessionTimeSpan), "INFO");
        }

        void SkipMissedIncidents(DataSample data)
        {
            var hasSkipped = false;
            while (nextIncident.Current != null && (nextIncident.Current.StartSessionTime + 1.0) < data.Telemetry.SessionTime)
            {
                hasSkipped = true;
                Trace.WriteLine("{0} Skipping incident at time {1}".F(data.Telemetry.SessionTimeSpan, nextIncident.Current.StartSessionTimeSpan), "INFO");
                nextIncident.MoveNext();
            }

            if (nextIncident.Current != null && hasSkipped)
                Trace.WriteLine("{0} (Skip) Next incident at {1}".F(data.Telemetry.SessionTimeSpan, nextIncident.Current.StartSessionTimeSpan), "INFO");
        }

        void SwitchToIncident(DataSample data)
        {
            pitBoxStartTime = 0;

            var incidentCar = data.SessionData.DriverInfo.Drivers[nextIncident.Current.CarIdx];

            Trace.WriteLine("{0} Showing incident with {1} starting from {2}".F(data.Telemetry.SessionTimeSpan, incidentCar.UserName, nextIncident.Current.StartSessionTimeSpan), "INFO");

            iRacing.Replay.CameraOnDriver((short)incidentCar.CarNumber, TV2.CameraNumber);
        }
    }
}
