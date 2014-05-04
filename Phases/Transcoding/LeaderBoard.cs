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

using iRacingReplayOverlay.Drawing;
using iRacingReplayOverlay.Phases.Capturing;
using iRacingReplayOverlay.Support;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace iRacingReplayOverlay.Phases.Transcoding
{
    class LeaderBoard
    {
        public OverlayData OverlayData;

        internal void Overlay(Graphics graphics, long timestamp)
        {
            var timeInSeconds = timestamp.FromNanoToSeconds();
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var sample = OverlayData.TimingSamples.LastOrDefault(s => s.StartTime <= timeInSeconds);

            if (sample != null)
            {
                DrawLeaderboard(graphics, sample);
                DrawCurrentDriverRow(graphics, sample.CurrentDriver);
            }

            var messageState = OverlayData.MessageStates.LastOrDefault(s => s.Time <= timeInSeconds);
            if (messageState != null)
                DrawRaceMessages(graphics, timeInSeconds, messageState);

            var fastestLap = OverlayData.FastestLaps.LastOrDefault(s => s.StartTime <= timeInSeconds && s.StartTime + 15 > timeInSeconds);
            if (fastestLap != null)
                DrawFastestLap(graphics, fastestLap);
        }

        private void DrawRaceMessages(Graphics g, double timeInSeconds, OverlayData.MessageState messageState)
        {
            Func<GraphicRect, GraphicRect> blueBox = rr =>
               rr.WithBrush(Styles.Brushes.TransparentLightBlue)
               .WithPen(Styles.Pens.Black)
               .DrawRectangleWithBorder()
               .WithBrush(Styles.Brushes.Black)
               .WithFont("Calibri", 20, FontStyle.Bold)
               .WithStringFormat(StringAlignment.Near);

            var shiftFactor = (double)Math.Min(timeInSeconds - messageState.Time, 1d);
            var offset = (int)(34 * shiftFactor);

            offset = offset + (messageState.Messages.Length - 1) * 34;

            var row4Top = 900 + 34 * 3;
            offset = row4Top - offset;

            var r = g.InRectangle(80, offset, 450, 34);

            g.SetClip(new Rectangle(80, 900, 450, 34+34+34));

            foreach (var msg in messageState.Messages)
                r = r.With(blueBox)
                    .DrawText(" " + msg)
                    .ToBelow();

            g.ResetClip();
        }

        private void DrawFastestLap(Graphics g, Capturing.OverlayData.FastLap fastestLap)
        {
            Func<GraphicRect, GraphicRect> blueBox = rr =>
               rr.WithBrush(Styles.Brushes.TransparentLightBlue)
               .WithPen(Styles.Pens.Black)
               .DrawRectangleWithBorder()
               .WithBrush(Styles.Brushes.Black)
               .WithFont("Calibri", 20, FontStyle.Bold)
               .WithStringFormat(StringAlignment.Center);

            g.InRectangle(1920 - 80 - 450, 900, 450, 34)
                .With(blueBox)
                .DrawText("New Fast Lap")
                .ToBelow(width: 50)
                .With(blueBox)
                .DrawText(fastestLap.Driver.CarNumber)
                .ToRight(width: 250)
                .With(blueBox)
                .DrawText(fastestLap.Driver.Name)
                .ToRight(width: 150)
                .With(blueBox)
                .DrawText(TimeSpan.FromSeconds(fastestLap.Time).ToString(@"mm\:ss\.fff"));
        }


        static Func<GraphicRect, GraphicRect> SimpleWhiteBox = rr =>
               rr.WithLinearGradientBrush(Styles.WhiteSmoke, Styles.White, LinearGradientMode.BackwardDiagonal)
               .WithPen(Styles.Pens.Black)
               .DrawRectangleWithBorder()
               .WithBrush(Styles.Brushes.Black)
               .WithFont("Calibri", 24, FontStyle.Bold)
               .WithStringFormat(StringAlignment.Center);

        static public Func<GraphicRect, GraphicRect> ColourBox(Color color)
        {
            return rr =>
                rr.WithBrush(new SolidBrush(color))
                    .WithPen(Styles.Pens.Black)
                    .DrawRectangleWithBorder()
                    .WithFont("Calibri", 24, FontStyle.Bold)
                    .WithBrush(Styles.Brushes.Black)
                    .WithStringFormat(StringAlignment.Center);
        }

		private void DrawLeaderboard(Graphics g, OverlayData.TimingSample sample)
        {
            var r = g.InRectangle(80, 80, 210, 40)
                .With(SimpleWhiteBox)
                .DrawText(sample.RacePosition);

			if(sample.LapCounter != null)
				r = r.ToBelow()
					.With(SimpleWhiteBox)
					.DrawText(sample.LapCounter);

            foreach( var d in sample.Drivers.Take(20))
            { 
                r = r.ToBelow(width: 40);

                r.With(ColourBox(Styles.LightYellow))
                    .DrawText(d.Position)
                    .ToRight(width: 50)
                    .With(SimpleWhiteBox)
                    .DrawText(d.CarNumber)
                    .ToRight(width:120)
                    .With(SimpleWhiteBox)
                    .WithStringFormat(StringAlignment.Near)
                    .DrawText(d.ShortName, 10);
            }
        }

		private void DrawCurrentDriverRow(Graphics g, OverlayData.Driver p)
        {
            g.InRectangle(1920/2-420/2, 980, 70, 40)
                .WithBrush(Styles.Brushes.Yellow)
                .WithPen(Styles.Pens.Black)
                .DrawRectangleWithBorder()
                .WithFont("Calibri", 24, FontStyle.Bold)
                .WithBrush(Styles.Brushes.Black)
                .WithStringFormat(StringAlignment.Near)
                .Center(cg => cg
                            .DrawText(p.Position.ToString())
                            .AfterText(p.Position.ToString())
                            .MoveRight(3)
                            .WithFont("Calibri", 18, FontStyle.Bold)
                            .DrawText(p.Indicator)
                )

                .ToRight(50)
                .WithLinearGradientBrush(Styles.White, Styles.WhiteSmoke, LinearGradientMode.BackwardDiagonal)
                .DrawRectangleWithBorder()
                .WithStringFormat(StringAlignment.Center)
                .WithBrush(Styles.Brushes.Black)
                .DrawText(p.CarNumber)

                .ToRight(300)
                .WithLinearGradientBrush(Styles.White, Styles.WhiteSmoke, LinearGradientMode.BackwardDiagonal)
                .DrawRectangleWithBorder()
                .WithStringFormat(StringAlignment.Center)
                .WithBrush(Styles.Brushes.Black)
                .DrawText(p.Name);
        }

        public static class Styles
        {
            public const int AlphaLevel = 120;
            public readonly static Color White = Color.FromArgb(AlphaLevel, Color.White);
            public readonly static Color WhiteSmoke = Color.FromArgb(AlphaLevel, Color.WhiteSmoke);
            public readonly static Color Black = Color.FromArgb(AlphaLevel, Color.Black);
            public readonly static Color LightYellow = Color.FromArgb(AlphaLevel, Color.LightYellow);
            public readonly static Color Yellow = Color.FromArgb(AlphaLevel, 150, 150, 0);

            public static class Pens
            {
                public readonly static Pen Black = new Pen(Styles.Black);
            }

            public static class Fonts
            {
                public readonly static Font LeaderBoard = new Font("Calibri", 24, FontStyle.Bold);
            }

            public static class Brushes
            {
                public readonly static Brush Black = new SolidBrush(Color.Black);
				public readonly static Brush Yellow = new SolidBrush(Color.Yellow);
                public readonly static Brush TransparentLightBlue = new SolidBrush(Color.FromArgb(AlphaLevel, Color.LightBlue));
            }
        }
    }
}
