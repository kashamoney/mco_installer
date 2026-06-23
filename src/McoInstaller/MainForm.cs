using System.Text;

namespace McoInstaller;

public sealed class MainForm : Form
{
    private readonly TextBox _installPathBox = new();
    private readonly Label _folderStatusLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Button _installButton = new();
    private readonly Button _cancelButton = new();
    private readonly Button _detailsButton = new();
    private readonly CheckBox _launchGameCheckBox = new();
    private readonly ProgressBar _progressBar = new();
    private readonly TextBox _detailsBox = new();
    private readonly StringBuilder _log = new();

    private PayloadPackage? _payload;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _detailsVisible;

    public MainForm()
    {
        Text = $"Motor City Online Setup {AppVersion.Display}";
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? Application.ExecutablePath) ?? Icon;
        ClientSize = new Size(640, 420);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont;

        BuildUi();
        Load += OnLoad;
        FormClosing += OnFormClosing;
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = SystemColors.Control
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildContent(), 0, 1);
        root.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = SystemColors.ControlDark }, 0, 2);
        root.Controls.Add(BuildButtons(), 0, 3);
        Controls.Add(root);
    }

    private Control BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(16, 12, 16, 10)
        };

        var icon = new PictureBox
        {
            Image = (Icon ?? SystemIcons.Application).ToBitmap(),
            SizeMode = PictureBoxSizeMode.CenterImage,
            Size = new Size(40, 40),
            Location = new Point(16, 17)
        };

        var title = new Label
        {
            Text = $"Motor City Online Setup {AppVersion.Display}",
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(66, 15)
        };

        var subtitle = new Label
        {
            Text = "This will configure your existing game install for the custom server.",
            AutoSize = true,
            Location = new Point(66, 39)
        };

        header.Controls.Add(icon);
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        return header;
    }

    private Control BuildContent()
    {
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            Padding = new Padding(16, 16, 16, 10)
        };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var intro = new Label
        {
            Text = "Select your Motor City Online folder.",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6)
        };

        var note = new Label
        {
            Text = "The folder must contain MCity.exe.",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 14)
        };

        content.Controls.Add(intro, 0, 0);
        content.Controls.Add(note, 0, 1);
        content.Controls.Add(BuildPathPanel(), 0, 2);

        _folderStatusLabel.AutoSize = true;
        _folderStatusLabel.Margin = new Padding(0, 2, 0, 14);
        content.Controls.Add(_folderStatusLabel, 0, 3);

        _launchGameCheckBox.Text = "Launch Motor City Online when setup finishes";
        _launchGameCheckBox.AutoSize = true;
        _launchGameCheckBox.Margin = new Padding(0, 0, 0, 16);
        content.Controls.Add(_launchGameCheckBox, 0, 4);

        _progressBar.Dock = DockStyle.Top;
        _progressBar.Style = ProgressBarStyle.Marquee;
        _progressBar.MarqueeAnimationSpeed = 0;
        _progressBar.Height = 18;
        _progressBar.Margin = new Padding(0, 0, 0, 10);
        content.Controls.Add(_progressBar, 0, 5);

        _statusLabel.Text = "Ready to install.";
        _statusLabel.AutoSize = true;
        _statusLabel.Margin = new Padding(0, 0, 0, 8);
        content.Controls.Add(_statusLabel, 0, 6);

        _detailsBox.Dock = DockStyle.Fill;
        _detailsBox.Multiline = true;
        _detailsBox.ScrollBars = ScrollBars.Vertical;
        _detailsBox.ReadOnly = true;
        _detailsBox.Visible = false;
        _detailsBox.Font = new Font("Consolas", 9);
        content.Controls.Add(_detailsBox, 0, 7);

        return content;
    }

    private Control BuildPathPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 3,
            RowCount = 1,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 0)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var label = new Label
        {
            Text = "Folder:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 4, 8, 0)
        };

        _installPathBox.Dock = DockStyle.Fill;
        _installPathBox.Margin = new Padding(0, 0, 8, 0);
        _installPathBox.TextChanged += (_, _) => ValidateInstallFolder();

        var browseButton = new Button
        {
            Text = "Browse...",
            AutoSize = true,
            Anchor = AnchorStyles.Right
        };
        browseButton.Click += OnBrowseInstallFolder;

        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(_installPathBox, 1, 0);
        panel.Controls.Add(browseButton, 2, 0);
        return panel;
    }

    private Control BuildButtons()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(12, 10, 12, 10)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _detailsButton.Text = "Details";
        _detailsButton.AutoSize = true;
        _detailsButton.Click += (_, _) => ToggleDetails();

        _installButton.Text = AdminUtil.IsAdministrator() ? "Install" : "Restart as administrator";
        _installButton.AutoSize = true;
        _installButton.Click += OnInstallAsync;

        _cancelButton.Text = "Cancel";
        _cancelButton.AutoSize = true;
        _cancelButton.Margin = new Padding(8, 0, 0, 0);
        _cancelButton.Click += (_, _) =>
        {
            if (_cancellationTokenSource is null)
            {
                Close();
            }
            else
            {
                _cancellationTokenSource.Cancel();
            }
        };

        panel.Controls.Add(_detailsButton, 0, 0);
        panel.Controls.Add(new Panel(), 1, 0);
        panel.Controls.Add(_installButton, 2, 0);
        panel.Controls.Add(_cancelButton, 3, 0);
        return panel;
    }

    private void OnLoad(object? sender, EventArgs e)
    {
        LoadPayload();
        ValidateInstallFolder();
    }

    private void LoadPayload()
    {
        try
        {
            _payload = PayloadPackage.Load();
            var detectedPath = GameInstallLocator.Locate(_payload.Settings.PreferredInstallPath);
            _installPathBox.Text = detectedPath ?? GameInstallLocator.DefaultInstallPath;
            AppendLog($"Installer version: {AppVersion.Display}");
            AppendLog(detectedPath is null
                ? "Game folder was not detected automatically."
                : $"Detected game folder: {detectedPath}");
        }
        catch (Exception ex)
        {
            _payload = null;
            _statusLabel.Text = "This installer package is incomplete.";
            _statusLabel.ForeColor = Color.DarkRed;
            _installButton.Enabled = false;
            AppendLog(ex.Message);
        }
    }

    private void ValidateInstallFolder()
    {
        if (_cancellationTokenSource is not null)
        {
            return;
        }

        var path = GameInstallLocator.NormalizeInstallDirectory(_installPathBox.Text);
        var valid = GameInstallLocator.IsGameInstallDirectory(path);

        if (valid)
        {
            _folderStatusLabel.Text = "MCity.exe found.";
            _folderStatusLabel.ForeColor = Color.DarkGreen;
            _statusLabel.Text = "Ready to install.";
            _statusLabel.ForeColor = SystemColors.ControlText;
        }
        else
        {
            _folderStatusLabel.Text = "MCity.exe was not found in this folder.";
            _folderStatusLabel.ForeColor = Color.DarkRed;
            _statusLabel.Text = "Choose the folder where Motor City Online is installed.";
            _statusLabel.ForeColor = SystemColors.ControlText;
        }

        _installButton.Enabled = _payload is not null && (valid || !AdminUtil.IsAdministrator());
    }

    private void OnBrowseInstallFolder(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the folder that contains MCity.exe",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_installPathBox.Text) ? _installPathBox.Text : GameInstallLocator.DefaultInstallPath
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _installPathBox.Text = dialog.SelectedPath;
        }
    }

    private async void OnInstallAsync(object? sender, EventArgs e)
    {
        if (!AdminUtil.IsAdministrator())
        {
            if (AdminUtil.TryRestartElevated())
            {
                Close();
            }
            else
            {
                MessageBox.Show(this, "Administrator permission is required to finish setup.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return;
        }

        if (_payload is null)
        {
            MessageBox.Show(this, "This installer package is incomplete.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (!GameInstallLocator.IsGameInstallDirectory(GameInstallLocator.NormalizeInstallDirectory(_installPathBox.Text)))
        {
            MessageBox.Show(this, "Choose the folder that contains MCity.exe.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            ValidateInstallFolder();
            return;
        }

        var options = new InstallOptions
        {
            InstallPath = _installPathBox.Text.Trim(),
            ApplyUpdate = true,
            InstallCertificate = true,
            PatchRegistry = true,
            LaunchGame = _launchGameCheckBox.Checked
        };

        SetRunningState(true);
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var engine = new InstallerEngine(AppendLog);
            await Task.Run(async () => await engine.RunAsync(options, _payload, _cancellationTokenSource.Token), _cancellationTokenSource.Token);
            _statusLabel.Text = "Setup complete.";
            _statusLabel.ForeColor = Color.DarkGreen;
            MessageBox.Show(this, "Motor City Online setup is complete.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "Setup canceled.";
            AppendLog("Setup canceled.");
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Setup failed.";
            _statusLabel.ForeColor = Color.DarkRed;
            AppendLog("ERROR: " + ex.Message);
            MessageBox.Show(this, ex.Message, "Setup failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
            SetRunningState(false);
            ValidateInstallFolder();
        }
    }

    private void SetRunningState(bool isRunning)
    {
        _installPathBox.Enabled = !isRunning;
        _launchGameCheckBox.Enabled = !isRunning;
        _installButton.Enabled = !isRunning;
        _cancelButton.Text = isRunning ? "Cancel" : "Close";
        _progressBar.MarqueeAnimationSpeed = isRunning ? 30 : 0;
        _progressBar.Style = isRunning ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
        if (isRunning)
        {
            _statusLabel.Text = "Installing...";
            _statusLabel.ForeColor = SystemColors.ControlText;
        }
    }

    private void ToggleDetails()
    {
        _detailsVisible = !_detailsVisible;
        _detailsBox.Visible = _detailsVisible;
        _detailsButton.Text = _detailsVisible ? "Hide details" : "Details";
        ClientSize = _detailsVisible ? new Size(640, 560) : new Size(640, 420);
        _detailsBox.Text = _log.ToString();
        _detailsBox.SelectionStart = _detailsBox.TextLength;
        _detailsBox.ScrollToCaret();
    }

    private void AppendLog(string message)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(AppendLog), message);
            return;
        }

        _log.AppendLine($"[{DateTime.Now:T}] {message}");
        if (_detailsVisible)
        {
            _detailsBox.AppendText($"[{DateTime.Now:T}] {message}{Environment.NewLine}");
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_cancellationTokenSource is null)
        {
            return;
        }

        var result = MessageBox.Show(this, "Setup is still running. Cancel it and close?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes)
        {
            e.Cancel = true;
            return;
        }

        _cancellationTokenSource.Cancel();
    }
}
