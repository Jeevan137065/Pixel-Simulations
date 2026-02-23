
// File: ShaderManager.cs
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

public class ShaderManager
{
    private readonly GraphicsDevice _gd;
    //private RenderTarget2D _sceneRenderTarget;
    private readonly Dictionary<string, Effect> _effects = new();
    private readonly SpriteFont _font;
    private Texture2D _quad;
    private bool _showUI = true;
    private string _currentKey = "Fog";
    private float _time;
    private int _selectedParam;
    private KeyboardState _previousKeyboardState;

    private readonly Dictionary<Keys, float> _keyHeldTime = new();
    private const float KEY_REPEAT_DELAY = 0.3f; // Seconds before repeat starts
    private const float KEY_REPEAT_INTERVAL = 0.03f; // Seconds between repeats once started
    // Fog fields
    public Vector2 FogCenter = new Vector2(0.5f, 0.5f);
    public float FogOverallDensity = 1.0f; // Controls overall visibility of fog
    public float FogNoiseScale = 1.0f; // Scale for the base noise texture
    public float FogScrollSpeedX = 0.05f; // Speed of fog movement
    public float FogScrollSpeedY = 0.03f;
    public float FogTurbulence = 0.5f; // How "swirly" the fog is
    public float FogRange = 0.5f; // How quickly fog fades with distance 
    public Color FogColor = Color.LightGray;

    // Rain fields
    public float RainCountPrimary = 10f;
    public float RainSlantPrimary = 0.2f;
    public float RainSpeedPrimary = 5f;
    public float RainCountSecondary = 5f;         // Added
    public float RainSlantSecondary = 0.1f;       // Added
    public float RainSpeedSecondary = 8f;       // Added
    public float RainBlurPrimary = 0.05f;         // Added
    public float RainBlurSecondary = 0.03f;       // Added
    public Vector2 RainSizePrimary = new Vector2(0.005f, 0.05f); // Added
    public Vector2 RainSizeSecondary = new Vector2(0.003f, 0.03f); // Added
    public Color RainEffectColor = Color.LightSkyBlue; // Added (was u_RainColor)
    public float RainAlpha = 0.7f;                // Added (was u_Alpha)
    // Snow fields
    public int SnowLayers = 1;
    public float SnowSpread = 0.5f;
    public float SnowTransparency = 0.5f;
    public float SnowSize = 0.1f; // Added
    public float SnowSpeed = 5.0f; // Added
    public float SnowWind = 0.2f;  // Added

    private class ParamDescriptor
    {
        public string Name;
        public Func<string> GetValue;
        public Action<float> Adjust;
        public float Step = 0.1f; // Default step for adjustment
        public float FineStep = 0.01f; // Smaller step for fine adjustment
    }
    private readonly List<ParamDescriptor> _params = new();

    public ShaderManager(ContentManager content, GraphicsDevice gd)
    {
        _gd = gd;
        //_sceneRenderTarget = new RenderTarget2D(gd, gd.PresentationParameters.BackBufferWidth, gd.PresentationParameters.BackBufferHeight, false, gd.PresentationParameters.BackBufferFormat, DepthFormat.Depth24);
        _quad = new Texture2D(gd, 1, 1);
        _quad.SetData(new[] { Color.White });
        _font = content.Load<SpriteFont>("UIFont");
        _effects["Fog"] = content.Load<Effect>("FogEffect");
        _effects["Rain"] = content.Load<Effect>("RainEffect");
        _effects["Snow"] = content.Load<Effect>("SnowEffect");
        BuildParamList();
    }

    public void Update(GameTime gt)
    {
        _time += (float)gt.ElapsedGameTime.TotalSeconds;
        var ks = Keyboard.GetState();
        if (ks.IsKeyDown(Keys.F2) && _previousKeyboardState.IsKeyUp(Keys.F2)) { _showUI = !_showUI; }
        if (ks.IsKeyDown(Keys.F3) && _previousKeyboardState.IsKeyUp(Keys.F3))
        {
            _currentKey = _currentKey == "Fog" ? "Rain"
                        : _currentKey == "Rain" ? "Snow"
                        : "Fog";
            BuildParamList();
        }
        if (ks.IsKeyDown(Keys.Up) && _previousKeyboardState.IsKeyUp(Keys.Up)) _selectedParam = (_selectedParam - 1 + _params.Count) % _params.Count;
        if (ks.IsKeyDown(Keys.Down) && _previousKeyboardState.IsKeyUp(Keys.Down)) _selectedParam = (_selectedParam + 1) % _params.Count;

        // Parameter adjustment with key repeat
        ProcessKeyRepeat(Keys.Left, ks, gt, -1f);
        ProcessKeyRepeat(Keys.Right, ks, gt, 1f);

        _previousKeyboardState = ks;
    }
    private void ProcessKeyRepeat(Keys key, KeyboardState currentKs, GameTime gt, float direction)
    {
        bool isKeyDown = currentKs.IsKeyDown(key);
        bool wasKeyDown = _previousKeyboardState.IsKeyDown(key);

        if (isKeyDown)
        {
            float elapsed = (float)gt.ElapsedGameTime.TotalSeconds;
            if (!_keyHeldTime.ContainsKey(key))
            {
                _keyHeldTime[key] = 0f;
            }

            if (!wasKeyDown) // Just pressed
            {
                AdjustCurrentParameter(direction);
                _keyHeldTime[key] = 0f; // Reset for repeat delay
            }
            else // Key is held
            {
                _keyHeldTime[key] += elapsed;
                // If past delay, and enough time for next interval
                if (_keyHeldTime[key] >= KEY_REPEAT_DELAY)
                {
                    float repeatTime = _keyHeldTime[key] - KEY_REPEAT_DELAY;
                    if (repeatTime >= KEY_REPEAT_INTERVAL)
                    {
                        int numRepeats = (int)(repeatTime / KEY_REPEAT_INTERVAL);
                        for (int i = 0; i < numRepeats; i++)
                        {
                            AdjustCurrentParameter(direction);
                        }
                        _keyHeldTime[key] -= numRepeats * KEY_REPEAT_INTERVAL; // Adjust time for next interval
                    }
                }
            }
        }
        else if (wasKeyDown) // Key released
        {
            _keyHeldTime.Remove(key); // Clear held time
        }
    }

    private void AdjustCurrentParameter(float direction)
    {
        if (_selectedParam >= 0 && _selectedParam < _params.Count)
        {
            var param = _params[_selectedParam];
            float delta = Keyboard.GetState().IsKeyDown(Keys.LeftShift) || Keyboard.GetState().IsKeyDown(Keys.RightShift)
                        ? param.FineStep
                        : param.Step;
            param.Adjust(direction * delta);
        }
    }


    private void BuildParamList()
    {
        _params.Clear();
        _selectedParam = 0;
        if (_currentKey == "Fog")
        {
            _params.Add(new ParamDescriptor { Name = "FogOverallDensity", GetValue = () => $"{FogOverallDensity:F2}", Adjust = v => FogOverallDensity = MathHelper.Clamp(FogOverallDensity + v, 0.0f, 2.0f), Step = 0.05f, FineStep = 0.01f });
            _params.Add(new ParamDescriptor { Name = "FogNoiseScale", GetValue = () => $"{FogNoiseScale:F2}", Adjust = v => FogNoiseScale = MathHelper.Clamp(FogNoiseScale + v, 0.1f, 10.0f), Step = 0.1f, FineStep = 0.01f });
            _params.Add(new ParamDescriptor { Name = "FogScrollSpeedX", GetValue = () => $"{FogScrollSpeedX:F2}", Adjust = v => FogScrollSpeedX = MathHelper.Clamp(FogScrollSpeedX + v, -0.5f, 0.5f), Step = 0.01f, FineStep = 0.001f });
            _params.Add(new ParamDescriptor { Name = "FogScrollSpeedY", GetValue = () => $"{FogScrollSpeedY:F2}", Adjust = v => FogScrollSpeedY = MathHelper.Clamp(FogScrollSpeedY + v, -0.5f, 0.5f), Step = 0.01f, FineStep = 0.001f });
            _params.Add(new ParamDescriptor { Name = "FogTurbulence", GetValue = () => $"{FogTurbulence:F2}", Adjust = v => FogTurbulence = MathHelper.Clamp(FogTurbulence + v, 0.0f, 5.0f), Step = 0.1f, FineStep = 0.01f });
            _params.Add(new ParamDescriptor { Name = "FogRange", GetValue = () => $"{FogRange:F2}", Adjust = v => FogRange = MathHelper.Clamp(FogRange + v, 0.0f, 2.0f), Step = 0.05f, FineStep = 0.005f }); // Distance falloff
            _params.Add(new ParamDescriptor { Name = "FogColor.R", GetValue = () => $"{FogColor.R}", Adjust = v => FogColor.R = (byte)MathHelper.Clamp(FogColor.R + v * 25, 0, 255), Step = 10, FineStep = 1 });
            _params.Add(new ParamDescriptor { Name = "FogColor.G", GetValue = () => $"{FogColor.G}", Adjust = v => FogColor.G = (byte)MathHelper.Clamp(FogColor.G + v * 25, 0, 255), Step = 10, FineStep = 1 });
            _params.Add(new ParamDescriptor { Name = "FogColor.B", GetValue = () => $"{FogColor.B}", Adjust = v => FogColor.B = (byte)MathHelper.Clamp(FogColor.B + v * 25, 0, 255), Step = 10, FineStep = 1 });
            _params.Add(new ParamDescriptor { Name = "FogCenterX", GetValue = () => $"{FogCenter.X:F2}", Adjust = v => FogCenter.X = MathHelper.Clamp(FogCenter.X + v * 0.05f, 0, 1), Step = 0.05f, FineStep = 0.005f });
            _params.Add(new ParamDescriptor { Name = "FogCenterY", GetValue = () => $"{FogCenter.Y:F2}", Adjust = v => FogCenter.Y = MathHelper.Clamp(FogCenter.Y + v * 0.05f, 0, 1), Step = 0.05f, FineStep = 0.005f });
        }
        else if (_currentKey == "Rain")
        {
            _params.Add(new ParamDescriptor { Name = "RainCountPrimary", GetValue = () => $"{RainCountPrimary:F0}", Adjust = v => RainCountPrimary = Math.Max(0, RainCountPrimary + v) });
            _params.Add(new ParamDescriptor { Name = "RainSlantPrimary", GetValue = () => $"{RainSlantPrimary:F2}", Adjust = v => RainSlantPrimary = MathHelper.Clamp(RainSlantPrimary + v, -1, 1) });
            _params.Add(new ParamDescriptor { Name = "RainSpeedPrimary", GetValue = () => $"{RainSpeedPrimary:F1}", Adjust = v => RainSpeedPrimary = Math.Max(0, RainSpeedPrimary + v) });

        }
        else if (_currentKey == "Snow")
        {
            _params.Add(new ParamDescriptor { Name = "SnowLayers", GetValue = () => $"{SnowLayers}", Adjust = v => SnowLayers = (int)MathHelper.Clamp(SnowLayers + (v > 0 ? 1 : -1), 1, 100) }); // Adjusted for int
            _params.Add(new ParamDescriptor { Name = "SnowSpread", GetValue = () => $"{SnowSpread:F2}", Adjust = v => SnowSpread = MathHelper.Clamp(SnowSpread + v, 0, 1) });
            _params.Add(new ParamDescriptor { Name = "SnowTransparency", GetValue = () => $"{SnowTransparency:F2}", Adjust = v => SnowTransparency = MathHelper.Clamp(SnowTransparency + v, 0, 1) });
            _params.Add(new ParamDescriptor { Name = "SnowSize", GetValue = () => $"{SnowSize:F2}", Adjust = v => SnowSize = MathHelper.Clamp(SnowSize + v * 0.1f, 0.01f, 2f) }); // Added
            _params.Add(new ParamDescriptor { Name = "SnowSpeed", GetValue = () => $"{SnowSpeed:F1}", Adjust = v => SnowSpeed = MathHelper.Clamp(SnowSpeed + v, 0, 50f) }); // Added
            _params.Add(new ParamDescriptor { Name = "SnowWind", GetValue = () => $"{SnowWind:F2}", Adjust = v => SnowWind = MathHelper.Clamp(SnowWind + v, -2f, 2f) }); // Added
        }
    }

    public void ApplyParams()
    {
        var e = _effects[_currentKey];
        e.Parameters["u_Resolution"]?.SetValue(new Vector2(_gd.Viewport.Width, _gd.Viewport.Height));
        e.Parameters["u_Time"]?.SetValue(_time);
        //if (_quad != null) e.Parameters["u_SceneTex"]?.SetValue(_quad);
        if (_currentKey == "Fog")
        {
            e.Parameters["u_FogCenter"]?.SetValue(FogCenter);
            e.Parameters["u_FogOverallDensity"]?.SetValue(FogOverallDensity);
            e.Parameters["u_FogNoiseScale"]?.SetValue(FogNoiseScale);
            e.Parameters["u_FogScrollSpeed"]?.SetValue(new Vector2(FogScrollSpeedX, FogScrollSpeedY));
            e.Parameters["u_FogTurbulence"]?.SetValue(FogTurbulence);
            e.Parameters["u_FogRange"]?.SetValue(FogRange);
            e.Parameters["u_FogColor"]?.SetValue(FogColor.ToVector3());

        }
        else if (_currentKey == "Rain")
        {
            e.Parameters["u_CountPrimary"]?.SetValue(RainCountPrimary);
            e.Parameters["u_SlantPrimary"]?.SetValue(RainSlantPrimary);
            e.Parameters["u_SpeedPrimary"]?.SetValue(RainSpeedPrimary);
            e.Parameters["u_CountSecondary"]?.SetValue(RainCountSecondary);     // Added
            e.Parameters["u_SlantSecondary"]?.SetValue(RainSlantSecondary);   // Added
            e.Parameters["u_SpeedSecondary"]?.SetValue(RainSpeedSecondary);   // Added
            e.Parameters["u_BlurPrimary"]?.SetValue(RainBlurPrimary);         // Added
            e.Parameters["u_BlurSecondary"]?.SetValue(RainBlurSecondary);       // Added
            e.Parameters["u_SizePrimary"]?.SetValue(RainSizePrimary);         // Added
            e.Parameters["u_SizeSecondary"]?.SetValue(RainSizeSecondary);       // Added
            e.Parameters["u_RainColor"]?.SetValue(RainEffectColor.ToVector3());// Added
            e.Parameters["u_Alpha"]?.SetValue(RainAlpha);                     // Added
        }
        else if (_currentKey == "Snow")
        {
            e.Parameters["u_NumLayers"]?.SetValue(SnowLayers);
            e.Parameters["u_Spread"]?.SetValue(SnowSpread);
            e.Parameters["u_SnowTransparency"]?.SetValue(SnowTransparency);
            e.Parameters["u_Size"]?.SetValue(SnowSize);     // Added
            e.Parameters["u_Speed"]?.SetValue(SnowSpeed);   // Added
            e.Parameters["u_Wind"]?.SetValue(SnowWind);     // Added
        }
    }

    public void DrawUI(SpriteBatch sb)
    {
        if (!_showUI) return;
        sb.Begin();
        sb.DrawString(_font, $"Shader: {_currentKey} (F3 to switch)", new Vector2(10, 10), Color.Yellow);
        sb.DrawString(_font, $"Controls: Up/Down to select, Left/Right to adjust (Shift for fine)", new Vector2(10, 30), Color.LightGray);
        int y = 60;
        foreach (var pd in _params)
        {
            Color col = (_params.IndexOf(pd) == _selectedParam) ? Color.Cyan : Color.White;
            sb.DrawString(_font, $"{pd.Name}: {pd.GetValue()}", new Vector2(10, y), col);
            y += 20;
        }
        sb.End();
    }

    public Effect CurrentEffect => _effects[_currentKey];
    public void DrawEffectLayer(SpriteBatch sb)
    {
        // bind parameters
        ApplyParams();
        // draw full-screen quad with shader
        sb.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            samplerState: SamplerState.LinearClamp,
            depthStencilState: null,
            rasterizerState: RasterizerState.CullNone,
            effect: CurrentEffect);

        sb.Draw(
            _quad,
            new Rectangle(0, 0, _gd.Viewport.Width, _gd.Viewport.Height),
            Color.White);

        sb.End();
    }
}
