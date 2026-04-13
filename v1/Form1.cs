using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;

namespace FindTheEgg
{
	/// <summary>
	/// Summary description for Form1.
	/// </summary>
	public class Form1 : System.Windows.Forms.Form
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;

		private const int sqLand = 0;
        private const int sqEgg = 1;
		private const int sqRock = 2;

		private int[,] EggMap;

		private int PlayerXPosition = 0;
		private int PlayerYPosition = 0;
		private int RemainingSeconds;
		private int CurrentRound;

		private Timer timer = new Timer();

		public Form1()
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();

			Text = "Find the Egg!";
			BackColor = SystemColors.Window;
			ForeColor = SystemColors.WindowText;
			ResizeRedraw = true;

			//
			// TODO: Add any constructor code after InitializeComponent call
			//
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if (components != null) 
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			// 
			// Form1
			// 
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
			this.ClientSize = new System.Drawing.Size(592, 566);
			this.Name = "Form1";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Find the Egg!";
			this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
			this.Load += new System.EventHandler(this.Form1_Load);

		}
		#endregion

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main() 
		{
			Application.Run(new Form1());
		}

		private void Form1_Load(object sender, System.EventArgs e)
		{
			EggMap = new int[50, 50];
			timer.Interval = 1000;
			timer.Tick += new EventHandler(TimerOnTick);
			CurrentRound = 0;
			StartNewRound();
		}

		protected override void OnKeyDown(KeyEventArgs kea)
		{
			DrawPlayer(false);
			switch (kea.KeyCode) 
			{
				case Keys.Up:
					if (PlayerYPosition > 0) 
						if (EggMap[PlayerXPosition, PlayerYPosition-1] != sqRock) PlayerYPosition--;
					break;
				case Keys.Down:
					if (PlayerYPosition < 49) 
						if (EggMap[PlayerXPosition, PlayerYPosition+1] != sqRock) PlayerYPosition++;
					break;
				case Keys.Left:
					if (PlayerXPosition > 0) 
						if (EggMap[PlayerXPosition-1, PlayerYPosition] != sqRock) PlayerXPosition--;
					break;
				case Keys.Right:
					if (PlayerXPosition < 49) 
						if (EggMap[PlayerXPosition+1, PlayerYPosition] != sqRock) PlayerXPosition++;
					break;
			}
			DrawPlayer(true);
			if (EggMap[PlayerXPosition, PlayerYPosition] == sqEgg) 
			{
//				MessageBox.Show("You found the egg!", "You win!");
				EggMap[PlayerXPosition, PlayerYPosition] = sqLand;
				timer.Stop();
				StartNewRound();
			}
		}

		protected override void OnPaint(PaintEventArgs pea)
		{
			DrawMap();
		}

		private void CreateMap()
		{
			int i, j;
			Random rnd = new Random();

			EggMap[rnd.Next(48), rnd.Next(48)] = sqEgg;

			if (CurrentRound < 5) 
			{
				for (i=0; i < 50; i++)
					for (j=0; j < 50; j++)
						if (rnd.Next(10) == 5) EggMap[j,i] = sqRock;
			}
		}

		private void DrawMap()
		{
			Graphics gfx = Form1.ActiveForm.CreateGraphics();
			int i, j;

			for (i=0; i < 50; i++)
				for (j=0; j < 50; j++)
					switch (EggMap[j, i]) 
					{
						case sqEgg:
							gfx.DrawEllipse(Pens.AntiqueWhite, j * 12, i * 12, 12, 6);
							break;
						case sqRock:
							gfx.DrawRectangle(Pens.AntiqueWhite, j * 12, i * 12, 12, 12);
							break;
					}
		}

		private void DrawPlayer(bool Visible)
		{
			Graphics gfx = Form1.ActiveForm.CreateGraphics();

			if (Visible)
				gfx.FillRectangle(Brushes.Black, PlayerXPosition * 12, PlayerYPosition * 12, 12, 12);
			else
				gfx.FillRectangle(Brushes.White, PlayerXPosition * 12, PlayerYPosition * 12, 12, 12);
		}

		private void StartNewRound()
		{
			CurrentRound++;
			RemainingSeconds = 70 - (CurrentRound * 5);
			Form1.ActiveForm.Text = "Find the Egg! - " + RemainingSeconds + " seconds left";
			CreateMap();
			DrawMap();
			DrawPlayer(true);
			timer.Start();
		}
		
		private void TimerOnTick(Object obj, EventArgs ea)
		{
			if (RemainingSeconds > 0) 
			{
				RemainingSeconds--;
				Form1.ActiveForm.Text = "Find the Egg! - Round " + CurrentRound + " (" + RemainingSeconds + " seconds left)";
			}
			else 
			{
				timer.Stop();
				Form1.ActiveForm.Close();
//				MessageBox.Show("You failed to find the egg!", "You lose!");
			}

		}



	}
}
