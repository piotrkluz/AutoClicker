using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Gma.System.MouseKeyHook;

namespace MacroRecorderPro;

public partial class MainForm : Form
{
    [DllImport("user32.dll")]
    static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

    private const uint MOUSEEVENTF_LEFTDOWN = 0x02;
    private const uint MOUSEEVENTF_LEFTUP = 0x04;

    private IKeyboardMouseEvents _globalHook;
    private Stopwatch _stopwatch = new();
    private StringBuilder _textBuffer = new();
    private bool _isRecording;
    private bool _isRunning;

    private TextBox txtScript;
    private Button btnRecord;
    private Button btnRun;
    private NumericUpDown repeatTimes;
    private Label lblTimes;

    public MainForm()
    {
        InitializeUiComponent();
    }

    private void InitializeUiComponent()
    {
        Text = "Macro Recorder Pro (by Zabijakaa)";
        Size = new System.Drawing.Size(500, 600);
        TopMost = true;

        txtScript = new TextBox
        {
            Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical,
            Font = new System.Drawing.Font("Consolas", 10)
        };

        var pnl = new Panel { Dock = DockStyle.Bottom, Height = 60 };
        btnRecord = new Button { Text = "Record", Dock = DockStyle.Left, Width = 80 };
        btnRun = new Button { Text = "Run", Dock = DockStyle.Right, Width = 80 };

        lblTimes = new Label { Text = "Repeat times:", Left = 80, Top = 5, Width = 100 };
        repeatTimes = new NumericUpDown { Left = 85, Top = 30, Width = 60, Minimum = 0, Value = 1 };

        btnRecord.Click += BtnRecord_Click;
        btnRun.Click += BtnRun_Click;

        pnl.Controls.AddRange(new Control[] { btnRecord, btnRun, lblTimes, repeatTimes });
        Controls.Add(txtScript);
        Controls.Add(pnl);
    }

    private void BtnRecord_Click(object sender, EventArgs e)
    {
        if (!_isRecording)
        {
            _textBuffer.Clear();
            // txtScript.Clear();
            _globalHook = Hook.GlobalEvents();
            _globalHook.MouseDownExt += (s, ex) =>
            {
                if (Bounds.Contains(ex.Location)) return;
                WriteTextFromBuffer();
                if (_stopwatch.ElapsedMilliseconds > 50) AddLine($"Delay({_stopwatch.ElapsedMilliseconds})");
                AddLine($"Click({ex.X},{ex.Y})");
                _stopwatch.Restart();
            };
            _globalHook.KeyDown += (s, ex) =>
            {
                var key = MapFunctionalKey(ex.KeyCode);
                if(key != null) _textBuffer.Append("{" + key + "}");
                _stopwatch.Restart();
            };
            _globalHook.KeyPress += (s, ex) =>
            {
                if (!char.IsControl(ex.KeyChar)) _textBuffer.Append(ex.KeyChar);
                _stopwatch.Restart();
            };
            _stopwatch.Restart();
            btnRecord.Text = "Stop REC";
            _isRecording = true;
        }
        else
        {
            StopHook();
            btnRecord.Text = "Record";
            _isRecording = false;
        }
    }

    private void StopHook()
    {
        WriteTextFromBuffer();
        _globalHook.Dispose();
    }

    private void AddLine(string text) => txtScript.AppendText(text + Environment.NewLine);

    private void WriteTextFromBuffer()
    {
        if (_textBuffer.Length == 0) return;
        AddLine($"TypeText({_textBuffer})");
        if (_stopwatch.ElapsedMilliseconds > 50) AddLine($"Delay({_stopwatch.ElapsedMilliseconds})");
        _textBuffer.Clear();
        _stopwatch.Restart();
    }

    private async void BtnRun_Click(object sender, EventArgs e)
    {
        if (_isRunning) return;
        if (repeatTimes.Value == 0) repeatTimes.Value = 1;

        _isRunning = true;
        btnRun.Text = "STOP (Ctrl+C)";
        btnRun.Enabled = false;

        // Wait for Ctrl+C
        using var runHook = Hook.GlobalEvents();
        runHook.KeyDown += (s, ex) =>
        {
            if (ex.Control && ex.KeyCode == Keys.C)
            {
                _isRunning = false;
                ex.Handled = true; // Zapobiega przesłaniu Ctrl+C do innych aplikacji
            }
        };

        await Task.Delay(500);

        while (repeatTimes.Value > 0 && _isRunning)
        {
            var lines = txtScript.Lines;

            for (int i = 0; i < lines.Length; i++)
            {
                if (!_isRunning) break;
                HighlightLine(i);
                var line = lines[i];
                try
                {
                    ExecuteCommand(line);
                }
                catch (Exception ex)
                {
                    _isRunning = false;
                    Invoke((MethodInvoker)delegate
                    {
                        MessageBox.Show(
                            $"Parse line #{i + 1} failed: '{line}'. {ex} Use commands:\n- Delay(xxx)\n- Click(x,y)\n- TypeText(abc{{tab}}def{{enter}}aa)",
                            "Script error.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    });
                    break;
                }
                await Task.Delay(10); // lets process to handle KeyDown event
            }

            if (_isRunning) Invoke((MethodInvoker)delegate { repeatTimes.Value--; });
        }

        _isRunning = false;
        btnRun.Text = "Run";
        btnRun.Enabled = true;
        WindowState = FormWindowState.Normal;
    }

    private void ExecuteCommand(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        var match = Regex.Match(line, @"^(Delay|Click|TypeText)\((.*)\)");
        if (!match.Success) throw new("Invalid command");
        var command = match.Groups[1].Value;
        var args = match.Groups[2].Value;

        switch (command)
        {
            case "Delay":
                Thread.Sleep(int.Parse(args));
                break;
            case "TypeText":
                SendKeys.SendWait(args);
                break;
            case "Click":
                var xy = args.Split(",").Select(int.Parse).ToArray();
                int x = xy[0], y = xy[1];
                Cursor.Position = new System.Drawing.Point(x, y);
                mouse_event(MOUSEEVENTF_LEFTDOWN, (uint)x, (uint)y, 0, 0);
                mouse_event(MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, 0);
                break;
        }
    }
    
    /// <summary> Map Key to be valid for System.Windows.Forms.SendKeys methods </summary>
    private string? MapFunctionalKey(Keys key) => key switch
    {
        Keys.Enter => "ENTER",
        Keys.Tab => "TAB",
        Keys.Escape => "ESC",
        Keys.Home => "HOME",
        Keys.End => "END",
        Keys.Left => "LEFT",
        Keys.Right => "RIGHT",
        Keys.Up => "UP",
        Keys.Down => "DOWN",
        Keys.Prior => "PGUP",
        Keys.Next => "PGDN",
        Keys.Cancel => "BREAK",
        Keys.Back => "BACKSPACE",
        Keys.Clear => "CLEAR",
        Keys.Capital => "CAPSLOCK",
        Keys.Insert => "INSERT",
        Keys.Delete => "DEL",
        Keys.F1 => "F1",
        Keys.F2 => "F2",
        Keys.F3 => "F3",
        Keys.F4 => "F4",
        Keys.F5 => "F5",
        Keys.F6 => "F6",
        Keys.F7 => "F7",
        Keys.F8 => "F8",
        Keys.F9 => "F9",
        Keys.F10 => "F10",
        Keys.F11 => "F11",
        Keys.F12 => "F12",
        Keys.Multiply => "MULTIPLY",
        Keys.Add => "ADD",
        Keys.Subtract => "SUBTRACT",
        Keys.Divide => "DIVIDE",
        _ => null
    };
    
    private void HighlightLine(int lineIndex)
    {
        Invoke((MethodInvoker)delegate
        {
            var start = txtScript.GetFirstCharIndexFromLine(lineIndex);
            var length = txtScript.Lines[lineIndex].Length;

            if (start < 0) return;
            txtScript.Focus();
            txtScript.Select(start, length);
            txtScript.ScrollToCaret();
        });
    }
}