using System.Runtime.InteropServices;

namespace BaumAdminTool;

internal static class AppTheme
{
    public static Color BgDeep        = Color.FromArgb(13,  13,  18);
    public static Color BgMain        = Color.FromArgb(18,  18,  26);
    public static Color BgPanel       = Color.FromArgb(22,  22,  32);
    public static Color BgCard        = Color.FromArgb(30,  30,  44);
    public static Color BgCardHover   = Color.FromArgb(38,  38,  54);
    public static Color Accent        = Color.FromArgb(78,  131, 253);
    public static Color AccentHover   = Color.FromArgb(98,  151, 255);
    public static Color Success       = Color.FromArgb(72,  199, 142);
    public static Color Warning       = Color.FromArgb(255, 189, 46);
    public static Color Danger        = Color.FromArgb(240, 80,  80);
    public static Color Border        = Color.FromArgb(45,  45,  65);
    public static Color TextPrimary   = Color.FromArgb(230, 230, 240);
    public static Color TextSecondary = Color.FromArgb(160, 160, 180);
    public static Color TextMuted     = Color.FromArgb(100, 100, 120);

    public static readonly Font FontTitle  = new("Segoe UI", 16f, FontStyle.Bold);
    public static readonly Font FontHeader = new("Segoe UI", 11f, FontStyle.Bold);
    public static readonly Font FontBody   = new("Segoe UI", 10f);
    public static readonly Font FontBold   = new("Segoe UI", 10f, FontStyle.Bold);
    public static readonly Font FontSmall  = new("Segoe UI",  9f);
    public static readonly Font FontButton = new("Segoe UI",  9f, FontStyle.Bold);
    public static readonly Font FontMono   = new("Consolas",  9f);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string pszSubAppName, string? pszSubIdList);

    public static void ApplyDarkScrollBar(Control c)
    {
        void Apply() { try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { } }
        if (c.IsHandleCreated) Apply();
        else c.HandleCreated += (_, _) => Apply();
    }

    public static Button MakeButton(string text, Color? bg = null)
    {
        var btn = new Button
        {
            Text      = text,
            ForeColor = TextPrimary,
            BackColor = bg ?? BgCard,
            FlatStyle = FlatStyle.Flat,
            Font      = FontButton,
            Cursor    = Cursors.Hand,
        };
        btn.FlatAppearance.BorderColor = Border;
        btn.FlatAppearance.MouseOverBackColor = bg.HasValue
            ? ControlPaint.Light(bg.Value, 0.2f)
            : BgCardHover;
        return btn;
    }

    public static Label MakeLabel(string text, Font? font = null, Color? color = null) => new()
    {
        Text      = text,
        ForeColor = color ?? TextPrimary,
        Font      = font  ?? FontBody,
        AutoSize  = true,
        BackColor = Color.Transparent,
    };
}
