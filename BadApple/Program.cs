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
		static List<string> AllFrames = new List<string>();
		static string[] Chars = new string[] { "@", "#", "S", "%", "?", "*", "+", ";", ":", ",", " " };

		static void Main(string[] args)
		{
			string videoPath = @"D:\BadApple.mp4";

			float stepCharsSize = 255 / (Chars.Length - 1);
			int framesCount = 6570;
			int threads = 6;

			int frameRate = 30;
			double frameRateInterval = 1000 / (double)frameRate;

			Console.BackgroundColor = ConsoleColor.White;
			Console.ForegroundColor = ConsoleColor.Black;
			Console.SetWindowSize(Console.LargestWindowWidth, Console.LargestWindowHeight);

			Dictionary<int, string[]> tmps = new Dictionary<int, string[]>();
			Stopwatch conversionTime = new Stopwatch();
			conversionTime.Start();

			int allFramesC = 0;
			for(int t = 0; t < threads; t++)
			{
				int siz = framesCount / threads;
				int from = t * siz;
				int to = (t + 1) * siz;
				int tid = t;

				Task.Run(() =>
				{
					var ffMpeg = new NReco.VideoConverter.FFMpegConverter();
					string[] tmp = new string[siz];

					int index = 0;

					for (int i = from; i < to; i++)
					{
						float time = (float)i / frameRate;

						MemoryStream ms = new MemoryStream();
						ffMpeg.GetVideoThumbnail(videoPath, ms, time);
						Bitmap bm = ResizeBitmap((Bitmap)Image.FromStream(ms), 81, 61);

						string frame = string.Empty;

						for (int y = 0; y < 61; y++)
						{
							for (int x = 0; x < 81; x++)
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

						tmp[index] = frame;
						allFramesC++;
						index++;
					}

					tmps.Add(tid, tmp);
				});
			}

			while (allFramesC != framesCount) 
			{
				int percentage = allFramesC * 100 / framesCount;
				int eta = (int)TimeSpan.FromMilliseconds(((conversionTime.ElapsedMilliseconds / (allFramesC == 0 ? 1 : allFramesC)) * (framesCount - allFramesC))).TotalSeconds;
				int speed = (int)(allFramesC / conversionTime.Elapsed.TotalSeconds);

				Console.Title = $"CONVERTING IMAGES TO TEXT... [{GetProgressBar((float)allFramesC / framesCount, 25)}] ({percentage}%) (Frame {allFramesC} of {framesCount}, eta. {eta}s) ({speed} fps)";
			}

			for(int i = 0; i < tmps.Count; i++)
			{
				AllFrames.AddRange(tmps[i]);
			}

			Console.WriteLine("DONE, PRESS `ENTER` TO SHOW!");
			if(Console.ReadKey().Key == ConsoleKey.Enter)
			{
				Console.Clear();

				Stopwatch sw = new Stopwatch();
				sw.Start();

				var file = new AudioFileReader(videoPath);
				var player = new WaveOutEvent();
				player.Init(file);
				player.Play();

				bool start = true;
				while (start)
				{
					if (sw.ElapsedMilliseconds % (int)frameRateInterval == 0)
					{
						int frameIndex = (int)Math.Floor(sw.ElapsedMilliseconds / frameRateInterval);
						if (frameIndex >= framesCount)
						{
							start = false;
							break;
						}

						FastConsole.WriteLine(AllFrames[frameIndex]);
						FastConsole.Flush();
					}
				}

				Console.WriteLine(sw.Elapsed);

				Thread.Sleep(-1);
			}
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
