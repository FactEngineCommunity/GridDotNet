using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using ScrollBarOptions = System.Windows.Forms.ScrollBars;

namespace GridDotNet
{
    /// <summary>Managed WinForms grid with rectangular selection and a legacy MSGrid-style API.
    /// Indices are zero-based. Dimensions use logical pixels at 96 DPI, including grid lines.
    /// Programmatic current-cell changes do not change the selection bounds.</summary>
    [DefaultEvent(nameof(SelectionCompleted))]
    public class GridControl : Control
    {
        private int rows = 10, cols = 10, row, col, startRow, endRow, startCol, endCol;
        private int fixedRows, fixedCols, fillStyle, gridLineWidth = 1, updateDepth;
        private bool highlight = true, dragging, layingOut, horizontalShown, verticalShown;
        private ScrollBarOptions scrollBars = ScrollBarOptions.None;
        private int anchorRow, anchorCol;
        private int[] heights, widths, rowEdges, colEdges;
        private int defaultRowHeight = 26, defaultColWidth = 60;
        private bool[] customRowHeights, customColWidths;
        private readonly Dictionary<long, GridCell> cells = new Dictionary<long, GridCell>();
        private readonly HScrollBar horizontal = new HScrollBar();
        private readonly VScrollBar vertical = new VScrollBar();
        private readonly Timer dragTimer = new Timer { Interval = 60 };
        private TextBox editor;
        private int editRow, editCol;
        private bool endingEdit;

        public GridControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            BackColor = Color.White;
            ForeColor = Color.Black;
            Size = new Size(600, 300);
            heights = MakeSizes(rows, defaultRowHeight); widths = MakeSizes(cols, defaultColWidth);
            customRowHeights = new bool[rows]; customColWidths = new bool[cols];
            Cells = new GridCellCollection(this);
            Controls.Add(horizontal); Controls.Add(vertical);
            horizontal.ValueChanged += ScrollChanged;
            vertical.ValueChanged += ScrollChanged;
            dragTimer.Tick += DragTick;
            RebuildLayout();
        }

        [DefaultValue(10)] public int Rows { get => rows; set => ResizeGrid(value, cols); }
        [DefaultValue(10)] public int Cols { get => cols; set => ResizeGrid(rows, value); }
        [Category("Layout"), DefaultValue(26)]
        [Description("Height in logical pixels of rows without an individual height override, including new rows.")]
        public int DefaultRowHeight
        {
            get => defaultRowHeight;
            set
            {
                int size = ValidateSize(value);
                if (defaultRowHeight == size) return;
                EndEdit(true); defaultRowHeight = size;
                for (int r = 0; r < rows; r++) if (!customRowHeights[r]) heights[r] = size;
                RebuildLayout();
            }
        }
        [Category("Layout"), DefaultValue(60)]
        [Description("Width in logical pixels of columns without an individual width override, including new columns.")]
        public int DefaultColWidth
        {
            get => defaultColWidth;
            set
            {
                int size = ValidateSize(value);
                if (defaultColWidth == size) return;
                EndEdit(true); defaultColWidth = size;
                for (int c = 0; c < cols; c++) if (!customColWidths[c]) widths[c] = size;
                RebuildLayout();
            }
        }
        [DefaultValue(0)] public int Row { get => row; set { CheckRow(value); SetCurrent(value, col); } }
        [DefaultValue(0)] public int Col { get => col; set { CheckCol(value); SetCurrent(row, value); } }
        [DefaultValue(0)] public int SelStartRow { get => startRow; set { CheckRow(value); startRow = value; SelectionDidChange(); } }
        [DefaultValue(0)] public int SelEndRow { get => endRow; set { CheckRow(value); endRow = value; SelectionDidChange(); } }
        [DefaultValue(0)] public int SelStartCol { get => startCol; set { CheckCol(value); startCol = value; SelectionDidChange(); } }
        [DefaultValue(0)] public int SelEndCol { get => endCol; set { CheckCol(value); endCol = value; SelectionDidChange(); } }
        [Browsable(false)] public CellRange Selection => new CellRange(startRow, startCol, endRow, endCol);
        [Browsable(false)] public bool CellSelected => rows > 0 && cols > 0;
        [Browsable(false)] public GridCellCollection Cells { get; }
        [DefaultValue(0)] public int FillStyle
        {
            get => fillStyle;
            set { if (value != 0 && value != 1) throw new ArgumentOutOfRangeException(nameof(value)); fillStyle = value; }
        }
        [DefaultValue(true)] public bool HighLight { get => highlight; set { highlight = value; RequestPaint(); } }
        [DefaultValue(1)] public int GridLineWidth
        {
            get => gridLineWidth;
            set { if (value < 0 || value > 20) throw new ArgumentOutOfRangeException(nameof(value)); gridLineWidth = value; RequestPaint(); }
        }
        [DefaultValue(0)] public int FixedRows
        {
            get => fixedRows;
            set { if (value < 0 || value >= rows) throw new ArgumentOutOfRangeException(nameof(value)); fixedRows = value; RebuildLayout(); }
        }
        [DefaultValue(0)] public int FixedCols
        {
            get => fixedCols;
            set { if (value < 0 || value >= cols) throw new ArgumentOutOfRangeException(nameof(value)); fixedCols = value; RebuildLayout(); }
        }
        [DefaultValue(false)] public bool AllowEditing { get; set; }
        /// <summary>Which scrollbars may be displayed when content overflows. Hidden by default;
        /// scrolling by code, keyboard, wheel, and selection dragging remains available.</summary>
        [Category("Appearance"), DefaultValue(ScrollBarOptions.None)]
        [Description("Scrollbars to display when needed. None hides both without disabling scrolling.")]
        public ScrollBarOptions ScrollBars
        {
            get => scrollBars;
            set
            {
                if (!Enum.IsDefined(typeof(ScrollBarOptions), value)) throw new ArgumentOutOfRangeException(nameof(value));
                if (scrollBars == value) return;
                EndEdit(true);
                int oldTop = TopRow, oldLeft = LeftCol;
                scrollBars = value; RebuildLayout();
                if (oldTop != TopRow || oldLeft != LeftCol) ScrollPositionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        [DefaultValue(typeof(Color), "LightGray")]
        public Color GridLineColor { get; set; } = Color.LightGray;
        [DefaultValue(typeof(Color), "235, 238, 242")]
        public Color HeaderBackColor { get; set; } = Color.FromArgb(235, 238, 242);
        [DefaultValue(typeof(Color), "80, 40, 110, 220")]
        public Color SelectionColor { get; set; } = Color.FromArgb(80, 40, 110, 220);

        /// <summary>Text of the current cell; assigning with FillStyle=1 fills the selection.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override string Text
        {
            get => GetCell(row, col).Text;
            set { ApplyLegacy(cell => cell.Text = value); OnTextChanged(EventArgs.Empty); }
        }
        /// <summary>Image of the current cell. Assign null (Nothing in VB) to clear it.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Image Picture { get => GetCell(row, col).Image; set => ApplyLegacy(cell => cell.Image = value); }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int TopRow
        {
            get => FindIndex(rowEdges, rowEdges[fixedRows] + vertical.Value);
            set { CheckRow(value); SetScroll(vertical, rowEdges[Math.Max(fixedRows, value)] - rowEdges[fixedRows]); }
        }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int LeftCol
        {
            get => FindIndex(colEdges, colEdges[fixedCols] + horizontal.Value);
            set { CheckCol(value); SetScroll(horizontal, colEdges[Math.Max(fixedCols, value)] - colEdges[fixedCols]); }
        }

        public event EventHandler RowColChange;
        public event EventHandler ScrollPositionChanged;
        public event EventHandler<GridSelectionEventArgs> SelectionChanged;
        public event EventHandler<GridSelectionEventArgs> SelectionCompleted;
        public event EventHandler<GridCellEventArgs> CellDoubleClick;
        public event EventHandler<GridCellEventArgs> CellEdited;
        /// <summary>Managed replacement event. Handler arguments are WinForms MouseEventArgs, not COM args.</summary>
        public event MouseEventHandler MouseUpEvent;

        public int get_RowHeight(int index) { CheckRow(index); return heights[index]; }
        public void set_RowHeight(int index, double value) { CheckRow(index); heights[index] = ValidateSize(value); customRowHeights[index] = true; RebuildLayout(); }
        public int get_ColWidth(int index) { CheckCol(index); return widths[index]; }
        public void set_ColWidth(int index, double value) { CheckCol(index); widths[index] = ValidateSize(value); customColWidths[index] = true; RebuildLayout(); }
        public void ResetRowHeight(int index) { CheckRow(index); customRowHeights[index] = false; heights[index] = defaultRowHeight; RebuildLayout(); }
        public void ResetColWidth(int index) { CheckCol(index); customColWidths[index] = false; widths[index] = defaultColWidth; RebuildLayout(); }
        // Methods also accept the old VB syntax grid.RowHeight(index) and grid.ColWidth(index).
        public int RowHeight(int index) => get_RowHeight(index);
        public int ColWidth(int index) => get_ColWidth(index);
        public bool get_RowIsVisible(int index)
        {
            CheckRow(index);
            int y = RowY(index), min = index < fixedRows ? 0 : rowEdges[fixedRows];
            return y < Viewport.Height && y + Scaled(heights[index]) > min;
        }

        public void BeginUpdate() { updateDepth++; }
        public void EndUpdate()
        {
            if (updateDepth == 0) throw new InvalidOperationException("EndUpdate must follow BeginUpdate.");
            if (--updateDepth == 0) Invalidate();
        }
        public void SelectRange(int row1, int col1, int row2, int col2)
        {
            CheckRow(row1); CheckRow(row2); CheckCol(col1); CheckCol(col2);
            startRow = row1; startCol = col1; endRow = row2; endCol = col2;
            SelectionDidChange();
        }
        public void FillSelection(Image image) => ApplyRange(Selection, cell => cell.Image = image);
        public void ClearSelectionContent() => ApplyRange(Selection, cell => { cell.Image = null; cell.Text = ""; });

        /// <summary>Add a row, appending by default. Tab-separated text populates successive columns.
        /// The optional index inserts before that row. FillStyle does not affect this operation.</summary>
        public void AddItem(string text, int index = -1)
        {
            if (index == -1) index = rows;
            if (index < 0 || index > rows) throw new ArgumentOutOfRangeException(nameof(index));
            if (rows == 100000) throw new InvalidOperationException("The grid already has the maximum row count.");
            string[] values = (text ?? "").Split('\t');
            if (values.Length > cols) throw new ArgumentException("The item contains more fields than the grid has columns.", nameof(text));
            ChangeRowStructure(index, true);
            BeginUpdate();
            try { for (int c = 0; c < values.Length; c++) GetCell(index, c).Text = values[c]; }
            finally { EndUpdate(); }
        }

        /// <summary>Delete the indexed row, shifting all following cells and row heights upward.
        /// At least one row must remain.</summary>
        public void RemoveItem(int index)
        {
            CheckRow(index);
            if (rows == 1) throw new InvalidOperationException("At least one row must remain in the grid.");
            ChangeRowStructure(index, false);
        }

        private void ChangeRowStructure(int index, bool inserting)
        {
            EndEdit(false); CancelDrag();
            int oldTop = TopRow;
            int newCount = rows + (inserting ? 1 : -1);
            var movedCells = new Dictionary<long, GridCell>();
            foreach (var pair in cells)
            {
                int r = (int)(pair.Key >> 32), c = (int)(uint)pair.Key;
                if (!inserting && r == index) continue;
                movedCells.Add(Key(AdjustRow(r, index, inserting, newCount), c), pair.Value);
            }
            int[] newHeights = new int[newCount];
            bool[] newCustomHeights = new bool[newCount];
            Array.Copy(heights, 0, newHeights, 0, index);
            Array.Copy(customRowHeights, 0, newCustomHeights, 0, index);
            if (inserting)
            {
                newHeights[index] = defaultRowHeight;
                Array.Copy(heights, index, newHeights, index + 1, rows - index);
                Array.Copy(customRowHeights, index, newCustomHeights, index + 1, rows - index);
            }
            else
            {
                Array.Copy(heights, index + 1, newHeights, index, rows - index - 1);
                Array.Copy(customRowHeights, index + 1, newCustomHeights, index, rows - index - 1);
            }
            cells.Clear();
            foreach (var pair in movedCells) cells.Add(pair.Key, pair.Value);
            heights = newHeights; customRowHeights = newCustomHeights; rows = newCount;
            if (index < fixedRows) fixedRows += inserting ? 1 : -1;
            fixedRows = Math.Min(fixedRows, rows - 1);
            startRow = AdjustRow(startRow, index, inserting, rows);
            endRow = AdjustRow(endRow, index, inserting, rows);
            int newRow = AdjustRow(row, index, inserting, rows);
            bool currentChanged = row != newRow;
            row = newRow;
            // Rebuild before events so handlers see the new cell geometry and counts.
            RebuildLayout();
            TopRow = AdjustRow(oldTop, index, inserting, rows);
            if (currentChanged) RowColChange?.Invoke(this, EventArgs.Empty);
            SelectionDidChange();
        }

        private static int AdjustRow(int value, int index, bool inserting, int newCount)
        {
            if (inserting) return value >= index ? value + 1 : value;
            return Math.Min(newCount - 1, value > index ? value - 1 : value);
        }

        internal GridCell GetCell(int r, int c)
        {
            CheckRow(r); CheckCol(c);
            long key = Key(r, c);
            if (!cells.TryGetValue(key, out GridCell cell))
            {
                cell = new GridCell(RequestPaint);
                cells.Add(key, cell);
            }
            return cell;
        }
        private static long Key(int r, int c) => ((long)r << 32) | (uint)c;
        private void ApplyLegacy(Action<GridCell> action)
        {
            if (fillStyle == 1) ApplyRange(Selection, action);
            else action(GetCell(row, col));
        }
        private void ApplyRange(CellRange range, Action<GridCell> action)
        {
            BeginUpdate();
            try { for (int r = range.Top; r <= range.Bottom; r++) for (int c = range.Left; c <= range.Right; c++) action(GetCell(r, c)); }
            finally { EndUpdate(); }
        }
        private void CheckRow(int value, [CallerMemberName] string operation = "")
        {
            if (value < 0 || value >= rows)
                throw new ArgumentOutOfRangeException("row", value,
                    $"{DiagnosticName}: {operation} requested row {value}. Rows={rows}; valid indices are 0 through {rows - 1}.");
        }
        private void CheckCol(int value, [CallerMemberName] string operation = "")
        {
            if (value < 0 || value >= cols)
                throw new ArgumentOutOfRangeException("column", value,
                    $"{DiagnosticName}: {operation} requested column {value}. Cols={cols}; valid indices are 0 through {cols - 1}.");
        }
        private string DiagnosticName => string.IsNullOrEmpty(Name) ? GetType().Name : Name;
        private static int ValidateSize(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value > 10000)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Size must be finite and no greater than 10000 logical pixels.");
            // Legacy layout calculations can produce negative or zero dimensions while
            // forms are being sized. Keep the cell valid instead of failing form loading.
            return (int)Math.Round(Math.Max(1, value));
        }
        private static int[] MakeSizes(int count, int size)
        {
            var result = new int[count]; for (int i = 0; i < count; i++) result[i] = size; return result;
        }
        private void ResizeGrid(int newRows, int newCols)
        {
            if (newRows < 1 || newRows > 100000 || newCols < 1 || newCols > 100000) throw new ArgumentOutOfRangeException("Grid dimensions must be between 1 and 100000.");
            if (newRows == rows && newCols == cols) return;
            EndEdit(false); CancelDrag();
            int oldTop = TopRow, oldLeft = LeftCol;
            int[] newHeights = MakeSizes(newRows, defaultRowHeight), newWidths = MakeSizes(newCols, defaultColWidth);
            bool[] newCustomHeights = new bool[newRows], newCustomWidths = new bool[newCols];
            Array.Copy(heights, newHeights, Math.Min(rows, newRows)); Array.Copy(widths, newWidths, Math.Min(cols, newCols));
            Array.Copy(customRowHeights, newCustomHeights, Math.Min(rows, newRows));
            Array.Copy(customColWidths, newCustomWidths, Math.Min(cols, newCols));
            customRowHeights = newCustomHeights; customColWidths = newCustomWidths;
            heights = newHeights; widths = newWidths; rows = newRows; cols = newCols;
            fixedRows = Math.Min(fixedRows, rows - 1); fixedCols = Math.Min(fixedCols, cols - 1);
            var remove = new List<long>();
            foreach (long key in cells.Keys) if ((int)(key >> 32) >= rows || (int)(uint)key >= cols) remove.Add(key);
            foreach (long key in remove) cells.Remove(key);
            startRow = Math.Min(startRow, rows - 1); endRow = Math.Min(endRow, rows - 1);
            startCol = Math.Min(startCol, cols - 1); endCol = Math.Min(endCol, cols - 1);
            int newRow = Math.Min(row, rows - 1), newCol = Math.Min(col, cols - 1);
            bool currentChanged = newRow != row || newCol != col;
            row = newRow; col = newCol;
            // Events must observe matching dimensions, geometry, and scroll positions.
            RebuildLayout();
            if (currentChanged) RowColChange?.Invoke(this, EventArgs.Empty);
            if (oldTop != TopRow || oldLeft != LeftCol) ScrollPositionChanged?.Invoke(this, EventArgs.Empty);
            SelectionDidChange();
        }
        private void SetCurrent(int r, int c)
        {
            if (r == row && c == col) return;
            row = r; col = c; RequestPaint(); RowColChange?.Invoke(this, EventArgs.Empty);
        }
        private void SelectionDidChange() { RequestPaint(); SelectionChanged?.Invoke(this, new GridSelectionEventArgs(Selection)); }
        private void RequestPaint() { if (updateDepth == 0) Invalidate(); }
        private int Scaled(int value) => Math.Max(1, (int)Math.Round(value * DeviceDpi / 96.0));
        private Rectangle Viewport => new Rectangle(0, 0, Math.Max(0, ClientSize.Width - (verticalShown ? vertical.Width : 0)), Math.Max(0, ClientSize.Height - (horizontalShown ? horizontal.Height : 0)));
        private int RowY(int r) => rowEdges[r] - (r < fixedRows ? 0 : vertical.Value);
        private int ColX(int c) => colEdges[c] - (c < fixedCols ? 0 : horizontal.Value);

        public Rectangle GetCellBounds(int r, int c)
        {
            CheckRow(r); CheckCol(c);
            return new Rectangle(ColX(c), RowY(r), colEdges[c + 1] - colEdges[c], rowEdges[r + 1] - rowEdges[r]);
        }
        public CellAddress? HitTest(Point point)
        {
            if (!Viewport.Contains(point)) return null;
            int x = point.X < colEdges[fixedCols] ? point.X : point.X + horizontal.Value;
            int y = point.Y < rowEdges[fixedRows] ? point.Y : point.Y + vertical.Value;
            if (x >= colEdges[cols] || y >= rowEdges[rows]) return null;
            return new CellAddress(FindIndex(rowEdges, y), FindIndex(colEdges, x));
        }
        private static int FindIndex(int[] edges, int position)
        {
            int index = Array.BinarySearch(edges, position);
            return Math.Max(0, Math.Min(edges.Length - 2, index >= 0 ? index : ~index - 1));
        }
        private void RebuildLayout()
        {
            if (heights == null || layingOut) return;
            layingOut = true;
            try
            {
                rowEdges = new int[rows + 1]; colEdges = new int[cols + 1];
                for (int i = 0; i < rows; i++) rowEdges[i + 1] = checked(rowEdges[i] + Scaled(heights[i]));
                for (int i = 0; i < cols; i++) colEdges[i + 1] = checked(colEdges[i] + Scaled(widths[i]));
                bool h = false, v = false;
                bool allowHorizontal = scrollBars == ScrollBarOptions.Horizontal || scrollBars == ScrollBarOptions.Both;
                bool allowVertical = scrollBars == ScrollBarOptions.Vertical || scrollBars == ScrollBarOptions.Both;
                for (int i = 0; i < 3; i++)
                {
                    h = allowHorizontal && colEdges[cols] > ClientSize.Width - (v ? vertical.Width : 0);
                    v = allowVertical && rowEdges[rows] > ClientSize.Height - (h ? horizontal.Height : 0);
                }
                horizontalShown = h; verticalShown = v;
                horizontal.Visible = h; vertical.Visible = v;
                Rectangle area = Viewport;
                horizontal.SetBounds(0, area.Height, area.Width, horizontal.Height);
                vertical.SetBounds(area.Width, 0, vertical.Width, area.Height);
                ConfigureScroll(horizontal, colEdges[cols] - colEdges[fixedCols], Math.Max(1, area.Width - colEdges[fixedCols]));
                ConfigureScroll(vertical, rowEdges[rows] - rowEdges[fixedRows], Math.Max(1, area.Height - rowEdges[fixedRows]));
            }
            finally { layingOut = false; }
            RequestPaint();
        }
        private static void ConfigureScroll(ScrollBar bar, int extent, int page)
        {
            int old = bar.Value;
            bar.Minimum = 0; bar.Maximum = Math.Max(0, extent - 1);
            bar.LargeChange = Math.Max(1, Math.Min(extent, page)); bar.SmallChange = 20;
            bar.Value = Math.Min(old, Math.Max(0, bar.Maximum - bar.LargeChange + 1));
        }
        private static void SetScroll(ScrollBar bar, int value) => bar.Value = Math.Max(0, Math.Min(value, Math.Max(0, bar.Maximum - bar.LargeChange + 1)));
        private void ScrollChanged(object sender, EventArgs e)
        {
            EndEdit(true); RequestPaint();
            if (!layingOut) ScrollPositionChanged?.Invoke(this, EventArgs.Empty);
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e); RebuildLayout(); }
        protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); RebuildLayout(); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); RequestPaint(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Rectangle area = Viewport;
            // Separate frozen and scrolling regions so scrolled content cannot paint over headers.
            PaintRegion(e.Graphics, e.ClipRectangle, new Rectangle(0, 0, colEdges[fixedCols], rowEdges[fixedRows]), 0, fixedRows, 0, fixedCols);
            PaintRegion(e.Graphics, e.ClipRectangle, new Rectangle(colEdges[fixedCols], 0, Math.Max(0, area.Width - colEdges[fixedCols]), rowEdges[fixedRows]), 0, fixedRows, LeftCol, cols);
            PaintRegion(e.Graphics, e.ClipRectangle, new Rectangle(0, rowEdges[fixedRows], colEdges[fixedCols], Math.Max(0, area.Height - rowEdges[fixedRows])), TopRow, rows, 0, fixedCols);
            PaintRegion(e.Graphics, e.ClipRectangle, new Rectangle(colEdges[fixedCols], rowEdges[fixedRows], Math.Max(0, area.Width - colEdges[fixedCols]), Math.Max(0, area.Height - rowEdges[fixedRows])), TopRow, rows, LeftCol, cols);
        }
        private void PaintRegion(Graphics graphics, Rectangle clip, Rectangle region, int firstRow, int lastRow, int firstCol, int lastCol)
        {
            region.Intersect(Viewport); region.Intersect(clip);
            if (region.IsEmpty || region.Width <= 0 || region.Height <= 0) return;
            GraphicsState state = graphics.Save(); graphics.SetClip(region);
            try
            {
                for (int r = firstRow; r < lastRow && RowY(r) < region.Bottom; r++)
                    for (int c = firstCol; c < lastCol && ColX(c) < region.Right; c++)
                    {
                        Rectangle bounds = GetCellBounds(r, c);
                        if (!bounds.IntersectsWith(region)) continue;
                        cells.TryGetValue(Key(r, c), out GridCell cell);
                        bool header = r < fixedRows || c < fixedCols;
                        using (var background = new SolidBrush(cell != null && !cell.BackColor.IsEmpty ? cell.BackColor : header ? HeaderBackColor : BackColor)) graphics.FillRectangle(background, bounds);
                        Rectangle content = bounds;
                        int line = gridLineWidth == 0 ? 0 : Scaled(gridLineWidth);
                        content.Width = Math.Max(0, content.Width - line); content.Height = Math.Max(0, content.Height - line);
                        if (cell?.Image != null && content.Width > 0 && content.Height > 0) DrawCellImage(graphics, cell, content);
                        if (!string.IsNullOrEmpty(cell?.Text)) TextRenderer.DrawText(graphics, cell.Text, Font, Rectangle.Inflate(content, -2, -1), cell.ForeColor.IsEmpty ? ForeColor : cell.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.PreserveGraphicsClipping);
                        if (highlight && !header && Selection.Contains(r, c))
                            using (var selection = new SolidBrush(SelectionColor)) graphics.FillRectangle(selection, content);
                        if (line > 0) using (var pen = new Pen(GridLineColor, line))
                        {
                            graphics.DrawLine(pen, bounds.Right - line / 2f, bounds.Top, bounds.Right - line / 2f, bounds.Bottom);
                            graphics.DrawLine(pen, bounds.Left, bounds.Bottom - line / 2f, bounds.Right, bounds.Bottom - line / 2f);
                        }
                        if (Focused && r == row && c == col && !header && content.Width > 2 && content.Height > 2) ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(content, -1, -1));
                    }
            }
            finally { graphics.Restore(state); }
        }
        private static void DrawCellImage(Graphics g, GridCell cell, Rectangle bounds)
        {
            GraphicsState state = g.Save(); g.SetClip(bounds, CombineMode.Intersect);
            try
            {
                Image image = cell.Image;
                switch (cell.ImageLayout)
                {
                    case CellImageLayout.Stretch: g.DrawImage(image, bounds); break;
                    case CellImageLayout.Tile:
                        using (var brush = new TextureBrush(image)) { brush.TranslateTransform(bounds.X, bounds.Y); g.FillRectangle(brush, bounds); } break;
                    case CellImageLayout.Zoom:
                        double ratio = Math.Min((double)bounds.Width / image.Width, (double)bounds.Height / image.Height);
                        int w = Math.Max(1, (int)(image.Width * ratio)), h = Math.Max(1, (int)(image.Height * ratio));
                        g.DrawImage(image, new Rectangle(bounds.X + (bounds.Width - w) / 2, bounds.Y + (bounds.Height - h) / 2, w, h)); break;
                    default: g.DrawImageUnscaled(image, bounds.X + (bounds.Width - image.Width) / 2, bounds.Y + (bounds.Height - image.Height) / 2); break;
                }
            }
            finally { g.Restore(state); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            EndEdit(true); Focus();
            CellAddress? hit = HitTest(e.Location);
            if (!hit.HasValue || hit.Value.Row < fixedRows || hit.Value.Column < fixedCols) return;
            anchorRow = hit.Value.Row; anchorCol = hit.Value.Column;
            dragging = true; Capture = true; dragTimer.Start();
            SelectRange(anchorRow, anchorCol, anchorRow, anchorCol); SetCurrent(anchorRow, anchorCol);
        }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (dragging) ExtendDrag(e.Location); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && dragging)
            {
                ExtendDrag(e.Location); CancelDrag();
                SelectionCompleted?.Invoke(this, new GridSelectionEventArgs(Selection));
            }
            base.OnMouseUp(e); MouseUpEvent?.Invoke(this, e);
        }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) CancelDrag(); }
        private void CancelDrag() { dragging = false; dragTimer.Stop(); if (Capture) Capture = false; }
        private void ExtendDrag(Point point)
        {
            Rectangle area = Viewport;
            if (area.Width <= colEdges[fixedCols] || area.Height <= rowEdges[fixedRows]) return;
            point.X = Math.Max(colEdges[fixedCols], Math.Min(point.X, area.Right - 1));
            point.Y = Math.Max(rowEdges[fixedRows], Math.Min(point.Y, area.Bottom - 1));
            CellAddress? hit = HitTest(point);
            int r = hit?.Row ?? FindIndex(rowEdges, point.Y + vertical.Value);
            int c = hit?.Column ?? FindIndex(colEdges, point.X + horizontal.Value);
            if (endRow == r && endCol == c) return;
            SelectRange(anchorRow, anchorCol, r, c); SetCurrent(r, c);
        }
        private void DragTick(object sender, EventArgs e)
        {
            if (!dragging) return;
            Point point = PointToClient(MousePosition); Rectangle area = Viewport;
            if (point.X < colEdges[fixedCols] + 12) SetScroll(horizontal, horizontal.Value - 20);
            else if (point.X >= area.Right - 12) SetScroll(horizontal, horizontal.Value + 20);
            if (point.Y < rowEdges[fixedRows] + 12) SetScroll(vertical, vertical.Value - 20);
            else if (point.Y >= area.Bottom - 12) SetScroll(vertical, vertical.Value + 20);
            ExtendDrag(point);
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ScrollBar bar = (ModifierKeys & Keys.Shift) != 0 ? (ScrollBar)horizontal : vertical;
            SetScroll(bar, bar.Value - e.Delta / 120 * Scaled(heights[row]) * 3);
        }
        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down || base.IsInputKey(keyData);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled) return;
            if (e.KeyCode == Keys.Escape) { CancelDrag(); EndEdit(false); e.Handled = true; return; }
            if (e.KeyCode == Keys.F2 || e.KeyCode == Keys.Enter) { BeginEdit(); e.Handled = AllowEditing; return; }
            int r = row, c = col;
            switch (e.KeyCode)
            {
                case Keys.Left: c--; break; case Keys.Right: c++; break;
                case Keys.Up: r--; break; case Keys.Down: r++; break;
                default: return;
            }
            r = Math.Max(fixedRows, Math.Min(rows - 1, r)); c = Math.Max(fixedCols, Math.Min(cols - 1, c));
            if (e.Shift) SelectRange(Math.Max(fixedRows, startRow), Math.Max(fixedCols, startCol), r, c); else SelectRange(r, c, r, c);
            SetCurrent(r, c); EnsureVisible(r, c); e.Handled = true; e.SuppressKeyPress = true;
        }
        public void EnsureVisible(int r, int c)
        {
            CheckRow(r); CheckCol(c); Rectangle area = Viewport;
            if (r >= fixedRows)
            {
                if (RowY(r) < rowEdges[fixedRows]) TopRow = r;
                else if (RowY(r) + Scaled(heights[r]) > area.Bottom) SetScroll(vertical, rowEdges[r + 1] - area.Bottom);
            }
            if (c >= fixedCols)
            {
                if (ColX(c) < colEdges[fixedCols]) LeftCol = c;
                else if (ColX(c) + Scaled(widths[c]) > area.Right) SetScroll(horizontal, colEdges[c + 1] - area.Right);
            }
        }
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e); CellAddress? hit = HitTest(e.Location);
            if (e.Button != MouseButtons.Left || !hit.HasValue || hit.Value.Row < fixedRows || hit.Value.Column < fixedCols) return;
            SetCurrent(hit.Value.Row, hit.Value.Column);
            CellDoubleClick?.Invoke(this, new GridCellEventArgs(row, col)); BeginEdit();
        }
        public void BeginEdit()
        {
            if (!AllowEditing || row < fixedRows || col < fixedCols) return;
            EndEdit(true); EnsureVisible(row, col); editRow = row; editCol = col;
            if (editor == null)
            {
                editor = new TextBox(); Controls.Add(editor);
                editor.KeyDown += (s, e) =>
                {
                    if (e.KeyCode != Keys.Enter && e.KeyCode != Keys.Escape) return;
                    EndEdit(e.KeyCode == Keys.Enter); Focus(); e.Handled = true; e.SuppressKeyPress = true;
                };
                editor.LostFocus += (s, e) => EndEdit(true);
            }
            editor.Font = Font; editor.Bounds = Rectangle.Intersect(GetCellBounds(row, col), Viewport);
            editor.Text = GetCell(row, col).Text; editor.Visible = true; editor.BringToFront(); editor.Focus(); editor.SelectAll();
        }
        public void EndEdit(bool commit)
        {
            if (editor == null || !editor.Visible || endingEdit) return;
            endingEdit = true;
            try
            {
                string value = editor.Text; editor.Visible = false;
                if (commit && GetCell(editRow, editCol).Text != value)
                {
                    GetCell(editRow, editCol).Text = value;
                    CellEdited?.Invoke(this, new GridCellEventArgs(editRow, editCol));
                }
            }
            finally { endingEdit = false; }
        }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); RequestPaint(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); RequestPaint(); }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { dragTimer.Stop(); dragTimer.Dispose(); cells.Clear(); }
            base.Dispose(disposing);
        }
    }
}
