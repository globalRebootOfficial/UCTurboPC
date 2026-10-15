using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace UCTurboPC;

public sealed class Form1 : Form
{
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    private readonly TextBox address = new();
    private readonly Panel toolbar = new() { Dock = DockStyle.Top, Height = 48 };
    private readonly Button back = new(), forward = new(), reload = new(), add = new();
    private CoreWebView2Environment? engine;
    private bool initializing;
    private const string Home = "https://www.google.com";

    public Form1()
    {
        Text = "UC Turbo PC";
        Size = new Size(1250, 850);
        MinimumSize = new Size(700, 450);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(25, 27, 35);
        KeyPreview = true;
        toolbar.BackColor = Color.FromArgb(32, 35, 45);
        Configure(back, "←", 8);
        Configure(forward, "→", 50);
        Configure(reload, "⟳", 92);
        Configure(add, "+", 134);
        address.SetBounds(180, 9, 750, 30);
        address.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        address.BackColor = Color.FromArgb(48, 51, 63);
        address.ForeColor = Color.White;
        address.BorderStyle = BorderStyle.FixedSingle;
        address.Font = new Font("Segoe UI", 11);
        toolbar.Controls.Add(address);
        tabs.SelectedIndexChanged += (_, _) => UpdateAddress();
        Controls.Add(tabs);
        Controls.Add(toolbar);
        back.Click += (_, _) => { var w = Current(); if (w?.CoreWebView2 != null && w.CanGoBack) w.GoBack(); };
        forward.Click += (_, _) => { var w = Current(); if (w?.CoreWebView2 != null && w.CanGoForward) w.GoForward(); };
        reload.Click += (_, _) => { var w = Current(); if (w?.CoreWebView2 != null) w.Reload(); };
        add.Click += async (_, _) => await CreateTab();
        address.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Navigate(address.Text); } };
        KeyDown += async (_, e) =>
        {
            if (!e.Control) return;
            if (e.KeyCode == Keys.T) { e.SuppressKeyPress = true; await CreateTab(); }
            else if (e.KeyCode == Keys.W) { e.SuppressKeyPress = true; CloseTab(); }
            else if (e.KeyCode == Keys.L) { e.SuppressKeyPress = true; address.Focus(); address.SelectAll(); }
        };
        Shown += async (_, _) => await InitializeEngine();
    }

    private void Configure(Button b, string label, int x)
    {
        b.Text = label;
        b.SetBounds(x, 7, 38, 34);
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.BackColor = Color.FromArgb(48, 51, 63);
        b.ForeColor = Color.White;
        toolbar.Controls.Add(b);
    }

    private async Task InitializeEngine()
    {
        if (initializing || engine != null) return;
        initializing = true;
        try
        {
            string profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UCTurboPC", "Profile");
            Directory.CreateDirectory(profile);
            try
            {
                engine = await CoreWebView2Environment.CreateAsync(userDataFolder: profile);
            }
            catch (WebView2RuntimeNotFoundException first)
            {
                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "EdgeWebView", "Application");
                string? folder = Directory.Exists(root)
                    ? Directory.GetDirectories(root)
                        .Where(p => File.Exists(Path.Combine(p, "msedgewebview2.exe")))
                        .OrderByDescending(p => Version.TryParse(Path.GetFileName(p), out var v) ? v : new Version(0, 0))
                        .FirstOrDefault()
                    : null;
                if (folder == null)
                    throw new InvalidOperationException("WebView2 Runtime was not found in the standard installation directory.", first);
                try
                {
                    engine = await CoreWebView2Environment.CreateAsync(browserExecutableFolder: folder, userDataFolder: profile);
                }
                catch (Exception second)
                {
                    throw new AggregateException("WebView2 failed both automatic detection and explicit runtime loading from: " + folder, first, second);
                }
            }
            await CreateTab();
        }
        catch (Exception ex) { ShowError(ex); }
        finally { initializing = false; }
    }

    private WebView2? Current() =>
        tabs.SelectedTab?.Controls.OfType<WebView2>().FirstOrDefault();

    private async Task CreateTab()
    {
        if (engine == null) return;
        var page = new TabPage("Новая вкладка");
        var web = new WebView2 { Dock = DockStyle.Fill };
        page.Controls.Add(web);
        tabs.TabPages.Add(page);
        tabs.SelectedTab = page;
        try
        {
            await web.EnsureCoreWebView2Async(engine);
            if (page.IsDisposed || web.IsDisposed) return;
            web.CoreWebView2.NavigationStarting += (_, e) =>
            {
                if (tabs.SelectedTab == page) address.Text = e.Uri;
            };
            web.CoreWebView2.NavigationCompleted += (_, _) =>
            {
                if (page.IsDisposed || web.IsDisposed) return;
                string title = web.CoreWebView2.DocumentTitle;
                page.Text = string.IsNullOrWhiteSpace(title) ? "Новая вкладка" :
                    title.Length > 24 ? title[..24] + "…" : title;
                if (tabs.SelectedTab == page) UpdateAddress();
            };
            web.CoreWebView2.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                    web.CoreWebView2.Navigate(uri.ToString());
            };
            web.CoreWebView2.Navigate(Home);
        }
        catch (Exception ex)
        {
            tabs.TabPages.Remove(page);
            page.Dispose();
            ShowError(ex);
        }
    }

    private void Navigate(string value)
    {
        var web = Current();
        if (web?.CoreWebView2 == null) return;
        value = value.Trim();
        if (value.Length == 0) return;
        if (!value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            value = value.Contains(' ') || !value.Contains('.')
                ? "https://www.google.com/search?q=" + Uri.EscapeDataString(value)
                : "https://" + value;
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            web.CoreWebView2.Navigate(uri.ToString());
    }

    private void UpdateAddress() => address.Text = Current()?.CoreWebView2?.Source ?? "";

    private void CloseTab()
    {
        var page = tabs.SelectedTab;
        if (page == null) return;
        tabs.TabPages.Remove(page);
        page.Dispose();
        if (tabs.TabCount == 0 && engine != null) _ = CreateTab();
    }

    private void ShowError(Exception ex)
    {
        string details = "Ошибка UC Turbo PC:\n\n" + ex;
        try
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UCTurboPC");
            Directory.CreateDirectory(folder);
            string log = Path.Combine(folder, "error.log");
            File.WriteAllText(log, details);
            details += "\n\nЛог: " + log;
        }
        catch { }
        MessageBox.Show(details, "UC Turbo PC", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
