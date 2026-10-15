using System;
using System.Drawing;

namespace GridDotNet
{
    public enum CellImageLayout { Center, Stretch, Zoom, Tile }

    /// <summary>Cell content. Images are borrowed; their owner must keep them alive and dispose them.</summary>
    public sealed class GridCell
    {
        private readonly Action changed;
        private string text = "";
        private Image image;
        private Color backColor = Color.Empty, foreColor = Color.Empty;
        private CellImageLayout imageLayout = CellImageLayout.Stretch;
        internal GridCell(Action changed) { this.changed = changed; }
        public string Text { get => text; set { text = value ?? ""; changed(); } }
        public Image Image { get => image; set { image = value; changed(); } }
        public Color BackColor { get => backColor; set { backColor = value; changed(); } }
        public Color ForeColor { get => foreColor; set { foreColor = value; changed(); } }
        public CellImageLayout ImageLayout { get => imageLayout; set { imageLayout = value; changed(); } }
        public object Tag { get; set; }
    }

    public readonly struct CellAddress
    {
        public CellAddress(int row, int column) { Row = row; Column = column; }
        public int Row { get; }
        public int Column { get; }
    }

    public readonly struct CellRange
    {
        public CellRange(int row1, int col1, int row2, int col2)
        {
            Top = Math.Min(row1, row2); Bottom = Math.Max(row1, row2);
            Left = Math.Min(col1, col2); Right = Math.Max(col1, col2);
        }
        public int Top { get; }
        public int Bottom { get; }
        public int Left { get; }
        public int Right { get; }
        public bool Contains(int row, int col) => row >= Top && row <= Bottom && col >= Left && col <= Right;
    }

    public sealed class GridSelectionEventArgs : EventArgs
    {
        public GridSelectionEventArgs(CellRange range) { Range = range; }
        public CellRange Range { get; }
    }

    public sealed class GridCellEventArgs : EventArgs
    {
        public GridCellEventArgs(int row, int column) { Row = row; Column = column; }
        public int Row { get; }
        public int Column { get; }
    }

    public sealed class GridCellCollection
    {
        private readonly GridControl grid;
        internal GridCellCollection(GridControl grid) { this.grid = grid; }
        public GridCell this[int row, int column] => grid.GetCell(row, column);
    }
}
