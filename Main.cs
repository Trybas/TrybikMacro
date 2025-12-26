using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Diagnostics;

namespace Macro
{
    public partial class Main : Form
    {
        [DllImport("user32.dll")] static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(Keys vKey);
        [DllImport("user32.dll", SetLastError = true)] static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);
        [DllImport("user32.dll")] static extern IntPtr SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
        [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);
        
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_SHOWWINDOW = 0x0040;
        const uint SWP_NOACTIVATE = 0x0010;

        const byte WKey = 0x57;

        [StructLayout(LayoutKind.Sequential)] 
        struct INPUT 
        { 
            public uint type; 
            public InputUnion U; 
        }
        
        [StructLayout(LayoutKind.Explicit)] 
        struct InputUnion 
        { 
            [FieldOffset(0)] public MOUSEINPUT mi; 
            [FieldOffset(0)] public KEYBDINPUT ki; 
        }
        
        [StructLayout(LayoutKind.Sequential)] 
        struct MOUSEINPUT 
        { 
            public int dx; 
            public int dy; 
            public uint mouseData; 
            public uint dwFlags; 
            public uint time; 
            public IntPtr dwExtraInfo; 
        }
        
        [StructLayout(LayoutKind.Sequential)] 
        struct KEYBDINPUT 
        { 
            public ushort wVk; 
            public ushort wScan; 
            public uint dwFlags; 
            public uint time; 
            public IntPtr dwExtraInfo; 
        }

        const uint INPUT_MOUSE = 0;
        const uint INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_KEYUP = 0x0002;
        const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        const uint MOUSEEVENTF_LEFTUP = 0x0004;
        static double EaseInOutQuad(double t) => t < 0.5 ? 2 * t * t : -1 + (4 - 2 * t) * t;

        static readonly Color BG_COLOR = Color.FromArgb(12, 15, 22);
        static readonly Color TAB_BG = Color.FromArgb(15, 18, 26);
        static readonly Color TAB_ACTIVE_BG = Color.FromArgb(12, 15, 22);
        static readonly Color BLUE_TEXT = Color.FromArgb(100, 140, 255);
        static readonly Color GRAY_TEXT = Color.Gray;
        static readonly Color STATUS_ENABLED = Color.FromArgb(80, 180, 255);
        static readonly Color STATUS_DISABLED = Color.FromArgb(140, 160, 200);
        static readonly Color PICKAXE_ACTIVE = Color.FromArgb(50, 120, 220);
        static readonly Color PICKAXE_INACTIVE = Color.FromArgb(20, 28, 42);
        static readonly Color BORDER_ACTIVE = Color.FromArgb(80, 150, 255);
        static readonly Color BORDER_INACTIVE = Color.FromArgb(30, 42, 65);

        bool running = false;
        bool leftHeld = false;
        bool pickaxePressed = false;
        Label statusLabel, kilofLabel, bottomLabel, hotkeyLabel, hintLabel, alwaysOnTopNoteLabel;
        CheckBox alwaysOnTopCheckBox;
        Button[] pickaxeButtons = new Button[9];
        int pickaxeSlot = 1;
        Panel topBar, tabPanel, contentPanel;
        Button mainTab, settingsTab, authorsTab;
        Panel mainContent, settingsContent, authorsContent;
        Keys toggleKey = Keys.F6;
        bool isRecordingHotkey = false;
        string currentLanguage = "en";
        System.Windows.Forms.Timer topMostTimer;
        int currentTab = 0;
        bool shouldBeTopMost = false;
        bool isAlwaysOnTop = false;
        
        Dictionary<string, Dictionary<string, string>> translations = new Dictionary<string, Dictionary<string, string>>()
        {
            { "alwaysOnTop", new Dictionary<string, string> { { "en", "Always on top" }, { "pl", "Zawsze na wierzchu" } } },
            { "alwaysOnTopNote", new Dictionary<string, string> { { "en", "(Doesn't work over Minecraft)" }, { "pl", "(Nie działa nad Minecraftem)" } } },
            { "hotkey", new Dictionary<string, string> { { "en", "Hotkey" }, { "pl", "Skrót" } } },
            { "pressKey", new Dictionary<string, string> { { "en", "Press a key..." }, { "pl", "Naciśnij klawisz..." } } },
            { "clickToChange", new Dictionary<string, string> { { "en", "(Click to change)" }, { "pl", "(Kliknij aby zmienić)" } } },
            { "disabled", new Dictionary<string, string> { { "en", "DISABLED" }, { "pl", "WYŁĄCZONY" } } },
            { "enabled", new Dictionary<string, string> { { "en", "ENABLED" }, { "pl", "WŁĄCZONY" } } },
            { "pickaxeSlot", new Dictionary<string, string> { { "en", "Pickaxe slot" }, { "pl", "Slot kilofa" } } },
            { "bottomText", new Dictionary<string, string> { { "en", "Make sure hotbar keybinds are 1-9" }, { "pl", "Upewnij się, że bindy paska to 1-9" } } },
            { "mainTab", new Dictionary<string, string> { { "en", "Main" }, { "pl", "Główne" } } },
            { "settingsTab", new Dictionary<string, string> { { "en", "Settings" }, { "pl", "Ustawienia" } } },
            { "authorsTab", new Dictionary<string, string> { { "en", "Authors" }, { "pl", "Autorzy" } } },
            { "notReady", new Dictionary<string, string> { { "en", "Not ready" }, { "pl", "Nie gotowe" } } },
            { "programmer", new Dictionary<string, string> { { "en", "Programmer" }, { "pl", "Programista" } } },
            { "ideaGiver", new Dictionary<string, string> { { "en", "Idea Giver" }, { "pl", "Pomysłodawca" } } },
            { "githubProfile", new Dictionary<string, string> { { "en", "GitHub Profile" }, { "pl", "Profil GitHub" } } }
        };

        public Main()
        {
            InitializeComponent();
            
            this.Opacity = 0;
            
            this.Shown += (s, e) => AnimateFadeIn();
            
            this.Text = "TrybikMacro";
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(600, 420);
            this.BackColor = BG_COLOR;
            this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, this.Width, this.Height, 15, 15));

            topBar = new Panel() 
            { 
                Size = new Size(this.ClientSize.Width, 40), 
                Location = new Point(0, 0), 
                BackColor = Color.FromArgb(18, 22, 32) 
            };
            this.Controls.Add(topBar);

            Label topBarTitle = new Label() 
            { 
                Text = "TrybikMacro", 
                Font = new Font("Segoe UI", 12, FontStyle.Bold), 
                AutoSize = true, 
                ForeColor = Color.FromArgb(220, 230, 255),
                Location = new Point(16, 11)
            };
            topBar.Controls.Add(topBarTitle);

            Button langBtn = new Button()
            {
                Text = "EN",
                Size = new Size(40, 26),
                Location = new Point(topBar.Width - 165, 7),
                BackColor = Color.FromArgb(35, 45, 65),
                ForeColor = Color.FromArgb(180, 200, 240),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            langBtn.FlatAppearance.BorderSize = 0;
            langBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(45, 55, 75);
            langBtn.Click += (s, e) =>
            {
                currentLanguage = currentLanguage == "en" ? "pl" : "en";
                langBtn.Text = currentLanguage.ToUpper();
                UpdateLanguage();
            };
            topBar.Controls.Add(langBtn);

            Button closeBtn = new Button() 
            { 
                Text = "✕", 
                Size = new Size(40, 40), 
                Location = new Point(topBar.Width - 40, 0),
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(180, 190, 210),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 13),
                Cursor = Cursors.Hand
            };
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 60, 80);
            closeBtn.Click += (s, e) => Application.Exit();
            topBar.Controls.Add(closeBtn);

            Button minBtn = new Button() 
            { 
                Text = "─", 
                Size = new Size(40, 40), 
                Location = new Point(topBar.Width - 80, 0),
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(180, 190, 210),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11),
                Cursor = Cursors.Hand
            };
            minBtn.FlatAppearance.BorderSize = 0;
            minBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 35, 48);
            minBtn.Click += (s, e) => AnimateMinimize();
            topBar.Controls.Add(minBtn);

            bool dragging = false;
            Point dragCursor = Point.Empty;
            Point dragForm = Point.Empty;
            
            topBar.MouseDown += (s, e) => 
            { 
                dragging = true; 
                dragCursor = Cursor.Position; 
                dragForm = this.Location; 
            };
            
            topBar.MouseMove += (s, e) => 
            { 
                if (dragging) 
                { 
                    Point diff = Point.Subtract(Cursor.Position, new Size(dragCursor)); 
                    this.Location = Point.Add(dragForm, new Size(diff)); 
                } 
            };
            
            topBar.MouseUp += (s, e) => { dragging = false; };

            tabPanel = new Panel()
            {
                Size = new Size(this.ClientSize.Width, 45),
                Location = new Point(0, 40),
                BackColor = TAB_BG
            };
            this.Controls.Add(tabPanel);

            mainTab = CreateTab(translations["mainTab"][currentLanguage], 0);
            settingsTab = CreateTab(translations["settingsTab"][currentLanguage], 1);
            authorsTab = CreateTab(translations["authorsTab"][currentLanguage], 2);

            mainTab.Click += (s, e) => SwitchTab(0);
            settingsTab.Click += (s, e) => SwitchTab(1);
            authorsTab.Click += (s, e) => SwitchTab(2);

            tabPanel.Controls.Add(mainTab);
            tabPanel.Controls.Add(settingsTab);
            tabPanel.Controls.Add(authorsTab);

            contentPanel = new Panel()
            {
                Size = new Size(this.ClientSize.Width, this.ClientSize.Height - 85),
                Location = new Point(0, 85),
                BackColor = BG_COLOR
            };
            this.Controls.Add(contentPanel);

            CreateMainContent();
            CreatePlaceholderContent(settingsContent = new Panel() { Size = new Size(contentPanel.Width, contentPanel.Height), Location = new Point(0, 0), BackColor = BG_COLOR, Visible = false }, "settings");
            CreateAuthorsContent(authorsContent = new Panel() { Size = new Size(contentPanel.Width, contentPanel.Height), Location = new Point(0, 0), BackColor = BG_COLOR, Visible = false });

            contentPanel.Controls.Add(settingsContent);
            contentPanel.Controls.Add(authorsContent);

            SwitchTab(0);

            topMostTimer = new System.Windows.Forms.Timer();
            topMostTimer.Interval = 50;
            topMostTimer.Tick += (s, e) =>
            {
                if (shouldBeTopMost)
                {
                    SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
                }
            };
            topMostTimer.Start();

            Thread globalHotkey = new Thread(() => 
            { 
                while (true) 
                { 
                    if (isRecordingHotkey)
                    {
                        for (Keys k = Keys.A; k <= Keys.Z; k++)
                        {
                            if ((GetAsyncKeyState(k) & 0x8000) != 0)
                            {
                                toggleKey = k;
                                isRecordingHotkey = false;
                                this.Invoke(new Action(() => 
                                {
                                    toggleKey = k;
                                    isRecordingHotkey = false;
                                    UpdateHotkeyDisplay();
                                }));
                                Thread.Sleep(200);
                                break;
                            }
                        }
                        for (Keys k = Keys.F1; k <= Keys.F12; k++)
                        {
                            if ((GetAsyncKeyState(k) & 0x8000) != 0)
                            {
                                toggleKey = k;
                                isRecordingHotkey = false;
                                this.Invoke(new Action(() => 
                                {
                                    toggleKey = k;
                                    isRecordingHotkey = false;
                                    UpdateHotkeyDisplay();
                                }));
                                Thread.Sleep(200);
                                break;
                            }
                        }
                    }
                    else if ((GetAsyncKeyState(toggleKey) & 0x8000) != 0) 
                    { 
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
                            LeftDown(); 
                            leftHeld = true;
                            keybd_event(WKey, 0, 0, 0);
                            pickaxePressed = true;
                        }
                    }
                    else
                    {
                        if (leftHeld) 
                        { 
                            LeftUp(); 
                            leftHeld = false; 
                        }
                        if (pickaxePressed) 
                        { 
                            keybd_event(WKey, 0, 2, 0); 
                            pickaxePressed = false; 
                        }
                    }
                    Thread.Sleep(10);
                }
            }) { IsBackground = true };
            macroThread.Start();
        }

        void AnimateFadeIn()
        {
            System.Windows.Forms.Timer fadeInTimer = new System.Windows.Forms.Timer();
            fadeInTimer.Interval = 20;
            int fadeStep = 0;
            int fadeTotal = 20;
            fadeInTimer.Tick += (se, ee) =>
            {
                fadeStep++;
                double progress = (double)fadeStep / fadeTotal;
                double eased = EaseInOutQuad(progress);
                this.Opacity = eased;
                if (fadeStep >= fadeTotal)
                {
                    this.Opacity = 1;
                    fadeInTimer.Stop();
                }
            };
            fadeInTimer.Start();
        }

        void AnimateMinimize()
        {
            System.Windows.Forms.Timer minTimer = new System.Windows.Forms.Timer();
            minTimer.Interval = 20;
            int minStep = 0;
            int minTotal = 10;
            minTimer.Tick += (se, ee) =>
            {
                minStep++;
                double progress = (double)minStep / minTotal;
                double eased = EaseInOutQuad(progress);
                this.Opacity = 1 - eased;
                if (minStep >= minTotal)
                {
                    minTimer.Stop();
                    this.WindowState = FormWindowState.Minimized;
                    this.Opacity = 1;
                }
            };
            minTimer.Start();
        }

        void SwitchTab(int tabIndex)
        {
            if (tabIndex == currentTab) return;
            
            mainTab.BackColor = TAB_BG;
            settingsTab.BackColor = TAB_BG;
            authorsTab.BackColor = TAB_BG;
            
            mainTab.ForeColor = Color.FromArgb(140, 160, 200);
            settingsTab.ForeColor = Color.FromArgb(140, 160, 200);
            authorsTab.ForeColor = Color.FromArgb(140, 160, 200);

            switch (tabIndex)
            {
                case 0:
                    mainTab.BackColor = TAB_ACTIVE_BG;
                    mainTab.ForeColor = BLUE_TEXT;
                    break;
                case 1:
                    settingsTab.BackColor = TAB_ACTIVE_BG;
                    settingsTab.ForeColor = BLUE_TEXT;
                    break;
                case 2:
                    authorsTab.BackColor = TAB_ACTIVE_BG;
                    authorsTab.ForeColor = BLUE_TEXT;
                    break;
            }
            
            Panel currentPanel = GetPanel(currentTab);
            Panel newPanel = GetPanel(tabIndex);
            
            System.Windows.Forms.Timer slideTimer = new System.Windows.Forms.Timer();
            slideTimer.Interval = 10;
            int slideStep = 0;
            int slideTotal = 20;
            int direction = tabIndex > currentTab ? -1 : 1;
            
            slideTimer.Tick += (s, e) =>
            {
                slideStep++;
                double progress = (double)slideStep / slideTotal;
                double eased = EaseInOutQuad(progress);
                int offset = (int)(600 * eased * direction);
                currentPanel.Location = new Point(offset, 0);
                newPanel.Location = new Point((direction == -1 ? 600 : -600) + offset, 0);
                newPanel.Visible = true;
                
                if (slideStep >= slideTotal)
                {
                    slideTimer.Stop();
                    currentPanel.Visible = false;
                    currentPanel.Location = new Point(0, 0);
                    newPanel.Location = new Point(0, 0);
                    currentTab = tabIndex;
                }
            };
            slideTimer.Start();
        }

        void CreateMainContent()
        {
            mainContent = new Panel()
            {
                Size = new Size(contentPanel.Width, contentPanel.Height),
                Location = new Point(0, 0),
                BackColor = BG_COLOR
            };
            contentPanel.Controls.Add(mainContent);

            alwaysOnTopCheckBox = new CheckBox()
            {
                Text = translations["alwaysOnTop"][currentLanguage],
                Font = new Font("Segoe UI", 9),
                AutoSize = true,
                ForeColor = BLUE_TEXT,
                Location = new Point(20, 20),
                Checked = isAlwaysOnTop
            };
            alwaysOnTopCheckBox.CheckedChanged += (s, e) =>
            {
                isAlwaysOnTop = alwaysOnTopCheckBox.Checked;
                shouldBeTopMost = isAlwaysOnTop;
                if (shouldBeTopMost)
                {
                    this.TopMost = true;
                    SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                }
                else
                {
                    this.TopMost = false;
                    shouldBeTopMost = false;
                    SetWindowPos(this.Handle, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                }
            };
            mainContent.Controls.Add(alwaysOnTopCheckBox);

            alwaysOnTopNoteLabel = new Label() 
            { 
                Text = translations["alwaysOnTopNote"][currentLanguage], 
                Font = new Font("Segoe UI", 9), 
                AutoSize = true, 
                ForeColor = GRAY_TEXT,
                Location = new Point(20, 20)
            };
            mainContent.Controls.Add(alwaysOnTopNoteLabel);

            UpdateAlwaysOnTopDisplay();

            hotkeyLabel = new Label() 
            { 
                Text = "", 
                Font = new Font("Segoe UI", 9), 
                AutoSize = true, 
                ForeColor = BLUE_TEXT,
                Cursor = Cursors.Hand,
                Location = new Point(0, 22)
            };
            mainContent.Controls.Add(hotkeyLabel);

            hintLabel = new Label() 
            { 
                Text = "", 
                Font = new Font("Segoe UI", 9), 
                AutoSize = true, 
                ForeColor = GRAY_TEXT,
                Cursor = Cursors.Hand,
                Location = new Point(0, 22)
            };
            mainContent.Controls.Add(hintLabel);

            UpdateHotkeyDisplay();

            hotkeyLabel.Click += (s, e) => 
            {
                if (!isRecordingHotkey)
                {
                    isRecordingHotkey = true;
                    UpdateHotkeyDisplay();
                }
            };

            hintLabel.Click += (s, e) => 
            {
                if (!isRecordingHotkey)
                {
                    isRecordingHotkey = true;
                    UpdateHotkeyDisplay();
                }
            };

            statusLabel = new Label() 
            { 
                Text = translations["disabled"][currentLanguage], 
                Font = new Font("Segoe UI", 28, FontStyle.Bold), 
                AutoSize = true, 
                ForeColor = STATUS_DISABLED,
                TextAlign = ContentAlignment.MiddleCenter,
                Tag = "status"
            };
            int statusTextWidth = TextRenderer.MeasureText(statusLabel.Text, statusLabel.Font).Width;
            statusLabel.Location = new Point((mainContent.Width - statusTextWidth) / 2, 58);
            mainContent.Controls.Add(statusLabel);

            kilofLabel = new Label() 
            { 
                Text = translations["pickaxeSlot"][currentLanguage], 
                Font = new Font("Segoe UI", 10, FontStyle.Regular), 
                AutoSize = true, 
                ForeColor = Color.FromArgb(140, 160, 200),
                Tag = "pickaxeSlot"
            };
            kilofLabel.Location = new Point((mainContent.Width - 9 * 52) / 2, 130);
            mainContent.Controls.Add(kilofLabel);

            for (int i = 0; i < 9; i++)
            {
                Button pb = new Button() 
                { 
                    Text = (i + 1).ToString(), 
                    Size = new Size(48, 48), 
                    Location = new Point((mainContent.Width - 9 * 52) / 2 + i * 52, 160), 
                    BackColor = PICKAXE_INACTIVE, 
                    ForeColor = Color.FromArgb(180, 200, 240), 
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 13, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                pb.FlatAppearance.BorderSize = 2;
                pb.FlatAppearance.BorderColor = BORDER_INACTIVE;
                pb.FlatAppearance.MouseOverBackColor = Color.FromArgb(28, 38, 58);
                
                int pidx = i + 1; 
                pb.Click += (s, e) => 
                { 
                    pickaxeSlot = pidx; 
                    UpdatePickaxeHighlight(); 
                }; 
                
                mainContent.Controls.Add(pb); 
                pickaxeButtons[i] = pb;
            }

            UpdatePickaxeHighlight();

            bottomLabel = new Label() 
            { 
                Text = translations["bottomText"][currentLanguage], 
                AutoSize = true, 
                ForeColor = Color.FromArgb(80, 95, 125),
                Font = new Font("Segoe UI", 9),
                Tag = "bottomText"
            };
            int bottomTextWidth = TextRenderer.MeasureText(bottomLabel.Text, bottomLabel.Font).Width;
            bottomLabel.Location = new Point((mainContent.Width - bottomTextWidth) / 2, mainContent.Height - 30);
            mainContent.Controls.Add(bottomLabel);
        }

        void CreatePlaceholderContent(Panel panel, string tag)
        {
            contentPanel.Controls.Add(panel);

            Label notReadyLabel = new Label()
            {
                Text = translations["notReady"][currentLanguage].ToUpper(),
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 120, 160),
                Tag = tag
            };
            int notReadyTextWidth = TextRenderer.MeasureText(notReadyLabel.Text, notReadyLabel.Font).Width;
            int notReadyTextHeight = TextRenderer.MeasureText(notReadyLabel.Text, notReadyLabel.Font).Height;
            notReadyLabel.Location = new Point((panel.Width - notReadyTextWidth) / 2, (panel.Height - notReadyTextHeight) / 2);
            panel.Controls.Add(notReadyLabel);
        }

        void CreateAuthorsContent(Panel panel)
        {
            contentPanel.Controls.Add(panel);

            PictureBox programmerPic = new PictureBox()
            {
                Size = new Size(64, 64),
                Location = new Point((panel.Width - 300) / 2, 50),
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            try
            {
                using (var client = new HttpClient())
                {
                    var task = client.GetByteArrayAsync("https://avatars.githubusercontent.com/u/181464721?v=4&size=64");
                    task.Wait();
                    var imageBytes = task.Result;
                    using (var ms = new System.IO.MemoryStream(imageBytes))
                    {
                        var tempImage = Image.FromStream(ms);
                        programmerPic.Image = new Bitmap(tempImage);
                        tempImage.Dispose();
                    }
                }
            }
            catch
            {
                programmerPic.BackColor = Color.Gray;
            }
            panel.Controls.Add(programmerPic);

            Label programmerName = new Label()
            {
                Text = "Trybas",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                AutoSize = true,
                ForeColor = BLUE_TEXT,
                Location = new Point(programmerPic.Right + 20, programmerPic.Top + 10)
            };
            panel.Controls.Add(programmerName);

            Label programmerRole = new Label()
            {
                Text = translations["programmer"][currentLanguage],
                Font = new Font("Segoe UI", 10),
                AutoSize = true,
                ForeColor = GRAY_TEXT,
                Location = new Point(programmerPic.Right + 20, programmerName.Bottom + 5),
                Tag = "programmer"
            };
            panel.Controls.Add(programmerRole);

            LinkLabel programmerLink = new LinkLabel()
            {
                Text = translations["githubProfile"][currentLanguage],
                Font = new Font("Segoe UI", 10),
                AutoSize = true,
                ForeColor = BLUE_TEXT,
                LinkColor = BLUE_TEXT,
                ActiveLinkColor = Color.FromArgb(150, 180, 255),
                Location = new Point(programmerRole.Right + 10, programmerPic.Top + programmerPic.Height / 2 - 8),
                Tag = "githubProfile"
            };
            programmerLink.LinkClicked += (s, e) => System.Diagnostics.Process.Start(new ProcessStartInfo { FileName = "https://github.com/Trybas", UseShellExecute = true });
            panel.Controls.Add(programmerLink);

            Label ideaGiverQuestion = new Label()
            {
                Text = "?",
                Font = new Font("Segoe UI", 48, FontStyle.Bold),
                AutoSize = true,
                ForeColor = GRAY_TEXT,
                Location = new Point((panel.Width - 300) / 2, programmerPic.Bottom + 50)
            };
            panel.Controls.Add(ideaGiverQuestion);

            Label ideaGiverName = new Label()
            {
                Text = "Susek",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                AutoSize = true,
                ForeColor = BLUE_TEXT,
                Location = new Point(ideaGiverQuestion.Right + 20, ideaGiverQuestion.Top + 10)
            };
            panel.Controls.Add(ideaGiverName);

            Label ideaGiverRole = new Label()
            {
                Text = translations["ideaGiver"][currentLanguage],
                Font = new Font("Segoe UI", 10),
                AutoSize = true,
                ForeColor = GRAY_TEXT,
                Location = new Point(ideaGiverQuestion.Right + 20, ideaGiverName.Bottom + 5),
                Tag = "ideaGiver"
            };
            panel.Controls.Add(ideaGiverRole);
        }

        void UpdateLanguage()
        {
            if (currentLanguage == null) currentLanguage = "en";
            if (translations == null) return;
            if (translations.ContainsKey("mainTab") && translations["mainTab"].ContainsKey(currentLanguage))
                mainTab.Text = translations["mainTab"][currentLanguage];
            if (translations.ContainsKey("settingsTab") && translations["settingsTab"].ContainsKey(currentLanguage))
                settingsTab.Text = translations["settingsTab"][currentLanguage];
            if (translations.ContainsKey("authorsTab") && translations["authorsTab"].ContainsKey(currentLanguage))
                authorsTab.Text = translations["authorsTab"][currentLanguage];

            foreach (Control ctrl in mainContent.Controls)
            {
                if (ctrl.Tag != null)
                {
                    if (ctrl.Tag.ToString() == "status")
                    {
                        string statusKey = running ? "enabled" : "disabled";
                        if (translations.ContainsKey(statusKey) && translations[statusKey].ContainsKey(currentLanguage))
                        {
                            ctrl.Text = translations[statusKey][currentLanguage];
                            int textWidth = TextRenderer.MeasureText(ctrl.Text, ctrl.Font).Width;
                            ctrl.Location = new Point((mainContent.Width - textWidth) / 2, 58);
                        }
                    }
                    else if (translations.ContainsKey(ctrl.Tag.ToString()) && translations[ctrl.Tag.ToString()].ContainsKey(currentLanguage))
                    {
                        ctrl.Text = translations[ctrl.Tag.ToString()][currentLanguage];
                        if (ctrl is Label && ctrl.Tag.ToString() == "bottomText")
                        {
                            int textWidth = TextRenderer.MeasureText(ctrl.Text, ctrl.Font).Width;
                            ctrl.Location = new Point((mainContent.Width - textWidth) / 2, mainContent.Height - 30);
                        }
                    }
                }
            }

            UpdateHotkeyDisplay();
            UpdateAlwaysOnTopDisplay();

            foreach (Panel panel in new[] { settingsContent, authorsContent })
            {
                if (panel != null)
                {
                    foreach (Control ctrl in panel.Controls)
                    {
                        if (ctrl != null && ctrl.Tag != null)
                        {
                            string tag = ctrl.Tag.ToString();
                            if (tag == "settings" || tag == "authors")
                            {
                                if (translations.ContainsKey("notReady") && translations["notReady"].ContainsKey(currentLanguage))
                                {
                                    ctrl.Text = translations["notReady"][currentLanguage].ToUpper();
                                    int textWidth = TextRenderer.MeasureText(ctrl.Text, ctrl.Font).Width;
                                    int textHeight = TextRenderer.MeasureText(ctrl.Text, ctrl.Font).Height;
                                    ctrl.Location = new Point((panel.Width - textWidth) / 2, (panel.Height - textHeight) / 2);
                                }
                            }
                            else if (tag == "programmer" || tag == "ideaGiver" || tag == "githubProfile")
                            {
                                if (translations.ContainsKey(tag) && translations[tag].ContainsKey(currentLanguage))
                                {
                                    ctrl.Text = translations[tag][currentLanguage];
                                }
                            }
                        }
                    }
                }
            }
        }

        void ToggleMacro() 
        { 
            running = !running; 
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => 
                {
                    statusLabel.Text = running ? translations["enabled"][currentLanguage] : translations["disabled"][currentLanguage];
                    statusLabel.ForeColor = running ? STATUS_ENABLED : STATUS_DISABLED;
                    statusLabel.Location = new Point((mainContent.Width - statusLabel.PreferredWidth) / 2, 58);
                }));
            }
            else
            {
                statusLabel.Text = running ? translations["enabled"][currentLanguage] : translations["disabled"][currentLanguage];
                statusLabel.ForeColor = running ? STATUS_ENABLED : STATUS_DISABLED;
                statusLabel.Location = new Point((mainContent.Width - statusLabel.PreferredWidth) / 2, 58);
            }
        }

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

        void UpdatePickaxeHighlight() 
        { 
            for (int i = 0; i < 9; i++)
            {
                if (i + 1 == pickaxeSlot)
                {
                    pickaxeButtons[i].BackColor = PICKAXE_ACTIVE;
                    pickaxeButtons[i].ForeColor = Color.White;
                    pickaxeButtons[i].FlatAppearance.BorderColor = BORDER_ACTIVE;
                }
                else
                {
                    pickaxeButtons[i].BackColor = PICKAXE_INACTIVE;
                    pickaxeButtons[i].ForeColor = Color.FromArgb(180, 200, 240);
                    pickaxeButtons[i].FlatAppearance.BorderColor = BORDER_INACTIVE;
                }
            }
        }

        void UpdateHotkeyDisplay()
        {
            if (translations == null) return;
            if (isRecordingHotkey)
            {
                if (translations.ContainsKey("pressKey") && translations["pressKey"].ContainsKey(currentLanguage))
                    hotkeyLabel.Text = translations["pressKey"][currentLanguage];
                if (translations.ContainsKey("clickToChange") && translations["clickToChange"].ContainsKey(currentLanguage))
                    hintLabel.Text = translations["clickToChange"][currentLanguage];
            }
            else
            {
                if (translations.ContainsKey("hotkey") && translations["hotkey"].ContainsKey(currentLanguage) &&
                    translations.ContainsKey("clickToChange") && translations["clickToChange"].ContainsKey(currentLanguage))
                {
                    hotkeyLabel.Text = string.Format("{0}: {1}", translations["hotkey"][currentLanguage], toggleKey.ToString());
                    hintLabel.Text = translations["clickToChange"][currentLanguage];
                }
            }
            int gap = 5;
            int hotkeyTextWidth = TextRenderer.MeasureText(hotkeyLabel.Text, hotkeyLabel.Font).Width;
            int hintTextWidth = TextRenderer.MeasureText(hintLabel.Text, hintLabel.Font).Width;
            int totalWidth = hotkeyTextWidth + gap + hintTextWidth;
            int minX = 20;
            int maxX = mainContent.Width - 20 - totalWidth;
            int startX = Math.Max(minX, Math.Min(maxX, mainContent.Width - 185));
            hotkeyLabel.Location = new Point(startX, 22);
            hintLabel.Location = new Point(startX + hotkeyTextWidth + gap, 22);
        }

        void UpdateAlwaysOnTopDisplay()
        {
            if (translations == null) return;
            if (translations.ContainsKey("alwaysOnTop") && translations["alwaysOnTop"].ContainsKey(currentLanguage))
                alwaysOnTopCheckBox.Text = translations["alwaysOnTop"][currentLanguage];
            alwaysOnTopCheckBox.Checked = isAlwaysOnTop;
            if (translations.ContainsKey("alwaysOnTopNote") && translations["alwaysOnTopNote"].ContainsKey(currentLanguage))
                alwaysOnTopNoteLabel.Text = translations["alwaysOnTopNote"][currentLanguage];
            alwaysOnTopNoteLabel.Location = new Point(20 + alwaysOnTopCheckBox.Width + 5, 20 + (alwaysOnTopCheckBox.Height - alwaysOnTopNoteLabel.Height) / 2);
        }

        Button CreateTab(string text, int index)
        {
            Button tab = new Button()
            {
                Text = text,
                Size = new Size(120, 45),
                Location = new Point(20 + index * 125, 0),
                BackColor = TAB_BG,
                ForeColor = Color.FromArgb(140, 160, 200),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            tab.FlatAppearance.BorderSize = 0;
            tab.FlatAppearance.MouseOverBackColor = Color.FromArgb(20, 25, 35);
            
            tab.Paint += (s, e) =>
            {
                System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath();
                int radius = 10;
                path.AddArc(0, 0, radius, radius, 180, 90);
                path.AddArc(tab.Width - radius, 0, radius, radius, 270, 90);
                path.AddLine(tab.Width, radius, tab.Width, tab.Height);
                path.AddLine(tab.Width, tab.Height, 0, tab.Height);
                path.AddLine(0, tab.Height, 0, radius);
                path.CloseFigure();
                tab.Region = new System.Drawing.Region(path);
            };
            
            return tab;
        }

        Panel GetPanel(int index)
        {
            switch (index)
            {
                case 0: return mainContent;
                case 1: return settingsContent;
                case 2: return authorsContent;
                default: return mainContent;
            }
        }
    }
}