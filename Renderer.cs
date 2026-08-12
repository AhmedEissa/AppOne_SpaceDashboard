using System.Drawing.Drawing2D;

namespace AppOneSpaceDashboard;

// ═══════════════════════════════════════════════════════════════════════════
// Renderer — GDI+ port of the Ring/Qt painting code (DrawScene + panels)
// Coordinates, colours and layout mirror the original qPainter code.
// ═══════════════════════════════════════════════════════════════════════════

public sealed class Renderer
{
    // ── Canvas geometry (from the Ring source) ──────────────────────────────
    public const int W = 1880, H = 1020;
    const int CX = 450, CY = 490;
    const double A = 320, B = 316;
    static readonly double C = Math.Sqrt(A * A - B * B);
    static readonly double SUN_X = CX, SUN_Y = CY + C;
    static readonly double FOCUS2_X = CX, FOCUS2_Y = CY - C;

    const double PI = Math.PI;
    const double RAD = PI / 180.0;

    // ── Colours ─────────────────────────────────────────────────────────────
    static Color Rgb(int r, int g, int b, int a = 255) => Color.FromArgb(a, r, g, b);

    static readonly Color ColBgDark  = Rgb(5, 5, 20);
    static readonly Color ColWhite   = Rgb(255, 255, 255);
    static readonly Color ColBlue    = Rgb(22, 90, 210);
    static readonly Color ColSkyBlue = Rgb(65, 145, 255, 215);
    static readonly Color ColGold    = Rgb(255, 190, 15);
    static readonly Color ColYellow  = Rgb(255, 215, 55);
    static readonly Color ColViolet  = Rgb(200, 192, 255, 230);
    static readonly Color ColGreen   = Rgb(135, 208, 135, 200);
    static readonly Color ColRingVio = Rgb(155, 135, 210, 140);
    static readonly Color ColTick    = Rgb(220, 210, 255, 240);
    static readonly Color ColSector  = Rgb(90, 90, 150, 65);
    static readonly Color ColEarthLb = Rgb(135, 190, 255);
    static readonly Color ColSunLbl  = Rgb(255, 222, 90);
    static readonly Color ColFocus   = Rgb(125, 115, 185, 155);
    static readonly Color ColTitle   = Rgb(220, 210, 255);
    static readonly Color ColSub     = Rgb(195, 195, 235, 240);
    static readonly Color ColSpring  = Rgb(0, 255, 100);
    static readonly Color ColSummer  = Rgb(255, 215, 45);
    static readonly Color ColAutumn  = Rgb(255, 135, 35);
    static readonly Color ColWinter  = Rgb(70, 195, 255);
    static readonly Color ColPeri    = Rgb(255, 100, 100);
    static readonly Color ColAph     = Rgb(100, 200, 255);
    static readonly Color ColToday   = Rgb(255, 255, 0);
    static readonly Color ColAngle   = Rgb(255, 210, 50, 220);
    static readonly Color ColMoonOrb = Rgb(180, 180, 220, 100);
    static readonly Color ColMoonLit = Rgb(240, 240, 250);
    static readonly Color ColMoonDrk = Rgb(35, 35, 55);
    static readonly Color ColMoonLbl = Rgb(225, 225, 250);

    static readonly Color ColPanel   = Rgb(8, 10, 30, 230);
    static readonly Color ColPanBdr  = Rgb(110, 100, 170, 200);
    static readonly Color ColPanTtl  = Rgb(235, 225, 255);
    static readonly Color ColPanSub  = Rgb(195, 195, 235, 240);
    static readonly Color ColPerigee = Rgb(255, 90, 90);
    static readonly Color ColApogee  = Rgb(90, 200, 255);
    static readonly Color ColAscNode = Rgb(80, 255, 140);
    static readonly Color ColDscNode = Rgb(255, 165, 50);
    static readonly Color ColEclip   = Rgb(60, 130, 255, 180);
    static readonly Color ColMoonPth = Rgb(200, 180, 255, 200);
    static readonly Color ColMoonPos = Rgb(235, 235, 248);
    static readonly Color ColSolarTot = Rgb(255, 200, 40);
    static readonly Color ColSolarAnn = Rgb(255, 140, 30);
    static readonly Color ColSolarPar = Rgb(255, 230, 140, 240);
    static readonly Color ColLunarTot = Rgb(255, 110, 100);
    static readonly Color ColLunarPar = Rgb(240, 155, 120);
    static readonly Color ColLunarPen = Rgb(220, 195, 170);
    static readonly Color ColToday2   = Rgb(80, 255, 160);
    static readonly Color ColIncLine  = Rgb(255, 210, 50, 220);

    // ── Fonts (pixel-sized: Qt pt at 96dpi ≈ pt*96/72 px, layout stays fixed) ─
    static Font F(string family, float qtPt, bool bold = false, bool italic = false)
    {
        var style = (bold ? FontStyle.Bold : FontStyle.Regular) | (italic ? FontStyle.Italic : FontStyle.Regular);
        return new Font(family, qtPt * 96f / 72f, style, GraphicsUnit.Pixel);
    }

    static readonly Font FontGlyph  = F("Segoe UI", 11, bold: true);
    static readonly Font FontName   = F("Segoe UI", 8);
    static readonly Font FontDate   = F("Segoe UI", 7, italic: true);
    static readonly Font FontLabel  = F("Segoe UI", 9, bold: true);
    static readonly Font FontSmall  = F("Segoe UI", 8);
    static readonly Font FontAngle  = F("Segoe UI", 10, bold: true);
    static readonly Font FontTitle  = F("Georgia", 14, bold: true);
    static readonly Font FontSub    = F("Segoe UI", 8, italic: true);
    static readonly Font FontToday  = F("Segoe UI", 9, bold: true);
    static readonly Font FontMoon   = F("Segoe UI", 8);
    static readonly Font FontPanTtl = F("Georgia", 11, bold: true);
    static readonly Font FontPanSub = F("Segoe UI", 9, bold: true);
    static readonly Font FontPanLbl = F("Segoe UI", 9);
    static readonly Font FontPanSml = F("Segoe UI", 8);
    static readonly Font FontClock  = F("Consolas", 16, bold: true);

    readonly Graphics g;
    readonly DashState st;

    public Renderer(Graphics graphics, DashState state)
    {
        g = graphics;
        st = state;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Small drawing helpers (create + dispose GDI objects per call)
    // ═══════════════════════════════════════════════════════════════════════

    void Ln(double x1, double y1, double x2, double y2, Color c, float w = 1, bool dash = false)
    {
        using var p = new Pen(c, w);
        if (dash) p.DashStyle = DashStyle.Dash;
        g.DrawLine(p, (float)x1, (float)y1, (float)x2, (float)y2);
    }

    void ElF(double x, double y, double w, double h, Color fill)
    {
        using var b = new SolidBrush(fill);
        g.FillEllipse(b, (float)x, (float)y, (float)w, (float)h);
    }

    void ElS(double x, double y, double w, double h, Color stroke, float sw = 1, bool dash = false)
    {
        using var p = new Pen(stroke, sw);
        if (dash) p.DashStyle = DashStyle.Dash;
        g.DrawEllipse(p, (float)x, (float)y, (float)w, (float)h);
    }

    void El(double x, double y, double w, double h, Color stroke, float sw, Color fill)
    {
        ElF(x, y, w, h, fill);
        ElS(x, y, w, h, stroke, sw);
    }

    void RectSF(double x, double y, double w, double h, Color stroke, Color fill)
    {
        using var b = new SolidBrush(fill);
        g.FillRectangle(b, (float)x, (float)y, (float)w, (float)h);
        using var p = new Pen(stroke, 1);
        g.DrawRectangle(p, (float)x, (float)y, (float)w, (float)h);
    }

    // Text: Qt drawText(x, y) places the BASELINE at y. TextRenderer (GDI) is
    // used for reliable glyph fallback (☊ ☋ ☀ ☾ ★ …); alpha is pre-blended
    // against the dark background because GDI text ignores alpha.
    void Txt(string s, Font f, Color c, double x, double yBase)
    {
        if (c.A < 255)
        {
            const int br = 9, bg = 11, bb = 31;
            double a = c.A / 255.0;
            c = Color.FromArgb(255,
                (int)(c.R * a + br * (1 - a)),
                (int)(c.G * a + bg * (1 - a)),
                (int)(c.B * a + bb * (1 - a)));
        }
        var fam = f.FontFamily;
        float ascent = f.Size * fam.GetCellAscent(f.Style) / fam.GetEmHeight(f.Style);
        TextRenderer.DrawText(g, s, f,
            new Point((int)Math.Round(x), (int)Math.Round(yBase - ascent)),
            c, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }

    static (double x, double y) LonToXY(double lon)
    {
        double ang = lon * RAD;
        return (CX + B * Math.Cos(ang), CY - A * Math.Sin(ang));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Scene
    // ═══════════════════════════════════════════════════════════════════════

    public void Draw()
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(ColBgDark);

        DrawStars();
        DrawZodiacWheel();
        DrawOrbitAndMarkers();
        DrawSunAndFocus();
        DrawPlanets();
        DrawEarthToday();
        DrawTitles();
        DrawEclipseCountdown();

        Ln(910, 10, 910, H - 10, Rgb(90, 80, 150, 160));   // main/panel divider

        DrawInsertAnomalistic();
        DrawInsertDraconic();
        DrawInsertInclination();
        DrawInsertSidereal();
        DrawInsertSynodic();
        DrawInsertEclipses();
    }

    // ── Stars: deterministic LCG so the sky never flickers ──────────────────
    void DrawStars()
    {
        long seed = 137;
        for (int i = 1; i <= 240; i++)
        {
            seed = (seed * 1664525 + 1013904223) % 4294967296L;
            long sx = seed % W;
            seed = (seed * 1664525 + 1013904223) % 4294967296L;
            long sy = seed % H;
            seed = (seed * 1664525 + 1013904223) % 4294967296L;
            int br = (int)(80 + seed % 175);
            using var b = new SolidBrush(Rgb(br, br, br, 190));
            g.FillRectangle(b, sx, sy, 1.4f, 1.4f);
        }
    }

    // ── Zodiac wheel: sectors, ring, ticks, labels ──────────────────────────
    void DrawZodiacWheel()
    {
        for (int i = 0; i <= 11; i++)
        {
            var pt = LonToXY(i * 30);
            Ln(CX, CY, pt.x, pt.y, ColSector, 1, dash: true);
        }

        const int RING_R = 415;
        ElS(CX - RING_R, CY - RING_R, 2 * RING_R, 2 * RING_R, ColRingVio);

        for (int i = 0; i <= 11; i++)
        {
            double ang = i * 30 * RAD;
            Ln(CX + (RING_R - 13) * Math.Cos(ang), CY - (RING_R - 13) * Math.Sin(ang),
               CX + (RING_R + 13) * Math.Cos(ang), CY - (RING_R + 13) * Math.Sin(ang),
               ColTick, 2);
        }

        const double GLYPH_R = 396, NAME_R = 369, DATE_R = 349;
        for (int i = 0; i < 12; i++)
        {
            double midLon = i * 30 + 15;
            double ang = midLon * RAD;
            double gx = CX + GLYPH_R * Math.Cos(ang), gy = CY - GLYPH_R * Math.Sin(ang);
            double nx = CX + NAME_R * Math.Cos(ang),  ny = CY - NAME_R * Math.Sin(ang);
            double dx = CX + DATE_R * Math.Cos(ang),  dy = CY - DATE_R * Math.Sin(ang);

            Txt(Astro.Zodiac[i].Abbr, FontGlyph, ColYellow, gx - 10, gy + 5);
            Txt(Astro.Zodiac[i].Name, FontName, ColViolet, nx - 22, ny + 4);
            Txt(Astro.Zodiac[i].Date, FontDate, ColGreen, dx - 18, dy + 4);
        }
    }

    // ── Orbit ellipse + equinox/solstice/aphelion/perihelion markers ────────
    void DrawOrbitAndMarkers()
    {
        ElS(CX - B, CY - A, 2 * B, 2 * A, ColSummer, 2);

        Txt("Aphelion ~Jul 4", FontSmall, ColAph, CX - 50, CY - A - 18);
        Txt("Cancer Jun 21",   FontSmall, ColAph, CX - 50, CY - A - 6);
        Txt("Perihelion ~Jan 3", FontSmall, ColPeri, CX - 50, CY + A + 8);
        Txt("Capricorn Dec 22",  FontSmall, ColPeri, CX - 50, CY + A + 20);

        var specials = new (int Lon, string L1, string L2, Color C)[]
        {
            (  0, "Vernal Equinox",    "Aries  Mar 21",    ColSpring),
            ( 90, "Aphelion ~Jul 4",   "Cancer Jun 21",    ColAph),
            (180, "Autumnal Equinox",  "Libra  Sep 23",    ColAutumn),
            (270, "Perihelion ~Jan 3", "Capricorn Dec 22", ColPeri),
        };

        foreach (var it in specials)
        {
            var pt = LonToXY(it.Lon);
            El(pt.x - 7, pt.y - 7, 14, 14, it.C, 2, it.C);

            if (it.Lon is > 85 and < 95) continue;       // aphelion labelled above
            if (it.Lon is > 265 and < 275) continue;     // perihelion labelled below
            if (Math.Cos(it.Lon * RAD) > 0)
            {
                Txt(it.L1, FontSmall, it.C, pt.x - 115, pt.y - 20);
                Txt(it.L2, FontSmall, it.C, pt.x - 115, pt.y - 8);
            }
            else
            {
                Txt(it.L1, FontSmall, it.C, pt.x + 14, pt.y - 2);
                Txt(it.L2, FontSmall, it.C, pt.x + 14, pt.y + 10);
            }
        }
    }

    // ── Sun at bottom focus + empty focus on top ────────────────────────────
    void DrawSunAndFocus()
    {
        for (int r = 34; r >= 14; r -= 4)
        {
            int a = 10 + (34 - r) * 4;
            ElF(SUN_X - r, SUN_Y - r, 2 * r, 2 * r, Rgb(255, 190, 35, a));
        }
        El(SUN_X - 13, SUN_Y - 13, 26, 26, ColSunLbl, 1, ColGold);
        Txt("Sun", FontLabel, ColSunLbl, SUN_X - 10, SUN_Y + 32);

        ElS(FOCUS2_X - 4, FOCUS2_Y - 4, 8, 8, ColFocus);
        Txt("empty focus", FontSmall, ColFocus, FOCUS2_X + 10, FOCUS2_Y + 5);
    }

    // ── Planets on the zodiac ring + legend (matches reference screenshot) ──
    static readonly Color[] PlanetColors =
    {
        Rgb(205, 205, 215),   // Mercury
        Rgb(255, 215, 120),   // Venus
        Rgb(255, 90, 60),     // Mars
        Rgb(255, 160, 60),    // Jupiter
        Rgb(235, 220, 140),   // Saturn
        Rgb(110, 230, 190),   // Uranus
        Rgb(110, 150, 255),   // Neptune
    };

    void DrawPlanets()
    {
        const double PR = 437;   // just outside the 415px zodiac ring
        for (int i = 0; i < st.Planets.Count; i++)
        {
            var p = st.Planets[i];
            var c = PlanetColors[i];
            double ang = p.HelioLon * RAD;
            double px = CX + PR * Math.Cos(ang);
            double py = CY - PR * Math.Sin(ang);

            ElF(px - 7, py - 7, 14, 14, Color.FromArgb(60, c));
            El(px - 4, py - 4, 8, 8, Color.FromArgb(230, ColWhite), 1, c);

            double lx = Math.Cos(ang) >= 0 ? px + 8 : px - 30;
            double ly = Math.Sin(ang) >= 0 ? py - 6 : py + 14;
            Txt(p.Abbrev, FontSmall, c, lx, ly);
        }

        // Legend box — bottom-left
        double BX = 18, BY = 862, BW = 200, BH = 146;
        RectSF(BX, BY, BW, BH, ColPanBdr, ColPanel);
        Txt("Planets — Zodiac Position", FontPanSub, ColPanTtl, BX + 10, BY + 17);
        for (int i = 0; i < st.Planets.Count; i++)
        {
            var p = st.Planets[i];
            var c = PlanetColors[i];
            double ry = BY + 34 + i * 16;
            El(BX + 10, ry - 8, 8, 8, Color.FromArgb(200, ColWhite), 1, c);
            Txt(p.Symbol + " " + p.Name, FontPanSml, c, BX + 24, ry);
            Txt(Astro.SignAbbr3[p.SignIndex] + " " + Astro.F2(p.DegInSign) + "°",
                FontPanSml, ColPanSub, BX + 118, ry);
        }
    }

    // ── Today's Earth: angle line, arc, glow, body, Moon, labels ────────────
    void DrawEarthToday()
    {
        var pt = LonToXY(st.TodayLon);
        double ex = pt.x, ey = pt.y;
        double angRad = st.TodayLon * RAD;

        Ln(CX, CY, CX + (B + 40) * Math.Cos(angRad), CY - (A + 40) * Math.Sin(angRad), ColAngle, 2);

        // Reference arc from Aries 0° to today's longitude
        const double arcRad = 90;
        int arcSteps = (int)Math.Floor(st.TodayLon) + 1;
        if (arcSteps < 1) arcSteps = 1;
        double pax = CX + arcRad, pay = CY;
        for (int s = 1; s <= arcSteps; s++)
        {
            double sa = s * RAD;
            double cx2 = CX + arcRad * Math.Cos(sa), cy2 = CY - arcRad * Math.Sin(sa);
            Ln(pax, pay, cx2, cy2, Rgb(255, 210, 50, 180), 1, dash: true);
            pax = cx2; pay = cy2;
        }

        // Angle value label near arc midpoint
        double midAng = (st.TodayLon / 2) * RAD;
        double lblR = arcRad + 36;
        double lblX = CX + lblR * Math.Cos(midAng);
        double lblY = CY - lblR * Math.Sin(midAng);
        if (lblX > CX - 80 && lblX < CX + 80) lblX = CX - 160;
        Txt(st.TodayDeg, FontAngle, Rgb(255, 220, 80, 230), lblX - 18, lblY + 4);

        Ln(CX + arcRad - 10, CY, CX + arcRad + 10, CY, ColSpring, 2);   // Aries 0° tick

        for (int r = 26; r >= 8; r -= 3)
        {
            int a = 25 + (26 - r) * 10;
            ElF(ex - r, ey - r, 2 * r, 2 * r, Rgb(45, 105, 255, a));
        }
        El(ex - 11, ey - 11, 22, 22, ColToday, 2, ColBlue);

        DrawMoon(ex, ey);

        double cosA = Math.Cos(angRad), sinA = Math.Sin(angRad);
        double lx = cosA >= 0 ? ex + 16 : ex - 90;
        double ly = sinA >= 0 ? ey - 28 : ey + 16;

        Txt("Earth", FontToday, ColToday, lx, ly);
        Txt(st.TodayStr, FontSmall, ColEarthLb, lx, ly + 13);
        Txt(st.TodayDeg, FontSmall, Rgb(255, 220, 80, 220), lx, ly + 25);
        Txt(st.TodayZodiac, FontSmall, ColYellow, lx, ly + 37);
    }

    // ── Moon orbit + phase-shaded Moon body around Earth ────────────────────
    void DrawMoon(double ex, double ey)
    {
        const double MOON_ORBIT_R = 52, MOON_R = 7;

        ElS(ex - MOON_ORBIT_R, ey - MOON_ORBIT_R, 2 * MOON_ORBIT_R, 2 * MOON_ORBIT_R,
            ColMoonOrb, 1, dash: true);

        double mRad = st.MoonAngle * RAD;
        double mx = ex + MOON_ORBIT_R * Math.Cos(mRad);
        double my = ey - MOON_ORBIT_R * Math.Sin(mRad);

        ElF(mx - MOON_R - 4, my - MOON_R - 4, 2 * (MOON_R + 4), 2 * (MOON_R + 4), Rgb(200, 200, 240, 40));

        double sunFromMoon = Astro.Norm360(st.MoonAngle + 180);
        DrawMoonPhaseDisc(mx, my, MOON_R, st.MoonPhase, sunFromMoon);

        Ln(ex, ey, mx, my, Rgb(150, 150, 200, 80), 1, dash: true);

        double mlx = Math.Cos(mRad) >= 0 ? mx + 10 : mx - 68;
        double mly = Math.Sin(mRad) >= 0 ? my - 10 : my + 18;
        Txt("Moon", FontMoon, ColMoonLbl, mlx, mly);
        Txt(st.MoonPhaseNm, FontMoon, ColMoonLbl, mlx, mly + 11);
    }

    // Phase-correct Moon disc: dark base + lit polygon bounded by the limb on
    // the Sun side and the terminator ellipse.  phase 0=New, 0.5=Full.
    // sunDirDeg: direction from Moon toward Sun, CCW from +X (math convention).
    void DrawMoonPhaseDisc(double cx, double cy, double r, double phase, double sunDirDeg)
    {
        ElF(cx - r, cy - r, 2 * r, 2 * r, ColMoonDrk);

        double c = Math.Cos(phase * 2 * PI);   // +1 new … 0 quarter … -1 full
        if (c < 0.999)                         // anything to light up?
        {
            double aDir = sunDirDeg * RAD;
            double ux = Math.Cos(aDir), uy = -Math.Sin(aDir);   // toward Sun (screen)
            double vx = -uy, vy = ux;

            const int N = 40;
            var pts = new PointF[2 * (N + 1)];
            int k = 0;
            for (int i = 0; i <= N; i++)       // limb (sun side): y from -r to +r
            {
                double y = -r + 2 * r * i / N;
                double x = Math.Sqrt(Math.Max(0, r * r - y * y));
                pts[k++] = new PointF((float)(cx + x * ux + y * vx), (float)(cy + x * uy + y * vy));
            }
            for (int i = 0; i <= N; i++)       // terminator: y from +r back to -r
            {
                double y = r - 2 * r * i / N;
                double x = c * Math.Sqrt(Math.Max(0, r * r - y * y));
                pts[k++] = new PointF((float)(cx + x * ux + y * vx), (float)(cy + x * uy + y * vy));
            }
            using var b = new SolidBrush(ColMoonLit);
            g.FillPolygon(b, pts);
        }

        ElS(cx - r, cy - r, 2 * r, 2 * r, Rgb(150, 150, 185, 180));
    }

    // ── Header titles ───────────────────────────────────────────────────────
    void DrawTitles()
    {
        Txt("Earth's Elliptical Orbit  -  Zodiac  +  Moon Orbit", FontTitle, ColTitle, 20, 30);
        Txt("Today: " + st.TodayStr + "   Lon: " + st.TodayDeg + "   Sign: " + st.TodayZodiac +
            "   |   Aries 0 deg = Mar 21 (right)  CCW",
            FontSub, ColSub, 20, 50);

        // Live digital clock — the whole scene is recomputed with the time of
        // day, so Earth, Moon and all panels creep forward as the clock runs
        if (st.TimeStr.Length > 0)
        {
            Txt(st.TimeStr, FontClock, Rgb(255, 220, 80), 716, 46);
            Txt("● LIVE — diagram follows the clock", FontPanSml, Rgb(120, 220, 160, 220), 716, 63);
        }
        else if (st.IsTestDate)
        {
            Txt("TEST DATE", FontClock, Rgb(255, 140, 90), 716, 46);
            Txt("fixed date — clock paused", FontPanSml, Rgb(220, 160, 130, 220), 716, 63);
        }
        double moonPct = Math.Floor(st.MoonPhase * 1000 + 0.5) / 10;
        Txt("Moon: " + st.MoonPhaseNm + "   Phase: " + Astro.F1(moonPct) + "%" +
            "   Orbit angle: " + Astro.F1(st.MoonAngle) + " deg",
            FontSub, ColMoonLbl, 20, 66);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Eclipse countdown + Moon face — bottom-centre of the main canvas.
    // Counts to the start of the next eclipse, then to its peak, then to its
    // end, then rolls over to the following one.  Start/end use an approximate
    // visibility window around greatest eclipse (±90 min solar, ±105 min lunar).
    // ═══════════════════════════════════════════════════════════════════════

    static string Cd(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return (t.Days > 0 ? t.Days + "d " : "") +
               t.Hours.ToString("00") + ":" + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00");
    }

    void DrawEclipseCountdown()
    {
        const int BX = 560, BY = 942, BW = 334, BH = 70;
        RectSF(BX, BY, BW, BH, ColPanBdr, ColPanel);

        // ── Moon face: current phase, drawn large (waxing lights the right) ─
        const double mcx = BX + 38, mcy = BY + 32, mr = 22;
        for (double gr = mr + 6; gr >= mr + 1; gr -= 1)
        {
            int ga = (int)(16 + (mr + 6 - gr) * 10);
            ElF(mcx - gr, mcy - gr, 2 * gr, 2 * gr, Rgb(200, 200, 240, ga));
        }
        DrawMoonPhaseDisc(mcx, mcy, mr, st.MoonPhase, 0);
        double illumPct = (1 - Math.Cos(st.MoonPhase * 2 * PI)) / 2 * 100;
        Txt(Astro.F1(illumPct) + "% lit", FontPanSml, ColMoonLbl, mcx - 20, BY + BH - 4);

        // ── Merge solar + lunar events chronologically ──────────────────────
        var evs = new List<(DateTime Peak, string Name, bool Solar, string Tz)>();
        foreach (var e in st.SolarEcl)
            evs.Add((new DateTime(e.Year, e.Month, e.Day, e.Hour, e.Min, 0), e.Type + " Solar Eclipse", true, e.Tz));
        foreach (var e in st.LunarEcl)
            evs.Add((new DateTime(e.Year, e.Month, e.Day, e.Hour, e.Min, 0), e.Type + " Lunar Eclipse", false, e.Tz));
        evs.Sort((a, b) => a.Peak.CompareTo(b.Peak));

        const int TX = 632;
        Txt("⏱  ECLIPSE COUNTDOWN", FontPanSub, ColPanTtl, TX, BY + 15);

        DateTime now = st.NowLocal;
        foreach (var ev in evs)
        {
            var half = TimeSpan.FromMinutes(ev.Solar ? 90 : 105);
            DateTime start = ev.Peak - half, end = ev.Peak + half;
            if (now >= end) continue;    // already over — look at the next one

            Color evCol = ev.Solar ? ColSolarTot : ColLunarTot;
            Txt(ev.Name + "  ·  " + Astro.MonthName3[ev.Peak.Month - 1] + " " +
                ev.Peak.Day.ToString("00") + "  " + ev.Peak.Hour.ToString("00") + ":" +
                ev.Peak.Minute.ToString("00") + " " + ev.Tz,
                FontPanSml, evCol, TX, BY + 30);

            string status;
            TimeSpan left;
            Color accent;
            if (now < start)
            {
                status = "starts in  (≈):";
                left = start - now;
                accent = Rgb(255, 220, 80);
            }
            else if (now < ev.Peak)
            {
                status = "● IN PROGRESS — peaks in:";
                left = ev.Peak - now;
                accent = Rgb(80, 255, 160);
            }
            else
            {
                status = "● IN PROGRESS — completes in  (≈):";
                left = end - now;
                accent = Rgb(80, 255, 160);
            }

            Txt(status, FontPanSml, accent, TX, BY + 45);
            Txt(Cd(left), FontClock, accent, TX, BY + 64);
            return;
        }

        Txt("No upcoming eclipse in range", FontPanSml, ColPanSub, TX, BY + 40);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INSERT 1 — Anomalistic Month: Perigee & Apogee
    // ═══════════════════════════════════════════════════════════════════════

    void DrawInsertAnomalistic()
    {
        const int PX = 918, PY = 12, PW = 700, PH = 240;

        RectSF(PX, PY, PW, PH, ColPanBdr, ColPanel);
        Txt("Anomalistic Month  —  Perigee & Apogee", FontPanTtl, ColPanTtl, PX + 12, PY + 22);
        Txt("Period: 27.55455 days   (Moon orbit ellipse around Earth)", FontPanSub, ColPanSub, PX + 12, PY + 40);
        Ln(PX + 10, PY + 46, PX + PW - 10, PY + 46, Rgb(90, 80, 150, 140));

        // Ellipse with exaggerated eccentricity (e=0.40) for clarity
        double ecx = PX + 260, ecy = PY + 130;
        double ea = 110, ecc = 0.40;
        double ec = ea * ecc;
        double eb = Math.Floor(Math.Sqrt(ea * ea - ec * ec));

        double etx = ecx + ec, ety = ecy;            // Earth at right focus
        double periX = ecx + ea, periY = ecy;
        double apoX = ecx - ea, apoY = ecy;

        ElS(ecx - ea, ecy - eb, 2 * ea, 2 * eb, ColMoonPth, 1, dash: true);
        Ln(apoX - 8, ecy, periX + 8, ecy, ColFocus, 1, dash: true);

        El(apoX - 6, apoY - 6, 12, 12, ColApogee, 2, ColApogee);
        Txt("Apogee", FontPanLbl, ColApogee, apoX - 16, apoY - 22);
        Txt("~406,700 km", FontPanSml, ColApogee, apoX - 22, apoY - 11);
        Txt("phase = 0.5", FontPanSml, ColApogee, apoX - 16, apoY + 17);

        El(periX - 6, periY - 6, 12, 12, ColPerigee, 2, ColPerigee);
        Txt("Perigee", FontPanLbl, ColPerigee, periX + 10, periY - 22);
        Txt("~356,500 km", FontPanSml, ColPerigee, periX + 10, periY - 10);
        Txt("phase = 0.0", FontPanSml, ColPerigee, periX + 10, periY + 17);

        for (int gr = 18; gr >= 7; gr -= 3)
        {
            int ga = 20 + (18 - gr) * 10;
            ElF(etx - gr, ety - gr, 2 * gr, 2 * gr, Rgb(45, 105, 255, ga));
        }
        El(etx - 9, ety - 9, 18, 18, ColToday, 2, ColBlue);
        Txt("Earth", FontPanLbl, ColEarthLb, etx - 14, ety - 22);

        double eFocX = ecx - ec;
        ElS(eFocX - 3, ecy - 3, 6, 6, ColFocus);
        Txt("empty focus", FontPanSml, ColFocus, eFocX - 22, ecy + 14);

        // Moon position (CCW: Perigee right → top → Apogee left → bottom)
        double mRad = st.AnomPhase * 2 * PI;
        double mX = ecx + ea * Math.Cos(mRad);
        double mY = ecy - eb * Math.Sin(mRad);

        ElF(mX - 13, mY - 13, 26, 26, Rgb(200, 200, 240, 50));
        El(mX - 7, mY - 7, 14, 14, ColMoonPos, 1, ColMoonPos);
        Ln(etx, ety, mX, mY, ColMoonLbl, 1, dash: true);

        if (mX >= ecx)
        {
            Txt("Moon", FontPanLbl, ColMoonLbl, mX + 12, mY - 4);
            Txt(st.AnomNm, FontPanLbl, ColMoonLbl, mX + 12, mY + 9);
        }
        else
        {
            Txt("Moon", FontPanLbl, ColMoonLbl, mX - 82, mY - 4);
            Txt(st.AnomNm, FontPanLbl, ColMoonLbl, mX - 82, mY + 9);
        }

        // Info box
        const int IBW = 195;
        double ix = PX + PW - IBW - 10, iy = PY + 22;
        RectSF(ix - 8, iy - 14, IBW, 162, ColPanBdr, Rgb(15, 18, 50, 210));

        Txt("Today's Status:", FontPanSub, ColPanTtl, ix, iy);
        double anomPct = Math.Floor(st.AnomPhase * 1000 + 0.5) / 10;
        double anomDay = Math.Floor(st.AnomPhase * 27.55455 * 10 + 0.5) / 10;
        Txt("Phase: " + Astro.F1(anomPct) + "%", FontPanSml, ColPanSub, ix, iy + 14);
        Txt("Day:   " + Astro.F1(anomDay) + " / 27.6", FontPanSml, ColPanSub, ix, iy + 26);
        Txt(st.AnomNm, FontPanSml, ColPanSub, ix, iy + 38);
        Txt("Perigee: ~356,500 km", FontPanSml, ColPanSub, ix, iy + 54);
        Txt("Apogee:  ~406,700 km", FontPanSml, ColPanSub, ix, iy + 66);
        Txt("Mean:    ~384,400 km", FontPanSml, ColPanSub, ix, iy + 78);
        Txt("Eccent:  ~0.0549", FontPanSml, ColPanSub, ix, iy + 90);
        Txt("Period: 27.55455 d", FontPanSml, ColPanSub, ix, iy + 102);
        Txt("* eccent. exaggerated", FontPanSml, Rgb(110, 110, 155, 170), ix, iy + 118);
        Txt("  for visual clarity", FontPanSml, Rgb(110, 110, 155, 170), ix, iy + 129);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INSERT 2 — Draconic Month: Ascending & Descending Nodes
    // ═══════════════════════════════════════════════════════════════════════

    void DrawInsertDraconic()
    {
        const int PX = 918, PY = 260, PW = 700, PH = 200;

        RectSF(PX, PY, PW, PH, ColPanBdr, ColPanel);
        Txt("Draconic Month  —  Ascending & Descending Nodes", FontPanTtl, ColPanTtl, PX + 12, PY + 22);
        Txt("Period: 27.21222 days   (Moon crosses ecliptic plane)", FontPanSub, ColPanSub, PX + 12, PY + 40);
        Ln(PX + 10, PY + 46, PX + PW - 10, PY + 46, Rgb(90, 80, 150, 140));

        double dcx = PX + 260, dcy = PY + 112;
        const double DA = 145, DB = 40, TILT = 20;

        Ln(dcx - DA - 15, dcy, dcx + DA + 15, dcy, ColEclip, 2);
        Txt("Ecliptic", FontPanSml, ColEclip, dcx - DA - 55, dcy + 4);

        // Moon orbit — solid above the ecliptic, dashed below
        const int steps = 120;
        double prevX = 0, prevY = 0;
        for (int s = 0; s <= steps; s++)
        {
            double ang = s * 2 * PI / steps;
            double ox = dcx + DA * Math.Cos(ang);
            double oy = dcy - DB * Math.Sin(ang) - TILT * Math.Sin(ang);
            if (s > 0)
            {
                bool above = Math.Sin(ang - PI / steps) > 0;
                Ln(prevX, prevY, ox, oy, ColMoonPth, above ? 2 : 1, dash: !above);
            }
            prevX = ox; prevY = oy;
        }

        for (int gr = 16; gr >= 6; gr -= 3)
        {
            int ga = 20 + (16 - gr) * 14;
            ElF(dcx - gr, dcy - gr, 2 * gr, 2 * gr, Rgb(45, 105, 255, ga));
        }
        El(dcx - 8, dcy - 8, 16, 16, ColToday, 2, ColBlue);
        Txt("Earth", FontPanLbl, ColEarthLb, dcx - 12, dcy - 22);

        double anx = dcx + DA, any = dcy;
        El(anx - 7, any - 7, 14, 14, ColAscNode, 2, ColAscNode);
        Txt("Ascending Node ☊", FontPanSub, ColAscNode, anx - 50, any - 28);
        Txt("crosses ecliptic  ▲ NORTH", FontPanSml, ColAscNode, anx - 44, any - 17);

        double dnx = dcx - DA, dny = dcy;
        El(dnx - 7, dny - 7, 14, 14, ColDscNode, 2, ColDscNode);
        Txt("Descending Node ☋", FontPanSub, ColDscNode, dnx - 14, dny - 30);
        Txt("crosses ecliptic  ▼ SOUTH", FontPanSml, ColDscNode, dnx - 14, dny - 18);

        Txt("North of Ecliptic", FontPanSml, Rgb(180, 230, 255, 200), dcx - 18, dcy - DB - TILT - 6);
        Txt("South of Ecliptic", FontPanSml, Rgb(255, 200, 150, 200), dcx - 18, dcy + DB + TILT + 14);

        // Moon position on the draconic orbit
        double mdRad = st.DracPhase * 2 * PI;
        double mdx = dcx + DA * Math.Cos(mdRad);
        double mdy = dcy - DB * Math.Sin(mdRad) - TILT * Math.Sin(mdRad);

        ElF(mdx - 12, mdy - 12, 24, 24, Rgb(200, 200, 240, 50));
        El(mdx - 7, mdy - 7, 14, 14, ColMoonPos, 1, ColMoonPos);
        Ln(dcx, dcy, mdx, mdy, ColMoonLbl, 1, dash: true);

        double dist = mdx - dcx;
        if (dist >= 0)
        {
            if (mdx > dcx + DA - 20)
            {
                Txt("Moon", FontPanLbl, ColMoonLbl, mdx - 20, mdy - 22);
                Txt(st.DracNm, FontPanLbl, ColMoonLbl, mdx - 20, mdy - 10);
            }
            else
            {
                Txt("Moon", FontPanLbl, ColMoonLbl, mdx + 12, mdy - 4);
                Txt(st.DracNm, FontPanLbl, ColMoonLbl, mdx + 12, mdy + 9);
            }
        }
        else
        {
            if (mdx < dcx - DA + 20)
            {
                Txt("Moon", FontPanLbl, ColMoonLbl, mdx - 18, mdy + 16);
                Txt(st.DracNm, FontPanLbl, ColMoonLbl, mdx - 18, mdy + 28);
            }
            else
            {
                Txt("Moon", FontPanLbl, ColMoonLbl, mdx - 82, mdy - 4);
                Txt(st.DracNm, FontPanLbl, ColMoonLbl, mdx - 82, mdy + 9);
            }
        }

        // Info box
        const int IBW = 195;
        double ix = PX + PW - IBW - 10, iy = PY + 22;
        RectSF(ix - 8, iy - 14, IBW, 120, ColPanBdr, Rgb(15, 18, 50, 210));

        Txt("Today's Status:", FontPanSub, ColPanTtl, ix, iy);
        double dracPct = Math.Floor(st.DracPhase * 1000 + 0.5) / 10;
        double dracDay = Math.Floor(st.DracPhase * 27.21222 * 10 + 0.5) / 10;
        Txt("Phase: " + Astro.F1(dracPct) + "%", FontPanSml, ColPanSub, ix, iy + 14);
        Txt("Day:   " + Astro.F1(dracDay) + " / 27.2", FontPanSml, ColPanSub, ix, iy + 26);
        Txt(st.DracNm, FontPanSml, ColPanSub, ix, iy + 38);
        Txt("Eclipse near nodes", FontPanSml, ColPanSub, ix, iy + 54);
        Txt("at New/Full Moon", FontPanSml, ColPanSub, ix, iy + 66);
        Txt("Node prec: 18.6 yr CW", FontPanSml, ColPanSub, ix, iy + 78);
        Txt("Period: 27.21222 d", FontPanSml, ColPanSub, ix, iy + 90);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INSERT 3 — Moon's Orbital Inclination to the Ecliptic (5.14°)
    // ═══════════════════════════════════════════════════════════════════════

    void DrawInsertInclination()
    {
        const int PX = 918, PY = 468, PW = 700, PH = 155;

        RectSF(PX, PY, PW, PH, ColPanBdr, ColPanel);
        Txt("Moon's Orbital Inclination to Ecliptic Plane", FontPanTtl, ColPanTtl, PX + 12, PY + 22);
        Txt("Inclination: 5.14°  (relative to ecliptic — slowly precesses)", FontPanSub, ColPanSub, PX + 12, PY + 40);
        Ln(PX + 10, PY + 46, PX + PW - 10, PY + 46, Rgb(90, 80, 150, 140));

        double icx = PX + 255, icy = PY + 82;
        const double IHW = 130;
        const double INCL_DEG = 5.14;
        const double INCL_RAD_DRAW = 25.0 * RAD;   // exaggerated for visibility

        Ln(icx - IHW, icy, icx + IHW, icy, ColEclip, 2);
        Txt("Ecliptic Plane", FontPanSml, ColEclip, icx - IHW - 52, icy + 4);

        const double CEQ = 28;
        Ln(icx - IHW, icy + CEQ, icx + IHW, icy + CEQ, ColSkyBlue, 1, dash: true);
        Txt("Cel. Equator", FontPanSml, Rgb(65, 145, 255, 180), icx - IHW - 52, icy + CEQ + 12);
        Txt("(23.44°)", FontPanSml, Rgb(65, 145, 255, 180), icx - IHW - 52, icy + CEQ + 22);

        const double IML = 130;
        double mpx1 = icx - IML * Math.Cos(INCL_RAD_DRAW), mpy1 = icy + IML * Math.Sin(INCL_RAD_DRAW);
        double mpx2 = icx + IML * Math.Cos(INCL_RAD_DRAW), mpy2 = icy - IML * Math.Sin(INCL_RAD_DRAW);
        Ln(mpx1, mpy1, mpx2, mpy2, ColMoonPth, 2);
        Txt("Moon's Orbital Plane", FontPanSml, ColMoonLbl, mpx2 - 65, mpy2 + 10);

        for (int gr = 16; gr >= 6; gr -= 3)
        {
            int ga = 20 + (16 - gr) * 14;
            ElF(icx - gr, icy - gr, 2 * gr, 2 * gr, Rgb(45, 105, 255, ga));
        }
        El(icx - 9, icy - 9, 18, 18, ColToday, 2, ColBlue);
        Txt("Earth", FontPanLbl, ColEarthLb, icx + 14, icy + 28);

        // Angle arc (exaggerated), labelled with the true 5.14°
        const double arcR = 52;
        double pax = icx + arcR, pay = icy;
        for (int s = 1; s <= 60; s++)
        {
            double a = s / 60.0 * INCL_RAD_DRAW;
            double cx2 = icx + arcR * Math.Cos(a), cy2 = icy - arcR * Math.Sin(a);
            Ln(pax, pay, cx2, cy2, ColIncLine, 2);
            pax = cx2; pay = cy2;
        }
        Txt("5.14°", FontPanSub, ColIncLine,
            icx + (arcR + 14) * Math.Cos(INCL_RAD_DRAW / 2),
            icy - (arcR + 14) * Math.Sin(INCL_RAD_DRAW / 2) - 2);
        Txt("(angle exaggerated)", FontPanSml, Rgb(120, 120, 160, 170), icx + arcR + 18, icy + 14);

        El(icx - 6, icy - 6, 12, 12, ColAscNode, 2, ColAscNode);

        Txt("▲ North", FontPanSml, ColAscNode, mpx2 + 4, mpy2 + 4);
        Txt("▼ South", FontPanSml, ColDscNode, mpx1 - 52, mpy1 - 4);

        // Moon ON the tilted plane at its current ecliptic latitude
        double dracAngle = st.DracPhase * 2 * PI;
        double latFrac = Math.Sin(dracAngle);
        double d = latFrac * IML;
        double mix = icx + d * Math.Cos(INCL_RAD_DRAW);
        double miy = icy - d * Math.Sin(INCL_RAD_DRAW);

        Ln(mix, miy, mix, icy, ColIncLine, 1, dash: true);
        Ln(mix - 5, icy - 5, mix + 5, icy + 5, ColIncLine, 2);
        Ln(mix - 5, icy + 5, mix + 5, icy - 5, ColIncLine, 2);

        const double IMONR = 11;
        for (double igr = IMONR + 6; igr >= IMONR + 1; igr -= 1)
        {
            int iga = (int)(18 + (IMONR + 6 - igr) * 12);
            ElF(mix - igr, miy - igr, 2 * igr, 2 * igr, Rgb(200, 200, 240, iga));
        }
        DrawMoonPhaseDisc(mix, miy, IMONR, st.MoonPhase, 0);   // Sun to the right

        double lat = latFrac * INCL_DEG;
        double latRnd = Math.Floor(lat * 10 + 0.5) / 10;
        string latTag = (latRnd >= 0 ? "+" : "") + Astro.F1(latRnd) + "°";
        if (latRnd >= 0)
        {
            Txt("Moon", FontPanSml, ColMoonLbl, mix - 10, miy - IMONR - 16);
            Txt(latTag, FontPanSml, ColIncLine, mix - 10, miy - IMONR - 5);
        }
        else
        {
            Txt("Moon", FontPanSml, ColMoonLbl, mix - 10, miy + IMONR + 12);
            Txt(latTag, FontPanSml, ColIncLine, mix - 10, miy + IMONR + 22);
        }

        // Key-facts box
        double ix = PX + PW - 195 - 10, iy = PY + 22;
        RectSF(ix - 8, iy - 14, 211, 120, ColPanBdr, Rgb(15, 18, 50, 180));
        Txt("Key Facts:", FontPanSub, ColPanTtl, ix, iy);
        Txt("Inclination: 5.14° to Ecliptic", FontPanSml, ColPanSub, ix, iy + 14);
        Txt("Moon wobbles ±5.14° ecliptic", FontPanSml, ColPanSub, ix, iy + 26);
        Txt("Nodes precess 18.6yr CW", FontPanSml, ColPanSub, ix, iy + 38);
        Txt("→ eclipse Saros cycle", FontPanSml, ColPanSub, ix, iy + 50);
        Txt("Ecliptic tilt 23.44° cel.eq", FontPanSml, ColPanSub, ix, iy + 62);

        string latStr = latRnd >= 0 ? "+" + Astro.F1(latRnd) + "°  N" : Astro.F1(latRnd) + "°  S";
        Txt("Moon latitude:", FontPanSub, ColMoonLbl, ix, iy + 78);
        Txt(latStr, FontPanSub, ColIncLine, ix, iy + 92);
        Txt(latRnd >= 0 ? "North of ecliptic" : "South of ecliptic", FontPanSml, ColPanSub, ix, iy + 104);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INSERT 4 — Sidereal Month: Moon orbit vs Fixed Stars
    // ═══════════════════════════════════════════════════════════════════════

    void DrawInsertSidereal()
    {
        const int PX = 918, PY = 631, PW = 700, PH = 155;

        RectSF(PX, PY, PW, PH, ColPanBdr, Rgb(8, 10, 30, 220));
        Txt("Sidereal Month  —  Moon orbit vs Fixed Stars", FontPanTtl, ColPanTtl, PX + 12, PY + 18);
        Txt("Period: 27.32166 days   (Moon returns to same star position)", FontPanSub, ColPanSub, PX + 12, PY + 32);

        double sidPhase = st.SiderealPhase;
        double sidDay = Math.Floor(sidPhase * 27.32166 * 10 + 0.5) / 10;

        double scx = PX + 230, scy = PY + 82;
        const double SR = 60;

        Ln(scx, scy, scx + SR + 30, scy, Rgb(180, 160, 255, 120), 1, dash: true);
        Txt("★ Star ref", FontPanSml, Rgb(180, 160, 255, 120), scx + SR + 8, scy - 4);

        ElS(scx - SR, scy - SR, 2 * SR, 2 * SR, Rgb(120, 120, 180, 140), 1, dash: true);

        El(scx - 6, scy - 6, 12, 12, Rgb(45, 105, 255), 2, Rgb(45, 105, 255));
        Txt("Earth", FontPanSml, ColPanSub, scx + 9, scy + 4);

        double sRad = sidPhase * 2 * PI;
        double msx = scx + SR * Math.Cos(sRad);
        double msy = scy - SR * Math.Sin(sRad);

        Ln(scx, scy, msx, msy, Rgb(200, 200, 255, 100), 1, dash: true);
        El(msx - 8, msy - 8, 16, 16, Rgb(220, 220, 255), 2, Rgb(200, 200, 240));

        if (msx >= scx)
        {
            Txt("Moon", FontPanSml, ColPanSub, msx + 10, msy - 4);
            Txt("Sidereal", FontPanSml, ColPanSub, msx + 10, msy + 7);
        }
        else
        {
            Txt("Moon", FontPanSml, ColPanSub, msx - 68, msy - 4);
            Txt("Sidereal", FontPanSml, ColPanSub, msx - 68, msy + 7);
        }

        // Swept-angle arc from the star reference
        int arcStep = (int)Math.Floor(sidPhase * 360) + 1;
        if (arcStep > 359) arcStep = 359;
        const double arcR = 28;
        double pax = scx + arcR, pay = scy;
        for (int s = 1; s <= arcStep; s++)
        {
            double a = s * RAD;
            double cx2 = scx + arcR * Math.Cos(a), cy2 = scy - arcR * Math.Sin(a);
            Ln(pax, pay, cx2, cy2, Rgb(160, 140, 255, 160), 2);
            pax = cx2; pay = cy2;
        }

        // Info box
        const int SIW = 195;
        double six = PX + PW - SIW - 10, siy = PY + 22;
        RectSF(six - 8, siy - 14, SIW, 148, ColPanBdr, Rgb(15, 18, 50, 210));

        Txt("Today's Status:", FontPanLbl, ColPanTtl, six, siy);
        double sidPct = Math.Floor(sidPhase * 1000 + 0.5) / 10;
        Txt("Phase: " + Astro.F1(sidPct) + "%", FontPanSml, ColPanSub, six, siy + 16);
        Txt("Day:   " + Astro.F1(sidDay) + " / 27.32", FontPanSml, ColPanSub, six, siy + 30);

        string sidNm;
        if (sidPhase < 0.04 || sidPhase >= 0.96) sidNm = "At Star Reference";
        else if (sidPhase < 0.25) sidNm = "0° → 90°  (Q1)";
        else if (sidPhase < 0.50) sidNm = "90° → 180° (Q2)";
        else if (sidPhase < 0.75) sidNm = "180° → 270° (Q3)";
        else sidNm = "270° → 360° (Q4)";
        Txt(sidNm, FontPanSml, ColPanSub, six, siy + 44);

        Txt("Angle: " + Astro.F1(Math.Floor(sidPhase * 3600 + 0.5) / 10) + "°", FontPanSml, ColPanSub, six, siy + 62);

        Txt("Sidereal < Synodic:", FontPanSml, Rgb(255, 220, 80, 200), six, siy + 80);
        Txt("27.32 vs 29.53 days", FontPanSml, ColPanSub, six, siy + 94);
        Txt("Extra 2.21 days for", FontPanSml, ColPanSub, six, siy + 108);
        Txt("Moon to re-align Sun", FontPanSml, ColPanSub, six, siy + 122);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INSERT 5 — Synodic Month: Moon phase cycle vs Sun
    // ═══════════════════════════════════════════════════════════════════════

    void DrawInsertSynodic()
    {
        const int PX = 918, PY = 794, PW = 700, PH = 190;

        RectSF(PX, PY, PW, PH, ColPanBdr, Rgb(8, 10, 30, 220));
        Txt("Synodic Month  —  Moon phase cycle vs Sun", FontPanTtl, ColPanTtl, PX + 12, PY + 18);
        Txt("Period: 29.53059 days   (New Moon → Full Moon → New Moon)", FontPanSub, ColPanSub, PX + 12, PY + 32);

        double synoPhase = st.MoonPhase;
        double synoDay = Math.Floor(synoPhase * 29.53059 * 10 + 0.5) / 10;

        double ycx = PX + 230, ycy = PY + 100;
        const double YR = 72;

        double sunX = PX + 60, sunY = ycy;
        El(sunX - 14, sunY - 14, 28, 28, Rgb(255, 215, 0), 2, Rgb(255, 215, 0));
        Txt("Sun", FontPanSml, Rgb(255, 215, 0), sunX - 8, sunY + 28);
        Ln(ycx, ycy, sunX + 14, sunY, Rgb(255, 215, 0, 80), 1, dash: true);

        ElS(ycx - YR, ycy - YR, 2 * YR, 2 * YR, Rgb(120, 120, 180, 140), 1, dash: true);

        El(ycx - 6, ycy - 6, 12, 12, Rgb(45, 105, 255), 2, Rgb(45, 105, 255));
        Txt("Earth", FontPanSml, ColPanSub, ycx + 9, ycy + 4);

        Txt("New", FontPanSml, Rgb(160, 160, 200, 160), ycx - YR - 32, ycy + 4);
        Txt("Full", FontPanSml, Rgb(160, 160, 200, 160), ycx + YR + 6, ycy + 4);
        Txt("Q1", FontPanSml, Rgb(160, 160, 200, 160), ycx - 12, ycy - YR - 8);
        Txt("Q3", FontPanSml, Rgb(160, 160, 200, 160), ycx - 12, ycy + YR + 12);

        // New Moon toward the Sun (left, 180°), CCW
        double mRad = (synoPhase + 0.5) * 2 * PI;
        double myx = ycx + YR * Math.Cos(mRad);
        double myy = ycy - YR * Math.Sin(mRad);

        Ln(ycx, ycy, myx, myy, Rgb(200, 200, 255, 100), 1, dash: true);
        El(myx - 8, myy - 8, 16, 16, Rgb(220, 220, 255), 2, Rgb(200, 200, 240));

        if (myx >= ycx)
        {
            Txt("Moon", FontPanSml, ColPanSub, myx + 10, myy - 4);
            Txt(st.MoonPhaseNm, FontPanSml, ColPanSub, myx + 10, myy + 7);
        }
        else
        {
            Txt("Moon", FontPanSml, ColPanSub, myx - 72, myy - 4);
            Txt(st.MoonPhaseNm, FontPanSml, ColPanSub, myx - 72, myy + 7);
        }

        // Info box
        const int YIW = 195;
        double yix = PX + PW - YIW - 10, yiy = PY + 22;
        RectSF(yix - 8, yiy - 14, YIW, 148, ColPanBdr, Rgb(15, 18, 50, 210));

        Txt("Today's Status:", FontPanLbl, ColPanTtl, yix, yiy);
        double synoPct = Math.Floor(synoPhase * 1000 + 0.5) / 10;
        Txt("Phase: " + Astro.F1(synoPct) + "%", FontPanSml, ColPanSub, yix, yiy + 16);
        Txt("Day:   " + Astro.F1(synoDay) + " / 29.53", FontPanSml, ColPanSub, yix, yiy + 30);
        Txt(st.MoonPhaseNm, FontPanSml, ColPanSub, yix, yiy + 44);

        Txt("Synodic > Sidereal:", FontPanSml, Rgb(255, 220, 80, 200), yix, yiy + 62);
        Txt("29.53 vs 27.32 days", FontPanSml, ColPanSub, yix, yiy + 76);
        Txt("Earth moves ~30°/month", FontPanSml, ColPanSub, yix, yiy + 90);
        Txt("Moon needs +2.21 days", FontPanSml, ColPanSub, yix, yiy + 104);
        Txt("to re-align with Sun", FontPanSml, ColPanSub, yix, yiy + 118);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INSERT 6 — Solar & Lunar Eclipses (right column)
    // ═══════════════════════════════════════════════════════════════════════

    void DrawInsertEclipses()
    {
        const int PX = 1628, PY = 12, PW = 240, PH = 996;

        RectSF(PX, PY, PW, PH, ColPanBdr, ColPanel);

        Txt("Solar & Lunar Eclipses", FontPanTtl, ColPanTtl, PX + 12, PY + 20);
        Txt((st.Year - 1) + "  ·  " + st.Year + "  ·  " + (st.Year + 1), FontPanSub, ColPanSub, PX + 12, PY + 36);
        Txt("Meeus algorithm  ·  Local PC time  ·  ☀ Solar  ☾ Lunar", FontPanSml, ColPanSub, PX + 12, PY + 50);
        Ln(PX + 10, PY + 56, PX + PW - 10, PY + 56, Rgb(90, 80, 150, 140));

        int sx = PX + 18;
        Txt("☀  SOLAR ECLIPSES", FontPanSub, ColSolarTot, sx, PY + 70);
        Txt("Date              Type          Local", FontPanSml, ColPanSub, sx, PY + 83);
        Ln(sx, PY + 86, PX + PW - 18, PY + 86, ColPanSub);

        long todayInt = st.Year * 10000L + st.Month * 100 + st.Day;
        const int ROW_H = 58;
        int y0 = PY + 94;

        Color solTxt = Rgb(255, 245, 210);
        Color yearCol = Rgb(190, 180, 230, 220);
        Color solDim = Rgb(200, 185, 140, 230);

        for (int i = 0; i < st.SolarEcl.Count; i++)
        {
            var e = st.SolarEcl[i];
            int rowY = y0 + i * ROW_H;
            long eclInt = e.Year * 10000L + e.Month * 100 + e.Day;
            bool isPast = eclInt < todayInt;

            Color eCol = e.Type switch
            {
                "Total" => ColSolarTot,
                "Annular" => ColSolarAnn,
                _ => ColSolarPar,
            };
            if (isPast) eCol = solDim;

            if (i > 0)
            {
                if (st.SolarEcl[i].Year != st.SolarEcl[i - 1].Year)
                {
                    Ln(sx, rowY - 8, PX + PW - 18, rowY - 8, ColPanBdr);
                    Txt("── " + e.Year, FontPanSml, yearCol, PX + PW - 80, rowY - 2);
                }
            }
            else
            {
                Txt("── " + e.Year, FontPanSml, yearCol, PX + PW - 80, rowY - 2);
            }

            El(sx, rowY + 6, 18, 18, eCol, isPast ? 1 : 2, eCol);

            if (eclInt == todayInt)
                ElS(sx - 4, rowY + 2, 26, 26, ColToday2, 2);

            Txt(Astro.MonthName3[e.Month - 1] + " " + e.Day.ToString("00") + "  " + e.Year,
                FontPanLbl, isPast ? eCol : solTxt, sx + 26, rowY + 14);
            Txt(e.Type + " Solar Eclipse", FontPanSub, eCol, sx + 26, rowY + 28);
            Txt(e.Hour.ToString("00") + ":" + e.Min.ToString("00") + " " + e.Tz,
                FontPanSml, isPast ? eCol : solTxt, sx + 26, rowY + 40);
        }

        // Divider + lunar section
        int sdivY = y0 + st.SolarEcl.Count * ROW_H + 8;
        Ln(sx, sdivY, PX + PW - 18, sdivY, ColPanBdr);

        Txt("☾  LUNAR ECLIPSES", FontPanSub, ColLunarTot, sx, sdivY + 16);
        Txt("Date              Type          Local", FontPanSml, ColPanSub, sx, sdivY + 30);
        Ln(sx, sdivY + 33, PX + PW - 18, sdivY + 33, ColPanSub);

        int ly0 = sdivY + 40;
        Color lunTxt = Rgb(235, 215, 210);
        Color lunDim = Rgb(230, 170, 160, 230);

        for (int i = 0; i < st.LunarEcl.Count; i++)
        {
            var e = st.LunarEcl[i];
            int rowY = ly0 + i * ROW_H;
            long eclInt = e.Year * 10000L + e.Month * 100 + e.Day;
            bool isPast = eclInt < todayInt;

            Color eCol = e.Type switch
            {
                "Total" => ColLunarTot,
                "Partial" => ColLunarPar,
                _ => ColLunarPen,
            };
            if (isPast) eCol = lunDim;

            if (i > 0)
            {
                if (st.LunarEcl[i].Year != st.LunarEcl[i - 1].Year)
                {
                    Ln(sx, rowY - 8, PX + PW - 18, rowY - 8, ColPanBdr);
                    Txt("── " + e.Year, FontPanSml, yearCol, PX + PW - 80, rowY - 2);
                }
            }
            else
            {
                Txt("── " + e.Year, FontPanSml, yearCol, PX + PW - 80, ly0 - 2);
            }

            El(sx, rowY + 6, 18, 18, eCol, isPast ? 1 : 2, eCol);

            if (eclInt == todayInt)
                ElS(sx - 4, rowY + 2, 26, 26, ColToday2, 2);

            Txt(Astro.MonthName3[e.Month - 1] + " " + e.Day.ToString("00") + "  " + e.Year,
                FontPanLbl, isPast ? eCol : lunTxt, sx + 26, rowY + 14);
            Txt(e.Type + " Lunar Eclipse", FontPanSub, eCol, sx + 26, rowY + 28);
            Txt(e.Hour.ToString("00") + ":" + e.Min.ToString("00") + " " + e.Tz,
                FontPanSml, isPast ? eCol : lunTxt, sx + 26, rowY + 40);
        }

        // Legend
        int lgy = PY + PH - 42;
        El(sx, lgy - 3, 9, 9, ColSolarTot, 2, ColSolarTot);
        Txt("Total", FontPanSml, ColPanSub, sx + 12, lgy + 4);
        El(sx + 58, lgy - 3, 9, 9, ColSolarAnn, 2, ColSolarAnn);
        Txt("Annular", FontPanSml, ColPanSub, sx + 70, lgy + 4);
        El(sx + 132, lgy - 3, 9, 9, ColSolarPar, 1, ColSolarPar);
        Txt("Partial ☀", FontPanSml, ColPanSub, sx + 144, lgy + 4);

        El(sx, lgy + 13, 9, 9, ColLunarTot, 2, ColLunarTot);
        Txt("Total", FontPanSml, ColPanSub, sx + 12, lgy + 20);
        El(sx + 58, lgy + 13, 9, 9, ColLunarPar, 1, ColLunarPar);
        Txt("Partial", FontPanSml, ColPanSub, sx + 70, lgy + 20);
        El(sx + 132, lgy + 13, 9, 9, ColLunarPen, 1, ColLunarPen);
        Txt("Penumbra ☾", FontPanSml, ColPanSub, sx + 144, lgy + 20);

        Txt("dimmed=past  ○=today", FontPanSml, Rgb(170, 170, 210, 200), sx, lgy + 34);
    }
}
