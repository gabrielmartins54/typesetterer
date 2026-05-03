# Typesetterer 3.0

Typesetterer 3.0 is a small Windows-only script pasting assistant for Photoshop.

## Behavior

- Load script lines from the clipboard or from a `.txt`, `.md`, or `.docx` file.
- Loaded text is formatted automatically: prefixes before the first space are removed, `//` splits into separate lines, and `.docx` lines above colored text are skipped.
- Select a line to copy it to the clipboard.
- Use `S1`/`S2` as project save slots: right-click to save the current formatted script and line, left-click to restore that project later.
- Press `Ctrl+V` in Photoshop to paste the current clipboard line; Typesetterer then advances and copies the next line.
- Blank lines are hidden and skipped.

## Build

Install the .NET 8 SDK, then run:

```powershell
dotnet build
```

To publish a standalone Windows executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

The app does not use a custom paste hotkey; it follows normal `Ctrl+V` pastes while Photoshop is focused.
