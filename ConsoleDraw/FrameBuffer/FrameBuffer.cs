﻿using ConsoleDraw.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleDraw.Data
{
    public class FrameBuffer : IDisposable
    {
        private int _fbWidth = 0;
        private int _fbHeight = 0;
        private FrameBufferPixel[] _previousFrameBuffer;
        private FrameBufferPixel[] _drawingFramebuffer;
        private Thread _drawThread;
        private Stopwatch _watch = new Stopwatch();
        private List<IDrawExtension> _drawExtensions = new List<IDrawExtension>();

        public int Width => _fbWidth;
        public int Height => _fbHeight;
        public int X => Console.CursorLeft;
        public int Y => Console.CursorTop;

        public int DrawTime { get; private set; }
        public int DrawFPS { get; private set; }
        public int DrawnFrames { get; private set; }
        public bool UseFrameLimiter { get; set; } = true;
        public FrameBufferPixel[] RawFrameBuffer { get; set; }

        public int FrameLimit
        {
            get
            {
                return (_frameLimitMS + 1) * 1000;
            }
            set
            {
                _frameLimitMS = 1000 / (value - 1);
            }
        }

        private int _frameLimitMS = 16;
        private static Color[] RGBDosColors =
        {
            Color.FromArgb(0,0,0),
            Color.FromArgb(0,0,0xa8),
            Color.FromArgb(0,0xa8,0),
            Color.FromArgb(0,0xa8,0xa8),
            Color.FromArgb(0xa8,0,0),
            Color.FromArgb(0xa8,0,0xa8),
            Color.FromArgb(0xa8,0xa8,0),
            Color.FromArgb(0xa8,0xa8,0xa8),
            Color.FromArgb(0x54,0x54,0x54),
            Color.FromArgb(0x54,0x54,0xff),
            Color.FromArgb(0x54,0xff,0x54),
            Color.FromArgb(0x54,0xff,0xff),
            Color.FromArgb(0xff,0x54,0x54),
            Color.FromArgb(0xff,0x54,0xff),
            Color.FromArgb(0xff,0xff,0x54),
            Color.FromArgb(255,255,255)
        };
        private static double ColorDistance(Color a, Color b) => Math.Sqrt(Math.Pow(a.R - b.R, 2) + Math.Pow(a.G - b.G, 2) + Math.Pow(a.B - b.B, 2));
        private static int NearestColorIndex(Color a, Color[] b)
        {
            int nearest = 0;
            for (int i = 0; i < b.Length; i++)
            {
                if(ColorDistance(a, b[i]) < ColorDistance(a, b[nearest]))
			        nearest = i;
            }
            return nearest;
        }
        private static byte[] ImageTo4Bit(Bitmap bmp)
        {
            int j = 0;
            byte[] buffer = new byte[bmp.Width * bmp.Height];
            for (int y = 0; bmp.Height > y; y++)
                for (int x = 0; bmp.Width > x; x++, j++)
                    buffer[j] = (byte)NearestColorIndex(bmp.GetPixel(x, y),RGBDosColors);
            return buffer;
        }
        public static FrameBuffer FromFile(string path)
        {
            try 
            { 
                Bitmap bmp = (Bitmap)Image.FromFile(path);
                FrameBuffer fb = new FrameBuffer();
                fb.Init(bmp.Width, bmp.Height);
                byte[] data = ImageTo4Bit(bmp);
                for (int i = 0; bmp.Width * bmp.Height > i; i++)
                    fb.RawFrameBuffer[i] = FrameBufferPixel.FromByte(data[i]);
                return fb;
            } catch { throw new Exception("Exception! Make sure your image is valid."); }
        }
        public void Init(int width, int height)
        {
            _fbWidth = width; _fbHeight = height;
            RawFrameBuffer = new FrameBufferPixel[_fbWidth * _fbHeight];
            for (int i = 0; i < RawFrameBuffer.Length; i++)
            {
                RawFrameBuffer[i] = new FrameBufferPixel() { BackgroundColour = ConsoleColor.Black, Character = ' ' };
            }
            _previousFrameBuffer = new FrameBufferPixel[_fbWidth * _fbHeight];
            _drawingFramebuffer = new FrameBufferPixel[_fbWidth * _fbHeight];
        }

        public void Run()
        {
            Console.CursorVisible = false;
            _drawThread = new Thread(() =>
            {
                while (true)
                {
                    Draw();
                }
            });
            _drawThread.Start();
        }

        public void Draw()
        {
            _watch.Start();

            foreach (IDrawExtension draw in _drawExtensions)
                draw.RunExtension();

            lock (RawFrameBuffer)
            {
                RawFrameBuffer.CopyTo(_drawingFramebuffer, 0);
                for (int i = 0; i < _fbHeight; i++)
                {
                    for (int j = 0; j < _fbWidth; j++)
                    {
                        int index = (j + (i * _fbWidth)).Clamp(0, _drawingFramebuffer.Length - 1);
                        if (_drawingFramebuffer[index] != _previousFrameBuffer[index])
                        {
                            try
                            {
                                Console.CursorLeft = j;
                                Console.CursorTop = i;
                                Console.BackgroundColor = _drawingFramebuffer[index].BackgroundColour;
                                Console.ForegroundColor = _drawingFramebuffer[index].ForegroundColour;
                                Console.Write(_drawingFramebuffer[index].Character);
                                _previousFrameBuffer[index] = _drawingFramebuffer[index];
                            }
                            catch { }
                        }
                    }

                }
            }
            Console.CursorLeft = 0;
            Console.CursorTop = 0;


            if (UseFrameLimiter)
            {
                if (_frameLimitMS - _watch.ElapsedMilliseconds > 0)
                    Thread.Sleep(_frameLimitMS - (int)_watch.ElapsedMilliseconds);
            }

            try
            {
                DrawTime = (int)_watch.ElapsedMilliseconds;
                DrawFPS += 1000 / (int)_watch.ElapsedMilliseconds;
                DrawFPS /= 2;
                DrawnFrames += 1;
            }
            catch { }


            _watch.Reset();
        }

        public void AddDrawExtension(IDrawExtension extension)
        {
            _drawExtensions.Add(extension);
        }

        public void Dispose()
        {
            if ((_drawThread?.IsAlive).GetValueOrDefault())
                _drawThread.Abort();
            Console.CursorVisible = true;
        }
    }
}
