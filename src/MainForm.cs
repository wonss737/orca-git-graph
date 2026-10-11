using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GitGraphLite
{
    public sealed class MainForm : Form
    {
        readonly GraphView view = new GraphView();
        readonly TextBox details = new TextBox();
        readonly ToolStripComboBox recentBox = new ToolStripComboBox();
        readonly ToolStripComboBox limitBox = new ToolStripComboBox();
        readonly ToolStripButton allButton = new ToolStripButton("All branches");
        readonly ToolStripLabel status = new ToolStripLabel();
        readonly RepoWatcher watcher = new RepoWatcher();
        readonly OrcaFollower orca = new OrcaFollower();
        string repo;
        int loadVersion, detailVersion;
        bool syncingRecent;

        public MainForm(string startPath)
        {
            Text = "Git Graph Lite";
            Size = new Size(Scaled(1100), Scaled(760));
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);
            KeyPreview = true;
            AllowDrop = true;
            Controls.Add(BuildBody());
            Controls.Add(BuildToolbar());
            view.SelectedChanged += delegate { ShowDetails(); };
            watcher.Changed += delegate { if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(Reload)); };
            orca.Changed += path => { if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(() => OpenFolder(path))); };
            DragEnter += (s, e) => e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            DragDrop += (s, e) => OnDropped(e.Data.GetData(DataFormats.FileDrop) as string[]);
            SetRecent(Recent.Load());
            Shown += delegate
            {
                // Follow the worktree Orca is showing; a folder opened by hand stays until Orca switches.
                string followed = orca.Start();
                string first = startPath ?? followed ?? Recent.Load().FirstOrDefault();
                if (first != null) OpenFolder(first);
            };
        }

        ToolStrip BuildToolbar()
        {
            var bar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
            var open = new ToolStripButton("Open folder…");
            open.Click += delegate { ChooseFolder(); };
            recentBox.DropDownStyle = ComboBoxStyle.DropDownList;
            recentBox.AutoSize = false;
            recentBox.Width = Scaled(380);
            recentBox.SelectedIndexChanged += delegate
            {
                if (!syncingRecent && recentBox.SelectedItem != null) OpenFolder((string)recentBox.SelectedItem);
            };
            var refresh = new ToolStripButton("Refresh (F5)");
            refresh.Click += delegate { Reload(); };
            allButton.CheckOnClick = true;
            allButton.Checked = true;
            allButton.CheckedChanged += delegate { Reload(); };
            limitBox.DropDownStyle = ComboBoxStyle.DropDownList;
            limitBox.Items.AddRange(new object[] { "500", "2000", "10000" });
            limitBox.SelectedIndex = 1;
            limitBox.AutoSize = false;
            limitBox.Width = Scaled(70);
            limitBox.SelectedIndexChanged += delegate { Reload(); };
            bar.Items.AddRange(new ToolStripItem[]
            {
                open, recentBox, refresh, new ToolStripSeparator(), allButton,
                new ToolStripLabel("Max commits"), limitBox, new ToolStripSeparator(), status,
            });
            return bar;
        }

        Control BuildBody()
        {
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
            view.Dock = DockStyle.Fill;
            split.Panel1.Controls.Add(view);
            details.Multiline = true;
            details.ReadOnly = true;
            details.WordWrap = false;
            details.ScrollBars = ScrollBars.Both;
            details.BackColor = SystemColors.Window;
            details.Font = new Font("Consolas", 9.5f);
            details.Dock = DockStyle.Fill;
            split.Panel2.Controls.Add(details);
            Load += delegate { split.SplitterDistance = (int)(split.Height * 0.68); };
            return split;
        }

        // Pixel sizes are written for 96 DPI; scale them to the screen.
        int Scaled(int pixels)
        {
            using (var g = CreateGraphics()) return (int)(pixels * g.DpiX / 96f);
        }

        int Limit
        {
            get { return int.Parse((string)limitBox.SelectedItem); }
        }

        void ChooseFolder()
        {
            using (var dialog = new FolderBrowserDialog { Description = "Choose a git repository folder", ShowNewFolderButton = false })
            {
                if (repo != null) dialog.SelectedPath = repo;
                if (dialog.ShowDialog(this) == DialogResult.OK) OpenFolder(dialog.SelectedPath);
            }
        }

        void OnDropped(string[] paths)
        {
            if (paths == null || paths.Length == 0) return;
            string path = paths[0];
            OpenFolder(Directory.Exists(path) ? path : Path.GetDirectoryName(path));
        }

        void SetRecent(List<string> list)
        {
            syncingRecent = true;
            recentBox.Items.Clear();
            recentBox.Items.AddRange(list.Cast<object>().ToArray());
            if (list.Count > 0) recentBox.SelectedIndex = 0;
            syncingRecent = false;
        }

        async void OpenFolder(string path)
        {
            status.Text = "Opening…";
            RepoInfo info;
            try { info = await Task.Run(() => Git.Resolve(path)); }
            catch (Exception ex)
            {
                status.Text = "Not a git repository: " + path + " (" + FirstLine(ex.Message) + ")";
                return;
            }
            repo = info.Root;
            watcher.Watch(info.CommonDir);
            SetRecent(Recent.Add(repo));
            Reload();
        }

        async void Reload()
        {
            if (repo == null) return;
            int version = ++loadVersion;
            string dir = repo;
            int limit = Limit;
            bool all = allButton.Checked;
            status.Text = "Loading…";
            try
            {
                var result = await Task.Run(() => LoadGraph(dir, limit, all));
                if (version != loadVersion) return;
                view.SetData(result.Item1, result.Item2);
                Text = "Git Graph Lite - " + dir + "  [" + result.Item3 + "]";
                status.Text = result.Item1.Count + " commits" + (result.Item1.Count >= limit ? " (limit reached)" : "");
            }
            catch (Exception ex)
            {
                if (version == loadVersion) status.Text = "Error: " + FirstLine(ex.Message);
            }
        }

        static Tuple<List<Commit>, GraphLayout, string> LoadGraph(string dir, int limit, bool all)
        {
            var commits = Git.Log(dir, limit, all);
            return Tuple.Create(commits, GraphLayout.Build(commits), Git.CurrentBranch(dir));
        }

        async void ShowDetails()
        {
            var commit = view.Selected;
            int version = ++detailVersion;
            if (commit == null) { details.Text = ""; return; }
            string dir = repo;
            string text;
            try { text = await Task.Run(() => Git.Show(dir, commit.Hash)); }
            catch (Exception ex) { text = ex.Message; }
            if (version == detailVersion) details.Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
        }

        static string FirstLine(string text)
        {
            string line = (text ?? "").Split('\n')[0].Trim();
            return line.Length > 0 ? line : "Unknown error";
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5) { Reload(); return true; }
            if (keyData == (Keys.Control | Keys.O)) { ChooseFolder(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { orca.Dispose(); watcher.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
