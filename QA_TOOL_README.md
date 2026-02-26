# SQL Script QA Tool - Temporary Frontend

This is a temporary QA testing tool for safely testing SQL scripts. Scripts are executed within a `BEGIN TRANSACTION...ROLLBACK TRANSACTION` wrapper, so changes are tested against real data but automatically rolled back - nothing is permanently changed.

## What Was Changed

### API Changes
1. **ScriptController.cs**: Removed the `execute` parameter - scripts are now ALWAYS executed within a safe transaction wrapper
2. **Startup.cs**: Added CORS support to allow the frontend to communicate with the API

### New Files
- `index.html` - Single-file frontend (everything inline, no dependencies)
- `QA_TOOL_README.md` - This file

## Setup & Usage

### 1. Start the API

```bash
dotnet run --project src/API/API.csproj
```

The API will typically run on `http://localhost:5000` or `https://localhost:5001`

### 2. Update Frontend API URL (if needed)

Open `index.html` and find this line near the top of the `<script>` section:

```javascript
const API_URL = 'http://localhost:5000/script';
```

Update the port if your API runs on a different port.

### 3. Open the Frontend

Simply open `index.html` in your browser:
- Double-click the file, or
- Right-click → Open with → Your browser, or
- Drag and drop into a browser window

### 4. Use the Tool

1. Paste your SQL script into the textarea
2. Click "Analyze Script" (or press Ctrl+Enter)
3. View the results showing before/after table comparisons
4. Cell-level differences are highlighted:
   - **Yellow**: Changed values
   - **Green**: New rows added
   - **Red**: Rows removed
   - **White**: Unchanged

## Features

- ✅ Safe execution (scripts run within `BEGIN TRANSACTION...ROLLBACK TRANSACTION`)
- ✅ Real database testing with automatic rollback
- ✅ Cell-level difference highlighting
- ✅ Side-by-side before/after comparison
- ✅ Single file for easy removal
- ✅ No dependencies or installations needed
- ✅ Works offline once loaded

## API Endpoint

The tool uses the `/script` endpoint:
- **Method**: POST
- **Content-Type**: text/plain
- **Body**: Raw SQL script
- **Response**: JSON with before/after table comparisons
- **Behavior**: Script is executed within a transaction that is automatically rolled back (no permanent changes)

## Removing the Tool

When you're done with this temporary tool, simply delete:
1. `index.html`
2. `QA_TOOL_README.md`

If you want to revert the API changes:
1. Remove CORS configuration from `Startup.cs`
2. Optionally restore the `execute` parameter in `ScriptController.cs` (if needed for other purposes)

## Troubleshooting

**Error: Failed to fetch**
- Make sure the API is running
- Check the API URL in index.html matches your API's port
- Verify CORS is enabled in Startup.cs

**No results showing**
- Check browser console (F12) for errors
- Verify the API response format matches expected structure
- Ensure your SQL script references tables that exist in the database

**Highlighting not working**
- The tool uses an ID column or the first column as a key for comparison
- Ensure your tables have a consistent identifier column
