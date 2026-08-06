using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace PublisherToPdf
{
    public sealed class MainForm : Form
    {
        private ListView _list;
        private ColumnHeader _colFile;
        private ColumnHeader _colFolder;
        private ColumnHeader _colStatus;

        private Button _btnAdd;
        private Button _btnRemove;
        private Button _btnClear;

        private RadioButton _rdoSameFolder;
        private RadioButton _rdoCustomFolder;
        private TextBox _txtOutput;
        private Button _btnBrowseOut;
        private CheckBox _chkFitToContent;
        private CheckBox _chkOpenWhenDone;

        private ProgressBar _progress;
        private Label _status;
        private Button _btnConvert;
        private Button _btnCancel;

        private Thread _worker;
        private volatile bool _cancelRequested;

        public MainForm()
        {
            BuildUi();
        }

        // ---------------------------------------------------------------- UI

        private void BuildUi()
        {
            Text = "Publisher to PDF Converter";
            Font = new Font("Segoe UI", 9F);
            Size = new Size(780, 600);
            MinimumSize = new Size(620, 520);
            StartPosition = FormStartPosition.CenterScreen;
            AllowDrop = true;
            DragEnter += MainForm_DragEnter;
            DragDrop += MainForm_DragDrop;

            var intro = new Label
            {
                Text = "Add one or more Microsoft Publisher (.pub) files, choose where to save the PDFs, then click Convert.",
                Dock = DockStyle.Top,
                Padding = new Padding(12, 12, 12, 6),
                Height = 40,
                AutoSize = false
            };

            // --- File list + side buttons ---
            _list = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HideSelection = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(12, 52),
                Size = new Size(590, 238)
            };
            _colFile = _list.Columns.Add("File", 220);
            _colFolder = _list.Columns.Add("Folder", 250);
            _colStatus = _list.Columns.Add("Status", 110);
            _list.SelectedIndexChanged += (s, e) => UpdateButtons();

            _btnAdd = MakeSideButton("Add Files…", 52);
            _btnAdd.Click += BtnAdd_Click;
            _btnRemove = MakeSideButton("Remove", 88);
            _btnRemove.Click += BtnRemove_Click;
            _btnClear = MakeSideButton("Clear All", 124);
            _btnClear.Click += (s, e) => { _list.Items.Clear(); UpdateButtons(); };

            // --- Output options group ---
            var grpOut = new GroupBox
            {
                Text = "Output",
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(12, 300),
                Size = new Size(680, 150)
            };

            _rdoSameFolder = new RadioButton
            {
                Text = "Save each PDF next to its original .pub file",
                Location = new Point(14, 24),
                AutoSize = true,
                Checked = true
            };
            _rdoCustomFolder = new RadioButton
            {
                Text = "Save all PDFs to this folder:",
                Location = new Point(14, 52),
                AutoSize = true
            };
            _rdoCustomFolder.CheckedChanged += (s, e) => UpdateButtons();

            _txtOutput = new TextBox
            {
                Location = new Point(36, 74),
                Width = 520,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Enabled = false
            };
            _btnBrowseOut = new Button
            {
                Text = "Browse…",
                Location = new Point(566, 72),
                Width = 90,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Enabled = false
            };
            _btnBrowseOut.Click += BtnBrowseOut_Click;

            _rdoCustomFolder.CheckedChanged += (s, e) =>
            {
                bool custom = _rdoCustomFolder.Checked;
                _txtOutput.Enabled = custom;
                _btnBrowseOut.Enabled = custom;
            };

            _chkFitToContent = new CheckBox
            {
                Text = "Fit page to content (prevents clipping of items past the page edge)",
                Location = new Point(14, 98),
                AutoSize = true,
                Checked = true
            };

            _chkOpenWhenDone = new CheckBox
            {
                Text = "Open the output folder when finished",
                Location = new Point(14, 122),
                AutoSize = true,
                Checked = true
            };

            grpOut.Controls.AddRange(new Control[]
            {
                _rdoSameFolder, _rdoCustomFolder, _txtOutput, _btnBrowseOut, _chkFitToContent, _chkOpenWhenDone
            });

            // --- Progress + action row ---
            _progress = new ProgressBar
            {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(12, 462),
                Size = new Size(680, 18)
            };
            _status = new Label
            {
                Text = "Ready.",
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(12, 486),
                Size = new Size(470, 40),
                AutoSize = false
            };
            _btnConvert = new Button
            {
                Text = "Convert",
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Location = new Point(566, 488),
                Size = new Size(126, 34)
            };
            _btnConvert.Click += BtnConvert_Click;
            _btnCancel = new Button
            {
                Text = "Cancel",
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Location = new Point(482, 488),
                Size = new Size(78, 34),
                Enabled = false
            };
            _btnCancel.Click += (s, e) => { _cancelRequested = true; _btnCancel.Enabled = false; SetStatus("Finishing current file, then stopping…"); };

            Controls.AddRange(new Control[]
            {
                intro, _list, _btnAdd, _btnRemove, _btnClear,
                grpOut, _progress, _status, _btnConvert, _btnCancel
            });

            UpdateButtons();
        }

        private Button MakeSideButton(string text, int top)
        {
            return new Button
            {
                Text = text,
                Location = new Point(612, top),
                Size = new Size(150, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
        }

        // ------------------------------------------------------------- Files

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Select Publisher files";
                dlg.Filter = "Publisher files (*.pub)|*.pub|All files (*.*)|*.*";
                dlg.Multiselect = true;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    AddFiles(dlg.FileNames);
            }
        }

        private void AddFiles(IEnumerable<string> paths)
        {
            var existing = new HashSet<string>(
                _list.Items.Cast<ListViewItem>().Select(i => (string)i.Tag),
                StringComparer.OrdinalIgnoreCase);

            int added = 0;
            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) continue;
                if (existing.Contains(path)) continue;

                existing.Add(path);
                var item = new ListViewItem(Path.GetFileName(path)) { Tag = path };
                item.SubItems.Add(Path.GetDirectoryName(path));
                item.SubItems.Add("Pending");
                _list.Items.Add(item);
                added++;
            }

            if (added > 0)
            {
                _colFile.Width = -1;   // auto-size to content
                _colFolder.Width = -1;
            }
            UpdateButtons();
        }

        private void BtnRemove_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in _list.SelectedItems)
                item.Remove();
            UpdateButtons();
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var pub = files.Where(f =>
                Directory.Exists(f)
                    ? false
                    : string.Equals(Path.GetExtension(f), ".pub", StringComparison.OrdinalIgnoreCase));
            // Also expand any dropped folders for .pub files.
            var fromFolders = files.Where(Directory.Exists)
                                   .SelectMany(d => Directory.EnumerateFiles(d, "*.pub", SearchOption.AllDirectories));
            AddFiles(pub.Concat(fromFolders));
        }

        // ------------------------------------------------------------ Output

        private void BtnBrowseOut_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Choose a folder for the converted PDFs";
                if (Directory.Exists(_txtOutput.Text))
                    dlg.SelectedPath = _txtOutput.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    _txtOutput.Text = dlg.SelectedPath;
            }
        }

        // --------------------------------------------------------- Converting

        private void BtnConvert_Click(object sender, EventArgs e)
        {
            if (_list.Items.Count == 0)
            {
                MessageBox.Show(this, "Add at least one .pub file first.", "Nothing to convert",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string customFolder = null;
            if (_rdoCustomFolder.Checked)
            {
                customFolder = _txtOutput.Text.Trim();
                if (string.IsNullOrEmpty(customFolder))
                {
                    MessageBox.Show(this, "Choose an output folder, or select \"next to original\".",
                        "Output folder required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                try { Directory.CreateDirectory(customFolder); }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Cannot create output folder:\r\n" + ex.Message,
                        "Output folder error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            if (!PublisherConverter.IsPublisherInstalled)
            {
                MessageBox.Show(this,
                    "Microsoft Publisher is not installed on this computer.\r\n\r\n" +
                    "Publisher is required to open and convert .pub files. Please install it and try again.",
                    "Publisher not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Snapshot the work list on the UI thread.
            var jobs = _list.Items.Cast<ListViewItem>()
                .Select(i => new Job { Item = i, Input = (string)i.Tag })
                .ToList();

            foreach (var j in jobs)
                SetItemStatus(j.Item, "Pending");

            bool fitToContent = _chkFitToContent.Checked;

            _cancelRequested = false;
            SetBusy(true);
            _progress.Minimum = 0;
            _progress.Maximum = jobs.Count;
            _progress.Value = 0;

            _worker = new Thread(() => RunBatch(jobs, customFolder, fitToContent))
            {
                IsBackground = true,
                Name = "PublisherConvert"
            };
            _worker.SetApartmentState(ApartmentState.STA); // Office automation wants STA.
            _worker.Start();
        }

        private sealed class Job
        {
            public ListViewItem Item;
            public string Input;
        }

        private void RunBatch(List<Job> jobs, string customFolder, bool fitToContent)
        {
            int ok = 0, failed = 0, skipped = 0;
            string lastOutputDir = null;
            PublisherConverter converter = null;

            try
            {
                converter = new PublisherConverter();
                converter.Start();

                foreach (var job in jobs)
                {
                    if (_cancelRequested)
                    {
                        SetItemStatus(job.Item, "Skipped");
                        skipped++;
                        continue;
                    }

                    string dir = customFolder ?? Path.GetDirectoryName(job.Input);
                    string outPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(job.Input) + ".pdf");
                    lastOutputDir = dir;

                    SetItemStatus(job.Item, "Converting…");
                    SetStatus("Converting " + Path.GetFileName(job.Input) + " …");

                    try
                    {
                        converter.Convert(job.Input, outPath, fitToContent);
                        SetItemStatus(job.Item, "Done");
                        ok++;
                    }
                    catch (Exception ex)
                    {
                        SetItemStatus(job.Item, "Failed");
                        job.Item.ToolTipText = ex.Message;
                        failed++;
                    }

                    StepProgress();
                }
            }
            catch (Exception ex)
            {
                // Fatal (e.g. Publisher failed to start): surface and mark remaining.
                BeginInvoke((Action)(() =>
                    MessageBox.Show(this, ex.Message, "Conversion error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error)));
            }
            finally
            {
                if (converter != null) converter.Dispose();
            }

            BeginInvoke((Action)(() => FinishBatch(ok, failed, skipped, customFolder ?? lastOutputDir)));
        }

        private void FinishBatch(int ok, int failed, int skipped, string outputDir)
        {
            SetBusy(false);
            _progress.Value = _progress.Maximum;

            var parts = new List<string> { ok + " converted" };
            if (failed > 0) parts.Add(failed + " failed");
            if (skipped > 0) parts.Add(skipped + " skipped");
            SetStatus("Finished: " + string.Join(", ", parts) + ".");

            if (ok > 0 && _chkOpenWhenDone.Checked && !string.IsNullOrEmpty(outputDir) && Directory.Exists(outputDir))
            {
                try { Process.Start("explorer.exe", "\"" + outputDir + "\""); } catch { }
            }

            if (failed > 0)
            {
                MessageBox.Show(this,
                    failed + " file(s) could not be converted. Hover the \"Failed\" rows to see why.",
                    "Some files failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // -------------------------------------------------- UI state helpers

        private void SetBusy(bool busy)
        {
            _btnConvert.Enabled = !busy;
            _btnCancel.Enabled = busy;
            _btnAdd.Enabled = !busy;
            _btnRemove.Enabled = !busy && _list.SelectedItems.Count > 0;
            _btnClear.Enabled = !busy && _list.Items.Count > 0;
            _list.Enabled = !busy;
            _rdoSameFolder.Enabled = !busy;
            _rdoCustomFolder.Enabled = !busy;
            _txtOutput.Enabled = !busy && _rdoCustomFolder.Checked;
            _btnBrowseOut.Enabled = !busy && _rdoCustomFolder.Checked;
            _chkFitToContent.Enabled = !busy;
            _chkOpenWhenDone.Enabled = !busy;
        }

        private void UpdateButtons()
        {
            bool hasItems = _list.Items.Count > 0;
            bool hasSel = _list.SelectedItems.Count > 0;
            _btnRemove.Enabled = hasSel;
            _btnClear.Enabled = hasItems;
            _btnConvert.Enabled = hasItems;
            _txtOutput.Enabled = _rdoCustomFolder.Checked;
            _btnBrowseOut.Enabled = _rdoCustomFolder.Checked;
        }

        private void SetItemStatus(ListViewItem item, string status)
        {
            if (_list.InvokeRequired)
            {
                _list.BeginInvoke((Action)(() => SetItemStatus(item, status)));
                return;
            }
            item.SubItems[2].Text = status;
            item.ForeColor =
                status == "Done" ? Color.ForestGreen :
                status == "Failed" ? Color.Firebrick :
                status == "Skipped" ? Color.DimGray :
                SystemColors.WindowText;
            _list.ShowItemToolTips = true;
        }

        private void SetStatus(string text)
        {
            if (_status.InvokeRequired) { _status.BeginInvoke((Action)(() => _status.Text = text)); return; }
            _status.Text = text;
        }

        private void StepProgress()
        {
            if (_progress.InvokeRequired) { _progress.BeginInvoke((Action)StepProgress); return; }
            if (_progress.Value < _progress.Maximum) _progress.Value++;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_worker != null && _worker.IsAlive)
            {
                var r = MessageBox.Show(this,
                    "A conversion is still running. Stop it and exit?",
                    "Conversion in progress", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r == DialogResult.No) { e.Cancel = true; return; }
                _cancelRequested = true;
            }
            base.OnFormClosing(e);
        }
    }
}
