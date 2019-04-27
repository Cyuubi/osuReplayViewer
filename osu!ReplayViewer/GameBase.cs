using CyuUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NAudio.Wave;
using osuReplayViewer.SoundTouch;
using System;

namespace osuReplayViewer
{
    public class GameBase : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        private AudioFileReader _audioFile;
        private VarispeedProvider _audioSpeed;
        private WasapiOut _outputDevice;

        private ReplayReader _reader;
        private ReplayReader.ReplayFrame _currentFrame;
        private int _frameCount;
        private int _frameIndex = 0;

        private bool _replayEnded = false;

        private int _cursorMiddle;
        private Texture2D _cursorTexture;

        public GameBase()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            // always run at 60fps, if this is set to false then the framerate will be unlocked but the replay will go through frames too fast (ignore this!!! read below)
            // never mind now, i fixed it by adding " || _currentFrame.Time > audioTime" into UpdateReplayIndex
            IsFixedTimeStep = false;

            _graphics.PreferredBackBufferWidth = 1366;
            _graphics.PreferredBackBufferHeight = 768;

            _audioFile = new AudioFileReader("audio.mp3");
            _audioSpeed = new VarispeedProvider(_audioFile, 100, new SoundTouchProfile(false, true)); // change the first variable of SoundTouchProfile to true if you dont want it to sound like a chipmunk
            _outputDevice = new WasapiOut();
            _outputDevice.Init(_audioSpeed);

            _reader = new ReplayReader("replay.osr");
            _frameCount = _reader.ReplayFrames.Count;
        }

        protected override void Initialize()
        {
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _cursorTexture = Content.Load<Texture2D>("cursor");

            if (_cursorTexture.Height != _cursorTexture.Width)
            {
                throw new Exception("Cursor must have the same width as height!");
            }

            _cursorMiddle = _cursorTexture.Height / 2;
        }

        protected override void UnloadContent()
        {
            _outputDevice.Dispose();
            _audioSpeed.Dispose();
            _audioFile.Dispose();
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            if (_frameIndex < _frameCount)
            {
                if (_replayEnded) return;

                if (_frameIndex == 0)
                {
                    _outputDevice.Play();
                    _audioSpeed.PlaybackRate = 1.5f; // .50f for half time, 1f for normal speed and 1.5f for double time
                    _audioFile.CurrentTime = new TimeSpan(0, 0, 0, 0, _reader.ReplayFrames[1].Time); // set our audio position to the first replay frame time
                }

                int audioTime = (int)(_outputDevice.GetPosition() * 1500.0 / // 500.0 for half time, 1000.0 for normal speed and 1500.0 for double time
                    _outputDevice.OutputWaveFormat.BitsPerSample /
                    _outputDevice.OutputWaveFormat.Channels * 8 /
                    _outputDevice.OutputWaveFormat.SampleRate);

                _currentFrame = _reader.ReplayFrames[_frameIndex]; // update our current frame

                UpdateReplayIndex(audioTime); // update replay index with audio time to make things smoother
            }
            else
            {
                _replayEnded = true;
            }

            base.Update(gameTime);
        }

        private void UpdateReplayIndex(int audioTime)
        {
            // sync replay with audio if out of sync or increment our current frame index
            if (_currentFrame.Time < audioTime || _currentFrame.Time > audioTime)
            {
                if ((audioTime - _currentFrame.Time) >= 10)
                {
                    for (int i = _frameIndex; i < _frameCount; i++)
                    {
                        if ((_reader.ReplayFrames[i].Time - audioTime) <= 0) _frameIndex = i;
                    }
                }
            }
            else
                _frameIndex++; // not used much, but still keeping it in case (actually never mind, it is used when framerate is unlimited)
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.SlateGray);

            _spriteBatch.Begin();
            if (!_replayEnded) _spriteBatch.Draw(_cursorTexture, new Vector2(_currentFrame.X + ((_graphics.PreferredBackBufferWidth - 512) / 2) - _cursorMiddle, _currentFrame.Y + ((_graphics.PreferredBackBufferHeight - 384) / 2) - _cursorMiddle));
            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
