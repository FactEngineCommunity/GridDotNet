# GridDotNet

A managed Windows Forms grid for .NET Framework 4.8.1, with image cells,
rectangular drag selection, and a compatibility API for the AxMSGrid calls
found in Rosters. Implemented in C#; usable from VB.NET. No third-party packages
or ActiveX installation are required when the 4.8.1 targeting pack is installed.

## Features

- Rectangular left-button drag selection in any direction, with edge scrolling.
- Text and shared images in cells, with Stretch, Center, Zoom, or Tile layouts.
- Configurable default row heights and column widths, plus individual overrides.
- Optional fixed header rows and columns.
- Independent horizontal/vertical scrollbar visibility; both hidden by default.
- Optional text editing, keyboard navigation, and selection/scroll events.
- Legacy members including `Row`, `Col`, selection endpoints, `Text`, `Picture`,
  `FillStyle`, indexed sizing methods, `AddItem`, and `RemoveItem`.

The control is general purpose and unbound: Rosters supplies employee, shift,
validation, and database logic.

## Try the demo in Visual Studio 2026

1. Open `GridDotNet.sln`.
2. Set **GridDotNet.Demo** as the startup project.
3. Press **F5**.
4. Hold the left mouse button and drag over cells, then click **Fill with image**
   or **Clear selection**. **Fill using old API** demonstrates `FillStyle` and `Picture`.

The demo has 40 sample employee rows and 96 quarter-hour columns, fixed name and
time headers, and some existing image-filled cells. The control itself has no
employee or shift logic. Drag in any direction; dragging at an edge scrolls.
Arrow keys move the current cell; Shift + arrows extend selection. Shift + mouse
wheel scrolls horizontally. Double-click, F2, or Enter starts text editing;
Enter saves, Escape cancels, and leaving the editor commits.

Both scrollbars are hidden by default. The demo's **Scrollbars** dropdown lets
you enable the horizontal scrollbar, vertical scrollbar, or both. Enabled bars
appear when content overflows. Hiding them retains scrolling through code,
keyboard navigation, the mouse wheel, and dragging at the edges.

```vb
grid.ScrollBars = System.Windows.Forms.ScrollBars.None       ' Default: both hidden
grid.ScrollBars = System.Windows.Forms.ScrollBars.Horizontal
grid.ScrollBars = System.Windows.Forms.ScrollBars.Vertical
grid.ScrollBars = System.Windows.Forms.ScrollBars.Both
```

Useful manual smoke checks: drag a rectangle both down/right and up/left;
fill and clear it; select across existing images; hold the pointer near a grid
edge while dragging; scroll both ways and confirm headers stay fixed; double-click
a cell to edit, then try Enter and Escape. Also try Windows display scaling and
adding the DLL to the VS 2026 WinForms toolbox. Automated checks do not replace
these interactive checks.

## Projects and verification

- **GridDotNet**: reusable control DLL, `GridDotNet.GridControl`.
- **GridDotNet.Demo**: a simple WinForms demo.
- **GridDotNet.Tests**: a dependency-free STA executable that checks selection,
  range fills, image rendering, fixed headers, scrolling, editing, row operations,
  default sizing, bounds, and properties in a WinForms design host/PropertyGrid.
  Run this executable directly; it is not a Visual Studio Test Explorer project.

```powershell
dotnet build GridDotNet.sln
& .\GridDotNet.Tests\bin\Debug\net481\GridDotNet.Tests.exe
```

Visual Studio needs the .NET Framework 4.8.1 developer/targeting pack and desktop
development tools. The runtime is Windows-specific. The solution uses Any CPU
and supports both 32-bit and 64-bit consumers without COM registration.

For a Release build:

```powershell
dotnet build GridDotNet.sln --configuration Release
& .\GridDotNet.Tests\bin\Release\net481\GridDotNet.Tests.exe
```

The reusable DLL is `GridDotNet/bin/Release/net481/GridDotNet.dll` (or the same
path under `Debug` for a Debug build). Build outputs and local Visual Studio
settings are excluded from source control; build the solution to generate them.

## Add the control to an existing WinForms project

1. Target .NET Framework 4.8.1 in the consuming project.
2. Add a project reference to GridDotNet, or copy the built DLL into a stable
   dependency folder and add it through **References → Add Reference → Browse**.
3. Open a form designer. Right-click the Toolbox, choose **Choose Items**, and
   browse to that DLL under **.NET Framework Components**. Select **GridControl**.
4. Drag the control onto the form and set its name, bounds, rows, and columns.

When updating a copied DLL, close the form designers, replace the copy that the
project reference uses, then rebuild. Keep the Toolbox entry and project reference
pointing at the intended build.

### Properties missing in the designer

In Categorized view, `DefaultRowHeight` and `DefaultColWidth` are under **Layout**.
In Alphabetical view, look under **D**; there is no Layout heading.

If a property is available in code but still absent from the designer, check the
referenced DLL, close and reopen the designer, and restart Visual Studio. During
Rosters integration, stale designer state persisted until the solution's local
`.vs` folder was removed with Visual Studio closed. Renaming that folder before
reopening the solution is a recoverable way to reset the local state. It is not
part of the repository.

## Using it from VB.NET

Set **DefaultRowHeight** and **DefaultColWidth** in the designer's **Layout**
category or from code. Initial defaults are 26 and 60 logical pixels:

```vb
grid.DefaultRowHeight = 30
grid.DefaultColWidth = 40
grid.set_ColWidth(0, 120)  ' Individual override for a name column
```

Changing a default updates existing rows/columns that have no individual override,
and applies to new rows/columns (including `AddItem`). Individual sizes stay intact,
even if explicitly set to the previous default. Use `ResetRowHeight(index)` or
`ResetColWidth(index)` to return an individual row/column to following the default.

Reference the GridDotNet project or its built DLL, then create a
`GridDotNet.GridControl`, add it to the form, and set its bounds or `Dock`.
After building, the public control can also be added to the WinForms toolbox
via **Choose Items** and the DLL.

```vb
Private WithEvents grid As New GridDotNet.GridControl()

Private Sub InitialiseGrid()
    grid.Dock = DockStyle.Fill
    grid.Rows = 50
    grid.Cols = 96
    Controls.Add(grid)
End Sub

Private Sub FillSelectedCells(shiftImage As Image)
    grid.FillStyle = 1
    grid.Picture = shiftImage
    grid.FillStyle = 0
End Sub

Private Sub SelectionFinished(sender As Object,
    e As GridDotNet.GridSelectionEventArgs) Handles grid.SelectionCompleted
    ' e.Range.Top / Bottom / Left / Right are normalized, inclusive bounds.
    ' Rosters can decide what the selected rectangle means here.
End Sub
```

Direct cell access also works independently of the current cell and `FillStyle`:

```vb
grid.Cells(4, 12).Image = shiftImage
grid.Cells(4, 12).Text = ""
grid.Cells(4, 12).ImageLayout = GridDotNet.CellImageLayout.Stretch
grid.SelectRange(4, 12, 4, 20)
grid.FillSelection(shiftImage)
```

Legacy row operations are also available:

```vb
grid.AddItem("")                 ' Append a blank row
grid.AddItem("Name" & vbTab & "Status", 0)  ' Insert before row 0
grid.RemoveItem(0)                ' Delete row 0 and shift later rows up
```

At least one row must remain. Setting `Rows` to a smaller count removes rows from
the end; use `RemoveItem` to remove a particular row. All indices are zero-based.
Use `grid.Picture = Nothing` to clear an image. Images belong to the caller and
must remain alive while the grid references them.

See [compatibility notes](docs/compatibility.md) before replacing the old form controls.
This is an initial implementation of the observed API, not verified binary or
behavioural compatibility with the unavailable ActiveX control. Existing COM
designer state, twips-based layout calculations, and application event handlers
still require migration in the consuming application.
