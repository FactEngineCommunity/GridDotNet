using System;
using System.Drawing;
using System.Windows.Forms;

namespace GridDotNet.Demo
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new DemoForm());
        }
    }

    public sealed class DemoForm : Form
    {
        private readonly GridControl grid = new GridControl();
        private readonly Label status = new Label { Dock = DockStyle.Bottom, Height = 30, Padding = new Padding(6) };
        private readonly Bitmap shiftImage = new Bitmap(32, 24);

        public DemoForm()
        {
            Text = "GridDotNet — image cells and rectangle selection";
            ClientSize = new Size(1050, 600);
            AutoScaleMode = AutoScaleMode.Dpi;
            using (Graphics g = Graphics.FromImage(shiftImage))
            {
                g.Clear(Color.SeaGreen);
                using (var pen = new Pen(Color.FromArgb(90, Color.White), 2))
                    for (int x = -24; x < 56; x += 8) g.DrawLine(pen, x, 24, x + 24, 0);
            }
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(5) };
            AddButton(toolbar, "Fill with image", () => { grid.FillSelection(shiftImage); grid.Focus(); });
            AddButton(toolbar, "Clear selection", () => { grid.ClearSelectionContent(); grid.Focus(); });
            AddButton(toolbar, "Fill using old API", () =>
            {
                int previous = grid.FillStyle;
                grid.FillStyle = 1; grid.Picture = shiftImage; grid.FillStyle = previous; grid.Focus();
            });
            var highlight = new CheckBox { Text = "Show highlight", Checked = true, AutoSize = true, Margin = new Padding(10, 7, 0, 0) };
            highlight.CheckedChanged += (s, e) => grid.HighLight = highlight.Checked;
            toolbar.Controls.Add(highlight);
            var scrollOptions = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 95, Margin = new Padding(10, 4, 0, 0) };
            scrollOptions.DataSource = Enum.GetValues(typeof(ScrollBars));
            scrollOptions.SelectedIndexChanged += (s, e) =>
            {
                if (scrollOptions.SelectedItem is ScrollBars option) grid.ScrollBars = option;
            };
            toolbar.Controls.Add(new Label { Text = "Scrollbars:", AutoSize = true, Margin = new Padding(10, 8, 0, 0) });
            toolbar.Controls.Add(scrollOptions);
            var instructions = new Label
            {
                Dock = DockStyle.Top, Height = 38, Padding = new Padding(6),
                Text = "Hold the left mouse button and drag a rectangle, then fill or clear it. Drag at the edges to scroll.\nArrow keys move; Shift + arrows extend. Double-click or F2 edits text; Enter saves and Escape cancels."
            };
            grid.Dock = DockStyle.Fill;
            // Exercise the new control default (both hidden); use the dropdown to enable either axis.
            scrollOptions.SelectedItem = ScrollBars.None;
            grid.BeginUpdate();
            grid.Rows = 41; grid.Cols = 97;
            grid.FixedRows = 1; grid.FixedCols = 1; grid.AllowEditing = true;
            grid.set_ColWidth(0, 120);
            grid.set_RowHeight(0, 32);
            grid.Cells[0, 0].Text = "Employee / time";
            for (int c = 1; c < grid.Cols; c++)
            {
                grid.set_ColWidth(c, 38);
                if ((c - 1) % 4 == 0) grid.Cells[0, c].Text = ((c - 1) / 4).ToString("00") + "h";
            }
            for (int r = 1; r < grid.Rows; r++)
            {
                grid.Cells[r, 0].Text = "Employee " + r;
                if (r % 5 == 0) grid.set_RowHeight(r, 34);
                for (int c = 9 + r % 4; c < 25 + r % 4; c++) grid.Cells[r, c].Image = shiftImage;
            }
            grid.SelectRange(1, 1, 1, 1); grid.Row = 1; grid.Col = 1; grid.EndUpdate();
            grid.SelectionChanged += (s, e) => ShowSelection(e.Range, false);
            grid.SelectionCompleted += (s, e) => ShowSelection(e.Range, true);
            grid.CellEdited += (s, e) => status.Text = "Edited cell " + e.Row + ", " + e.Column;
            Controls.Add(grid); Controls.Add(instructions); Controls.Add(toolbar); Controls.Add(status);
            ShowSelection(grid.Selection, false);
        }
        private static void AddButton(FlowLayoutPanel panel, string label, Action action)
        {
            var button = new Button { Text = label, AutoSize = true, Height = 28 };
            button.Click += (s, e) => action(); panel.Controls.Add(button);
        }
        private void ShowSelection(CellRange range, bool completed)
        {
            status.Text = $"Selected rows {range.Top}–{range.Bottom}, columns {range.Left}–{range.Right} ({range.Bottom - range.Top + 1} × {range.Right - range.Left + 1} cells)" + (completed ? " — mouse released" : "");
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) shiftImage.Dispose();
        }
    }
}
