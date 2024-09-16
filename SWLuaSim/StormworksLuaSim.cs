using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;

using NLua;
using SDL2;

namespace SWLuaSim {
    public class StormworksLuaSim {
        enum FunctionRunning {
            Body,
            OnTick,
            OnDraw,
        }

        private SDL_Window _window;
        private Size _size;
        private byte r, g, b, a;
        private Stopwatch _updateStopwatch;

        private Action<string> _print;
        private Action<string> _println;

        private Lua _lua;
        private LuaFunction _onTick;
        private LuaFunction _onDraw;
        private LuaFunction _sws_Error;
        private LuaFunction _printStats;
        private FunctionRunning _funcRunning;
        private bool _hasOnTickOrOnDraw;
        private bool _swsExit;

        private Dictionary<string, string> _propertyTexts;
        private Dictionary<string, double> _propertyNumbers;
        private Dictionary<string, bool> _propertyBools;

        private double[] InputNumbers;
        private bool[] InputBools;

        public StormworksLuaSim(Action<string> print, Action<string> println, string program, bool hasOnTickOrOnDraw, Dictionary<string, string> propertyTexts, Dictionary<string, double> propertyNumbers, Dictionary<string, bool> propertyBools, int width, int height) {
            _print = print;
            _println = println;
            _hasOnTickOrOnDraw = hasOnTickOrOnDraw;

            _size = new Size(width, height);

            _updateStopwatch = new Stopwatch();

            _lua = new Lua();

            _propertyTexts = propertyTexts;
            _propertyNumbers = propertyNumbers;
            _propertyBools = propertyBools;

            InputNumbers = new double[32];
            InputBools = new bool[32];

            RegisterLuaFunctions();

            _funcRunning = FunctionRunning.Body;
            _lua.DoString(program);

            if (_lua["onTick"] != null && _lua["onTick"].GetType() == typeof(LuaFunction)) {
                _onTick = _lua.GetFunction("onTick");
            }

            if (_lua["onDraw"] != null && _lua["onDraw"].GetType() == typeof(LuaFunction)) {
                _onDraw = _lua.GetFunction("onDraw");
            }

            if (_lua["sws_Error"] != null && _lua["sws_Error"].GetType() == typeof(LuaFunction)) {
                _sws_Error = _lua.GetFunction("sws_Error");
            }

            if (_lua["PrintStats"] != null && _lua["PrintStats"].GetType() == typeof(LuaFunction)) {
                _printStats = _lua.GetFunction("PrintStats");
            }

            _window = new SDL_Window(_size, _hasOnTickOrOnDraw);
            _window.OnUpdate += Update;
            _window.OnClose += OnWindowClose;
            _window.Run();
        }

        private void RegisterLuaFunctions() {
            _lua.RegisterFunction("PRINT", this, typeof(StormworksLuaSim).GetMethod("PRINT", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("PRINTLN", this, typeof(StormworksLuaSim).GetMethod("PRINTLN", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("EXIT", this, typeof(StormworksLuaSim).GetMethod("EXIT", BindingFlags.NonPublic | BindingFlags.Instance));

            //input
            _lua.NewTable("input");
            _lua.RegisterFunction("input.getNumber", this, typeof(StormworksLuaSim).GetMethod("getNumber", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("input.getBool", this, typeof(StormworksLuaSim).GetMethod("getBool", BindingFlags.NonPublic | BindingFlags.Instance));

            //output
            _lua.NewTable("output");
            _lua.RegisterFunction("output.setNumber", this, typeof(StormworksLuaSim).GetMethod("setNumber", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("output.setBool", this, typeof(StormworksLuaSim).GetMethod("setBool", BindingFlags.NonPublic | BindingFlags.Instance));

            //property
            _lua.NewTable("property");
            _lua.RegisterFunction("property.getText", this, typeof(StormworksLuaSim).GetMethod("propertyGetText", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("property.getNumber", this, typeof(StormworksLuaSim).GetMethod("propertyGetNumber", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("property.getBool", this, typeof(StormworksLuaSim).GetMethod("propertyGetBool", BindingFlags.NonPublic | BindingFlags.Instance));

            //screen
            _lua.NewTable("screen");
            _lua.RegisterFunction("screen.setColor", this, typeof(StormworksLuaSim).GetMethod("setColor", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("screen.drawClear", this, typeof(StormworksLuaSim).GetMethod("drawClear", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("screen.drawLine", this, typeof(StormworksLuaSim).GetMethod("drawLine", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("screen.drawCircle", this, typeof(StormworksLuaSim).GetMethod("drawCircle", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("screen.drawCircleF", this, typeof(StormworksLuaSim).GetMethod("drawCircleF", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("screen.drawRect", this, typeof(StormworksLuaSim).GetMethod("drawRect", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("screen.drawRectF", this, typeof(StormworksLuaSim).GetMethod("drawRectF", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("screen.drawTriangle", this, typeof(StormworksLuaSim).GetMethod("drawTriangle", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("screen.drawTriangleF", this, typeof(StormworksLuaSim).GetMethod("drawTriangleF", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("screen.drawText", this, typeof(StormworksLuaSim).GetMethod("drawText", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("screen.drawTextBox", this, typeof(StormworksLuaSim).GetMethod("drawTextBox", BindingFlags.NonPublic | BindingFlags.Instance));

            _lua.RegisterFunction("screen.drawMap", this, typeof(StormworksLuaSim).GetMethod("drawMap", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("screen.setMapColorOcean", this, typeof(StormworksLuaSim).GetMethod("setMapColorOcean", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("screen.setMapColorShallows", this, typeof(StormworksLuaSim).GetMethod("setMapColorShallows", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("screen.setMapColorLand", this, typeof(StormworksLuaSim).GetMethod("setMapColorLand", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("screen.setMapColorGrass", this, typeof(StormworksLuaSim).GetMethod("setMapColorGrass", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("screen.setMapColorSand", this, typeof(StormworksLuaSim).GetMethod("setMapColorSand", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("screen.setMapColorSnow", this, typeof(StormworksLuaSim).GetMethod("setMapColorSnow", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("screen.setMapColorRock", this, typeof(StormworksLuaSim).GetMethod("setMapColorRock", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("screen.setMapColorGravel", this, typeof(StormworksLuaSim).GetMethod("setMapColorGravel", BindingFlags.NonPublic | BindingFlags.Instance));

            _lua.RegisterFunction("screen.getWidth", this, typeof(StormworksLuaSim).GetMethod("getWidth", BindingFlags.NonPublic | BindingFlags.Instance));
            _lua.RegisterFunction("screen.getHeight", this, typeof(StormworksLuaSim).GetMethod("getHeight", BindingFlags.NonPublic | BindingFlags.Instance));
        }


        private void Update(object o, EventArgs e) {
            if (_swsExit) return;

            double KeyPair(SDL.SDL_Keycode a, SDL.SDL_Keycode b) {
                bool aPressed = _window.PressedKeys[a];
                bool bPressed = _window.PressedKeys[b];

                if (aPressed && bPressed) {
                    return 0;
                } else if (aPressed) {
                    return -1;
                } else if (bPressed) {
                    return 1;
                }

                return 0;
            }

            InputNumbers[0] = KeyPair(SDL.SDL_Keycode.SDLK_a, SDL.SDL_Keycode.SDLK_d);
            InputNumbers[1] = KeyPair(SDL.SDL_Keycode.SDLK_s, SDL.SDL_Keycode.SDLK_w);
            InputNumbers[2] = KeyPair(SDL.SDL_Keycode.SDLK_LEFT, SDL.SDL_Keycode.SDLK_RIGHT);
            InputNumbers[3] = KeyPair(SDL.SDL_Keycode.SDLK_DOWN, SDL.SDL_Keycode.SDLK_UP);

            InputBools[0] = _window.PressedKeys[SDL.SDL_Keycode.SDLK_1];
            InputBools[1] = _window.PressedKeys[SDL.SDL_Keycode.SDLK_2];
            InputBools[2] = _window.PressedKeys[SDL.SDL_Keycode.SDLK_3];
            InputBools[3] = _window.PressedKeys[SDL.SDL_Keycode.SDLK_4];
            InputBools[4] = _window.PressedKeys[SDL.SDL_Keycode.SDLK_5];
            InputBools[5] = _window.PressedKeys[SDL.SDL_Keycode.SDLK_6];

            InputBools[30] = _window.PressedKeys[SDL.SDL_Keycode.SDLK_SPACE];
            InputBools[31] = true;

            double onTickTime = 0, onDrawTime = 0;

            _updateStopwatch.Restart();

            _funcRunning = FunctionRunning.OnTick;
            _onTick?.Call();
            onTickTime = _updateStopwatch.Elapsed.TotalMilliseconds;
            
            _updateStopwatch.Restart();

            _funcRunning = FunctionRunning.OnDraw;
            _onDraw?.Call();
            onDrawTime = _updateStopwatch.Elapsed.TotalMilliseconds;

            _window.SetTitle($"Stormworks Lua Simulator (ESC to close) onTick: {onTickTime} ms, onDraw: {onDrawTime} ms");
        }

        private void OnWindowClose(object sender, EventArgs e) {
            if (!_swsExit) {
                while(_lua.IsExecuting) {

                }
                _printStats?.Call();
            }
        }

        private void PRINT(params object[] parameters) {
            string output = "";

            if(parameters.Length > 0) {
                output = parameters[0].ToString();
            }

            _print(output);
        }

        private void PRINTLN(params object[] parameters) {
            string output = "";

            if (parameters.Length > 0) {
                output = parameters[0].ToString();
            }

            _println(output);
        }

        private void EXIT(params object[] parameters) {
            _swsExit = true;
            _window.Freeze();

            //no onTick / onDraw, don't keep window open
            if (!_hasOnTickOrOnDraw) {
                _window.Close();
            }
        }

        private void CheckIfOnTick(string funcName) {
            if (_funcRunning != FunctionRunning.OnTick) {
                _sws_Error?.Call($"Attempted to call '{funcName}' outside onTick. *(A function called by onTick can use these functions)");
            }
        }

        private void CheckIfOnDraw(string funcName) {
            if (_funcRunning != FunctionRunning.OnDraw) {
                _sws_Error?.Call($"Attempted to call '{funcName}' outside onDraw. *(A function called by onDraw can use these functions)");
            }
        }

        #region input
        private object getNumber(params object[] parameters) {
            CheckIfOnTick("input.getNumber");
            
            if (TryParseDoubles(parameters, 1, out double[] output)) {
                int channel = (int)output[0] - 1;
                if (channel >= 0 && channel <= 31) {
                    return InputNumbers[channel];
                }
            }
            
            return (double)0;
        }

        private object getBool(params object[] parameters) {
            CheckIfOnTick("input.getBool");

            if (TryParseDoubles(parameters, 1, out double[] output)) {
                int channel = (int)output[0] - 1;
                if (channel >= 0 && channel <= 31) {
                    return InputBools[channel];
                }
            }

            return false;
        }
        #endregion
        #region output
        //output doesn't do anything
        private void setNumber(params object[] parameters) {
            CheckIfOnTick("output.setNumber");
        }

        private void setBool(params object[] parameters) {
            CheckIfOnTick("output.setBool");
        }
        #endregion
        #region property
        private object propertyGetText(params object[] parameters) {
            if (TryParseStringSingle(parameters, out string name) && _propertyTexts.TryGetValue(name, out string value)) {
                return value;
            }

            return null;
        }

        private object propertyGetNumber(params object[] parameters) {
            if(TryParseStringSingle(parameters, out string name) && _propertyNumbers.TryGetValue(name, out double value)) {
                return value;
            }

            return null;
        }

        private object propertyGetBool(params object[] parameters) {
            if (TryParseStringSingle(parameters, out string name) && _propertyBools.TryGetValue(name, out bool value)) {
                return value;
            }

            return null;
        }
        #endregion
        #region screen
        private void setColor(params object[] parameters) {
            CheckIfOnDraw("screen.setColor");

            if (TryParseDoubles(parameters, 3, out double[] rgb)) {
                r = (byte)rgb[0];
                g = (byte)rgb[1];
                b = (byte)rgb[2];
                a = 255;
            }

            if(TryParseDoubles(parameters, 4, out double[] rgba)) {
                r = (byte)rgba[0];
                g = (byte)rgba[1];
                b = (byte)rgba[2];
                a = (byte)rgba[3];
            }

            _window.SetColor(r, g, b, a);
        }

        private void drawClear(params object[] _) {
            CheckIfOnDraw("screen.drawClear");

            for (int x = 0; x < _size.Width; x++) {
                for (int y = 0; y < _size.Height; y++) {
                    _window.Pixel(x, y);
                }
            }
        }

        private void drawLine(params object[] parameters) {
            CheckIfOnDraw("screen.drawLine");

            if(TryParseDoubles(parameters, 4, out double[] data)) {
                _window.DrawLine(data[0], data[1], data[2], data[3]);
            }
        }

        private void drawCircle(params object[] parameters) {
            CheckIfOnDraw("screen.drawCircle");

            if (TryParseDoubles(parameters, 3, out double[] data)) {
                _window.DrawCircle(data[0], data[1], data[2]);
            }
        }

        private void drawCircleF(params object[] parameters) {
            CheckIfOnDraw("screen.drawCircleF");

            if (TryParseDoubles(parameters, 3, out double[] data)) {
                _window.DrawCircleF(data[0], data[1], data[2]);
            }
        }

        private void drawRect(params object[] parameters) {
            CheckIfOnDraw("screen.drawRect");

            if (TryParseDoubles(parameters, 4, out double[] data)) {
                _window.DrawRect(data[0], data[1], data[2], data[3]);
            }
        }

        private void drawRectF(params object[] parameters) {
            CheckIfOnDraw("screen.drawRectF");

            if (TryParseDoubles(parameters, 4, out double[] data)) {
                _window.DrawRectF(data[0], data[1], data[2], data[3]);
            }
        }

        private void drawTriangle(params object[] parameters) {
            CheckIfOnDraw("screen.drawTriangle");

            if (TryParseDoubles(parameters, 6, out double[] data)) {
                _window.DrawTriangle(data[0], data[1], data[2], data[3], data[4], data[5]);
            }
        }

        private void drawTriangleF(params object[] parameters) {
            CheckIfOnDraw("screen.drawTriangleF");

            if (TryParseDoubles(parameters, 6, out double[] data)) {
                _window.DrawTriangleF(data[0], data[1], data[2], data[3], data[4], data[5]);
            }
        }

        private void drawText(params object[] parameters) {
            CheckIfOnDraw("screen.drawText");

            if (TryParseToTypes(parameters, out object[] output, LuaType.Number, LuaType.Number, LuaType.String)) {
                int x = (int)(double)output[0];
                int y = (int)(double)output[1];
                string text = (string)output[2];

                int[] font = {
                    0b00000000000000000000,
                    0b00000000001011100000,
                    0b00000000110000000011,
                    0b01010111110101011111,
                    0b01001101111110110010,
                    0b10010101000010101001,
                    0b10000010101010101010,
                    0b00000000000001100000,
                    0b00000100010111000000,
                    0b00000011101000100000,
                    0b10101011100111010101,
                    0b00000001000111000100,
                    0b00000000000100010000,
                    0b00000001000010000100,
                    0b00000000001000000000,
                    0b00000000110010011000,
                    0b01110100111010101110,
                    0b00000111110001000000,
                    0b10010101011100110010,
                    0b01010101011010110001,
                    0b11111001000010000111,
                    0b01001101011010110111,
                    0b01000101011010101110,
                    0b00011111010000100001,
                    0b01010101011010101010,
                    0b01110101011010100010,
                    0b00000000000101000000,
                    0b00000000001101000000,
                    0b00000100010101000100,
                    0b00000010100101001010,
                    0b00000001000101010001,
                    0b00000000101010100001,
                    0b10111101011000101110,
                    0b11110001010010111110,
                    0b01010101011010111111,
                    0b01010100011000101110,
                    0b01110100011000111111,
                    0b10001101011010111111,
                    0b00001001010010111111,
                    0b01100101011000101110,
                    0b11111001000010011111,
                    0b00000000001111100000,
                    0b01111100001000001000,
                    0b10001010100010011111,
                    0b10000100001000011111,
                    0b11111000100001011111,
                    0b11111001000001011111,
                    0b01110100011000101110,
                    0b00010001010010111111,
                    0b11110110011000101110,
                    0b10010011010010111111,
                    0b01001101011010110010,
                    0b00000000011111100001,
                    0b01111100001000001111,
                    0b00000011111000001111,
                    0b11111010000100011111,
                    0b11011001000010011011,
                    0b00000000111110000011,
                    0b10011101011010111001,
                    0b00000100011111100000,
                    0b00000110000010000011,
                    0b00000111111000100000,
                    0b00000000100000100010,
                    0b10000100001000010000,
                    0b00000000100000100000,
                    0b11110001010010111110,
                    0b01010101011010111111,
                    0b01010100011000101110,
                    0b01110100011000111111,
                    0b10001101011010111111,
                    0b00001001010010111111,
                    0b01100101011000101110,
                    0b11111001000010011111,
                    0b00000000001111100000,
                    0b01111100001000001000,
                    0b10001010100010011111,
                    0b10000100001000011111,
                    0b11111000100001011111,
                    0b11111001000001011111,
                    0b01110100011000101110,
                    0b00010001010010111111,
                    0b11110110011000101110,
                    0b10010011010010111111,
                    0b01001101011010110010,
                    0b00000000011111100001,
                    0b01111100001000001111,
                    0b00000011111000001111,
                    0b11111010000100011111,
                    0b11011001000010011011,
                    0b00000000111110000011,
                    0b10011101011010111001,
                    0b00000100011111100100,
                    0b00000000001101100000,
                    0b00000001001111110001,
                    0b00010001000001000100,
                };

                for (int i = 0; i < text.Length; i++) {
                    int bitmap = font[text[i] - 32];
                    for(int j=0;j<20;j++) {
                        if ((bitmap & (1 << j)) > 0) {
                            _window.Pixel(x + j / 5 + i * 5, y + j % 5);
                        }
                    }
                }
            }
        }

        private void drawTextBox(params object[] parameters) {
            CheckIfOnDraw("screen.drawTextBox");
        }

        private void drawMap(params object[] parameters) {
            CheckIfOnDraw("screen.drawMap");
        }

        private void setMapColorOcean(params object[] parameters) {
            CheckIfOnDraw("screen.setMapColorOcean");
        }

        private void setMapColorShallows(params object[] parameters) {
            CheckIfOnDraw("screen.setMapColorShallows");
        }

        private void setMapColorLand(params object[] parameters) {
            CheckIfOnDraw("screen.setMapColorLand");
        }

        private void setMapColorGrass(params object[] parameters) {
            CheckIfOnDraw("screen.setMapColorGrass");
        }

        private void setMapColorSand(params object[] parameters) {
            CheckIfOnDraw("screen.setMapColorSand");
        }

        private void setMapColorSnow(params object[] parameters) {
            CheckIfOnDraw("screen.setMapColorSnow");
        }

        private void setMapColorRock(params object[] parameters) {
            CheckIfOnDraw("screen.setMapColorRock");
        }

        private void setMapColorGravel(params object[] parameters) {
            CheckIfOnDraw("screen.setMapColorGravel");
        }

        private long getWidth(params object[] parameters) {
            CheckIfOnDraw("screen.getWidth");
            return _size.Width;
        }

        private long getHeight(params object[] parameters) {
            CheckIfOnDraw("screen.getHeight");
            return _size.Height;
        }
        #endregion

        private bool TryParseStringSingle(object[] parameters, out string value) {
            value = string.Empty;
            
            if (parameters.Length < 1) {
                return false;
            }

            value = parameters[0].ToString();

            return true;
        }

        private bool TryParseDoubles(object[] parameters, int count, out double[] output) {
            output = new double[count];

            if (parameters.Length != count) {
                return false;
            }

            for (int i = 0; i < parameters.Length; i++) {
                if (parameters[i].GetType() == typeof(double)) {
                    output[i] = (double)parameters[i];
                } else if (parameters[i].GetType() == typeof(long)) {
                    output[i] = (double)(long)parameters[i];
                } else {
                    return false;
                }
            }

            return true;
        }

        enum LuaType {
            Number,
            String,
        }

        private bool TryParseToTypes(object[] parameters, out object[] output, params LuaType[] types) {
            output = new object[types.Length];

            if (parameters.Length != types.Length) {
                return false;
            }

            for (int i = 0; i < parameters.Length; i++) {
                if (types[i] == LuaType.Number) {
                    if (parameters[i].GetType() == typeof(double)) {
                        output[i] = (double)parameters[i];
                    } else if (parameters[i].GetType() == typeof(long)) {
                        output[i] = (double)(long)parameters[i];
                    } else {
                        return false;
                    }
                } else if (types[i] == LuaType.String) {
                    output[i] = parameters[i].ToString();
                }
            }

            return true;
        }
    }
}
