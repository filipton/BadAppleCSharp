using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BadApple
{
	class Program
	{
		public static string[] Chars = new string[] { "@", "#", "S", "%", "?", "*", "+", ";", ":", ",", " " };

		public static string videoPath = @"BadApple.mp4";
		public static int width = 80;
		public static int height = 60;
		public static int framesCount = 6571;
		public static int threads = 6;
		public static int frameRate = 30;

		static void Main(string[] args)
		{
			//CALCULATE SOME VALUES
			float stepCharsSize = 255 / (Chars.Length - 1);
			double frameRateInterval = 1000 / (double)frameRate;

			string[] videoFrames = new string[framesCount];

			//SET DEFAULT WINDOW COLOLR AND SIZE
			Console.BackgroundColor = ConsoleColor.White;
			Console.ForegroundColor = ConsoleColor.Black;
			Console.SetWindowSize((width * 2 ) + 1, height + 2);

			//START CONVERSION
			Stopwatch conversionTime = new Stopwatch();
			conversionTime.Start();

			int allFramesC = 0;
			for(int t = 0; t < threads; t++)
			{
				int siz = framesCount / threads;
				int from = t * siz;
				int to = (t + 1) * siz;
				int tid = t;

				new Thread(() =>
				{
					var ffMpeg = new NReco.VideoConverter.FFMpegConverter();

					while (allFramesC != framesCount)
					{
						int currIndex = allFramesC++;

						float time = (float)currIndex / frameRate;

						MemoryStream ms = new MemoryStream();
						ffMpeg.GetVideoThumbnail(videoPath, ms, time);
						Bitmap bm = ResizeBitmap((Bitmap)Image.FromStream(ms), width, height);

						string frame = string.Empty;

						for (int y = 0; y < height; y++)
						{
							for (int x = 0; x < width; x++)
							{
								Color color = bm.GetPixel(x, y);
								int r = color.R;
								int g = color.G;
								int b = color.B;
								int avg = (r + g + b) / 3;

								int CIndex = (int)Math.Floor(avg / stepCharsSize);

								frame += $"{Chars[CIndex]} ";
							}
							frame += Environment.NewLine;
						}

						videoFrames[currIndex] = frame;
					}
				}).Start();
			}

			//START PLAYING 
			Stopwatch playbackTimer = new Stopwatch();
			playbackTimer.Start();

			var file = new AudioFileReader(videoPath);
			var player = new WaveOutEvent();
			player.Init(file);
			player.Play();

			int CurrentPlayingFrame = 0;

			while (CurrentPlayingFrame != framesCount) 
			{
				if (playbackTimer.ElapsedMilliseconds % (int)frameRateInterval == 0)
				{
					int frameIndex = (int)Math.Floor(playbackTimer.ElapsedMilliseconds / frameRateInterval);
					if (frameIndex >= framesCount)
					{
						break;
					}

					FastConsole.WriteLine(videoFrames[frameIndex]);
					FastConsole.Flush();

					CurrentPlayingFrame = frameIndex;
				}

				int percentage = allFramesC * 100 / framesCount;
				int eta = (int)TimeSpan.FromMilliseconds(((conversionTime.ElapsedMilliseconds / (allFramesC == 0 ? 1 : allFramesC)) * (framesCount - allFramesC))).TotalSeconds;
				int speed = (int)(allFramesC / conversionTime.Elapsed.TotalSeconds);

				Console.Title = $"LIVE PLAY ({TimeSpan.FromSeconds((double)CurrentPlayingFrame / frameRate):mm':'ss'.'ff})... CONVERTING STATUS: {percentage}% [{GetProgressBar((float)allFramesC / framesCount, 25)}] (Frame {allFramesC} of {framesCount}, eta. {eta}s, {speed} fps)";
			}

			player.Stop();

			Thread.Sleep(-1);
		}

		public static Bitmap ResizeBitmap(Bitmap bmp, int width, int height)
		{
			Bitmap result = new Bitmap(width, height);
			using (Graphics g = Graphics.FromImage(result))
			{
				g.DrawImage(bmp, 0, 0, width, height);
			}

			return result;
		}

		public static string GetProgressBar(float progress, int barSize)
		{
			string output = string.Empty;
			float stepSize = 1 / (float)barSize;

			for(int b = 1; b <= barSize; b++)
			{
				if (progress >= stepSize * b)
				{
					output += "#";
				}
				else
				{
					output += "-";
				}
			}

			return output;
		}
	}

	//FROM https://stackoverflow.com/questions/5272177/console-writeline-slow
	public static class FastConsole
	{
		static readonly BufferedStream str;

		static FastConsole()
		{
			Console.OutputEncoding = Encoding.Unicode;  // crucial

			// avoid special "ShadowBuffer" for hard-coded size 0x14000 in 'BufferedStream' 
			str = new BufferedStream(Console.OpenStandardOutput(), 0x15000);
		}

		public static void WriteLine(String s) => Write(s + "\r\n");

		public static void Write(String s)
		{
			// avoid endless 'GetByteCount' dithering in 'Encoding.Unicode.GetBytes(s)'
			var rgb = new byte[s.Length << 1];
			Encoding.Unicode.GetBytes(s, 0, s.Length, rgb, 0);

			lock (str)   // (optional, can omit if appropriate)
				str.Write(rgb, 0, rgb.Length);
		}

		public static void Flush() { lock (str) str.Flush(); }
	};
}
