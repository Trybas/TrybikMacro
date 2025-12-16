using System;
using System.Drawing;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading;

namespace KoparkaMacro
{
    public partial class Form1 : Form
    {
        [DllImport("user32.dll")] static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(Keys vKey);
        [DllImport("user32.dll", SetLastError = true)] static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public InputUnion U; }
        [StructLayout(LayoutKind.Explicit)] struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
        [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        const uint INPUT_MOUSE = 0;
        const uint INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_KEYUP = 0x0002;
        const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        const uint MOUSEEVENTF_LEFTUP = 0x0004;
        const byte WKey = 0x57;

        bool running = false;
        bool leftHeld = false;
        bool pickaxePressed = false;
        System.Windows.Forms.Timer animationTimer;
        PictureBox imgLeft, imgRight;
        Label titleLabel, f5Label, statusLabel, kilofLabel, bottomLabel;
        Button[] pickaxeButtons = new Button[9];
        int pickaxeSlot = 1;
        Panel leftRGB, rightRGB, topRGB, bottomRGB;
        int rgbOffset = 0;

        public Form1()
        {
            InitializeComponent();
            this.Text = "KOPARKA BITKOJNUW";
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(700, 400);
            this.BackColor = Color.FromArgb(30, 30, 40);

            leftRGB = new Panel() { Size = new Size(5, ClientSize.Height), Location = new Point(0, 0) }; Controls.Add(leftRGB);
            rightRGB = new Panel() { Size = new Size(5, ClientSize.Height), Location = new Point(ClientSize.Width - 5, 0) }; Controls.Add(rightRGB);
            topRGB = new Panel() { Size = new Size(ClientSize.Width, 5), Location = new Point(0, 0) }; Controls.Add(topRGB);
            bottomRGB = new Panel() { Size = new Size(ClientSize.Width, 5), Location = new Point(0, ClientSize.Height - 5) }; Controls.Add(bottomRGB);

            titleLabel = new Label() { Text = "koparka donatuw trybika i susiego", Font = new Font("Arial", 16, FontStyle.Bold), AutoSize = true, ForeColor = Color.White };
            titleLabel.Location = new Point((ClientSize.Width - titleLabel.PreferredWidth) / 2, 30); Controls.Add(titleLabel);

            f5Label = new Label() { Text = "F5 start/stop", Font = new Font("Arial", 10), AutoSize = true, ForeColor = Color.LightGray };
            f5Label.Location = new Point((ClientSize.Width - f5Label.PreferredWidth) / 2, 70); Controls.Add(f5Label);

            statusLabel = new Label() { Text = "Status - Wyłączony", Font = new Font("Arial", 12, FontStyle.Bold), AutoSize = true, ForeColor = Color.LightGray };
            statusLabel.Location = new Point((ClientSize.Width - statusLabel.PreferredWidth) / 2, 95); Controls.Add(statusLabel);

            imgLeft = new PictureBox() { Size = new Size(64, 64), Location = new Point(titleLabel.Left - 70, titleLabel.Top - 10), SizeMode = PictureBoxSizeMode.StretchImage };
            imgRight = new PictureBox() { Size = new Size(64, 64), Location = new Point(titleLabel.Right + 10, titleLabel.Top - 10), SizeMode = PictureBoxSizeMode.StretchImage };

            using (WebClient wc = new WebClient())
            {
                byte[] bytes = wc.DownloadData("https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcROdxAwNzPCuWwknmyZEKHT6plpH2X4O0oGaA&s"); 
                using (var ms = new System.IO.MemoryStream(bytes))
                {
                    imgLeft.Image = Image.FromStream(ms);
                    imgRight.Image = Image.FromStream(ms);
                }
            }

            Controls.Add(imgLeft);
            Controls.Add(imgRight);

            kilofLabel = new Label() { Text = "Kilof:", Font = new Font("Arial", 10, FontStyle.Bold), AutoSize = true, ForeColor = Color.White };
            kilofLabel.Location = new Point((ClientSize.Width - 9 * 35) / 2, 130); Controls.Add(kilofLabel);

            bottomLabel = new Label() { Text = "Pamiętaj żeby bindy paska mieć od jeden do dziewięć bo nie będzie działać", AutoSize = true, ForeColor = Color.LightGray };
            bottomLabel.Location = new Point((ClientSize.Width - bottomLabel.PreferredWidth) / 2, ClientSize.Height - 30); Controls.Add(bottomLabel);

            for (int i = 0; i < 9; i++)
            {
                Button pb = new Button() { Text = (i + 1).ToString(), Size = new Size(30, 30), Location = new Point((ClientSize.Width - 9 * 35) / 2 + i * 35, 155), BackColor = Color.FromArgb(50, 50, 60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                int pidx = i + 1; pb.Click += (s, e) => { pickaxeSlot = pidx; UpdatePickaxeHighlight(); }; Controls.Add(pb); pickaxeButtons[i] = pb;
            }

            UpdatePickaxeHighlight();

            animationTimer = new System.Windows.Forms.Timer() { Interval = 20 }; animationTimer.Tick += AnimationTick; animationTimer.Start();

            Thread globalHotkey = new Thread(() => { 
                while (true) { 
                    if ((GetAsyncKeyState(Keys.F5) & 0x8000) != 0) { 
                        ToggleMacro(); 
                        Thread.Sleep(200); 
                    } 
                    Thread.Sleep(10); 
                } 
            }) { IsBackground = true };
            globalHotkey.Start();

            Thread macroThread = new Thread(() =>
            {
                while (true)
                {
                    if (running)
                    {
                        if (!pickaxePressed)
                        {
                            KeyPressSim((ushort)(0x30 + pickaxeSlot));
                            LeftDown(); leftHeld = true;
                            keybd_event(WKey, 0, 0, 0);
                            pickaxePressed = true;
                        }
                    }
                    else
                    {
                        if (leftHeld) { LeftUp(); leftHeld = false; }
                        if (pickaxePressed) { keybd_event(WKey, 0, 2, 0); pickaxePressed = false; }
                    }
                    Thread.Sleep(10);
                }
            }) { IsBackground = true };
            macroThread.Start();
        }

        void AnimationTick(object? sender, EventArgs e)
        {
            rgbOffset += 2;
            Color rgbColor = ColorFromHSV(rgbOffset % 360, 1, 1);
            leftRGB.BackColor = rgbColor; rightRGB.BackColor = rgbColor;
            topRGB.BackColor = rgbColor; bottomRGB.BackColor = rgbColor;
        }

        Color ColorFromHSV(double hue, double sat, double val)
        {
            int hi = (int)(hue / 60) % 6; double f = hue / 60 - hi; val *= 255; int v = (int)val; int p = (int)(val * (1 - sat)); int q = (int)(val * (1 - f * sat)); int t = (int)(val * (1 - (1 - f) * sat));
            switch (hi) { case 0: return Color.FromArgb(v, t, p); case 1: return Color.FromArgb(q, v, p); case 2: return Color.FromArgb(p, v, t); case 3: return Color.FromArgb(p, q, v); case 4: return Color.FromArgb(t, p, v); default: return Color.FromArgb(v, p, q); }
        }

        void ToggleMacro() { running = !running; statusLabel.Text = running ? "Status - Włączony" : "Status - Wyłączony"; }

        void KeyPressSim(ushort key)
        {
            INPUT[] i = new INPUT[1];
            i[0].type = INPUT_KEYBOARD;
            i[0].U.ki.wVk = key;
            SendInput(1, i, checked((int)Marshal.SizeOf(typeof(INPUT))));
            Thread.Sleep(50);
            i[0].U.ki.dwFlags = KEYEVENTF_KEYUP;
            SendInput(1, i, checked((int)Marshal.SizeOf(typeof(INPUT))));
        }

        void LeftDown()
        {
            INPUT[] i = new INPUT[1];
            i[0].type = INPUT_MOUSE;
            i[0].U.mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
            SendInput(1, i, checked((int)Marshal.SizeOf(typeof(INPUT))));
        }

        void LeftUp()
        {
            INPUT[] i = new INPUT[1];
            i[0].type = INPUT_MOUSE;
            i[0].U.mi.dwFlags = MOUSEEVENTF_LEFTUP;
            SendInput(1, i, checked((int)Marshal.SizeOf(typeof(INPUT))));
        }

        void UpdatePickaxeHighlight() { for (int i = 0; i < 9; i++) pickaxeButtons[i].BackColor = (i + 1 == pickaxeSlot) ? Color.Lime : Color.FromArgb(50, 50, 60); }
    }
}