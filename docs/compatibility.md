# Rosters compatibility inventory

The inventory includes active **and commented-out** grid references from
`roster_frm.vb` and `roster_graphic.vb` in Rostersv11.01. Comments disabled missing
control references during migration; they are evidence of required API, not
instructions to execute or ignore that functionality. The adjacent designer
still contains commented declarations referring to `AxMSGrid.AxGrid`.

## Implemented legacy surface

| Members | Contract in this version |
| --- | --- |
| `Rows`, `Cols` | Counts, at least one; zero-based indices. Shrinking removes out-of-range content and clamps current cell/selection. |
| `AddItem(text, index)` | Append a row, or insert before optional zero-based `index`. Tab-separated fields fill successive columns, independently of `FillStyle`. Too many fields throw rather than truncate. |
| `RemoveItem(index)` | Remove the zero-based row and shift following cells and heights up. Current cell, selection, and top row adjust. At least one row must remain; removing the sole row throws `InvalidOperationException`. Removing a fixed row reduces `FixedRows`. |
| `Row`, `Col` | Current cell. Setting these does **not** reset the selected rectangle or implicitly scroll. `RowColChange` fires when the current cell changes. |
| `SelStartRow`, `SelEndRow`, `SelStartCol`, `SelEndCol` | Inclusive selection endpoints; reversed endpoints are accepted. |
| `CellSelected` | True when a valid cell selection exists; this grid always has a valid selection, initially cell 0,0. |
| `FillStyle` | `0`: current cell only; `1`: repeat across the selection. Applies to `Text` and `Picture` assignments. Default is `0`. |
| `Text` | Read the current cell's text; writes follow `FillStyle`. This overrides the normal Control.Text meaning. |
| `Picture` | Read the current cell's image; writes follow `FillStyle`. Assign `Nothing` to clear the image. |
| `HighLight` | Show/hide the visual selection overlay without changing content or selection. VB member names are case insensitive, so `HIGHLIGHT` also works. |
| `GridLineWidth` | Width in logical pixels; zero hides grid lines. It draws inside each cell's dimensions. |
| `get_RowHeight`, `set_RowHeight`, `get_ColWidth`, `set_ColWidth` | Indexed sizes in logical pixels at 96 DPI. Finite sizes below 1 (including negatives and zero) clamp to 1; positive fractional values round. NaN, infinity, and values over 10000 throw. Cell dimensions include grid lines. |
| `RowHeight(index)`, `ColWidth(index)` | Getter methods for old VB-style indexed calls. Setter syntax is `set_RowHeight` / `set_ColWidth`. |
| `get_RowIsVisible` | True if any part of the row intersects its visible region; frozen rows count as visible when on-screen. |
| `TopRow`, `LeftCol` | First scrolling row/column, excluding fixed headers. Writes scroll to that index, clamped at the last full viewport. Wheel/scrollbar movement may show a partial first cell. |
| `RowColChange` | Standard `EventHandler`, suitable for the existing VB handler signature. |
| `MouseUpEvent` | Managed `MouseEventHandler`. Existing handlers with `AxMSGrid.GridEvents_MouseUpEvent` parameters must change to `System.Windows.Forms.MouseEventArgs`. |
| `Focus`, `Refresh`, `BringToFront`, `SendToBack`, `LostFocus`, `Left`, `Top`, `Width`, `Height`, `Visible` | Inherited WinForms members. |

## Additional surface

- `DefaultRowHeight` (initially 26) and `DefaultColWidth` (initially 60): logical
  pixel sizes, exposed under Layout in the designer. Changing a default updates
  uncustomized existing rows/columns and future ones. Explicit per-index sizes
  remain overrides. `ResetRowHeight(index)` and `ResetColWidth(index)` restore
  following the default. Insertion/deletion moves row overrides with their rows.
- `Cells(row, column)`: text, image, image layout (Stretch, Center, Zoom, Tile),
  foreground/background colours, and arbitrary `Tag`.
- `Selection`: normalized `CellRange`, including `Top`, `Bottom`, `Left`, `Right`.
- `SelectRange`, `FillSelection`, `ClearSelectionContent`.
- `SelectionChanged` and `SelectionCompleted`: selection updates and completed
  left-button drags. Capture loss or Escape cancels the drag without completion;
  its last highlighted rectangle remains selected.
- `FixedRows`, `FixedCols`: optional frozen header cells, excluded from mouse
  selection and text editing. Programmatic fills can still address them.
- `ScrollPositionChanged`: use to coordinate multiple controls. Consumers should
  guard feedback loops when syncing grids with different sizes or widths.
- `ScrollBars`: `System.Windows.Forms.ScrollBars.None` (default), `Horizontal`,
  `Vertical`, or `Both`. Enables the selected bars when content overflows; hidden
  bars reserve no space. Code, keyboard, wheel, and edge-drag scrolling still
  work when bars are hidden. Toggling clamps offsets only if the enlarged
  viewport makes the old position exceed the new scroll range.
- `HitTest`, `GetCellBounds`, `EnsureVisible`.
- `BeginUpdate` / `EndUpdate`: aggregate repaints for bulk cell changes; always
  pair these in `Try` / `Finally`.
- `AllowEditing`, `BeginEdit`, `EndEdit`, `CellEdited`, `CellDoubleClick`.
  Editing changes just the edited cell, regardless of `FillStyle`.

## Migration decisions requiring attention

1. Replace commented `AxMSGrid.AxGrid` designer declarations and control creation
   with `GridDotNet.GridControl`. Recreate placement and any old saved properties.
   This DLL does not deserialize COM `OcxState`.
2. Replace `Image.FromFile("")` with `Nothing`. The former throws before the
   assignment reaches the control. Old resource/icon loading helpers may also
   need converting to managed `System.Drawing.Image` resources.
3. Some drawing routines set selection bounds and assign `Picture` without
   setting `FillStyle`. Their old saved designer defaults are unavailable.
   Set `FillStyle = 1` explicitly for these grids/routines, or call
   `FillSelection(image)`. For example, `clear_roster_form_grid`,
   `clear_roster_form_required_shifts_grid`, and `redraw_required_shift` use ranges.
   Do not globally force range filling for grids that use current-cell writes.
4. Old sizing mixes twips helpers, literal offsets such as `30`, and pixel-based
   `MeasureString`. This control uses logical pixels, scaled by monitor DPI.
   Audit those calculations; do not blindly preserve twips conversions.
   In particular, do not add `GridLineWidth` again to `get_RowHeight` when
   calculating this control's full row height.
5. Restore disabled `Handles grid.RowColChange` and other event connections
   after the new controls exist. The grid supplies interaction; the application
   remains responsible for interpreting selection as bookings and updating data.
6. `RequiredShiftDBGrid` can use the same text/editing control, but its name does
   not imply database binding. This version is unbound. Database operations stay
   in the application.

## Rendering and ownership

The grid paints visible cells only, with WinForms double buffering and sparse
cell storage. It shares supplied images rather than cloning them per cell.
The caller owns images and must keep them alive while referenced. Clearing a
cell or disposing the grid does not dispose those images. UI changes must be
made on the WinForms UI thread.

Selection is an overlay: it never replaces or erases an image. The default image
layout stretches an image within the cell, making repeated time-bar images
continuous apart from grid lines. Set `GridLineWidth = 0` for uninterrupted bars.

The first version does not reproduce every unknown ActiveX behaviour, provide
database binding, expose accessible individual cells to screen readers, or
implement merged cells, printing, sorting, clipboard operations, and undo.
It uses a standard managed WinForms renderer, not DynamicGrid or native COM.
The same sources target `net481` and `net10.0-windows`; use the corresponding DLL
for each consuming application. The modern target includes explicit designer
serialization metadata for custom colours and excludes runtime scroll positions
from serialization. These declarations also apply to the Framework build.
High-DPI metrics are implemented, but mixed-monitor visual behaviour and VS
behaviour still need interactive validation in your environment. The default
size properties have been tested through a WinForms design host/PropertyGrid;
Rosters integration also confirmed that clearing stale local `.vs` state can
restore properties missing from the Visual Studio designer. See the README for
setup and troubleshooting steps.
Both runtime suites exercise a PropertyGrid. The Framework suite additionally
uses `System.Design.DesignSurface`; that Framework-only assembly is not used in
the .NET 10 suite. The .NET 10 Visual Studio designer still requires a manual
smoke test; a passing runtime PropertyGrid check is not that designer test.

## Diagnosing invalid indices

Invalid row/column exceptions include the control's `Name`, the member that
validated the index, the requested value, and the current count. Counts are not
last indices: a grid with `Cols = 10` has columns `0` through `9`. For example,
`grid.Col = 10` reports the `Col` operation and `Cols=10`.
Dimension changes rebuild geometry and clamp scroll/current positions before
raising change events, so event handlers can safely query the new grid state.
