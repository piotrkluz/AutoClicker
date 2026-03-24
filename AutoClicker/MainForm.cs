using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AutoClicker.MouseRecorder;

public partial class MainForm : Form
{
    // Importy WinAPI do symulacji kliknięć i przechwytywania myszy
    [DllImport("user32.dll")]
    static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

    private const int MOUSEEVENTF_LEFTDOWN = 0x02;
    private const int MOUSEEVENTF_LEFTUP = 0x04;

    private LowLevelMouseHook _mouseHook;
    private Stopwatch _stopwatch = new Stopwatch();
    private bool _isRecording = false;

    // Elementy UI
    private TextBox txtScript;
    private Button btnRecord;
    private Button btnRun;

    public MainForm()
    {
        InitializeUI();
        _mouseHook = new LowLevelMouseHook();
        _mouseHook.OnMouseClick += MouseHook_OnMouseClick;
    }

    private void InitializeUI()
    {
        this.Text = "Mouse Recorder";
        this.Size = new System.Drawing.Size(400, 500);

        txtScript = new TextBox
        {
            Multiline = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical,
            Font = new System.Drawing.Font("Consolas", 10)
        };

        Panel panelBottom = new Panel { Dock = DockStyle.Bottom, Height = 50 };
        btnRecord = new Button { Text = "Record", Dock = DockStyle.Left, Width = 100 };
        btnRun = new Button { Text = "Run", Dock = DockStyle.Right, Width = 100 };

        btnRecord.Click += BtnRecord_Click;
        btnRun.Click += BtnRun_Click;

        panelBottom.Controls.Add(btnRecord);
        panelBottom.Controls.Add(btnRun);
        this.Controls.Add(txtScript);
        this.Controls.Add(panelBottom);
    }

    private void MouseHook_OnMouseClick(int x, int y)
    {
        if (!_isRecording) return;

        if (_stopwatch.IsRunning)
        {
            txtScript.AppendText($"Delay({_stopwatch.ElapsedMilliseconds})\r\n");
        }
            
        txtScript.AppendText($"Click({x},{y})\r\n");
        _stopwatch.Restart();
    }

    private void BtnRecord_Click(object sender, EventArgs e)
    {
        _isRecording = !_isRecording;
        if (_isRecording)
        {
            txtScript.Clear();
            _stopwatch.Restart();
            btnRecord.Text = "STOP";
            _mouseHook.Install();
        }
        else
        {
            _stopwatch.Stop();
            btnRecord.Text = "Record";
            _mouseHook.Uninstall();
        }
    }

    private void BtnRun_Click(object sender, EventArgs e)
    {
        string[] lines = txtScript.Lines;
        this.WindowState = FormWindowState.Minimized; // Minimalizuj, by nie zasłaniać
        Thread.Sleep(500); // Chwila na przygotowanie

        foreach (string line in lines)
        {
            if (line.StartsWith("Click"))
            {
                var parts = line.Replace("Click(", "").Replace(")", "").Split(',');
                int x = int.Parse(parts[0]);
                int y = int.Parse(parts[1]);

                Cursor.Position = new System.Drawing.Point(x, y);
                mouse_event(MOUSEEVENTF_LEFTDOWN, x, y, 0, 0);
                mouse_event(MOUSEEVENTF_LEFTUP, x, y, 0, 0);
            }
            else if (line.StartsWith("Delay"))
            {
                int ms = int.Parse(line.Replace("Delay(", "").Replace(")", ""));
                Thread.Sleep(ms);
            }
        }
        this.WindowState = FormWindowState.Normal;
    }
}

// Klasa pomocnicza do przechwytywania myszy w całym systemie
public class LowLevelMouseHook
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;

    public delegate void MouseClickDelegate(int x, int y);
    public event MouseClickDelegate OnMouseClick;

    private LowLevelMouseProc _proc;
    private IntPtr _hookID = IntPtr.Zero;

    public LowLevelMouseHook() => _proc = HookCallback;

    public void Install() => _hookID = SetHook(_proc);
    public void Uninstall() => UnhookWindowsHookEx(_hookID);

    private IntPtr SetHook(LowLevelMouseProc proc)
    {
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule)
        {
            return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN)
        {
            MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            OnMouseClick?.Invoke(hookStruct.pt.x, hookStruct.pt.y);
        }
        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData; public uint flags; public uint time; public IntPtr dwExtraInfo; }

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}