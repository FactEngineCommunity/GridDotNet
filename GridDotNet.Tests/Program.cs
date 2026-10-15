using System;
using System.Drawing;
using System.Windows.Forms;

namespace GridDotNet.Tests
{
    internal sealed class TestGrid : GridControl
    {
        public void Down(Point p) => OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, p.X, p.Y, 0));
        public void DragTo(Point p) => OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, p.X, p.Y, 0));
        public void Up(Point p) => OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, p.X, p.Y, 0));
        public void Key(Keys key) => OnKeyDown(new KeyEventArgs(key));
    }

    internal static class Program
    {
        private static int checks;
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                using (var grid = new TestGrid { Size = new Size(360, 200), Rows = 30, Cols = 20 })
                using (var image = new Bitmap(8, 8))
                {
                    using (Graphics g = Graphics.FromImage(image)) g.Clear(Color.SeaGreen);
                    grid.CreateControl();
                    grid.SelectRange(4, 5, 2, 3);
                    grid.Row = 0; grid.Col = 0; grid.Text = "single";
                    Equal("single", grid.Cells[0, 0].Text, "single-cell write");
                    Equal("", grid.Cells[2, 3].Text, "single write preserves other cells");
                    Equal(2, grid.Selection.Top, "current-cell assignment preserves selection");
                    grid.FillStyle = 1; grid.Picture = image; grid.Text = "range";
                    for (int r = 2; r <= 4; r++) for (int c = 3; c <= 5; c++)
                    {
                        Equal(image, grid.Cells[r, c].Image, "reverse selection image fill");
                        Equal("range", grid.Cells[r, c].Text, "reverse selection text fill");
                    }
                    Equal(null, grid.Cells[1, 3].Image, "range boundary");
                    grid.Picture = null;
                    Equal(null, grid.Cells[4, 5].Image, "clear picture");
                    Equal("range", grid.Cells[4, 5].Text, "clearing image preserves text");
                    grid.ClearSelectionContent();
                    Equal("", grid.Cells[4, 5].Text, "clear selection text");

                    grid.set_RowHeight(1, 40); grid.set_ColWidth(1, 90);
                    Equal(40, grid.RowHeight(1), "variable row size");
                    Equal(90, grid.ColWidth(1), "variable column size");
                    grid.FixedRows = 1; grid.FixedCols = 1;
                    grid.TopRow = 10; grid.LeftCol = 8;
                    Equal(10, grid.TopRow, "vertical scroll"); Equal(8, grid.LeftCol, "horizontal scroll");
                    Equal(0, grid.HitTest(new Point(2, 2)).Value.Row, "frozen row hit test");
                    Equal(0, grid.HitTest(new Point(2, 2)).Value.Column, "frozen column hit test");
                    Equal(true, grid.get_RowIsVisible(10), "scrolled visible row");
                    Equal(false, grid.get_RowIsVisible(1), "row hidden behind fixed header");
                    Equal(10, grid.HitTest(Center(grid.GetCellBounds(10, 8))).Value.Row, "scrolled data hit test");

                    grid.TopRow = 0; grid.LeftCol = 0;
                    int completed = 0;
                    grid.SelectionCompleted += (s, e) => completed++;
                    grid.Down(Center(grid.GetCellBounds(3, 3)));
                    grid.DragTo(Center(grid.GetCellBounds(1, 1)));
                    grid.Up(Center(grid.GetCellBounds(1, 1)));
                    Equal(1, grid.Selection.Top, "upward drag top"); Equal(3, grid.Selection.Bottom, "upward drag bottom");
                    Equal(1, grid.Selection.Left, "leftward drag left"); Equal(3, grid.Selection.Right, "leftward drag right");
                    Equal(1, completed, "one completion event"); Equal(false, grid.Capture, "capture released");
                    grid.Down(new Point(2, 2)); grid.Up(new Point(2, 2));
                    Equal(1, completed, "header cannot start selection");
                    grid.Down(Center(grid.GetCellBounds(1, 1)));
                    grid.DragTo(new Point(-100, -100)); grid.Up(new Point(-100, -100));
                    Equal(1, grid.Selection.Top, "outside drag clamps to first data row");
                    Equal(1, grid.Selection.Left, "outside drag excludes header column");
                    grid.Down(Center(grid.GetCellBounds(1, 1)));
                    grid.Capture = false;
                    grid.DragTo(Center(grid.GetCellBounds(3, 3)));
                    Equal(1, grid.Selection.Bottom, "capture loss cancels dragging");
                    grid.Row = 1; grid.Col = 1; grid.SelectRange(1, 1, 1, 1);
                    grid.Key(Keys.Shift | Keys.Right); Equal(2, grid.Selection.Right, "keyboard extends selection");
                    grid.Key(Keys.Down); Equal(2, grid.Selection.Top, "arrow replaces selection");
                    grid.AllowEditing = true;
                    grid.FillStyle = 1; grid.SelectRange(1, 1, 3, 3);
                    grid.BeginEdit();
                    TextBox editor = null;
                    foreach (Control control in grid.Controls) if (control is TextBox box) editor = box;
                    editor.Text = "edited"; grid.EndEdit(true);
                    Equal("edited", grid.Cells[2, 2].Text, "editor saves original cell");
                    Equal("", grid.Cells[1, 1].Text, "editor ignores range FillStyle");

                    grid.HighLight = false; grid.Cells[1, 1].Image = image;
                    using (var rendered = new Bitmap(grid.Width, grid.Height))
                    {
                        grid.DrawToBitmap(rendered, grid.ClientRectangle);
                        Point p = Center(grid.GetCellBounds(1, 1));
                        Equal(Color.SeaGreen.ToArgb(), rendered.GetPixel(p.X, p.Y).ToArgb(), "cell image rendered");
                        grid.HighLight = true; grid.SelectRange(1, 1, 1, 1);
                        grid.DrawToBitmap(rendered, grid.ClientRectangle);
                        Equal(false, rendered.GetPixel(p.X, p.Y).ToArgb() == Color.SeaGreen.ToArgb(), "selection overlays image");
                        Equal(image, grid.Cells[1, 1].Image, "highlight preserves image");
                    }
                    grid.Cells[29, 19].Text = "discard";
                    grid.Rows = 4; grid.Cols = 4; grid.Rows = 30; grid.Cols = 20;
                    Equal("", grid.Cells[29, 19].Text, "resize removes deleted cells");
                    Throws(() => grid.Row = -1, "invalid row");
                    grid.set_ColWidth(0, -1);
                    Equal(1, grid.get_ColWidth(0), "negative width clamps to one pixel");
                    grid.set_RowHeight(0, -100);
                    Equal(1, grid.get_RowHeight(0), "negative height clamps to one pixel");
                    grid.set_ColWidth(0, 0);
                    Equal(1, grid.get_ColWidth(0), "zero width clamps to one pixel");
                    grid.set_RowHeight(0, 0.25);
                    Equal(1, grid.get_RowHeight(0), "subpixel height clamps to one pixel");
                    grid.set_ColWidth(0, 12.6);
                    Equal(13, grid.get_ColWidth(0), "positive fractional width rounds normally");
                    Throws(() => grid.set_ColWidth(0, double.NaN), "NaN size remains invalid");
                    Throws(() => grid.set_RowHeight(0, double.PositiveInfinity), "infinite size remains invalid");
                    Throws(() => grid.set_ColWidth(0, 10001), "oversized dimension remains invalid");
                    Throws(() => grid.FillStyle = 2, "invalid fill style");
                    using (var follower = new GridControl { Size = grid.Size, Rows = grid.Rows, Cols = grid.Cols })
                    {
                        grid.ScrollPositionChanged += (s, e) => follower.TopRow = grid.TopRow;
                        grid.TopRow = 12;
                        Equal(grid.TopRow, follower.TopRow, "scroll event synchronizes row position");
                    }
                }
                using (var grid = new GridControl { Rows = 4, Cols = 2, FillStyle = 1 })
                using (var image = new Bitmap(8, 8))
                {
                    grid.Cells[0, 0].Text = "first";
                    grid.Cells[1, 0].Text = "second";
                    grid.Cells[1, 1].Image = image;
                    grid.Cells[1, 1].BackColor = Color.Red;
                    grid.Cells[1, 1].Tag = "status";
                    grid.set_RowHeight(1, 42);
                    grid.Row = 2; grid.SelectRange(1, 0, 3, 1);
                    GridCell survivor = grid.Cells[1, 1];
                    grid.RemoveItem(0);
                    Equal(3, grid.Rows, "remove first row count");
                    Equal("second", grid.Cells[0, 0].Text, "remove shifts text up");
                    Equal(survivor, grid.Cells[0, 1], "remove preserves whole surviving cell");
                    Equal(image, grid.Cells[0, 1].Image, "remove preserves image");
                    Equal(Color.Red, grid.Cells[0, 1].BackColor, "remove preserves style");
                    Equal("status", grid.Cells[0, 1].Tag, "remove preserves tag");
                    Equal(42, grid.get_RowHeight(0), "remove shifts row height");
                    Equal(1, grid.Row, "remove shifts current row");
                    Equal(0, grid.Selection.Top, "remove shifts selection top");
                    Equal(2, grid.Selection.Bottom, "remove shifts selection bottom");
                    grid.AddItem("new\tstatus", 0);
                    Equal(4, grid.Rows, "insert row count");
                    Equal("new", grid.Cells[0, 0].Text, "insert first field");
                    Equal("status", grid.Cells[0, 1].Text, "insert tab-separated field");
                    Equal("second", grid.Cells[1, 0].Text, "insert shifts existing text down");
                    Equal(survivor, grid.Cells[1, 1], "insert preserves existing cell");
                    Equal(42, grid.get_RowHeight(1), "insert shifts row height down");
                    Equal(2, grid.Row, "insert shifts current row");
                    grid.AddItem("");
                    Equal(5, grid.Rows, "append blank row");
                    Equal("", grid.Cells[4, 0].Text, "appended row is blank");
                    grid.RemoveItem(2);
                    Equal(4, grid.Rows, "remove middle row count");
                    Equal("second", grid.Cells[1, 0].Text, "remove middle preserves earlier row");
                    grid.RemoveItem(3);
                    Equal(3, grid.Rows, "remove last row count");
                    Throws(() => grid.RemoveItem(-1), "invalid remove index");
                    Throws(() => grid.AddItem("", 4), "invalid insert index");
                    grid.Rows = 1;
                    try { grid.RemoveItem(0); throw new Exception("last row removal must fail"); }
                    catch (InvalidOperationException) { checks++; }
                    Equal(1, grid.Rows, "failed deletion leaves last row intact");
                    grid.Rows = 2; grid.FixedRows = 1; grid.Row = 1;
                    grid.RemoveItem(1);
                    Equal(0, grid.FixedRows, "deletion retains one non-fixed row");
                    Equal(0, grid.Row, "deleted last current row clamps");
                }
                using (var grid = new TestGrid { Name = "boundary_grid", Rows = 10, Cols = 10 })
                {
                    grid.CreateControl();
                    try { grid.Col = 10; throw new Exception("invalid column must fail"); }
                    catch (ArgumentOutOfRangeException e)
                    {
                        Equal(10, e.ActualValue, "column exception includes offending value");
                        Equal(true, e.Message.Contains("boundary_grid: Col"), "column exception identifies control and operation");
                        Equal(true, e.Message.Contains("Cols=10"), "column exception includes column count");
                    }
                    grid.Size = new Size(1000, 600);
                    grid.Down(Center(grid.GetCellBounds(0, 0)));
                    grid.DragTo(new Point(10000, 10000)); grid.Up(new Point(10000, 10000));
                    Equal(9, grid.Selection.Right, "drag beyond ten-column grid clamps to column nine");
                    Equal(9, grid.Selection.Bottom, "drag beyond ten-row grid clamps to row nine");
                    grid.Row = 9; grid.Col = 9;
                    grid.Key(Keys.Right); grid.Key(Keys.Down);
                    Equal(9, grid.Col, "right arrow at last column stays in bounds");
                    Equal(9, grid.Row, "down arrow at last row stays in bounds");
                    grid.Cols = 20;
                    grid.Size = new Size(180, 130);
                    grid.Col = 19; grid.LeftCol = 15;
                    grid.RowColChange += (s, e) =>
                    {
                        grid.get_ColWidth(grid.LeftCol);
                        Equal(true, grid.LeftCol < grid.Cols, "resize event sees valid scroll column");
                        grid.GetCellBounds(grid.Row, grid.Col);
                    };
                    grid.Cols = 10;
                    grid.Row = 9; grid.TopRow = 8;
                    grid.ScrollPositionChanged += (s, e) =>
                    {
                        grid.get_RowHeight(grid.TopRow);
                        grid.get_ColWidth(grid.LeftCol);
                    };
                    grid.Rows = 2;
                    Equal(true, grid.TopRow < grid.Rows, "resize clamps vertical scroll before events");
                    using (var bitmap = new Bitmap(grid.Width, grid.Height)) grid.DrawToBitmap(bitmap, grid.ClientRectangle);
                }
                using (var grid = new TestGrid { Size = new Size(300, 180), Rows = 30, Cols = 30 })
                {
                    grid.CreateControl();
                    HScrollBar hbar = null; VScrollBar vbar = null;
                    foreach (Control child in grid.Controls)
                    {
                        if (child is HScrollBar h) hbar = h;
                        if (child is VScrollBar v) vbar = v;
                    }
                    Equal(ScrollBars.None, grid.ScrollBars, "scrollbars hidden by default");
                    foreach (ScrollBars option in new[] { ScrollBars.None, ScrollBars.Horizontal, ScrollBars.Vertical, ScrollBars.Both })
                    {
                        grid.ScrollBars = option;
                        bool horizontal = option == ScrollBars.Horizontal || option == ScrollBars.Both;
                        bool vertical = option == ScrollBars.Vertical || option == ScrollBars.Both;
                        Equal(horizontal, hbar.Visible, "horizontal visibility for " + option);
                        Equal(vertical, vbar.Visible, "vertical visibility for " + option);
                        int right = grid.ClientSize.Width - (vertical ? vbar.Width : 0);
                        int bottom = grid.ClientSize.Height - (horizontal ? hbar.Height : 0);
                        Equal(true, grid.HitTest(new Point(right - 1, bottom - 1)).HasValue, "content fills available viewport for " + option);
                        if (horizontal) Equal(false, grid.HitTest(new Point(1, bottom)).HasValue, "horizontal bar excluded from hit test");
                        if (vertical) Equal(false, grid.HitTest(new Point(right, 1)).HasValue, "vertical bar excluded from hit test");
                        grid.TopRow = 10; grid.LeftCol = 10;
                        Equal(10, grid.TopRow, "programmatic row scrolling for " + option);
                        Equal(10, grid.LeftCol, "programmatic column scrolling for " + option);
                        grid.TopRow = 0; grid.LeftCol = 0;
                        grid.Down(Center(grid.GetCellBounds(0, 0)));
                        grid.DragTo(Center(grid.GetCellBounds(2, 2))); grid.Up(Center(grid.GetCellBounds(2, 2)));
                        Equal(2, grid.Selection.Bottom, "rectangular selection rows for " + option);
                        Equal(2, grid.Selection.Right, "rectangular selection columns for " + option);
                    }
                    grid.ScrollBars = ScrollBars.None;
                    grid.TopRow = 10; grid.LeftCol = 10;
                    grid.ScrollBars = ScrollBars.Both;
                    Equal(10, grid.TopRow, "showing bars retains vertical scroll");
                    Equal(10, grid.LeftCol, "showing bars retains horizontal scroll");
                    grid.ScrollBars = ScrollBars.None;
                    Equal(10, grid.TopRow, "hiding bars retains vertical scroll");
                    Equal(10, grid.LeftCol, "hiding bars retains horizontal scroll");
                    grid.Rows = 1; grid.Cols = 1; grid.ScrollBars = ScrollBars.Both;
                    Equal(false, hbar.Visible, "enabled horizontal scrollbar hides when content fits");
                    Equal(false, vbar.Visible, "enabled vertical scrollbar hides when content fits");
                    grid.Size = new Size(100, 100); grid.Rows = 1; grid.Cols = 1;
                    grid.set_ColWidth(0, 101); grid.set_RowHeight(0, 95);
                    Equal(true, hbar.Visible, "horizontal overflow shows horizontal bar");
                    Equal(true, vbar.Visible, "horizontal bar can induce vertical overflow");
                    grid.ScrollBars = ScrollBars.Vertical;
                    Equal(false, hbar.Visible, "horizontal disabled at overflow boundary");
                    Equal(false, vbar.Visible, "vertical no longer needed after horizontal is hidden");
                    Throws(() => grid.ScrollBars = (ScrollBars)99, "invalid scrollbar option");
                }
                using (var grid = new GridControl { Rows = 3, Cols = 3 })
                {
                    Equal(26, grid.DefaultRowHeight, "initial default row height");
                    Equal(60, grid.DefaultColWidth, "initial default column width");
                    grid.set_RowHeight(1, 26); grid.set_ColWidth(1, 60);
                    grid.DefaultRowHeight = 30; grid.DefaultColWidth = 80;
                    Equal(30, grid.get_RowHeight(0), "default updates existing uncustomized row");
                    Equal(80, grid.get_ColWidth(0), "default updates existing uncustomized column");
                    Equal(26, grid.get_RowHeight(1), "explicit height equal to old default stays customized");
                    Equal(60, grid.get_ColWidth(1), "explicit width equal to old default stays customized");
                    grid.Rows = 5; grid.Cols = 5;
                    Equal(30, grid.get_RowHeight(4), "grown row uses configured default");
                    Equal(80, grid.get_ColWidth(4), "grown column uses configured default");
                    grid.AddItem("", 0);
                    Equal(30, grid.get_RowHeight(0), "inserted row uses configured default");
                    grid.DefaultRowHeight = 32;
                    Equal(32, grid.get_RowHeight(0), "inserted row follows new default");
                    Equal(26, grid.get_RowHeight(2), "insertion moves row override");
                    grid.RemoveItem(0); grid.DefaultRowHeight = 34;
                    Equal(26, grid.get_RowHeight(1), "deletion moves row override");
                    grid.ResetRowHeight(1); grid.ResetColWidth(1);
                    Equal(34, grid.get_RowHeight(1), "reset height uses current default");
                    Equal(80, grid.get_ColWidth(1), "reset width uses current default");
                    grid.DefaultRowHeight = 36; grid.DefaultColWidth = 90;
                    Equal(36, grid.get_RowHeight(1), "reset row follows future defaults");
                    Equal(90, grid.get_ColWidth(1), "reset column follows future defaults");
                    grid.set_RowHeight(4, 70); grid.set_ColWidth(4, 110);
                    grid.Rows = 2; grid.Cols = 2; grid.Rows = 5; grid.Cols = 5;
                    Equal(36, grid.get_RowHeight(4), "regrown row discards removed override");
                    Equal(90, grid.get_ColWidth(4), "regrown column discards removed override");
                    grid.DefaultRowHeight = -1; grid.DefaultColWidth = 0;
                    Equal(1, grid.DefaultRowHeight, "default height clamps negative size");
                    Equal(1, grid.DefaultColWidth, "default width clamps zero size");
                    Throws(() => grid.DefaultRowHeight = 10001, "invalid default height");
                    Equal(1, grid.DefaultRowHeight, "failed default change is atomic");
                    var properties = System.ComponentModel.TypeDescriptor.GetProperties(grid);
                    Equal(true, properties["DefaultRowHeight"].IsBrowsable, "default row height available in designer");
                    Equal(true, properties["DefaultColWidth"].IsBrowsable, "default column width available in designer");
                    Equal(26, ((System.ComponentModel.DefaultValueAttribute)properties["DefaultRowHeight"].Attributes[typeof(System.ComponentModel.DefaultValueAttribute)]).Value, "designer row default metadata");
                    Equal(60, ((System.ComponentModel.DefaultValueAttribute)properties["DefaultColWidth"].Attributes[typeof(System.ComponentModel.DefaultValueAttribute)]).Value, "designer column default metadata");
                }
                using (var surface = new System.ComponentModel.Design.DesignSurface(typeof(Form)))
                using (var propertyGrid = new PropertyGrid())
                {
                    var host = (System.ComponentModel.Design.IDesignerHost)surface.GetService(typeof(System.ComponentModel.Design.IDesignerHost));
                    var control = (GridControl)host.CreateComponent(typeof(GridControl), "designerGrid");
                    ((Form)host.RootComponent).Controls.Add(control);
                    var visibleProperties = System.ComponentModel.TypeDescriptor.GetProperties(control,
                        new Attribute[] { System.ComponentModel.BrowsableAttribute.Yes });
                    Equal(true, visibleProperties["DefaultRowHeight"] != null, "design host exposes browsable row default");
                    Equal(true, visibleProperties["DefaultColWidth"] != null, "design host exposes browsable column default");
                    propertyGrid.SelectedObject = control;
                    propertyGrid.PropertySort = PropertySort.Categorized;
                    propertyGrid.CreateControl(); propertyGrid.Refresh();
                    GridItem root = PropertyRoot(propertyGrid);
                    Equal(true, HasGridItem(root, "Layout", GridItemType.Category), "categorized property grid contains Layout");
                    Equal(true, HasGridItem(root, "DefaultRowHeight", GridItemType.Property), "categorized property grid contains row default");
                    Equal(true, HasGridItem(root, "DefaultColWidth", GridItemType.Property), "categorized property grid contains column default");
                    propertyGrid.PropertySort = PropertySort.Alphabetical;
                    root = PropertyRoot(propertyGrid);
                    Equal(false, HasGridItem(root, "Layout", GridItemType.Category), "alphabetical property grid has no Layout category");
                    Equal(true, HasGridItem(root, "DefaultRowHeight", GridItemType.Property), "alphabetical property grid contains row default");
                    Equal(true, HasGridItem(root, "DefaultColWidth", GridItemType.Property), "alphabetical property grid contains column default");
                    visibleProperties["DefaultRowHeight"].SetValue(control, 37);
                    visibleProperties["DefaultColWidth"].SetValue(control, 83);
                    Equal(37, control.get_RowHeight(0), "design-time row property setter works");
                    Equal(83, control.get_ColWidth(0), "design-time column property setter works");
                    Equal(true, visibleProperties["DefaultRowHeight"].ShouldSerializeValue(control), "designer serializes changed row default");
                    Equal(true, visibleProperties["DefaultColWidth"].ShouldSerializeValue(control), "designer serializes changed column default");
                    visibleProperties["DefaultRowHeight"].ResetValue(control);
                    visibleProperties["DefaultColWidth"].ResetValue(control);
                    Equal(26, control.DefaultRowHeight, "design-time row reset works");
                    Equal(60, control.DefaultColWidth, "design-time column reset works");
                }
                if (args.Length == 2 && args[0] == "--snapshot")
                {
                    using (var form = new Demo.DemoForm())
                    using (var screenshot = new Bitmap(form.Width, form.Height))
                    {
                        form.ShowInTaskbar = false;
                        form.StartPosition = FormStartPosition.Manual;
                        form.Location = new Point(-10000, -10000);
                        form.Show(); Application.DoEvents(); form.PerformLayout();
                        form.DrawToBitmap(screenshot, new Rectangle(0, 0, form.Width, form.Height));
                        screenshot.Save(args[1]);
                        form.Close();
                    }
                }
                Console.WriteLine($"PASS: {checks} checks."); return 0;
            }
            catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        }
        private static Point Center(Rectangle rect) => new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        private static GridItem PropertyRoot(PropertyGrid grid)
        {
            GridItem item = grid.SelectedGridItem;
            if (item == null) throw new Exception("PropertyGrid has no selected item.");
            while (item.Parent != null) item = item.Parent;
            return item;
        }
        private static bool HasGridItem(GridItem item, string label, GridItemType type)
        {
            if (item.Label == label && item.GridItemType == type) return true;
            foreach (GridItem child in item.GridItems) if (HasGridItem(child, label, type)) return true;
            return false;
        }
        private static void Equal<T>(T expected, T actual, string name)
        {
            if (!Equals(expected, actual)) throw new Exception($"{name}: expected {expected}, got {actual}"); checks++;
        }
        private static void Throws(Action action, string name)
        {
            try { action(); } catch (ArgumentOutOfRangeException) { checks++; return; }
            throw new Exception(name + ": expected ArgumentOutOfRangeException");
        }
    }
}
