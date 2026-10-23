using System;

/// <summary>Stage grades are capped at D; totals use unrounded average points.</summary>
public static class ClearGradeRules
{
    public static int Points(double seconds)
    {
        if (seconds <= 600d) return 5;
        if (seconds <= 1200d) return 4;
        if (seconds <= 1800d) return 3;
        if (seconds <= 2400d) return 2;
        return 1;
    }
    public static string StageGrade(double seconds) => seconds < 0d ? "--" : "SABCD"[5-Points(seconds)].ToString();
    public static string OverallGrade(double average)
    {
        if (average >= 4.5d) return "S";
        if (average >= 3.5d) return "A";
        if (average >= 2.5d) return "B";
        if (average >= 1.5d) return "C";
        return "D";
    }
    public static string FormatTime(double seconds)
    {
        if (seconds < 0d) return "--:--:--";
        long time=(long)Math.Floor(seconds);
        return $"{time/3600:00}:{time/60%60:00}:{time%60:00}";
    }
}
