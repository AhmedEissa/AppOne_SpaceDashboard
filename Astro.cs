using System.Globalization;

namespace AppOneSpaceDashboard;

// ═══════════════════════════════════════════════════════════════════════════
// Astronomy engine — port of Earth_Orbit_Moon_Phases_Eclipses_Zodiac.ring
// (Bert Mariani, ring-lang/ring samples/General/Orbital)
// Solar eclipses: Meeus algorithm.  Lunar eclipses: full-moon bisection +
// Moon latitude vs umbra/penumbra radii.  Planets: Keplerian mean elements.
// ═══════════════════════════════════════════════════════════════════════════

public sealed class EclipseEvent
{
    public int Year, Month, Day, Hour, Min;   // in the PC's local time zone
    public string Type;      // Solar: Total/Annular/Hybrid/Partial   Lunar: Total/Partial/Penumbra
    public string Tz;        // per-event zone label, e.g. "GMT" / "BST" (DST-aware)
}

public sealed class PlanetPos
{
    public string Name, Abbrev, Symbol;
    public double HelioLon;  // heliocentric ecliptic longitude 0..360
    public int SignIndex;    // 0..11
    public double DegInSign;
}

public sealed class DashState
{
    public int Day, Month, Year;
    public double FracDay;        // time of day as fraction 0..1 (0 = midnight)
    public string TimeStr = "";   // "HH:mm" when live-updating, else empty
    public bool IsTestDate;
    public DateTime NowLocal;     // displayed instant (local date + time of day)

    public string TodayStr, TodayDeg, TodayZodiac;
    public double TodayLon;

    public double MoonPhase, MoonAngle;
    public string MoonPhaseNm;

    public double AnomPhase; public string AnomNm;
    public double DracPhase; public string DracNm;
    public double SiderealPhase;

    public List<EclipseEvent> SolarEcl = new();
    public List<EclipseEvent> LunarEcl = new();
    public List<PlanetPos> Planets = new();
}

public static class Astro
{
    public const double PI  = Math.PI;
    public const double RAD = PI / 180.0;

    // Eclipse calculation constants (from the Ring source)
    public const double SYNODIC_MONTH            = 29.530588853;
    public const double MOON_INCLINATION_DEG     = 5.145396;
    public const double BASE_UMBRA_AT_MEAN_DEG   = 0.7275;
    public const double SOLAR_ANGULAR_RADIUS_DEG = 0.2666;
    public const double PENUMBRA_EXTRA_DEG       = 0.2666;
    public const double EARTH_RADIUS_KM          = 6371.0;
    public const double MEAN_MOON_DIST_ER        = 60.36;
    public const double MEAN_MOON_ANG_RADIUS_DEG = 0.259;

    // Common-year month lengths; February is corrected for leap years by
    // DaysInMonth(year, month) — never index this array directly.
    static readonly int[] CommonYearDaysInMonth = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

    // Proleptic Gregorian leap-year rule
    public static bool IsLeapYear(int year) =>
        (year % 4 == 0 && year % 100 != 0) || year % 400 == 0;

    public static int DaysInMonth(int year, int month) =>
        month == 2 && IsLeapYear(year) ? 29 : CommonYearDaysInMonth[month - 1];

    // 1-based day of year, February 29 included in leap years
    public static int DayOfYear(int year, int month, int day)
    {
        int doy = day;
        for (int mo = 1; mo < month; mo++)
            doy += DaysInMonth(year, mo);
        return doy;
    }

    public static readonly string[] MonthName3 =
        { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

    // name, startLon, abbrev, date
    public static readonly (string Name, int Lon, string Abbr, string Date)[] Zodiac =
    {
        ("Aries",         0, "Ar", "Mar 21"),
        ("Taurus",       30, "Ta", "Apr 20"),
        ("Gemini",       60, "Ge", "May 21"),
        ("Cancer",       90, "Cn", "Jun 21"),
        ("Leo",         120, "Le", "Jul 23"),
        ("Virgo",       150, "Vi", "Aug 23"),
        ("Libra",       180, "Li", "Sep 23"),
        ("Scorpio",     210, "Sc", "Oct 23"),
        ("Sagittarius", 240, "Sg", "Nov 22"),
        ("Capricorn",   270, "Cp", "Dec 22"),
        ("Aquarius",    300, "Aq", "Jan 20"),
        ("Pisces",      330, "Pi", "Feb 19"),
    };

    public static readonly string[] SignAbbr3 =
        { "Ari", "Tau", "Gem", "Cnc", "Leo", "Vir", "Lib", "Sco", "Sgr", "Cap", "Aqr", "Psc" };

    // ── Ring-style number formatting: round to 1 decimal, print integers bare,
    //    fractions with two decimals ("141.90", "29", "18.80") ────────────────
    public static string F1(double v)
    {
        v = Math.Floor(v * 10 + 0.5) / 10;
        return v == Math.Floor(v)
            ? ((long)v).ToString(CultureInfo.InvariantCulture)
            : v.ToString("0.00", CultureInfo.InvariantCulture);
    }

    public static string F2(double v)
    {
        v = Math.Floor(v * 100 + 0.5) / 100;
        return v == Math.Floor(v)
            ? ((long)v).ToString(CultureInfo.InvariantCulture)
            : v.ToString("0.00", CultureInfo.InvariantCulture);
    }

    public static double Norm360(double a)
    {
        a %= 360.0;
        if (a < 0) a += 360.0;
        return a;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Master compute — everything derived from one calendar date
    // ═══════════════════════════════════════════════════════════════════════

    public static DashState Compute(int day, int month, int year, bool isTestDate,
                                    double fracDay = 0, string timeStr = "")
    {
        var st = new DashState
        {
            Day = day, Month = month, Year = year, IsTestDate = isTestDate,
            FracDay = fracDay, TimeStr = timeStr,
            NowLocal = new DateTime(year, month, day).AddDays(fracDay),
        };

        CalcTodayLon(st);
        CalcMoon(st);
        CalcInserts(st);
        CalcEclipses(st);
        CalcPlanets(st);

        return st;
    }

    // ── CalcTodayLon: date → day-of-year → ecliptic longitude ───────────────
    //    Vernal equinox Mar 21 = lon 0°, lon = (doy - doy(Mar 21))*360/365.25
    //    The Mar 21 anchor is day 80 in a common year and 81 in a leap year,
    //    so it must be recomputed per year rather than hard-coded.
    static void CalcTodayLon(DashState st)
    {
        st.TodayStr = MonthName3[st.Month - 1] + " " + st.Day + "  " + st.Year;

        int dayOfYear    = DayOfYear(st.Year, st.Month, st.Day);
        int equinoxDoy   = DayOfYear(st.Year, 3, 21);

        double lon = Norm360((dayOfYear + st.FracDay - equinoxDoy) * 360.0 / 365.25);
        st.TodayLon = lon;
        st.TodayDeg = F1(lon) + " deg";

        int signIdx = (int)Math.Floor(lon / 30);
        if (signIdx > 11) signIdx = 0;
        st.TodayZodiac = Zodiac[signIdx].Name;
    }

    // ── Julian Day Number (integer, noon-based) ─────────────────────────────
    public static long Jdn(int day, int month, int year)
    {
        long a = (14 - month) / 12;
        long y = year + 4800 - a;
        long m = month + 12 * a - 3;
        return day + (153 * m + 2) / 5 + 365 * y + y / 4 - y / 100 + y / 400 - 32045;
    }

    // ── CalcMoon: synodic phase + orbital angle around Earth ────────────────
    static void CalcMoon(DashState st)
    {
        double daysSinceNew = Jdn(st.Day, st.Month, st.Year) + st.FracDay - 2451550;   // new moon Jan 6 2000
        const double syno = 29.53059;

        double phase = (daysSinceNew % syno) / syno;
        if (phase < 0) phase += 1;
        st.MoonPhase = phase;

        double sunDir = Norm360(st.TodayLon + 180);
        st.MoonAngle = Norm360(sunDir + phase * 360);

        if (phase < 0.03 || phase >= 0.97) st.MoonPhaseNm = "New Moon";
        else if (phase < 0.22)             st.MoonPhaseNm = "Waxing Crescent";
        else if (phase < 0.28)             st.MoonPhaseNm = "First Quarter";
        else if (phase < 0.47)             st.MoonPhaseNm = "Waxing Gibbous";
        else if (phase < 0.53)             st.MoonPhaseNm = "Full Moon";
        else if (phase < 0.72)             st.MoonPhaseNm = "Waning Gibbous";
        else if (phase < 0.78)             st.MoonPhaseNm = "Last Quarter";
        else                               st.MoonPhaseNm = "Waning Crescent";
    }

    // ── CalcInserts: anomalistic (perigee/apogee), draconic (nodes),
    //    sidereal (vs fixed stars) phases ─────────────────────────────────────
    static void CalcInserts(DashState st)
    {
        double jdn = Jdn(st.Day, st.Month, st.Year) + st.FracDay;

        const double anomPeriod = 27.55455;      // perigee epoch Jan 4 2000 = JD 2451547
        double anomPhase = ((jdn - 2451547) % anomPeriod) / anomPeriod;
        if (anomPhase < 0) anomPhase += 1;
        st.AnomPhase = anomPhase;
        if (anomPhase < 0.04 || anomPhase >= 0.96) st.AnomNm = "At Perigee";
        else if (anomPhase < 0.46)                 st.AnomNm = "Waxing (→ Apogee)";
        else if (anomPhase < 0.54)                 st.AnomNm = "At Apogee";
        else                                       st.AnomNm = "Waning (→ Perigee)";

        const double dracPeriod = 27.21222;      // asc node epoch Jan 1 2000 = JD 2451545
        double dracPhase = ((jdn - 2451545) % dracPeriod) / dracPeriod;
        if (dracPhase < 0) dracPhase += 1;
        st.DracPhase = dracPhase;
        if (dracPhase < 0.04 || dracPhase >= 0.96) st.DracNm = "At Ascending Node ☊";
        else if (dracPhase < 0.46)                 st.DracNm = "North of Ecliptic";
        else if (dracPhase < 0.54)                 st.DracNm = "At Descending Node ☋";
        else                                       st.DracNm = "South of Ecliptic";

        const double siderealPeriod = 27.32166;  // star reference epoch Jan 1 2000
        double sidPhase = ((jdn - 2451545) % siderealPeriod) / siderealPeriod;
        if (sidPhase < 0) sidPhase += 1;
        st.SiderealPhase = sidPhase;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Eclipses — current year ± 1
    // ═══════════════════════════════════════════════════════════════════════

    // Convert an eclipse instant (algorithm output is UT/GMT) into the PC's
    // local time zone, DST-aware, with a per-event zone label.
    static EclipseEvent MakeLocalEvent(int y, int m, int d, int h, int min, string type)
    {
        var tz = TimeZoneInfo.Local;
        var utc = new DateTime(y, m, d, h, min, 0, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);

        string label;
        bool dst = tz.IsDaylightSavingTime(local);
        if (tz.Id == "GMT Standard Time")           // United Kingdom
        {
            label = dst ? "BST" : "GMT";
        }
        else
        {
            var off = tz.GetUtcOffset(local);
            label = off == TimeSpan.Zero
                ? "GMT"
                : "GMT" + (off < TimeSpan.Zero ? "-" : "+") + Math.Abs(off.Hours) +
                  (off.Minutes != 0 ? ":" + Math.Abs(off.Minutes).ToString("00") : "");
        }

        return new EclipseEvent
        {
            Year = local.Year, Month = local.Month, Day = local.Day,
            Hour = local.Hour, Min = local.Minute,
            Type = type, Tz = label,
        };
    }

    static void CalcEclipses(DashState st)
    {
        int startYear = st.Year - 1;
        int endYear   = st.Year + 1;

        // ── Solar eclipses (Meeus) ──────────────────────────────────────────
        long ks = (long)Math.Floor(NewMoonK(startYear));
        long ke = (long)Math.Ceiling(NewMoonK(endYear + 1));
        for (long k = ks; k <= ke; k++)
        {
            var (found, jde, type) = CalculateSolarEclipse(k);
            if (!found) continue;
            var (ey, em, ed, eh, emi) = JdeToDate(jde);
            if (ey < startYear || ey > endYear) continue;

            string es = type switch
            {
                "Total..."  => "Total",
                "Annular." => "Annular",
                "Hybrid.." => "Hybrid",
                _           => "Partial",
            };
            st.SolarEcl.Add(MakeLocalEvent(ey, em, ed, eh, emi, es));
        }

        // ── Lunar eclipses (full-moon refinement + shadow geometry) ─────────
        double jdLo = JulianDay(startYear, 1, 1);
        double jdHi = JulianDay(endYear, 12, 31);
        for (int y = startYear; y <= endYear; y++)
        {
            for (int m = 1; m <= 12; m++)
            {
                double seed = FullMoonSeed(y, m);
                double full = RefineFullMoonBisection(seed);
                if (full < jdLo || full > jdHi) continue;

                double latDeg = Math.Abs(MoonLatitudeDeg(full));
                double distER = MoonDistanceEarthRadii(full);
                double lunarR = MEAN_MOON_ANG_RADIUS_DEG * (MEAN_MOON_DIST_ER / distER);
                double umbraD = BASE_UMBRA_AT_MEAN_DEG   * (MEAN_MOON_DIST_ER / distER);
                double penumD = umbraD + PENUMBRA_EXTRA_DEG;

                string type = null;
                if (latDeg + lunarR <= umbraD)      type = "Total";
                else if (latDeg - lunarR < umbraD)  type = "Partial";
                else if (latDeg - lunarR < penumD)  type = "Penumbra";
                if (type == null) continue;

                var (ly, lm2, ld, lh, lmin) = JdeToDate(full);
                var ev = MakeLocalEvent(ly, lm2, ld, lh, lmin, type);
                bool dup = st.LunarEcl.Any(e => e.Year == ev.Year && e.Month == ev.Month && e.Day == ev.Day);
                if (!dup)
                    st.LunarEcl.Add(ev);
            }
        }
    }

    // ── Meeus building blocks ────────────────────────────────────────────────

    static double NewMoonK(double year) => (year - 2000.0) * 12.3685;

    static double MeanNewMoon(double k)
    {
        double t = k / 1236.85, t2 = t * t, t3 = t2 * t, t4 = t3 * t;
        return 2451550.09766 + 29.530588861 * k
             + 0.00015437 * t2 - 0.000000150 * t3 + 0.00000000073 * t4;
    }

    static double SunAnomaly(double k, double t) =>
        Norm360(2.5534 + 29.10535670 * k - 0.0000014 * t * t - 0.00000011 * t * t * t);

    static double MoonAnomaly(double k, double t)
    {
        double t2 = t * t, t3 = t2 * t, t4 = t3 * t;
        return Norm360(201.5643 + 385.81693528 * k + 0.0107582 * t2 + 0.00001238 * t3 - 0.000000058 * t4);
    }

    static double MoonArgument(double k, double t)
    {
        double t2 = t * t, t3 = t2 * t, t4 = t3 * t;
        return Norm360(160.7108 + 390.67050284 * k - 0.0016118 * t2 - 0.00000227 * t3 + 0.000000011 * t4);
    }

    static double MoonNode(double k, double t)
    {
        double t2 = t * t, t3 = t2 * t;
        return Norm360(124.7746 - 1.56375588 * k + 0.0020672 * t2 + 0.00000215 * t3);
    }

    static (bool found, double jde, string type) CalculateSolarEclipse(double k)
    {
        double t  = k / 1236.85;
        double t2 = t * t;

        double jde = MeanNewMoon(k);

        double M  = SunAnomaly(k, t) * RAD;
        double Mp = MoonAnomaly(k, t) * RAD;
        double F  = MoonArgument(k, t) * RAD;
        double Om = MoonNode(k, t) * RAD;

        double E = 1.0 - 0.002516 * t - 0.0000074 * t2;

        // Moon near a node? (F near 0° or 180°)
        double fDeg = MoonArgument(k, t);
        double fNodeDist = Math.Abs(fDeg);
        if (fNodeDist > 180) fNodeDist = 360 - fNodeDist;
        if (fNodeDist > 90)  fNodeDist = 180 - fNodeDist;
        if (fNodeDist > 15.5) return (false, jde, "");

        double correction =
            -0.4072 * Math.Sin(Mp)
            + 0.17241 * E * Math.Sin(M)
            + 0.01608 * Math.Sin(2 * Mp)
            + 0.01039 * Math.Sin(2 * F)
            + 0.00739 * E * Math.Sin(Mp - M)
            - 0.00514 * E * Math.Sin(Mp + M)
            + 0.00208 * E * E * Math.Sin(2 * M)
            - 0.00111 * Math.Sin(Mp - 2 * F)
            - 0.00057 * Math.Sin(Mp + 2 * F)
            + 0.00056 * E * Math.Sin(2 * Mp + M)
            - 0.00042 * Math.Sin(3 * Mp)
            + 0.00042 * E * Math.Sin(M + 2 * F)
            + 0.00038 * E * Math.Sin(M - 2 * F)
            - 0.00024 * E * Math.Sin(2 * Mp - M)
            - 0.00017 * Math.Sin(Om)
            - 0.00007 * Math.Sin(Mp + 2 * M)
            + 0.00004 * Math.Sin(2 * Mp - 2 * F)
            + 0.00004 * Math.Sin(3 * M)
            + 0.00003 * Math.Sin(Mp + M - 2 * F)
            + 0.00003 * Math.Sin(2 * Mp + 2 * F)
            - 0.00003 * Math.Sin(Mp + M + 2 * F)
            + 0.00003 * Math.Sin(Mp - M + 2 * F)
            - 0.00002 * Math.Sin(Mp - M - 2 * F)
            - 0.00002 * Math.Sin(3 * Mp + M)
            + 0.00002 * Math.Sin(4 * Mp);
        jde += correction;

        double P = 0.2070 * E * Math.Sin(M)
                 + 0.0024 * E * Math.Sin(2 * M)
                 - 0.0392 * Math.Sin(Mp)
                 + 0.0116 * Math.Sin(2 * Mp)
                 - 0.0073 * E * Math.Sin(Mp + M)
                 + 0.0067 * E * Math.Sin(Mp - M)
                 + 0.0118 * Math.Sin(2 * F);

        double Q = 5.2207
                 - 0.0048 * E * Math.Cos(M)
                 + 0.0020 * E * Math.Cos(2 * M)
                 - 0.3299 * Math.Cos(Mp)
                 - 0.0060 * E * Math.Cos(Mp + M)
                 + 0.0041 * E * Math.Cos(Mp - M);

        double Wv = Math.Abs(Math.Cos(F));
        double gamma = (P * Math.Cos(F) + Q * Math.Sin(F)) * (1.0 - 0.0048 * Wv);

        double u = 0.0059
                 + 0.0046 * E * Math.Cos(M)
                 - 0.0182 * Math.Cos(Mp)
                 + 0.0004 * Math.Cos(2 * Mp)
                 - 0.0005 * Math.Cos(M + Mp);

        double absGamma = Math.Abs(gamma);
        double threshold = 1.5433 + u;
        if (absGamma > threshold) return (false, jde, "");

        double diameterRatio = (1.5433 + u - absGamma) / (0.5461 + 2.0 * u);
        if (diameterRatio <= 0) return (false, jde, "");

        string type;
        if (absGamma >= 0.9972)
        {
            type = "Partial.";
        }
        else
        {
            if (u < 0) type = "Total...";
            else if (u > 0.0047) type = "Annular.";
            else
            {
                double w = 0.00464 * Math.Sqrt(1.0 - gamma * gamma);
                type = u < w ? "Hybrid.." : "Annular.";
            }
        }
        return (true, jde, type);
    }

    static (int y, int m, int d, int h, int min) JdeToDate(double jde)
    {
        double z = Math.Floor(jde + 0.5);
        double f = (jde + 0.5) - z;

        double alpha = Math.Floor((z - 1867216.25) / 36524.25);
        double a = z + 1 + alpha - Math.Floor(alpha / 4.0);
        double b = a + 1524;
        double c = Math.Floor((b - 122.1) / 365.25);
        double d = Math.Floor(365.25 * c);
        double e = Math.Floor((b - d) / 30.6001);

        int day = (int)Math.Floor(b - d - Math.Floor(30.6001 * e));
        int month = e < 14 ? (int)(e - 1) : (int)(e - 13);
        int year = month > 2 ? (int)(c - 4716) : (int)(c - 4715);

        int hour = (int)Math.Floor(f * 24);
        int minute = (int)Math.Floor((f * 24 - hour) * 60);
        return (year, month, day, hour, minute);
    }

    // ── Lunar-eclipse helpers ────────────────────────────────────────────────

    public static double JulianDay(int y, int m, double d)
    {
        int yy = y, mm = m;
        if (mm <= 2) { yy -= 1; mm += 12; }
        double a = Math.Floor(yy / 100.0);
        double b = 2 - a + Math.Floor(a / 4.0);
        return Math.Floor(365.25 * (yy + 4716)) + Math.Floor(30.6001 * (mm + 1)) + d + b - 1524.5;
    }

    static double SunLongitude(double jd)
    {
        double t = (jd - 2451545.0) / 36525.0;
        double l0 = Norm360(280.46646 + 36000.76983 * t + 0.0003032 * t * t);
        double m  = Norm360(357.52911 + 35999.05029 * t - 0.0001537 * t * t);
        double cc = (1.914602 - 0.004817 * t - 0.000014 * t * t) * Math.Sin(m * RAD)
                  + (0.019993 - 0.000101 * t) * Math.Sin(2 * m * RAD)
                  + 0.000289 * Math.Sin(3 * m * RAD);
        return Norm360(l0 + cc);
    }

    static double MoonLongitude(double jd)
    {
        double t = (jd - 2451545.0) / 36525.0;
        double l0 = 218.3164477 + 481267.88123421 * t - 0.0015786 * t * t;
        double m  = 134.9633964 + 477198.8675055 * t + 0.0087414 * t * t;
        double d  = 297.8501921 + 445267.1114034 * t - 0.0018819 * t * t;
        double lon = l0 + 6.289 * Math.Sin(m * RAD)
                   - 1.274 * Math.Sin((2 * d - m) * RAD)
                   + 0.658 * Math.Sin(2 * d * RAD)
                   + 0.214 * Math.Sin(2 * m * RAD)
                   - 0.11  * Math.Sin(d * RAD);
        return Norm360(lon);
    }

    static double MoonNodeLongitude(double jd)
    {
        double t = (jd - 2451545.0) / 36525.0;
        return Norm360(125.04452 - 1934.136261 * t + 0.0020708 * t * t);
    }

    static double MoonDistanceEarthRadii(double jd)
    {
        double t = (jd - 2451545.0) / 36525.0;
        double mm = 134.9633964 + 477198.8675055 * t + 0.0087414 * t * t;
        const double ecc = 0.0549;
        return MEAN_MOON_DIST_ER * (1 - ecc * Math.Cos(mm * RAD));
    }

    static double SunMoonSignedDiffDeg(double jd)
    {
        double diff = Norm360(MoonLongitude(jd) - SunLongitude(jd));
        if (diff > 180) diff -= 360;
        double val = diff - 180;
        if (val < -180) val += 360;
        if (val > 180) val -= 360;
        return val;
    }

    static double IsOppositionDeg(double jd) => Math.Abs(SunMoonSignedDiffDeg(jd));

    static double RefineFullMoonBisection(double initialJD)
    {
        double left = initialJD - 2.0, right = initialJD + 2.0;
        double fLeft = SunMoonSignedDiffDeg(left), fRight = SunMoonSignedDiffDeg(right);

        int tries = 0;
        while (fLeft * fRight > 0 && tries < 20)
        {
            left -= 1.0; right += 1.0;
            fLeft = SunMoonSignedDiffDeg(left);
            fRight = SunMoonSignedDiffDeg(right);
            tries++;
        }

        if (fLeft * fRight > 0)
        {
            // Golden-section fallback on |diff|
            double lo = initialJD - 2.0, hi = initialJD + 2.0;
            for (int i = 0; i < 80 && (hi - lo) > 0.00001; i++)
            {
                double c1 = lo + (hi - lo) * 0.382;
                double c2 = lo + (hi - lo) * 0.618;
                if (IsOppositionDeg(c1) < IsOppositionDeg(c2)) hi = c2; else lo = c1;
            }
            return (lo + hi) / 2.0;
        }

        for (int i = 0; i < 100 && (right - left) > 0.00001; i++)
        {
            double mid = (left + right) / 2.0;
            double fMid = SunMoonSignedDiffDeg(mid);
            if (fLeft * fMid <= 0) { right = mid; fRight = fMid; }
            else { left = mid; fLeft = fMid; }
        }
        return (left + right) / 2.0;
    }

    static double MoonLatitudeDeg(double jd)
    {
        double along = Norm360(MoonLongitude(jd) - MoonNodeLongitude(jd));
        if (along > 180) along -= 360;
        return MOON_INCLINATION_DEG * Math.Sin(along * RAD);
    }

    static double FullMoonSeed(int year, int month)
    {
        double y = (year + (month - 0.5) / 12.0) - 2000.0;
        double k = Math.Floor(y * 12.3685);
        double kp = k + 0.5;
        double t = kp / 1236.85;
        return 2451550.09765 + 29.530588853 * kp
             + 0.0001337 * t * t - 0.000000150 * t * t * t + 0.00000000073 * t * t * t * t;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Planets — Keplerian mean elements (JPL approximate, J2000..2050)
    // Heliocentric ecliptic longitude for placing on the zodiac ring.
    // ═══════════════════════════════════════════════════════════════════════

    // a[AU], e, I[deg], L[deg], longPeri[deg], longNode[deg] + rates per Julian century
    static readonly (string Name, string Abbrev, string Symbol, double[] El, double[] Rate)[] KepPlanets =
    {
        ("Mercury", "Mer", "☿",
            new[] { 0.38709927, 0.20563593, 7.00497902, 252.25032350, 77.45779628, 48.33076593 },
            new[] { 0.00000037, 0.00001906, -0.00594749, 149472.67411175, 0.16047689, -0.12534081 }),
        ("Venus", "Ven", "♀",
            new[] { 0.72333566, 0.00677672, 3.39467605, 181.97909950, 131.60246718, 76.67984255 },
            new[] { 0.00000390, -0.00004107, -0.00078890, 58517.81538729, 0.00268329, -0.27769418 }),
        ("Mars", "Mar", "♂",
            new[] { 1.52371034, 0.09339410, 1.84969142, -4.55343205, -23.94362959, 49.55953891 },
            new[] { 0.00001847, 0.00007882, -0.00813131, 19140.30268499, 0.44441088, -0.29257343 }),
        ("Jupiter", "Jup", "♃",
            new[] { 5.20288700, 0.04838624, 1.30439695, 34.39644051, 14.72847983, 100.47390909 },
            new[] { -0.00011607, -0.00013253, -0.00183714, 3034.74612775, 0.21252668, 0.20469106 }),
        ("Saturn", "Sat", "♄",
            new[] { 9.53667594, 0.05386179, 2.48599187, 49.95424423, 92.59887831, 113.66242448 },
            new[] { -0.00125060, -0.00050991, 0.00193609, 1222.49362201, -0.41897216, -0.28867794 }),
        ("Uranus", "Ura", "♅",
            new[] { 19.18916464, 0.04725744, 0.77263783, 313.23810451, 170.95427630, 74.01692503 },
            new[] { -0.00196176, -0.00004397, -0.00242939, 428.48202785, 0.40805281, 0.04240589 }),
        ("Neptune", "Nep", "♆",
            new[] { 30.06992276, 0.00859048, 1.77004347, -55.12002969, 44.96476227, 131.78422574 },
            new[] { 0.00026291, 0.00005105, 0.00035372, 218.45945325, -0.32241464, -0.00508664 }),
    };

    static void CalcPlanets(DashState st)
    {
        double jd = Jdn(st.Day, st.Month, st.Year) - 0.5 + st.FracDay;
        double T = (jd - 2451545.0) / 36525.0;

        foreach (var p in KepPlanets)
        {
            double a  = p.El[0] + p.Rate[0] * T;
            double e  = p.El[1] + p.Rate[1] * T;
            double L  = p.El[3] + p.Rate[3] * T;
            double lp = p.El[4] + p.Rate[4] * T;   // longitude of perihelion

            // Mean anomaly, Kepler solve (degrees)
            double M = Norm360(L - lp);
            if (M > 180) M -= 360;
            double eStar = e / RAD;                 // e in degrees
            double E = M + eStar * Math.Sin(M * RAD);
            for (int i = 0; i < 12; i++)
            {
                double dM = M - (E - eStar * Math.Sin(E * RAD));
                double dE = dM / (1 - e * Math.Cos(E * RAD));
                E += dE;
                if (Math.Abs(dE) < 1e-8) break;
            }

            // True anomaly → heliocentric ecliptic longitude (I≈0 approximation)
            double xv = Math.Cos(E * RAD) - e;
            double yv = Math.Sqrt(1 - e * e) * Math.Sin(E * RAD);
            double nu = Math.Atan2(yv, xv) / RAD;
            double lon = Norm360(nu + lp);

            int signIdx = (int)Math.Floor(lon / 30) % 12;
            st.Planets.Add(new PlanetPos
            {
                Name = p.Name, Abbrev = p.Abbrev, Symbol = p.Symbol,
                HelioLon = lon,
                SignIndex = signIdx,
                DegInSign = lon - signIdx * 30,
            });
        }
    }
}
