using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using SystemMessageBox = System.Windows.Forms.MessageBox;
using Marshal = System.Runtime.InteropServices.Marshal;

namespace Noxico
{
	static class Program
	{
		[STAThread]
		static void Main(string[] args)
		{
			//Switch to Invariant so we get "�1,000.50" instead of "� 1.000,50" or "$1,000.50" by default.
			//Can't do this in certain cases, which should be inapplicable to this program.
			System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

			if (args.Contains("-spreadem"))
			{
				Mix.SpreadEm();
				return;
			}

			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			try
			{
				Application.Run(new MainForm());
			}
			catch (ObjectDisposedException)
			{
			}
		}

		[System.Diagnostics.Conditional("DEBUG")]
		public static void WriteLine(string format, params object[] arg)
		{
			Console.WriteLine(format, arg);
		}
		[System.Diagnostics.Conditional("DEBUG")]
		public static void Write(string format, params object[] arg)
		{
			Console.Write(format, arg);
		}
		[System.Diagnostics.Conditional("DEBUG")]
		public static void WriteLine(object value)
		{
			Console.WriteLine(value.ToString());
		}
	}

    public class MainForm : Form, IGameHost
    {
		public NoxicoGame Noxico { get; set; }

		private struct Cell
        {
            public char Character;
			public Color Foreground;
			public Color Background;
        }
        private Cell[,] image = new Cell[100, 30];
        private Cell[,] previousImage = new Cell[100, 30];
		private Bitmap backBuffer;
		private Bitmap scrollBuffer;
		private bool starting = true, fatal = false;

        public bool Running { get; set; }

		private int CellWidth, CellHeight;
		private string pngFont = "unifont";
		private Dictionary<int, FontBlock> pngFonts;

		public string IniPath { get; set; }
		public new Point Cursor { get; set; }
		private Point prevCursor;

		private Dictionary<Keys, Keys> numpad = new Dictionary<Keys, Keys>()
			{
				{ Keys.NumPad8, Keys.Up },
				{ Keys.NumPad2, Keys.Down },
				{ Keys.NumPad4, Keys.Left },
				{ Keys.NumPad6, Keys.Right },
				{ Keys.NumPad5, Keys.OemPeriod },
				//{ Keys.Back, Keys.Escape },
			};

		private Timer timer;

		public MainForm()
		{
#if !DEBUG
			try
#endif
			{
				this.Text = "Noxico";
				this.BackColor = System.Drawing.Color.Black;
				this.DoubleBuffered = true;
				this.FormBorderStyle = FormBorderStyle.FixedSingle;
				this.FormClosing += new FormClosingEventHandler(this.Form1_FormClosing);
				this.KeyDown += new KeyEventHandler(this.Form1_KeyDown);
				this.KeyPress += new KeyPressEventHandler(this.Form1_KeyPress);
				this.KeyUp += new KeyEventHandler(this.Form1_KeyUp);
				this.Icon = global::Noxico.Properties.Resources.app;
				this.ClientSize = new Size(80 * CellWidth, 25 * CellHeight);
				this.Controls.Add(new Label()
				{
					Text = "Please hold...",
					AutoSize = true,
					Font = new System.Drawing.Font("Garamond", 16, FontStyle.Bold | FontStyle.Italic),
					ForeColor = System.Drawing.Color.Yellow,
					Visible = true,
					Location = new System.Drawing.Point(16, 16)
				});

				foreach (var reqDll in new[] { "Antlr3.Runtime.dll", "Jint.dll" })
					if (!File.Exists(reqDll))
						throw new FileNotFoundException("Required DLL " + reqDll + " is missing.");

				Mix.Initialize("Noxico");
				if (!Mix.FileExists("noxico.xml"))
				{
					SystemMessageBox.Show(this, "Could not find game data. Please redownload the game.", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
					Close();
					return;
				}

				var portable = false;
				IniPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "noxico.ini");
				if (File.Exists("portable"))
				{
					portable = true;
					var oldIniPath = IniPath;
					IniPath = "noxico.ini";
					var fi = new FileInfo(Application.ExecutablePath);
					var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
					if ((fi.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly || Application.ExecutablePath.StartsWith(pf))
					{
						var response = SystemMessageBox.Show(this, "Trying to start in portable mode, but from a protected location. Use non-portable mode?" + Environment.NewLine + "Selecting \"no\" may cause errors.", Application.ProductName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
						if (response == DialogResult.Cancel)
						{
							Close();
							return;
						}
						else if (response == DialogResult.Yes)
						{
							IniPath = oldIniPath;
							portable = false;
						}
					}
				}

				if (!File.Exists(IniPath))
					File.WriteAllText(IniPath, Mix.GetString("noxico.ini"));
				IniFile.Load(IniPath);

				if (portable)
				{
					IniFile.SetValue("misc", "vistasaves", false);
					IniFile.SetValue("misc", "savepath", "./saves");
					IniFile.SetValue("misc", "shotpath", "./screenshots");
				}

				pngFont = IniFile.GetValue("misc", "font", "unifont");
				pngFonts = new Dictionary<int, FontBlock>();
				if (Mix.FileExists(pngFont + "_00.png"))
				{
					var blockZero = Mix.GetBitmap(pngFont + "_00.png");
					CellWidth = blockZero.Width / 16;
					CellHeight = blockZero.Height / 16;
					CachePNGFont('A');
				}
				else
				{
					pngFont = "font\\unifont";
					if (Mix.FileExists(pngFont + "_00.png"))
					{
						var blockZero = Mix.GetBitmap(pngFont + "_00.png");
						CellWidth = blockZero.Width / 16;
						CellHeight = blockZero.Height / 16;
						CachePNGFont('A');
					}
					else
					{
						SystemMessageBox.Show(this, "Could not find font bitmaps. Please redownload the game.", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
						Close();
						return;
					}
				}

				ClientSize = new Size(100 * CellWidth, 30 * CellHeight);

				Show();
				Refresh();

				backBuffer = new Bitmap(100 * CellWidth, 30 * CellHeight, PixelFormat.Format24bppRgb);
				scrollBuffer = new Bitmap(100 * CellWidth, 30 * CellHeight, PixelFormat.Format24bppRgb);
				Noxico = new NoxicoGame();
				Noxico.Initialize(this);

				MouseUp += new MouseEventHandler(MainForm_MouseUp);
				MouseWheel += new MouseEventHandler(MainForm_MouseWheel);

				GotFocus += (s, e) => { Vista.GamepadFocused = true; };
				LostFocus += (s, e) => { Vista.GamepadFocused = false; };

				Vista.GamepadEnabled = IniFile.GetValue("misc", "xinput", true);

				Program.WriteLine("MONO CHECK: {0}", Environment.OSVersion.Platform);
				Program.WriteLine(Environment.OSVersion);
				if (Environment.OSVersion.Platform == PlatformID.Unix)
				{
					Program.WriteLine("*** You are running on a *nix system. ***");
					Program.WriteLine("Key repeat delays exaggerated.");
					NoxicoGame.Mono = true;
					Vista.GamepadEnabled = false;
				}

				this.Controls.Clear();
				starting = false;
				Running = true;

				Cursor = new Point(-1, -1);

#if GAMELOOP
				while (Running)
				{
					Noxico.Update();
					Application.DoEvents();
				}
#else
				FormClosing += new FormClosingEventHandler(MainForm_FormClosing);
				timer = new Timer()
				{
					Interval = 10,
					Enabled = true,
				};
				timer.Tick += new EventHandler(timer_Tick);
#endif
			}
#if !DEBUG
			catch (Exception x)
			{
				new ErrorForm(x).ShowDialog(this);
				SystemMessageBox.Show(this, x.ToString(), Application.ProductName, MessageBoxButtons.OK);
				Running = false;
				fatal = true;
				Application.ExitThread();
			}
#endif
			if (!fatal)
			{
				Noxico.SaveGame();
			}
		}

		void MainForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!fatal)
				Noxico.SaveGame();
		}

		void timer_Tick(object sender, EventArgs e)
		{
#if DEBUG
			Noxico.Update();
#else
			try
			{
				Noxico.Update();
			}
			catch (Exception x)
			{
				new ErrorForm(x).ShowDialog(this);
				SystemMessageBox.Show(this, x.ToString(), Application.ProductName, MessageBoxButtons.OK);
				Running = false;
				fatal = true;
			}
#endif
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			if (backBuffer == null)
			{
				base.OnPaint(e);
				return;
			}
			e.Graphics.DrawImage(backBuffer, ClientRectangle);
		}

		private void CachePNGFont(char p)
		{
			var block = (p >> 8);
			if (!pngFonts.ContainsKey(block))
			{
				var file = pngFont + "_" + block.ToString("X2") + ".png";
				if (Mix.FileExists(file))
				{
					Program.WriteLine("Caching font block U+{0:X2}--...", block);
					pngFonts.Add(block, new FontBlock(Mix.GetBitmap(file)));
				}
				else
				{
					Program.WriteLine("Warning: {0} does not exist!", file);
					pngFonts.Add(block, new FontBlock(new Bitmap(128, 256)));
				}
			}
		}
		
        public void SetCell(int row, int col, char character, Color foregroundColor, Color backgroundColor, bool forceRedraw = false)
        {
			if (col >= 100 || row >= 30 || col < 0 || row < 0)
				return;

            image[col, row].Character = character;
            image[col, row].Foreground = foregroundColor;
			if (backgroundColor != Color.Transparent)
	            image[col, row].Background = backgroundColor;
			if (forceRedraw)
				previousImage[col, row].Character = (char)(character + 4);
        }

		public void Clear(char character, Color foregroundColor, Color backgroundColor)
		{
			for (int row = 0; row < 30; row++)
			{
				for (int col = 0; col < 100; col++)
				{
					image[col, row].Character = character;
					image[col, row].Foreground = foregroundColor;
					image[col, row].Background = backgroundColor;
				}
			}
		}
		public void Clear()
		{
			Clear(' ', Color.White, Color.Black);
		}

		public void Write(string text, Color foregroundColor, Color backgroundColor, int row = 0, int col = 0)
		{
			if (!text.IsNormalized())
				text = text.Normalize();

			var rx = col;
			for (var i = 0; i < text.Length; i++)
			{
				var c = text[i];
				if (c == '\r')
					continue;
				if (c == '\n')
				{
					col = rx;
					row++;
					continue;
				}
				if (c == '<')
				{
					var gtPos = text.IndexOf('>', i + 1);
					if (gtPos != -1)
					{
						var tag = text.Substring(i + 1);
						i = gtPos;
						if (tag.StartsWith("nowrap"))
							continue;
						if (tag[0] == 'c')
						{
							var match = Regex.Match(tag, @"c(?:(?:(?<fore>\w+)(?:(?:,(?<back>\w+))?))?)");
							foregroundColor = !string.IsNullOrEmpty(match.Groups["fore"].Value) ? Color.FromName(match.Groups["fore"].Value) : Color.Silver;
							backgroundColor = !string.IsNullOrEmpty(match.Groups["back"].Value) ? Color.FromName(match.Groups["back"].Value) : Color.Transparent;
							continue;
						}
						else if (tag[0] == 'g')
						{
							var match = Regex.Match(tag, @"g(?:(?:(?<chr>\w{1,4}))?)");
							var chr = int.Parse(match.Groups["chr"].Value, System.Globalization.NumberStyles.HexNumber);
							c = (char)chr;
						}
					}
				}
				SetCell(row, col, c, foregroundColor, backgroundColor, true);
				col++;
				if (col >= 100)
				{
					col = rx;
					row++;
				}
				if (row >= 30)
					return;
			}
		}

        private void DrawCell(byte[] scan0, int stride, int row, int col, Cell cell)
        {
			var sTX = col * CellWidth;
            var sTY = row * CellHeight;
			var b = cell.Background;
			var f = cell.Foreground;
            var c = cell.Character;

			CachePNGFont(c);
			var block = c >> 8;
			var c2 = c & 0xFF;
			var fontBlock = pngFonts[block];
			var sSX = (c2 %	16) * CellWidth;
			var sSY = (c2 / 16) * CellHeight;
			for (var y = 0; y < CellHeight; y++)
			{
				for (var x = 0; x < CellWidth; x++)
				{
					var d = fontBlock.Data[c2, (y * CellWidth) + x];
					var color = (d == 0) ? b : f;
					var target = ((sTY + y) * stride) + ((sTX + x) * 3);
					scan0[target + 0] = color.B;
					scan0[target + 1] = color.G;
					scan0[target + 2] = color.R;
				}
			}
        }

		public void Draw()
        {
			var lockData = backBuffer.LockBits(new System.Drawing.Rectangle(0, 0, backBuffer.Width, backBuffer.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
			var size = lockData.Stride * lockData.Height;
			var scan0 = new byte[size];
			Marshal.Copy(lockData.Scan0, scan0, 0, size);
            using (var gfx = Graphics.FromImage(backBuffer))
            {
				for (int row = 0; row < 30; row++)
				{
					for (int col = 0; col < 100; col++)
					{
						if (image[col, row].Character != previousImage[col, row].Character ||
							image[col, row].Foreground != previousImage[col, row].Foreground ||
							image[col, row].Background != previousImage[col, row].Background)
						{
							DrawCell(scan0, lockData.Stride, row, col, image[col, row]);
							previousImage[col, row].Character = image[col, row].Character;
							previousImage[col, row].Foreground = image[col, row].Foreground;
							previousImage[col, row].Background = image[col, row].Background;
						}
					}
				}
            }
			Marshal.Copy(scan0, 0, lockData.Scan0, size);
			backBuffer.UnlockBits(lockData);

			if (Cursor.X != prevCursor.X || Cursor.Y != prevCursor.Y)
				prevCursor = Cursor;
			if (Cursor.X >= 0 && Cursor.X < 100 && Cursor.Y >= 0 && Cursor.Y < 30)
			{
				using (var gfx = Graphics.FromImage(backBuffer))
				{
					gfx.DrawRectangle(Environment.TickCount % 1000 < 500 ? Pens.Black : Pens.White /* new Pen(image[Cursor.X, Cursor.Y].Foreground) */, Cursor.X * CellWidth, Cursor.Y * CellHeight, CellWidth - 1, CellHeight - 1);
				}
			}
            this.Refresh();
        }

		public void ScrollUp(int topRow, int bottomRow, int leftCol, int rightCol, Color reveal)
		{
			for (var row = topRow; row < bottomRow; row++)
			{
				for (var col = leftCol; col < rightCol; col++)
				{
					image[col, row].Character = image[col, row + 1].Character;
					image[col, row].Foreground = image[col, row + 1].Foreground;
					image[col, row].Background = image[col, row + 1].Background;
				}
			}
		}

		public void ScrollDown(int topRow, int bottomRow, int leftCol, int rightCol, Color reveal)
		{
			for (var row = bottomRow; row > topRow; row--)
			{
				for (var col = leftCol; col < rightCol; col++)
				{
					image[col, row].Character = image[col, row - 1].Character;
					image[col, row].Foreground = image[col, row - 1].Foreground;
					image[col, row].Background = image[col, row - 1].Background;
				}
			}
		}

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Running = false;
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
			if (starting)
				return;
			if (NoxicoGame.Mono && (DateTime.Now - NoxicoGame.KeyRepeat[(int)e.KeyCode]).Milliseconds < 100)
				return;
			if (e.Control && (e.KeyCode == Keys.R || e.KeyCode == Keys.A))
				return;
			NoxicoGame.KeyRepeat[(int)e.KeyCode] = DateTime.Now;
			NoxicoGame.KeyMap[(int)e.KeyCode] = true;
			if (numpad.ContainsKey(e.KeyCode))
				NoxicoGame.KeyMap[(int)numpad[e.KeyCode]] = true;
			if (e.Modifiers == Keys.Shift)
				NoxicoGame.Modifiers[0] = true;
			if (e.KeyValue == 191 && NoxicoGame.Mode == UserMode.Walkabout)
				NoxicoGame.KeyMap[(int)Keys.L] = true;
        }

        private void Form1_KeyUp(object sender, KeyEventArgs e)
        {
            NoxicoGame.KeyMap[(int)e.KeyCode] = false;
			NoxicoGame.KeyTrg[(int)e.KeyCode] = true;
			if (numpad.ContainsKey(e.KeyCode))
			{
				NoxicoGame.KeyMap[(int)numpad[e.KeyCode]] = false;
				NoxicoGame.KeyTrg[(int)numpad[e.KeyCode]] = true;
			}
			if (e.Modifiers == Keys.Shift)
				NoxicoGame.Modifiers[0] = false;

			if (e.KeyCode == (Keys)NoxicoGame.KeyBindings[KeyBinding.Screenshot])
			{
				var shotDir = IniFile.GetValue("misc", "shotpath", "screenshots");
				if (shotDir.StartsWith("$"))
					shotDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + shotDir.Substring(1);

				if (!Directory.Exists(shotDir))
					Directory.CreateDirectory(shotDir);
				int i = 1;
				while (File.Exists(Path.Combine(shotDir, "screenshot" + i.ToString("000") + ".png")))
					i++;
				backBuffer.Save(Path.Combine(shotDir, "screenshot" + i.ToString("000") + ".png"), ImageFormat.Png);
				Program.WriteLine("Screenshot saved.");
			}
			if (e.KeyValue == 191)
				NoxicoGame.KeyMap[(int)Keys.L] = false;

			if (e.KeyCode == Keys.R && e.Control)
			{
				NoxicoGame.KeyMap[(int)Keys.R] = false;
				for (int row = 0; row < 30; row++)
					for (int col = 0; col < 100; col++)
						previousImage[col, row].Character = (char)0x500;
			}
		
			if (e.KeyCode == Keys.A && e.Control && NoxicoGame.Mode == UserMode.Walkabout)
			{
				NoxicoGame.KeyMap[(int)Keys.A] = false;
				NoxicoGame.ShowMessageLog();
			}
		}

		private void Form1_KeyPress(object sender, KeyPressEventArgs e)
		{
			NoxicoGame.LastPress = e.KeyChar;
		}

		private void MainForm_MouseUp(object x, MouseEventArgs y)
		{
			var tx = y.X / (CellWidth);
			var ty = y.Y / (CellHeight);
			if (tx < 0 || ty < 0 || tx > 99 || ty > 29)
				return;
			if (NoxicoGame.Mode == UserMode.Walkabout)
			{
				if (y.Button == MouseButtons.Left)
					Noxico.Player.AutoTravelTo(tx, ty);
				else if (y.Button == MouseButtons.Right)
				{
					NoxicoGame.Cursor.ParentBoard = Noxico.CurrentBoard;
					NoxicoGame.Cursor.XPosition = tx;
					NoxicoGame.Cursor.YPosition = ty;
					NoxicoGame.Cursor.Point();
					NoxicoGame.KeyMap[NoxicoGame.KeyBindings[KeyBinding.Accept]] = true;
					NoxicoGame.Cursor.Update();
				}
			}
			else if (NoxicoGame.Mode == UserMode.Subscreen)
			{
				if (y.Button == MouseButtons.Left)
				{
					if (NoxicoGame.Subscreen == MessageBox.Handler)
					{
						NoxicoGame.KeyMap[NoxicoGame.KeyBindings[KeyBinding.Accept]] = true;
						return;
					}
					Subscreens.MouseX = tx;
					Subscreens.MouseY = ty;
					Subscreens.Mouse = true;
				}
				else if (y.Button == MouseButtons.Right)
				{
					NoxicoGame.KeyMap[NoxicoGame.KeyBindings[KeyBinding.Back]] = true;
				}
			}
		}

		private void MainForm_MouseWheel(object x, MouseEventArgs y)
		{
			if (NoxicoGame.Mode == UserMode.Subscreen)
			{
				if (y.Delta < 0)
				{
					NoxicoGame.KeyMap[NoxicoGame.KeyBindings[KeyBinding.ScrollDown]] = true;
					NoxicoGame.ScrollWheeled = true;
				}
				else if (y.Delta > 0)
				{
					NoxicoGame.KeyMap[NoxicoGame.KeyBindings[KeyBinding.ScrollUp]] = true;
					NoxicoGame.ScrollWheeled = true;
				}
			}
		}
    }

	public class FontBlock
	{
		public byte[,] Data;
		public FontBlock(Bitmap source)
		{
			var cWidth = source.Width / 16;
			var cHeight = source.Height / 16;
			Data = new byte[255, cWidth * cHeight];
			for (var ch = 0; ch < 255; ch++)
			{
				var i = 0;
				var sX = (ch % 16) * cWidth;
				var sY = (ch / 16) * cHeight;
				for (var y = 0; y < cHeight; y++)
				{
					for (var x = 0; x < cWidth; x++)
					{
						Data[ch, i] = (byte)(source.GetPixel(sX + x, sY + y).R > 127 ? 1 : 0);
						i++;
					}
				}
			}
		}
	}
}
