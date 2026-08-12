using System.Drawing.Drawing2D;
using System.Globalization;

namespace AppOneSpaceDashboard;

// ═══════════════════════════════════════════════════════════════════════════
// AppOne SpaceDashboard 1.0 — main window
// Earth's elliptical orbit + zodiac + Moon cycles + eclipses (WinForms port
// of the Ring language "Orbital" sample).
// ═══════════════════════════════════════════════════════════════════════════

public sealed class MainForm : Form
{
    const string AppTitle = "AppOne SpaceDashboard 1.0  —  Earth Elliptical Orbit - Zodiac + Moon + Eclipses";

    readonly TextBox dateInput;
    readonly Button btnGo, btnToday, btnAnimate;
    readonly System.Windows.Forms.Timer animTimer;
    readonly System.Windows.Forms.Timer liveTimer;

    DateTime current;
    bool isTestDate;
    DashState state;
    Bitmap sceneBmp;   // scene rendered at native 1880x1020, scaled to the window

    public MainForm()
    {
        Text = AppTitle;
        ClientSize = new Size(Renderer.W, Renderer.H);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(5, 5, 20);
        DoubleBuffered = true;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimumSize = new Size(940, 560);
        WindowState = FormWindowState.Maximized;
        ResizeRedraw = true;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        var lblDate = new Label
        {
            Text = "Date DD/MM/YYYY:",
            Bounds = new Rectangle(8, 78, 120, 14),
            ForeColor = Color.FromArgb(0x98, 0x90, 0xC0),
            BackColor = Color.FromArgb(5, 5, 20),
            Font = new Font("Segoe UI", 7f),
        };
        Controls.Add(lblDate);

        dateInput = new TextBox
        {
            Bounds = new Rectangle(8, 92, 118, 22),
            BackColor = Color.FromArgb(0xFF, 0xFF, 0xC0),
            ForeColor = Color.FromArgb(0x1A, 0x10, 0x20),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9f),
        };
        dateInput.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; OnDateEntered(); }
        };
        Controls.Add(dateInput);

        btnGo = MakeButton("Go", new Rectangle(130, 92, 34, 22), Color.FromArgb(0x00, 0xC8, 0xC0));
        btnGo.Click += (_, _) => OnDateEntered();
        Controls.Add(btnGo);

        btnToday = MakeButton("Today", new Rectangle(8, 118, 56, 22), Color.FromArgb(0x00, 0xA8, 0xA8));
        btnToday.Click += (_, _) => OnTodayClicked();
        Controls.Add(btnToday);

        btnAnimate = MakeButton("Animate", new Rectangle(68, 118, 58, 22), Color.FromArgb(0x00, 0xC8, 0xC0));
        btnAnimate.Click += (_, _) => OnAnimateClicked();
        Controls.Add(btnAnimate);

        animTimer = new System.Windows.Forms.Timer { Interval = 150 };
        animTimer.Tick += (_, _) =>
        {
            current = current.AddDays(1);
            isTestDate = true;
            dateInput.Text = current.ToString("dd/MM/yyyy");
            Recalc();
        };

        // Live clock: in "today" mode the whole diagram is recomputed each
        // second with the current time of day
        liveTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        liveTimer.Tick += (_, _) =>
        {
            if (!isTestDate && !animTimer.Enabled)
            {
                current = DateTime.Now;
                Recalc();
            }
        };
        liveTimer.Start();

        current = DateTime.Now;
        isTestDate = false;
        dateInput.Text = current.ToString("dd/MM/yyyy");
        Recalc();
    }

    static Button MakeButton(string text, Rectangle bounds, Color back)
    {
        var b = new Button
        {
            Text = text,
            Bounds = bounds,
            BackColor = back,
            ForeColor = Color.FromArgb(0x00, 0x18, 0x20),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
        };
        b.FlatAppearance.BorderColor = Color.FromArgb(0x00, 0x68, 0x68);
        b.FlatAppearance.BorderSize = 1;
        return b;
    }

    void Recalc()
    {
        double fracDay = isTestDate ? 0 : current.TimeOfDay.TotalDays;
        string timeStr = isTestDate ? "" : current.ToString("HH:mm:ss");
        state = Astro.Compute(current.Day, current.Month, current.Year, isTestDate, fracDay, timeStr);
        Text = AppTitle + "  [ " + state.TodayStr + " ]" + (isTestDate ? "  *** TEST DATE ***" : "");

        sceneBmp?.Dispose();
        sceneBmp = new Bitmap(Renderer.W, Renderer.H);
        using (var g = Graphics.FromImage(sceneBmp))
            new Renderer(g, state).Draw();

        Invalidate();
    }

    void OnDateEntered()
    {
        if (DateTime.TryParseExact(dateInput.Text.Trim(), "dd/MM/yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            current = d;
            isTestDate = d.Date != DateTime.Today;
            Recalc();
        }
        else
        {
            dateInput.Text = current.ToString("dd/MM/yyyy");
        }
    }

    void OnTodayClicked()
    {
        StopAnimation();
        current = DateTime.Now;
        isTestDate = false;
        dateInput.Text = current.ToString("dd/MM/yyyy");
        Recalc();
    }

    void OnAnimateClicked()
    {
        if (animTimer.Enabled) StopAnimation();
        else
        {
            btnAnimate.Text = "Stop";
            animTimer.Start();
        }
    }

    void StopAnimation()
    {
        animTimer.Stop();
        btnAnimate.Text = "Animate";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (sceneBmp == null) return;

        // Scale the native 1880x1020 scene to fit the window, keeping aspect ratio
        var g = e.Graphics;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        double scale = Math.Min((double)ClientSize.Width / Renderer.W,
                                (double)ClientSize.Height / Renderer.H);
        int dw = (int)(Renderer.W * scale);
        int dh = (int)(Renderer.H * scale);
        int ox = (ClientSize.Width - dw) / 2;
        int oy = (ClientSize.Height - dh) / 2;

        g.DrawImage(sceneBmp, new Rectangle(ox, oy, dw, dh));

        // Keep the date controls pinned to the scene's date-input area
        RepositionControls(scale, ox, oy);
    }

    void RepositionControls(double scale, int ox, int oy)
    {
        Point Map(int x, int y) => new((int)(x * scale) + ox, (int)(y * scale) + oy);

        var lblPos = Map(8, 78);
        var inPos = Map(8, 92);
        foreach (Control c in Controls)
        {
            if (c is Label) c.Location = lblPos;
        }
        dateInput.Location = inPos;
        btnGo.Location = Map(130, 92);
        btnToday.Location = Map(8, 118);
        btnAnimate.Location = Map(68, 118);
    }
}
