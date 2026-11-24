// Standalone, invisible test window; never connects to TIA or loads product code.
using System;
using System.IO;
using System.Windows.Forms;

internal sealed class CloseTestTarget : Form
{
    private readonly string readyFile, closedFile;
    private readonly DateTime readyAfter = DateTime.UtcNow.AddSeconds(2);
    private readonly Timer safetyTimer = new Timer { Interval = 15000 };
    private CloseTestTarget(string ready, string closed, bool owned)
    {
        readyFile = ready; closedFile = closed;
        Text = owned ? "Uygulama zaten açık" : "TIA Portal Auto Save Manager - isolated installer test";
        ShowInTaskbar = !owned; Opacity = 0;
        Shown += delegate { File.WriteAllText(readyFile, "ready"); safetyTimer.Start(); };
        safetyTimer.Tick += delegate { safetyTimer.Stop(); Close(); };
        FormClosing += delegate(object sender, FormClosingEventArgs args)
        {
            if (DateTime.UtcNow < readyAfter) { args.Cancel = true; return; }
            File.WriteAllText(closedFile, "normal-close");
        };
    }
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            // An unowned top-level window like WPF's main window, hidden from the shell.
            parameters.ExStyle = (parameters.ExStyle | 0x80) & ~0x40000;
            return parameters;
        }
    }
    [STAThread] private static void Main(string[] args)
    {
        if (args.Length != 2 && args.Length != 3) return;
        Application.Run(new CloseTestTarget(args[0], args[1], args.Length == 3 && args[2] == "owned"));
    }
}
