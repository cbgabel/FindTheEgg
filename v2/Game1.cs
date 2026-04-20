// ================================================
// UPDATED Game1.cs — Fixed layout + permanent right-side status panel
// ================================================
// Replace your entire Game1.cs with this code.
//
// What’s new:
// • Permanent status panel on the RIGHT of the game board (always visible)
//   → ROUND, SCORE, TIME, BOMBS (shows even when 0)
// • Play area shifted left so everything fits perfectly with the larger Impact font
// • Version number safely placed in the bottom-right corner (no longer cut off)
// • Minor spacing tweaks for the bigger arcade-style font
//
// Rebuild the solution (Build → Rebuild Solution) and run — the UI is now clean and arcade-perfect!

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FindTheEgg2
{
    public enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        HighScores,
        EnteringName,
        GameOver,
        Victory
    }

    public enum PowerUpType
    {
        Clock,
        Bomb,
        CrackedEgg
    }

    public struct PowerUp
    {
        public Point Position;
        public PowerUpType Type;
        public bool Collected;
    }

    public struct HighScoreEntry
    {
        public string Name { get; set; }
        public int Score { get; set; }
        public int Round { get; set; }
    }

    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private SpriteFont _font;

        private const int GridWidth = 40;
        private const int GridHeight = 40;
        private const int TileSize = 20;
        private const int PlayAreaWidth = GridWidth * TileSize;   // 800
        private const int PlayAreaHeight = GridHeight * TileSize; // 800
        private const int ScreenWidth = 1024;
        private const int ScreenHeight = 864;

        // New layout constants for right-side status panel
        private const int PlayOffsetX = 40;
        private const int PlayOffsetY = 64;
        private const int StatusX = PlayOffsetX + PlayAreaWidth + 40; // 880

        private GameState _state = GameState.MainMenu;
        private int _selectedMenuItem = 0;

        private int[,] _map;
        private Point _playerPos;
        private Point _eggPos;
        private List<PowerUp> _powerUps = new List<PowerUp>();
        private int _bombsHeld = 0;
        private int _score = 0;
        private int _currentRound = 0;
        private float _timeRemaining = 0f;
        
        private bool _bombExploding = false;
        private float _bombExplosionTimer = 0f;
        private const float ExplosionDuration = 0.35f;   // quick & snappy 80s feel
        private Point _explosionCenter;

        private Random _rnd = new Random();

        private List<HighScoreEntry> _highScores = new List<HighScoreEntry>();
        private string _highScoreFilePath;
        private string _playerNameInput = "";
        private const int MaxHighScores = 20;

        private KeyboardState _prevKeyboard;

        private Texture2D _pixel;

        private int _pauseSelection = 0;

        private float _moveCooldown = 0f;
        private const float MoveDelay = 0.085f;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            _graphics.PreferredBackBufferWidth = ScreenWidth;
            _graphics.PreferredBackBufferHeight = ScreenHeight;
            _graphics.IsFullScreen = true;
            _graphics.ApplyChanges();

            _highScoreFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FindTheEgg2", "highscores.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_highScoreFilePath)!);
            LoadHighScores();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _font = Content.Load<SpriteFont>("gamefont");

            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        protected override void Update(GameTime gameTime)
        {
            KeyboardState ks = Keyboard.GetState();

            if (ks.IsKeyDown(Keys.F11) && _prevKeyboard.IsKeyUp(Keys.F11))
            {
                _graphics.IsFullScreen = !_graphics.IsFullScreen;
                _graphics.ApplyChanges();
            }

            switch (_state)
            {
                case GameState.MainMenu:
                    UpdateMainMenu(ks);
                    break;
                case GameState.Playing:
                    UpdatePlaying(ks, gameTime);
                    break;
                case GameState.Paused:
                    UpdatePaused(ks);
                    break;
                case GameState.HighScores:
                    if (ks.IsKeyDown(Keys.Escape) && _prevKeyboard.IsKeyUp(Keys.Escape))
                        _state = GameState.MainMenu;
                    break;
                case GameState.EnteringName:
                    UpdateNameEntry(ks);
                    break;
                case GameState.GameOver:
                    if (ks.IsKeyDown(Keys.Space) && _prevKeyboard.IsKeyUp(Keys.Space))
                        _state = GameState.MainMenu;
                    break;
                case GameState.Victory:
                    if (ks.IsKeyDown(Keys.Space) && _prevKeyboard.IsKeyUp(Keys.Space))
                    {
                        _playerNameInput = "";
                        _state = GameState.EnteringName;
                    }
                    break;
            }

            _prevKeyboard = ks;
            base.Update(gameTime);
        }

        private void UpdateMainMenu(KeyboardState ks)
        {
            if (ks.IsKeyDown(Keys.Down) && _prevKeyboard.IsKeyUp(Keys.Down))
                _selectedMenuItem = (_selectedMenuItem + 1) % 3;
            if (ks.IsKeyDown(Keys.Up) && _prevKeyboard.IsKeyUp(Keys.Up))
                _selectedMenuItem = (_selectedMenuItem + 2) % 3;

            if (ks.IsKeyDown(Keys.Enter) && _prevKeyboard.IsKeyUp(Keys.Enter))
            {
                switch (_selectedMenuItem)
                {
                    case 0:
                        _score = 0;
                        _currentRound = 0;
                        _bombsHeld = 0;
                        StartNewRound();
                        _state = GameState.Playing;
                        break;
                    case 1:
                        _state = GameState.HighScores;
                        break;
                    case 2:
                        Exit();
                        break;
                }
            }
        }

        private void UpdatePlaying(KeyboardState ks, GameTime gameTime)
        {
            if (_timeRemaining > 0)
            {
                _timeRemaining -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_timeRemaining <= 0)
                {
                    _timeRemaining = 0;
                    EndGame(false);
                }
            }

            if (ks.IsKeyDown(Keys.Escape) && _prevKeyboard.IsKeyUp(Keys.Escape))
            {
                _pauseSelection = 0;
                _state = GameState.Paused;
                return;
            }

            if (_bombExploding)
            {
                _bombExplosionTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_bombExplosionTimer >= ExplosionDuration)
                    _bombExploding = false;
            }

            _moveCooldown -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_moveCooldown <= 0)
            {
                Point newPos = _playerPos;
                if (ks.IsKeyDown(Keys.Up)) newPos.Y--;
                else if (ks.IsKeyDown(Keys.Down)) newPos.Y++;
                else if (ks.IsKeyDown(Keys.Left)) newPos.X--;
                else if (ks.IsKeyDown(Keys.Right)) newPos.X++;

                if (newPos != _playerPos &&
                    newPos.X >= 0 && newPos.X < GridWidth &&
                    newPos.Y >= 0 && newPos.Y < GridHeight &&
                    _map[newPos.X, newPos.Y] == 0)
                {
                    _playerPos = newPos;
                    CheckCollisions();
                    _moveCooldown = MoveDelay;
                }
                else
                {
                    _moveCooldown = 0;
                }
            }

            if (ks.IsKeyDown(Keys.Space) && _prevKeyboard.IsKeyUp(Keys.Space) && _bombsHeld > 0)
            {
                ExplodeBomb();
            }
        }

        private void UpdatePaused(KeyboardState ks)
        {
            if (ks.IsKeyDown(Keys.Escape) && _prevKeyboard.IsKeyUp(Keys.Escape))
            {
                _state = GameState.Playing;
                return;
            }

            if (ks.IsKeyDown(Keys.Down) && _prevKeyboard.IsKeyUp(Keys.Down))
                _pauseSelection = (_pauseSelection + 1) % 2;
            if (ks.IsKeyDown(Keys.Up) && _prevKeyboard.IsKeyUp(Keys.Up))
                _pauseSelection = (_pauseSelection + 1) % 2;

            if (ks.IsKeyDown(Keys.Enter) && _prevKeyboard.IsKeyUp(Keys.Enter))
            {
                if (_pauseSelection == 1)
                    _state = GameState.MainMenu;
                else
                    _state = GameState.Playing;
            }
        }

        private void UpdateNameEntry(KeyboardState ks)
        {
            foreach (Keys key in ks.GetPressedKeys())
            {
                if (_prevKeyboard.IsKeyUp(key))
                {
                    if (key == Keys.Enter && _playerNameInput.Length > 0)
                    {
                        _highScores.Add(new HighScoreEntry { Name = _playerNameInput.ToUpper(), Score = _score, Round = _currentRound });
                        _highScores = _highScores.OrderByDescending(h => h.Score).Take(MaxHighScores).ToList();
                        SaveHighScores();
                        _state = GameState.HighScores;
                        return;
                    }
                    if (key == Keys.Back && _playerNameInput.Length > 0)
                        _playerNameInput = _playerNameInput.Substring(0, _playerNameInput.Length - 1);
                    else if (_playerNameInput.Length < 10)
                    {
                        if (key >= Keys.A && key <= Keys.Z)
                            _playerNameInput += (char)key;
                        else if (key == Keys.Space)
                            _playerNameInput += " ";
                    }
                }
            }
        }

        private void CheckCollisions()
        {
            if (_playerPos == _eggPos)
            {
                _score += 100;
                if (_currentRound >= 100)
                {
                    _state = GameState.Victory;
                }
                else
                {
                    StartNewRound();
                }
                return;
            }

            for (int i = _powerUps.Count - 1; i >= 0; i--)
            {
                if (_powerUps[i].Position == _playerPos && !_powerUps[i].Collected)
                {
                    var pu = _powerUps[i];
                    pu.Collected = true;
                    _powerUps[i] = pu;

                    switch (pu.Type)
                    {
                        case PowerUpType.Clock: _timeRemaining += 10; break;
                        case PowerUpType.Bomb: _bombsHeld++; break;
                        case PowerUpType.CrackedEgg: _score += 100; break;
                    }
                }
            }
        }

        private void ExplodeBomb()
        {
            _bombsHeld--;
            _explosionCenter = _playerPos;        // remember where the boom happened
            _bombExploding = true;
            _bombExplosionTimer = 0f;

            // Clear the rocks immediately (animation is just visual flair)
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    int x = _playerPos.X + dx;
                    int y = _playerPos.Y + dy;
                    if (x >= 0 && x < GridWidth && y >= 0 && y < GridHeight)
                        if (_map[x, y] == 1) _map[x, y] = 0;
                }
        }

        private void StartNewRound()
        {
            _currentRound++;
            _timeRemaining = Math.Max(70 - (_currentRound * 5), 20);
            _playerPos = new Point(0, 0);
            _powerUps.Clear();
            _moveCooldown = 0;

            GenerateSolvableMap();

            int numPowerUps = _rnd.Next(1, 4);
            for (int i = 0; i < numPowerUps; i++)
            {
                Point pos;
                do
                {
                    pos = new Point(_rnd.Next(GridWidth), _rnd.Next(GridHeight));
                } while (pos == _playerPos || pos == _eggPos || _map[pos.X, pos.Y] == 1 ||
                         _powerUps.Any(p => p.Position == pos));

                PowerUpType type = (PowerUpType)_rnd.Next(3);
                _powerUps.Add(new PowerUp { Position = pos, Type = type, Collected = false });
            }
        }

        private void GenerateSolvableMap()
        {
            do
            {
                _map = new int[GridWidth, GridHeight];
                int rockCount = 280 + (Math.Min(_currentRound, 50) * 12);
                for (int i = 0; i < rockCount; i++)
                {
                    int x = _rnd.Next(GridWidth);
                    int y = _rnd.Next(GridHeight);
                    if (x != 0 || y != 0)
                        _map[x, y] = 1;
                }

                do
                {
                    _eggPos = new Point(_rnd.Next(GridWidth), _rnd.Next(GridHeight));
                } while (_map[_eggPos.X, _eggPos.Y] == 1 || _eggPos == _playerPos);
            } while (!IsSolvable());
        }

        private bool IsSolvable()
        {
            bool[,] visited = new bool[GridWidth, GridHeight];
            Queue<Point> queue = new Queue<Point>();
            queue.Enqueue(_playerPos);
            visited[_playerPos.X, _playerPos.Y] = true;

            Point[] dirs = { new Point(0, -1), new Point(0, 1), new Point(-1, 0), new Point(1, 0) };

            while (queue.Count > 0)
            {
                Point cur = queue.Dequeue();
                if (cur == _eggPos) return true;

                foreach (var d in dirs)
                {
                    Point next = new Point(cur.X + d.X, cur.Y + d.Y);
                    if (next.X >= 0 && next.X < GridWidth && next.Y >= 0 && next.Y < GridHeight &&
                        !visited[next.X, next.Y] && _map[next.X, next.Y] == 0)
                    {
                        visited[next.X, next.Y] = true;
                        queue.Enqueue(next);
                    }
                }
            }
            return false;
        }

        private void EndGame(bool wonByEgg)
        {
            if (_highScores.Count < MaxHighScores || _score > _highScores.LastOrDefault().Score)
            {
                _playerNameInput = "";
                _state = GameState.EnteringName;
            }
            else
            {
                _state = GameState.GameOver;
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            _spriteBatch.Begin();

            switch (_state)
            {
                case GameState.MainMenu:
                    DrawMainMenu();
                    break;
                case GameState.Playing:
                case GameState.Paused:
                    DrawPlaying();
                    if (_state == GameState.Paused)
                        DrawPauseOverlay();
                    break;
                case GameState.HighScores:
                    DrawHighScores();
                    break;
                case GameState.EnteringName:
                    DrawNameEntry();
                    break;
                case GameState.GameOver:
                    DrawGameOver();
                    break;
                case GameState.Victory:
                    DrawVictory();
                    break;
            }

            _spriteBatch.End();
            base.Draw(gameTime);
        }

        private void DrawMainMenu()
        {
            Vector2 titlePos = new Vector2(ScreenWidth / 2 - _font.MeasureString("FIND THE EGG II").X / 2, 120);
            _spriteBatch.DrawString(_font, "FIND THE EGG II", titlePos, Color.Yellow);

            string[] items = { "NEW GAME", "HIGH SCORES", "QUIT" };
            for (int i = 0; i < 3; i++)
            {
                Color color = (i == _selectedMenuItem) ? Color.Lime : Color.White;
                Vector2 pos = new Vector2(ScreenWidth / 2 - _font.MeasureString(items[i]).X / 2, 280 + i * 60);
                _spriteBatch.DrawString(_font, items[i], pos, color);
            }

            // Version number safely in bottom-right corner
            string version = "v2.0";
            Vector2 versionPos = new Vector2(ScreenWidth - _font.MeasureString(version).X - 30, ScreenHeight - 60);
            _spriteBatch.DrawString(_font, version, versionPos, Color.Gray);
        }

        private void DrawPlaying()
        {
            // Play area (shifted left to make room on the right)
            DrawRect(new Rectangle(PlayOffsetX, PlayOffsetY, PlayAreaWidth, PlayAreaHeight), Color.White);

            Color rockColor = new Color(245, 245, 220);

            // Rocks
            for (int x = 0; x < GridWidth; x++)
                for (int y = 0; y < GridHeight; y++)
                {
                    if (_map[x, y] == 1)
                    {
                        Rectangle r = new Rectangle(PlayOffsetX + x * TileSize, PlayOffsetY + y * TileSize, TileSize, TileSize);
                        DrawOutlineRect(r, rockColor, 2);
                    }
                }

            // Egg
            DrawEgg(PlayOffsetX + _eggPos.X * TileSize + TileSize / 2,
                    PlayOffsetY + _eggPos.Y * TileSize + TileSize / 2,
                    rockColor);

            // Power-ups
            foreach (var pu in _powerUps)
            {
                if (pu.Collected) continue;
                int px = PlayOffsetX + pu.Position.X * TileSize + TileSize / 2;
                int py = PlayOffsetY + pu.Position.Y * TileSize + TileSize / 2;

                switch (pu.Type)
                {
                    case PowerUpType.Clock:
                        DrawClock(px, py, 8, Color.Lime);
                        break;
                    case PowerUpType.Bomb:
                        DrawBomb(px, py, 8, Color.Red);
                        break;
                    case PowerUpType.CrackedEgg:
                        DrawCrackedEgg(px, py, rockColor);
                        break;
                }
            }

            // Player
            Rectangle playerRect = new Rectangle(PlayOffsetX + _playerPos.X * TileSize + 2,
                                                 PlayOffsetY + _playerPos.Y * TileSize + 2,
                                                 TileSize - 4, TileSize - 4);
            DrawRect(playerRect, Color.DarkBlue);
            
            // Bomb explosion animation
            if (_bombExploding)
            {
                float progress = _bombExplosionTimer / ExplosionDuration;
                float pulse = (float)Math.Sin(progress * Math.PI);           // smooth expand → contract
                int scale = (int)(pulse * TileSize * 2.8f);                  // max size ≈ 2.8 tiles

                int cx = PlayOffsetX + _explosionCenter.X * TileSize + TileSize / 2;
                int cy = PlayOffsetY + _explosionCenter.Y * TileSize + TileSize / 2;

                DrawAsterisk(cx, cy, scale, Color.Red);
            }

            // Right-side status panel
            int statusY = PlayOffsetY + 20;

            _spriteBatch.DrawString(_font, $"ROUND {_currentRound}", new Vector2(StatusX, statusY), Color.White);
            statusY += 70;

            _spriteBatch.DrawString(_font, $"SCORE {_score}", new Vector2(StatusX, statusY), Color.White);
            statusY += 70;

            _spriteBatch.DrawString(_font, $"TIME {Math.Max(0, (int)_timeRemaining)}", new Vector2(StatusX, statusY), Color.White);
            statusY += 70;

            Color bombColor = _bombsHeld > 0 ? Color.Red : Color.White;
            _spriteBatch.DrawString(_font, $"BOMBS {_bombsHeld}", new Vector2(StatusX, statusY), bombColor);
        }

        private void DrawPauseOverlay()
        {
            DrawRect(new Rectangle(0, 0, ScreenWidth, ScreenHeight), new Color(0, 0, 0, 180));

            string title = "QUIT THE GAME?";
            Vector2 tPos = new Vector2(ScreenWidth / 2 - _font.MeasureString(title).X / 2, ScreenHeight / 2 - 80);
            _spriteBatch.DrawString(_font, title, tPos, Color.Yellow);

            string[] options = { "NO", "YES" };
            for (int i = 0; i < 2; i++)
            {
                Color c = (i == _pauseSelection) ? Color.Lime : Color.White;
                Vector2 pos = new Vector2(ScreenWidth / 2 - _font.MeasureString(options[i]).X / 2, ScreenHeight / 2 + i * 50);
                _spriteBatch.DrawString(_font, options[i], pos, c);
            }
        }

        private void DrawHighScores()
        {
            // Title
            _spriteBatch.DrawString(_font, "HIGH SCORES",
                new Vector2(ScreenWidth / 2 - _font.MeasureString("HIGH SCORES").X / 2, 80),
                Color.Yellow);

            // Column headers (subtle, arcade style)
            _spriteBatch.DrawString(_font, "RANK", new Vector2(180, 130), Color.Gray);
            _spriteBatch.DrawString(_font, "NAME", new Vector2(300, 130), Color.Gray);
            _spriteBatch.DrawString(_font, "SCORE", new Vector2(520, 130), Color.Gray);
            _spriteBatch.DrawString(_font, "ROUND", new Vector2(700, 130), Color.Gray);

            // Actual high-score entries — perfectly column-aligned
            for (int i = 0; i < _highScores.Count && i < 20; i++)
            {
                var entry = _highScores[i];
                float y = 170 + i * 48;   // same generous spacing as before

                // RANK (right-aligned in a small column)
                string rank = $"{i + 1}.";
                _spriteBatch.DrawString(_font, rank,
                    new Vector2(180, y), Color.White);

                // NAME (left-aligned)
                _spriteBatch.DrawString(_font, entry.Name,
                    new Vector2(300, y), Color.White);

                // SCORE (right-aligned)
                string scoreStr = entry.Score.ToString();
                _spriteBatch.DrawString(_font, scoreStr,
                    new Vector2(620 - _font.MeasureString(scoreStr).X, y), Color.White);

                // ROUND
                string roundStr = $"R{entry.Round}";
                _spriteBatch.DrawString(_font, roundStr,
                    new Vector2(700, y), Color.White);
            }

            // Bottom instruction
            _spriteBatch.DrawString(_font, "PRESS ESC TO RETURN",
                new Vector2(ScreenWidth / 2 - _font.MeasureString("PRESS ESC TO RETURN").X / 2, ScreenHeight - 80),
                Color.Gray);
        }

        private void DrawNameEntry()
        {
            _spriteBatch.DrawString(_font, "NEW HIGH SCORE!", new Vector2(ScreenWidth / 2 - _font.MeasureString("NEW HIGH SCORE!").X / 2, 180), Color.Yellow);
            _spriteBatch.DrawString(_font, "ENTER YOUR NAME:", new Vector2(ScreenWidth / 2 - _font.MeasureString("ENTER YOUR NAME:").X / 2, 260), Color.White);
            _spriteBatch.DrawString(_font, _playerNameInput + "_", new Vector2(ScreenWidth / 2 - _font.MeasureString(_playerNameInput + "_").X / 2, 340), Color.Lime);
            _spriteBatch.DrawString(_font, "PRESS ENTER WHEN DONE", new Vector2(ScreenWidth / 2 - _font.MeasureString("PRESS ENTER WHEN DONE").X / 2, 460), Color.Gray);
        }

        private void DrawGameOver()
        {
            _spriteBatch.DrawString(_font, "GAME OVER", new Vector2(ScreenWidth / 2 - _font.MeasureString("GAME OVER").X / 2, 220), Color.Red);
            _spriteBatch.DrawString(_font, $"FINAL SCORE: {_score}", new Vector2(ScreenWidth / 2 - _font.MeasureString($"FINAL SCORE: {_score}").X / 2, 300), Color.White);
            _spriteBatch.DrawString(_font, "PRESS SPACE TO RETURN TO MENU", new Vector2(ScreenWidth / 2 - _font.MeasureString("PRESS SPACE TO RETURN TO MENU").X / 2, 400), Color.White);
        }

        private void DrawVictory()
        {
            string caption = "The Egg Has Been Found";
            Vector2 captionPos = new Vector2(ScreenWidth / 2 - _font.MeasureString(caption).X / 2, 60);
            _spriteBatch.DrawString(_font, caption, captionPos, Color.Gold);
            int centerX = ScreenWidth / 2;
            int centerY = 340;
            DrawCrownedEgg(centerX, centerY, 7);
            string scoreText = $"SCORE {_score}   ROUND {_currentRound}";
            Vector2 scorePos = new Vector2(ScreenWidth / 2 - _font.MeasureString(scoreText).X / 2, 580);
            _spriteBatch.DrawString(_font, scoreText, scorePos, Color.White);
            string instruction = "PRESS SPACE TO ENTER YOUR NAME";
            Vector2 instrPos = new Vector2(ScreenWidth / 2 - _font.MeasureString(instruction).X / 2, 680);
            _spriteBatch.DrawString(_font, instruction, instrPos, Color.Lime);
        }

        private void DrawCrownedEgg(int centerX, int centerY, int scale)
        {
            int w = 16 * scale;
            int h = 24 * scale;
            Color eggColor = new Color(255, 235, 180);
            for (int y = -h / 2; y <= h / 2; y++)
            {
                float t = (float)y / (h / 2f);
                int width = (int)(w * (0.75f - 0.25f * t * t));
                int left = centerX - width / 2;
                DrawRect(new Rectangle(left, centerY + y, width, 1), eggColor);
            }
            Color outline = new Color(180, 160, 80);
            for (int y = -h / 2 - 1; y <= h / 2 + 1; y += 3)
            {
                float t = (float)y / (h / 2f);
                int width = (int)(w * (0.75f - 0.25f * t * t));
                int left = centerX - width / 2;
                DrawRect(new Rectangle(left - 2, centerY + y, width + 4, 3), outline);
            }
            int crownY = centerY - h / 2 - scale * 4;
            int crownW = w * 3 / 4;
            Color crownColor = new Color(255, 215, 0);
            DrawRect(new Rectangle(centerX - crownW / 2, crownY, crownW, scale * 3), crownColor);
            int[] offsets = { -crownW / 2 + 8, -crownW / 4, 0, crownW / 4, crownW / 2 - 8 };
            for (int i = 0; i < 5; i++)
            {
                int sx = centerX + offsets[i];
                int sw = scale * 3;
                for (int sy = 0; sy < scale * 7; sy += 2)
                {
                    int cw = (int)(sw * (1.0f - (float)sy / (scale * 7)));
                    DrawRect(new Rectangle(sx - cw / 2, crownY - sy - scale * 2, cw, 3), crownColor);
                }
            }
            Color jewel = Color.OrangeRed;
            DrawRect(new Rectangle(centerX - 6, crownY - scale * 6, 12, 8), jewel);
            DrawRect(new Rectangle(centerX - crownW / 2 + 4, crownY - 2, 6, 8), jewel);
            DrawRect(new Rectangle(centerX + crownW / 2 - 10, crownY - 2, 6, 8), jewel);
        }

        private void DrawRect(Rectangle rect, Color color)
        {
            _spriteBatch.Draw(_pixel, rect, color);
        }

        private void DrawOutlineRect(Rectangle rect, Color color, int thickness)
        {
            DrawRect(new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            DrawRect(new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            DrawRect(new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            DrawRect(new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }

        private void DrawAsterisk(int centerX, int centerY, int size, Color color)
        {
            // Thick lines forming a classic asterisk *
            int half = size / 2;

            // Horizontal
            DrawRect(new Rectangle(centerX - half, centerY - 2, size, 4), color);
            // Vertical
            DrawRect(new Rectangle(centerX - 2, centerY - half, 4, size), color);

            // Two diagonals (slightly thinner for retro look)
            for (int i = -half; i <= half; i++)
            {
                // \ diagonal
                DrawRect(new Rectangle(centerX + i - 1, centerY + i - 1, 3, 3), color);
                // / diagonal
                DrawRect(new Rectangle(centerX - i - 1, centerY + i - 1, 3, 3), color);
            }
        }

        private void DrawEgg(int centerX, int centerY, Color color)
        {
            int w = 12;
            int h = 18;

            // Draw only the left and right edges of the egg shape
            for (int y = -h / 2; y <= h / 2; y++)
            {
                float t = (float)y / (h / 2f);
                int width = (int)(w * (1 - 0.3f * t * t));

                int left = centerX - width / 2;
                int right = centerX + width / 2 - 1;

                // Left outline
                DrawRect(new Rectangle(left, centerY + y, 2, 2), color);
                // Right outline
                DrawRect(new Rectangle(right, centerY + y, 2, 2), color);
            }

            // Top and bottom rounded caps (closes the outline)
            DrawRect(new Rectangle(centerX - w / 2 + 1, centerY - h / 2, w - 2, 2), color);     // top
            DrawRect(new Rectangle(centerX - w / 2 + 1, centerY + h / 2 - 1, w - 2, 2), color); // bottom
        }

        private void DrawCrackedEgg(int cx, int cy, Color color)
        {
            int w = 10;
            int h = 13;
            for (int y = -h / 2; y <= h / 2; y++)
            {
                float t = (float)y / (h / 2);
                int width = (int)(w * (1 - 0.4f * t * t));
                DrawRect(new Rectangle(cx - width / 2, cy + y, width, 2), color);
            }

            int crackX = cx;
            int crackY = cy - 6;
            DrawRect(new Rectangle(crackX - 4, crackY, 1, 3), color);
            DrawRect(new Rectangle(crackX - 2, crackY + 2, 1, 3), color);
            DrawRect(new Rectangle(crackX, crackY + 1, 1, 4), color);
            DrawRect(new Rectangle(crackX + 2, crackY + 3, 1, 3), color);
            DrawRect(new Rectangle(crackX - 5, crackY + 1, 1, 1), color);
            DrawRect(new Rectangle(crackX + 4, crackY + 2, 1, 1), color);
        }

        private void DrawClock(int cx, int cy, int radius, Color color)
        {
            for (int r = radius - 1; r <= radius; r++)
                DrawCircle(cx, cy, r, color);
            _spriteBatch.Draw(_pixel, new Rectangle(cx - 1, cy - radius / 2, 2, radius / 2), color);
            _spriteBatch.Draw(_pixel, new Rectangle(cx - 1, cy - radius + 2, 2, radius - 2), color);
        }

        private void DrawBomb(int cx, int cy, int radius, Color color)
        {
            for (int r = radius - 1; r <= radius; r++)
                DrawCircle(cx, cy, r, color);
            _spriteBatch.Draw(_pixel, new Rectangle(cx + radius - 2, cy - radius, 3, 6), Color.Orange);
        }

        private void DrawCircle(int cx, int cy, int radius, Color color)
        {
            for (int i = 0; i < 8; i++)
            {
                float a1 = i * MathHelper.TwoPi / 8;
                float a2 = (i + 1) * MathHelper.TwoPi / 8;
                int x1 = cx + (int)(radius * Math.Cos(a1));
                int y1 = cy + (int)(radius * Math.Sin(a1));
                int x2 = cx + (int)(radius * Math.Cos(a2));
                int y2 = cy + (int)(radius * Math.Sin(a2));
                _spriteBatch.Draw(_pixel, new Rectangle(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1) + 1, Math.Abs(y2 - y1) + 1), color);
            }
        }

        private void LoadHighScores()
        {
            if (File.Exists(_highScoreFilePath))
            {
                string json = File.ReadAllText(_highScoreFilePath);
                _highScores = JsonSerializer.Deserialize<List<HighScoreEntry>>(json) ?? new List<HighScoreEntry>();
            }
        }

        private void SaveHighScores()
        {
            string json = JsonSerializer.Serialize(_highScores, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_highScoreFilePath, json);
        }
    }
}