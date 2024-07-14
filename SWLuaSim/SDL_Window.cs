using System;
using System.Timers;
using System.Drawing;
using System.Collections.Generic;

using SDL2;
using System.Diagnostics;
using System.Threading;

namespace SWLuaSim {
    internal struct Point2d {
        public double X;
        public double Y;

        public Point2d(double x, double y) {
            X = x;
            Y = y;
        }

        public override string ToString() {
            return $"({X}, {Y})";
        }
    }

    public class SDL_Window {
        public Size Resolution;

        public Dictionary<SDL.SDL_Keycode, bool> PressedKeys;

        public IntPtr Window;
        public IntPtr Renderer;
        public IntPtr Texture;

        public event EventHandler OnUpdate;
        public event EventHandler OnClose;

        private Thread _onUpdateThread;
        private bool _onUpdateThreadRunning;

        private uint _color;
        private uint[] _screenData;
        private uint[] _blankScreen;

        private bool _freeze;

        private uint Color(byte r, byte g, byte b, byte a) {
            return (uint)(r + (g << 8) + (b << 16) + (a << 24));
        }

        private byte ColorR(uint color) {
            return (byte)(color & 0xFF);
        }

        private byte ColorG(uint color) {
            return (byte)((color >> 8) & 0xFF);
        }

        private byte ColorB(uint color) {
            return (byte)((color >> 16) & 0xFF);
        }

        private byte ColorA(uint color) {
            return (byte)((color >> 24) & 0xFF);
        }

        public SDL_Window(Size resolution) {
            Resolution = resolution;

            PressedKeys = new Dictionary<SDL.SDL_Keycode, bool>();
            SDL.SDL_Keycode[] keycodes = (SDL.SDL_Keycode[])Enum.GetValues(typeof(SDL.SDL_Keycode));
            foreach(SDL.SDL_Keycode keycode in keycodes) {
                PressedKeys.Add(keycode, false);
            }

            _screenData = new uint[Resolution.Width * Resolution.Height];
            _blankScreen = new uint[Resolution.Width * Resolution.Height];
        }

        public void Freeze() {
            _freeze = true;
        }

        public void Run() {
            SDL.SDL_Init(SDL.SDL_INIT_VIDEO);

            Window = SDL.SDL_CreateWindow("Stormworks Lua Simulator (ESC to close)", SDL.SDL_WINDOWPOS_UNDEFINED, SDL.SDL_WINDOWPOS_UNDEFINED, 800, 480, SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE | SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN | SDL.SDL_WindowFlags.SDL_WINDOW_ALLOW_HIGHDPI);

            Renderer = SDL.SDL_CreateRenderer(Window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_SOFTWARE);

            Texture = SDL.SDL_CreateTexture(Renderer, SDL.SDL_PIXELFORMAT_ARGB8888, (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_TARGET, Resolution.Width, Resolution.Height);

            SDL.SDL_RaiseWindow(Window);

            bool run = true;

            Stopwatch ticks = Stopwatch.StartNew();

            while (run) {
                SDL.SDL_PollEvent(out SDL.SDL_Event e);

                switch(e.type) {
                    case SDL.SDL_EventType.SDL_QUIT: {
                            OnClose?.Invoke(this, EventArgs.Empty);
                            run = false;
                            break;
                        }
                    case SDL.SDL_EventType.SDL_KEYDOWN: {
                            SDL.SDL_Keycode keyCode = e.key.keysym.sym;

                            PressedKeys[keyCode] = true;

                            if (keyCode == SDL.SDL_Keycode.SDLK_ESCAPE) {
                                OnClose?.Invoke(this, EventArgs.Empty);
                                run = false;
                            }
                            break;
                        }
                    case SDL.SDL_EventType.SDL_KEYUP: {
                            SDL.SDL_Keycode keyCode = e.key.keysym.sym;

                            PressedKeys[keyCode] = false;
                            break;
                        }
                }

                if(ticks.Elapsed.TotalMilliseconds > 16.666 && !_freeze && !_onUpdateThreadRunning) {
                    ticks.Restart();
                    _onUpdateThreadRunning = true;

                    _onUpdateThread = new Thread(Update);
                    _onUpdateThread.Start();
                }

                Thread.Sleep(1);
            }

            SDL.SDL_DestroyTexture(Texture);
            SDL.SDL_DestroyRenderer(Renderer);
            SDL.SDL_DestroyWindow(Window);
            SDL.SDL_Quit();
        }

        private void Update() {
            OnUpdate?.Invoke(this, EventArgs.Empty);

            SDL.SDL_SetRenderTarget(Renderer, Texture);
            SDL.SDL_SetRenderDrawColor(Renderer, 0, 0, 0, 255);
            SDL.SDL_RenderClear(Renderer);

            uint lastColor = 0;

            for (int yy = 0; yy < Resolution.Height; yy++) {
                for (int xx = 0; xx < Resolution.Width; xx++) {
                    uint color = _screenData[yy * Resolution.Width + xx];

                    if (color != lastColor) {
                        SDL.SDL_SetRenderDrawColor(Renderer, ColorR(color), ColorG(color), ColorB(color), 255);
                    }

                    SDL.SDL_RenderDrawPoint(Renderer, xx, yy);

                    lastColor = color;
                }
            }

            SDL.SDL_GetWindowSize(Window, out int windowW, out int windowH);

            float windowWidth = windowW;
            float windowHeight = windowH;
            float outputWidth = Resolution.Width;
            float outputHeight = Resolution.Height;

            float windowAspect = windowWidth / windowHeight;
            float outputAspect = outputWidth / outputHeight;

            float x = 0, y = 0, width = windowHeight * outputAspect, height = width / outputAspect;

            if (windowAspect > outputAspect) {
                width = windowHeight * outputAspect;
                height = width / outputAspect;
                x = (windowWidth - width) / 2;
            } else if (windowAspect < outputAspect) {
                height = windowWidth / outputAspect;
                width = height * outputAspect;
                y = (windowHeight - height) / 2;
            }

            SDL.SDL_Rect rect;
            rect.x = (int)x;
            rect.y = (int)y;
            rect.w = (int)width;
            rect.h = (int)height;

            SDL.SDL_SetRenderTarget(Renderer, IntPtr.Zero);
            SDL.SDL_SetRenderDrawColor(Renderer, 20, 20, 25, 255);
            SDL.SDL_RenderClear(Renderer);
            SDL.SDL_RenderCopy(Renderer, Texture, IntPtr.Zero, ref rect);
            SDL.SDL_RenderPresent(Renderer);

            Array.Copy(_blankScreen, _screenData, _blankScreen.Length);

            _onUpdateThreadRunning = false;
        }

        public void SetTitle(string title) {
            SDL.SDL_SetWindowTitle(Window, title);
        }

        public void Pixel(int x, int y) {
            if (x < 0 || y < 0 || x >= Resolution.Width || y >= Resolution.Height) return;

            int index = y * Resolution.Width + x;

            if (ColorA(_color) == 255) {
                _screenData[index] = _color;
            } else {
                uint color = _screenData[index];
                double alpha = ColorA(_color) / 255d;

                byte r = (byte)((alpha * ColorR(_color)) + (1 - alpha) * ColorR(color));
                byte g = (byte)((alpha * ColorG(_color)) + (1 - alpha) * ColorG(color));
                byte b = (byte)((alpha * ColorB(_color)) + (1 - alpha) * ColorB(color));

                _screenData[index] = Color(r, g, b, 255);
            }
        }

        public void Pixel(double x, double y) {
            Pixel((int)x, (int)y);
        }

        public void SetColor(byte r, byte g, byte b, byte a) {
            _color = Color(r, g, b, a);
        }

        public void DrawLine(double x, double y, double x2, double y2, bool correctForSW = true) {
            if(correctForSW) {
                if (x == x2 && y == y2) return;
                //helps emulate weirdness of stormworks line draws.
                x2 -= 0.5;
                y2 -= 0.5;
            }

            double w = x2 - x;
            double h = y2 - y;
            double dx1 = 0, dy1 = 0, dx2 = 0, dy2 = 0;
            if (w < 0) dx1 = -1; else if (w > 0) dx1 = 1;
            if (h < 0) dy1 = -1; else if (h > 0) dy1 = 1;
            if (w < 0) dx2 = -1; else if (w > 0) dx2 = 1;
            double longest = Math.Abs(w);
            double shortest = Math.Abs(h);
            if (!(longest > shortest)) {
                longest = Math.Abs(h);
                shortest = Math.Abs(w);
                if (h < 0) dy2 = -1; else if (h > 0) dy2 = 1;
                dx2 = 0;
            }
            double numerator = (int)longest >> 1;
            for (int i = 0; i <= longest; i++) {
                Pixel(x, y);
                numerator += shortest;
                if (!(numerator < longest)) {
                    numerator -= longest;
                    x += dx1;
                    y += dy1;
                } else {
                    x += dx2;
                    y += dy2;
                }
            }
        }

        public void DrawCircle(double centerX, double centerY, double radius) {
            double x = radius - 1;
            double y = 0;
            double dx = 1;
            double dy = 1;
            double err = dx - (radius * 2);

            while (x >= y) {
                Pixel(centerX + x, centerY + y);
                Pixel(centerX + y, centerY + x);
                Pixel(centerX - y, centerY + x);
                Pixel(centerX - x, centerY + y);
                Pixel(centerX - x, centerY - y);
                Pixel(centerX - y, centerY - x);
                Pixel(centerX + y, centerY - x);
                Pixel(centerX + x, centerY - y);

                if (err <= 0) {
                    y++;
                    err += dy;
                    dy += 2;
                }
                if (err > 0) {
                    x--;
                    dx += 2;
                    err += dx - (radius * 2);
                }
            }
        }

        public void DrawCircleF(double centerX, double centerY, double radius) {
            double x = radius - 1;
            double y = 0;
            double dx = 1;
            double dy = 1;
            double err = dx - (radius * 2);

            while (x >= y) {
                Horizontal(centerX - x, centerX + x, centerY + y);
                Horizontal(centerX - y, centerX + y, centerY + x);
                Horizontal(centerX - x, centerX + x, centerY - y);
                Horizontal(centerX - y, centerX + y, centerY - x);

                if (err <= 0) {
                    y++;
                    err += dy;
                    dy += 2;
                }
                if (err > 0) {
                    x--;
                    dx += 2;
                    err += dx - (radius * 2);
                }
            }

            void Horizontal(double startX, double endX, double y1) {
                for (double x1 = startX; x1 <= endX; x1++) {
                    Pixel(x1, y1);
                }
            }
        }

        public void DrawRect(double x, double y, double w, double h) {
            for (int i = 1; i <= w - 1; i++) {
                Pixel(x + i, y);
                Pixel(x + i, y + h);
            }

            for (int i = 0; i <= h; i++) {
                Pixel(x, y + i);
                Pixel(x + w, y + i);
            }
        }

        public void DrawRectF(double x, double y, double w, double h) {
            for (int i = 0; i < w; i++) {
                for (int j = 0; j < h; j++) {
                    Pixel(x + i, y + j);
                }
            }
        }

        public void DrawTriangle(double x1, double y1, double x2, double y2, double x3, double y3) {
            DrawLine(x1, y1, x2, y2, false);
            DrawLine(x3, y3, x2, y2, false);
            DrawLine(x1, y1, x3, y3, false);
        }

        public void DrawTriangleF(double x1, double y1, double x2, double y2, double x3, double y3) {
            Point2d p1 = new Point2d(x1, y1);
            Point2d p2 = new Point2d(x2, y2);
            Point2d p3 = new Point2d(x3, y3);

            if (y2 < y1) { Swap(ref p2, ref p1); }
            if (y3 < y1) { Swap(ref p3, ref p1); }
            if (y3 < y2) { Swap(ref p3, ref p2); }

            List<double> x01 = Interpolate(p1.Y, p1.X, p2.Y, p2.X);
            List<double> x12 = Interpolate(p2.Y, p2.X, p3.Y, p3.X);
            List<double> x02 = Interpolate(p1.Y, p1.X, p3.Y, p3.X);

            if (x01.Count > 0) {
                x01.RemoveAt(x01.Count - 1);
            }

            List<double> x012 = new List<double>(x01);
            x012.AddRange(x12);

            int m = (int)Math.Floor((double)x02.Count / 2);
            List<double> x_left, x_right;
            if (x02[m] < x012[m]) {
                x_left = x02;
                x_right = x012;
            } else {
                x_left = x012;
                x_right = x02;
            }

            for (int y = (int)p1.Y; y <= (int)p3.Y; y++) {
                for (int x = (int)x_left[y - (int)p1.Y]; x <= (int)x_right[y - (int)p1.Y]; x++) {
                    Pixel(x, y);
                }
            }

            void Swap(ref Point2d a, ref Point2d b) {
                Point2d temp = a;
                a = b;
                b = temp;
            }
        }

        private List<double> Interpolate(double y0, double x0, double y1, double x1) {
            List<double> values = new List<double>();
            if (y0 == y1) {
                values.Add(x0);
                return values;
            }
            double a = (x1 - x0) / (y1 - y0);
            double d = x0;
            for (double i = y0; i <= y1; i++) {
                values.Add(d);
                d += a;
            }
            return values;
        }
    }
}