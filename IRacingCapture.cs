// This file is part of iRacingReplayOverlay.
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

using System;
using System.Threading;
using iRacingSDK;
using System.IO;
using System.Linq;

namespace iRacingReplayOverlay.net
{
	class IRacingCaptureWorker : IDisposable
	{
		bool captureOn = false;
		Thread worker = null;
		
        bool workerStopRequest = false;
        FileSystemWatcher fileWatcher;
        string latestCreatedVideoFile;

        public event Action<string> NewVideoFileFound;
        private SynchronizationContext uiContext;
        
		public bool Toogle(string workingFolder)
		{
			captureOn = !captureOn;

			if(captureOn)
				StartCapture(workingFolder);
			else
				StopCapture();

			return captureOn;
		}

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            latestCreatedVideoFile = e.FullPath;

            if (NewVideoFileFound != null)
                uiContext.Post(state => NewVideoFileFound(latestCreatedVideoFile), null);
        }

		public void Dispose()
		{
			var w = worker;
			if(w == null)
				return;

			workerStopRequest = true;
			if(!w.Join(500))
			{
				w.Abort();
				throw new Exception("Capture thread did not shutdown cleanly");
			}
		}

		void StartCapture(string workingFolder)
		{
			if(worker != null)
				throw new Exception("Capture thread already running");

            uiContext = SynchronizationContext.Current;

            latestCreatedVideoFile = null;
            fileWatcher = new FileSystemWatcher(workingFolder, "*.mp4");
            fileWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime;
            fileWatcher.Created += OnCreated;
            fileWatcher.EnableRaisingEvents = true;

			workerStopRequest = false;
			worker = new Thread(Loop);
			worker.Start();
		}

		void Loop()
		{
            var tempFileName = Path.GetTempFileName();
            Console.WriteLine("Creating temp file for game data {0}", tempFileName);
            try
			{
                using(var file = File.CreateText(tempFileName))
				{
                   	var startTime = DateTime.Now;
					file.WriteLine("StartTime, Drivers");

                    foreach (var data in iRacing.GetDataFeed())
                    {
                        if (!data.IsConnected)
                        {
                            Console.Clear();
                            Console.WriteLine("Unable to connect to iRacing server ...");
                            continue;
                        }

                        WriteNewLeaderBoardRow(file, startTime, data);

						for(int i = 0; i < 2000; i++)
						{
                            if (workerStopRequest)
                                return;
							Thread.Sleep(1);
						}
                    }
				}
			}
			catch(Exception e)
			{
				Console.WriteLine("Error in worker " + e.Message);
				throw e;
			}
			finally
			{
                if (latestCreatedVideoFile != null)
                    File.Move(tempFileName, Path.ChangeExtension(latestCreatedVideoFile, ".csv"));

                worker = null;
			}
		}

        private static void WriteNewLeaderBoardRow(StreamWriter file, DateTime startTime, DataSample data)
        {
            var timeNow = DateTime.Now - startTime;

            var numberOfDrivers = data.SessionInfo.DriverInfo.Drivers.Length;

            var positions = data.Telementary.Cars
                .Take(numberOfDrivers)
                .Where(c => c.Index != 0)
                .OrderByDescending(c => c.Lap + c.DistancePercentage)
                .ToArray();

            var drivers = String.Join("|", positions.Select(c => c.Driver.UserName).ToArray());

            file.WriteLine(timeNow.Seconds.ToString() + "," + drivers);
            Console.WriteLine(timeNow.Seconds.ToString() + "," + drivers);
        }

		void StopCapture()
		{
            try
            {
                if (worker == null)
                    throw new Exception("Capture thread not running");

                workerStopRequest = true;
                if (!worker.Join(500))
                {
                    worker.Abort();
                    throw new Exception("Capture thread did not shutdown cleanly");
                }
            }
            finally
            {
                worker = null;
                fileWatcher.Dispose();
                fileWatcher = null;
            }
		}
	}
}
