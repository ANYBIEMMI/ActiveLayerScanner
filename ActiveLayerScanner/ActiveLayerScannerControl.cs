using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ActiveLayerScanner.Models;
using ActiveLayerScanner.Services;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace ActiveLayerScanner
{
    public class ActiveLayerScannerControl : PluginControlBase, IGitHubPlugin, IHelpPlugin
    {
        // TODO before publishing: set your real GitHub account (UserName / HelpUrl) and the nuspec projectUrl.
        public string RepositoryName => "ActiveLayerScanner";
        public string UserName => "ANYBIEMMI";
        public string HelpUrl => "https://github.com/ANYBIEMMI/ActiveLayerScanner#readme";

        private readonly ToolStripButton btnLoad = new ToolStripButton("Load solutions");
        private readonly ToolStripComboBox cbSolutions = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 380 };
        private readonly ToolStripButton btnScan = new ToolStripButton("Scan") { Enabled = false };
        private readonly ToolStripButton btnCancel = new ToolStripButton("Cancel") { Enabled = false };
        private readonly ToolStripButton btnRemove = new ToolStripButton("Remove active layer (selected)") { Enabled = false };
        private readonly ToolStripButton btnExport = new ToolStripButton("Export CSV") { Enabled = false };
        private readonly ToolStripButton btnSelectAll = new ToolStripButton("Select all") { Enabled = false };
        private readonly ToolStripButton btnSelectNone = new ToolStripButton("Clear") { Enabled = false };
        private readonly ToolStripLabel lblStatus = new ToolStripLabel("Connect, load solutions and scan.") { Alignment = ToolStripItemAlignment.Right, ForeColor = SystemColors.GrayText };
        private readonly ToolStripTextBox txtFilter = new ToolStripTextBox { Width = 160, ToolTipText = "Filter by type or name" };
        private readonly DataGridView grid = new DataGridView();
        private readonly RichTextBox rtbBase = new RichTextBox();
        private readonly RichTextBox rtbActive = new RichTextBox();
        private readonly Label lblBase = new Label { Dock = DockStyle.Top, Height = 20, Text = "Base layer" };
        private readonly Label lblActive = new Label { Dock = DockStyle.Top, Height = 20, Text = "Active layer (unmanaged)" };
        private readonly TextBox txtChanges = new TextBox { Dock = DockStyle.Bottom, Height = 48, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };

        private List<ComponentRow> allRows = new List<ComponentRow>();
        private BindingList<ComponentRow> view = new BindingList<ComponentRow>();
        private bool busy;

        public ActiveLayerScannerControl()
        {
            BuildUi();
            Wire();
        }

        private void BuildUi()
        {
            var tools = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
            tools.Items.AddRange(new ToolStripItem[]
            {
                btnLoad, new ToolStripSeparator(), cbSolutions, btnScan, btnCancel, new ToolStripSeparator(),
                btnSelectAll, btnSelectNone, new ToolStripSeparator(), btnRemove, btnExport, new ToolStripSeparator(),
                new ToolStripLabel("Filter:"), txtFilter, lblStatus
            });

            btnLoad.Image = UiIcons.Load(); btnLoad.ToolTipText = "Load the solutions of the connected environment";
            btnScan.Image = UiIcons.Scan(); btnScan.ToolTipText = "Scan the selected solution for components with an Active layer";
            btnCancel.Image = UiIcons.Cancel(); btnCancel.ToolTipText = "Cancel the running operation";
            btnRemove.Image = UiIcons.Remove(); btnRemove.ToolTipText = "Permanently remove the Active layer of the ticked components";
            btnExport.Image = UiIcons.Export(); btnExport.ToolTipText = "Export the list to CSV";
            btnSelectAll.Image = UiIcons.SelectAll(); btnSelectAll.ToolTipText = "Tick all components currently shown";
            btnSelectNone.Image = UiIcons.SelectNone(); btnSelectNone.ToolTipText = "Untick all components";
            foreach (var b in new[] { btnLoad, btnScan, btnCancel, btnRemove, btnExport, btnSelectAll, btnSelectNone })
            {
                b.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
                b.TextImageRelation = TextImageRelation.ImageBeforeText;
            }
            tools.ImageScalingSize = new Size(20, 20);
            btnRemove.Text = "Remove active layer";

            grid.Dock = DockStyle.Fill;
            grid.AutoGenerateColumns = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.AllowUserToResizeRows = false;
            grid.BackgroundColor = SystemColors.Window;
            grid.BorderStyle = BorderStyle.None;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 247, 252);
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(230, 233, 245);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold);
            grid.ColumnHeadersHeight = 28;
            grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "Selected", HeaderText = "", Width = 30 });
            AddCol("ComponentType", "Type", 150);
            AddCol("Name", "Name", 280);
            AddCol("BaseLayer", "Base layer", 200);
            AddCol("LayerCount", "Layers", 60);
            AddCol("ActiveModifiedOn", "Active modified", 140);
            AddCol("ComponentId", "Component Id", 260);
            for (var i = 1; i < grid.Columns.Count; i++) grid.Columns[i].ReadOnly = true;
            grid.DataSource = view;

            foreach (var r in new[] { rtbBase, rtbActive })
            {
                r.Dock = DockStyle.Fill; r.ReadOnly = true; r.WordWrap = false;
                r.Font = new Font("Consolas", 9f); r.BackColor = SystemColors.Window;
            }
            var pBase = new Panel { Dock = DockStyle.Fill }; pBase.Controls.Add(rtbBase); pBase.Controls.Add(lblBase);
            var pAct = new Panel { Dock = DockStyle.Fill }; pAct.Controls.Add(rtbActive); pAct.Controls.Add(lblActive);
            var diffSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
            diffSplit.Panel1.Controls.Add(pBase);
            diffSplit.Panel2.Controls.Add(pAct);
            var bottom = new Panel { Dock = DockStyle.Fill };
            bottom.Controls.Add(diffSplit);
            bottom.Controls.Add(txtChanges);

            var main = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 260 };
            main.Panel1.Controls.Add(grid);
            main.Panel2.Controls.Add(bottom);

            Controls.Add(main);
            Controls.Add(tools);
            Dock = DockStyle.Fill;
        }

        private void AddCol(string prop, string header, int width) =>
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = prop, HeaderText = header, Width = width });

        private void Wire()
        {
            btnLoad.Click += (s, e) => ExecuteMethod(LoadSolutions);
            btnScan.Click += (s, e) => Scan();
            btnCancel.Click += (s, e) => CancelWorker();
            btnRemove.Click += (s, e) => Remove();
            btnExport.Click += (s, e) => ExportCsv();
            btnSelectAll.Click += (s, e) => SetSelection(true);
            btnSelectNone.Click += (s, e) => SetSelection(false);
            txtFilter.TextChanged += (s, e) => ApplyFilter();
            cbSolutions.SelectedIndexChanged += (s, e) => UpdateButtons();
            grid.SelectionChanged += (s, e) => ShowDiff();
            grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                UpdateButtons();
            };
        }

        public override void ClosingPlugin(PluginCloseInfo info)
        {
            if (busy) CancelWorker();
            base.ClosingPlugin(info);
        }

        public override void UpdateConnection(Microsoft.Xrm.Sdk.IOrganizationService newService, McTools.Xrm.Connection.ConnectionDetail detail, string actionName, object parameter)
        {
            base.UpdateConnection(newService, detail, actionName, parameter);
            cbSolutions.Items.Clear();
            allRows.Clear(); ApplyFilter();
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            var connected = Service != null;
            btnLoad.Enabled = connected && !busy;
            btnScan.Enabled = connected && !busy && cbSolutions.SelectedItem is SolutionItem;
            btnCancel.Enabled = busy;
            var selected = allRows.Count(r => r.Selected);
            btnRemove.Enabled = !busy && selected > 0;
            btnRemove.Text = selected > 0 ? $"Remove active layer ({selected})" : "Remove active layer";
            btnExport.Enabled = !busy && allRows.Count > 0;
            btnSelectAll.Enabled = !busy && view.Count > 0;
            btnSelectNone.Enabled = !busy && selected > 0;
            lblStatus.Text = busy ? "Working..."
                : allRows.Count == 0 ? (Service == null ? "Not connected." : "No components to show.")
                : $"{view.Count} of {allRows.Count} components shown, {selected} selected";
        }

        private void SetSelection(bool value)
        {
            grid.EndEdit();
            foreach (var r in value ? view.ToList() : allRows) r.Selected = value;
            grid.Invalidate();
            UpdateButtons();
        }

        private void LoadSolutions()
        {
            busy = true; UpdateButtons();
            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading solutions...",
                Work = (w, a) => a.Result = SolutionService.LoadSolutions(Service),
                PostWorkCallBack = a =>
                {
                    busy = false;
                    if (a.Error != null) { MessageBox.Show(a.Error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); UpdateButtons(); return; }
                    cbSolutions.Items.Clear();
                    foreach (var s in (List<SolutionItem>)a.Result) cbSolutions.Items.Add(s);
                    UpdateButtons();
                }
            });
        }

        private void Scan()
        {
            var sol = cbSolutions.SelectedItem as SolutionItem;
            if (sol == null) return;
            busy = true; UpdateButtons();
            WorkAsync(new WorkAsyncInfo
            {
                Message = "Reading solution components...",
                IsCancelable = true,
                Work = (w, a) =>
                {
                    var comps = ComponentService.GetComponents(Service, sol.Id);
                    w.ReportProgress(0, $"Scanning {comps.Count} components...");
                    a.Result = LayerService.Scan(Service, comps, () => w.CancellationPending,
                        (done, total) => w.ReportProgress(total == 0 ? 0 : done * 100 / total, $"Scanning layers {done}/{total}"));
                },
                ProgressChanged = a => SetWorkingMessage(a.UserState as string ?? "Scanning..."),
                PostWorkCallBack = a =>
                {
                    busy = false;
                    if (a.Error != null) { MessageBox.Show(a.Error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); UpdateButtons(); return; }
                    allRows = (List<ComponentRow>)a.Result;
                    ApplyFilter();
                    LogInfo($"Scan finished: {allRows.Count} components with Active layer.");
                    UpdateButtons();
                }
            });
        }

        private void ApplyFilter()
        {
            var f = (txtFilter.Text ?? "").Trim();
            var q = allRows.Where(r => f.Length == 0
                || (r.Name ?? "").IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0
                || (r.ComponentType ?? "").IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0);
            view = new BindingList<ComponentRow>(q.OrderBy(r => r.ComponentType).ThenBy(r => r.Name).ToList());
            grid.DataSource = view;
            UpdateButtons();
        }

        private void ShowDiff()
        {
            rtbBase.Clear(); rtbActive.Clear(); txtChanges.Clear();
            var row = grid.CurrentRow?.DataBoundItem as ComponentRow;
            if (row == null || Service == null) return;
            txtChanges.Text = row.Changes ?? "";
            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading diff...",
                Work = (w, a) => a.Result = LayerService.GetActiveAndBase(Service, row),
                PostWorkCallBack = a =>
                {
                    if (a.Error != null) { rtbActive.Text = a.Error.Message; return; }
                    var pair = (Tuple<LayerInfo, LayerInfo>)a.Result;
                    lblBase.Text = "Base layer: " + (pair.Item1?.SolutionName ?? "(none)");
                    RenderDiff(DiffService.Diff(pair.Item1?.Json, pair.Item2?.Json));
                }
            });
        }

        private void RenderDiff(List<DiffLine> lines)
        {
            if (lines.All(l => l.Kind == DiffKind.Same) && lines.Count <= 1)
            {
                rtbActive.Text = "No diff available for this component type.";
                return;
            }
            foreach (var l in lines)
            {
                if (l.Kind != DiffKind.Added) Append(rtbBase, l.Text, l.Kind == DiffKind.Removed ? Color.MistyRose : Color.Empty);
                else Append(rtbBase, "", Color.Empty);
                if (l.Kind != DiffKind.Removed) Append(rtbActive, l.Text, l.Kind == DiffKind.Added ? Color.Honeydew : Color.Empty);
                else Append(rtbActive, "", Color.Empty);
            }
        }

        private static void Append(RichTextBox box, string text, Color back)
        {
            box.SelectionStart = box.TextLength;
            box.SelectionBackColor = back == Color.Empty ? box.BackColor : back;
            box.AppendText(text + "\n");
        }

        private void Remove()
        {
            var selected = allRows.Where(r => r.Selected).ToList();
            if (selected.Count == 0) return;
            var sb = new StringBuilder();
            sb.AppendLine($"This will PERMANENTLY remove the Active (unmanaged) layer of {selected.Count} component(s).");
            sb.AppendLine("Manual customizations will be lost and this cannot be undone.");
            sb.AppendLine().AppendLine("Environment: " + (ConnectionDetail?.ConnectionName ?? ""));
            if (MessageBox.Show(sb.ToString(), "Confirm removal", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            if (MessageBox.Show("Export a CSV report of these components first?", "Backup", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes
                && !ExportCsv(selected)) return;

            busy = true; UpdateButtons();
            WorkAsync(new WorkAsyncInfo
            {
                Message = "Removing active customizations...",
                IsCancelable = true,
                Work = (w, a) => a.Result = CleanupService.Remove(Service, selected, () => w.CancellationPending,
                    (done, total) => w.ReportProgress(done * 100 / total, $"Removing {done}/{total}")),
                ProgressChanged = a => SetWorkingMessage(a.UserState as string ?? "Removing..."),
                PostWorkCallBack = a =>
                {
                    busy = false;
                    if (a.Error != null) { MessageBox.Show(a.Error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); UpdateButtons(); return; }
                    var results = (List<CleanupResult>)a.Result;
                    var failed = results.Where(r => !r.Success).ToList();
                    foreach (var f in failed) LogError($"{f.Row.ComponentType} {f.Row.Name}: {f.Error}");
                    MessageBox.Show($"Removed: {results.Count - failed.Count}\nFailed: {failed.Count}" +
                        (failed.Count > 0 ? "\n\n" + string.Join("\n", failed.Take(10).Select(f => f.Row.Name + ": " + f.Error)) : ""),
                        "Result", MessageBoxButtons.OK, failed.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                    var ok = new HashSet<Guid>(results.Where(r => r.Success).Select(r => r.Row.ComponentId));
                    allRows = allRows.Where(r => !ok.Contains(r.ComponentId)).ToList();
                    ApplyFilter();
                }
            });
        }

        private void ExportCsv() => ExportCsv(allRows);

        private bool ExportCsv(IList<ComponentRow> rows)
        {
            using (var dlg = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "active-layers.csv" })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return false;
                var sb = new StringBuilder("Type,Name,BaseLayer,Layers,ActiveModified,ComponentId,Changes\n");
                foreach (var r in rows)
                    sb.AppendLine(string.Join(",", new[] { r.ComponentType, r.Name, r.BaseLayer, r.LayerCount.ToString(),
                        r.ActiveModifiedOn?.ToString("s"), r.ComponentId.ToString(), r.Changes }.Select(Csv)));
                File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                return true;
            }
        }

        private static string Csv(string s) => "\"" + (s ?? "").Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
    }
}
