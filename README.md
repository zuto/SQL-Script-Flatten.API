# SQL-Script-Flatten.API
[![dotnet](https://img.shields.io/static/v1?label=csharp&message=.NET8.0&color=green&style=plastic&logo=csharp)]()
## Description

A .NET API for safely testing SQL scripts against real databases. Scripts are executed within automatic transaction wrappers (`BEGIN TRANSACTION...ROLLBACK TRANSACTION`), allowing you to test against live data without making permanent changes.

## Quick Start

### 1. Run the API

Open the project in JetBrains Rider and run it (or press F5).

The API will typically start on `http://localhost:5000` or `https://localhost:5001`.

### 2. Open the Frontend

Open `index.html` in your web browser:
- Double-click the file, or
- Right-click → Open with → Your browser, or
- Drag and drop into a browser window

### 3. Test Your Scripts

1. Paste your SQL script into the textarea
2. Click "Analyze Script" (or press Ctrl+Enter)
3. View the before/after comparison results
4. Changes are highlighted:
   - **Yellow** - Changed values
   - **Green** - New rows
   - **Red** - Removed rows

## How It Works

When you submit a SQL script through the frontend:

1. The API wraps your script in a transaction
2. Captures the "before" state of affected tables
3. Executes your script
4. Captures the "after" state
5. Rolls back the transaction (nothing is saved)
6. Returns a detailed comparison showing what would have changed

All changes are automatically rolled back - your database remains untouched.

## API Endpoint

**POST** `/script`
- **Content-Type**: `text/plain`
- **Body**: Raw SQL script
- **Response**: JSON with before/after table comparisons
- **Behavior**: Automatic transaction rollback (no permanent changes)

## TODO
- Set up docker
- Set up Terraform