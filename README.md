![Platforms](https://img.shields.io/badge/platform-Windows|MacOS-lightgray.svg)
![.NET](https://img.shields.io/badge/.NET%20-8-blue.svg)
[![License](http://img.shields.io/:license-MIT-blue.svg)](http://opensource.org/licenses/MIT)
[![oAuth2](https://img.shields.io/badge/oAuth2-v1-green.svg)](http://developer.autodesk.com/)
[![Data-Management](https://img.shields.io/badge/Data%20Management-v2-green.svg)](http://developer.autodesk.com/)
[![AEC-Data-Model](https://img.shields.io/badge/AEC%20Data%20Model-v1-green.svg)](http://developer.autodesk.com/)

# APS AEC Data Model MCP Server for .NET

A comprehensive Model Context Protocol (MCP) server implementation that enables AI assistants like **Autodesk Assistant** or **Claude** to interact with Autodesk Platform Services (APS), specifically the AEC Data Model API. This server provides natural language access to building information models, clash detection, spatial analysis, and 3D visualization capabilities.

## Table of Contents

- [Introduction](#introduction)
- [Features](#features)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Configuration](#configuration)
- [Available Tools](#available-tools)
- [Usage Examples](#usage-examples)
- [Architecture](#architecture)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)
- [License](#license)

## Introduction

This project started as an experiment with the [Model Context Protocol](https://modelcontextprotocol.io/introduction) during an [Autodesk Platform Accelerator](https://aps.autodesk.com/accelerator-program). It demonstrates how AI assistants can interact with complex BIM (Building Information Modeling) data using natural language.


## Features

- 🔐 **Authentication** - OAuth token management for APS API access
- 🏗️ **BIM Navigation** - Browse hubs, projects, and element groups
- 🔍 **Element Querying** - Filter and retrieve building elements by category
- 📤 **IFC Export** - Export filtered elements to Industry Foundation Classes (IFC) format
- ⚠️ **Clash Detection** - Accurate geometric clash detection using bounding box analysis
- 📦 **Spatial Containment** - Find elements spatially contained within other elements
- 📁 **File Upload** - Upload files to Autodesk Docs (ACC/BIM 360)
- 👁️ **3D Visualization** - Render and highlight elements in Autodesk Viewer
- 🔌 **Dual Transport** - STDIO mode for Claude Desktop and HTTP mode for Autodesk Assistant web integration

## Prerequisites

### Software Requirements

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or higher
- [Claude Desktop](https://claude.ai/download) (for local AI assistant integration)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [Visual Studio Code](https://code.visualstudio.com/) (recommended)

### Autodesk Platform Services

- An APS account (free at [aps.autodesk.com](https://aps.autodesk.com))
- An APS application with:
  - **App Type**: Single Page Application (for PKCE authentication)
  - **Callback URL**: `http://localhost:8080/api/auth/callback`
  - **APIs Enabled**: Data Management API, AEC Data Model API, Model Derivative API
- Access to ACC (Autodesk Construction Cloud) projects

### Provisioning

⚠️ **Important**: You must [provision your APS app in your ACC hub](https://get-started.aps.autodesk.com/#provision-access-in-other-products) to access project data.

## Installation

### 1. Clone the Repository

```bash
git clone https://github.com/JoaoMartins-callmeJohn/aps-aecdm-mcp-dotnet.git
cd aps-aecdm-mcp-dotnet
```

### 2. Build the Project

#### Using Visual Studio

1. Open `mcp-server-aecdm/mcp-server-aecdm.sln`
2. Right-click the solution → **Build Solution** (or press `Ctrl+Shift+B`)

#### Using .NET CLI

```bash
cd mcp-server-aecdm
dotnet build
```

## Configuration

### 1. Set Environment Variables

You need to configure your APS credentials as environment variables:

**Windows (PowerShell)**:
```powershell
$env:CLIENT_ID="your_aps_client_id"
$env:CLIENT_SECRET="your_aps_client_secret"
$env:CALLBACK_URL="http://localhost:8080/api/auth/callback"
```

**Windows (Command Prompt)**:
```cmd
set CLIENT_ID=your_aps_client_id
set CLIENT_SECRET=your_aps_client_secret
set CALLBACK_URL=http://localhost:8080/api/auth/callback
```

**macOS/Linux (Bash)**:
```bash
export CLIENT_ID="your_aps_client_id"
export CLIENT_SECRET="your_aps_client_secret"
export CALLBACK_URL="http://localhost:8080/api/auth/callback"
```

Alternatively, you can set these in `Properties/launchSettings.json`:

```json
{
  "profiles": {
    "mcp-server-aecdm": {
      "commandName": "Project",
      "environmentVariables": {
        "CLIENT_ID": "your_aps_client_id",
        "CLIENT_SECRET": "your_aps_client_secret",
        "CALLBACK_URL": "http://localhost:8080/api/auth/callback"
      }
    }
  }
}
```

### 2. Configure Claude Desktop

To connect this MCP server with Claude Desktop, add the following to your `claude_desktop_config.json` file:

**Windows**: `%APPDATA%\Claude\claude_desktop_config.json`

**macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`

```json
{
  "mcpServers": {
    "aecdm": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\path\\to\\your\\aps-aecdm-mcp-dotnet\\mcp-server-aecdm\\mcp-server-aecdm.csproj",
        "--no-build"
      ]
    }
  }
}
```

⚠️ **Important**:
- Use the full absolute path to the `.csproj` file
- On Windows, use double backslashes (`\\`) in the path
- Build the project first with `dotnet build` before using `--no-build`

### 3. Restart Claude Desktop

After configuring, restart Claude Desktop completely. The MCP server should now be available.

## Available Tools

The server provides 13 MCP tools organized into 4 categories:

### 🔐 Authentication Tools

| Tool | Description |
|------|-------------|
| `GetToken` | Obtain OAuth token for APS API authentication |

### 🏗️ AEC Data Model Tools

| Tool | Description |
|------|-------------|
| `GetHubs` | Retrieve all ACC hubs from the user's account |
| `GetProjects` | Retrieve projects from a specific hub |
| `GetElementGroupsByProject` | Retrieve designs/models/element groups from a project |
| `GetElementsByElementGroupWithCategoryFilter` | Retrieve elements filtered by category (Walls, Windows, Floors, Doors, Furniture, Roofs, Ceilings, Electrical Equipment, Structural Framing, Structural Columns, Structural Rebar) |
| `ExportIfcForElementGroup` | Export IFC file for elements filtered by multiple categories |
| `ExportIfcForElements` | Export IFC file for specific elements by their IDs |
| `ClashDetectForElements` | Perform accurate clash detection analysis between building elements using bounding box overlap |
| `FindElementsContainedWithin` | Find elements within specified categories spatially contained inside a container element |
| `FindSpecificElementsContainedWithin` | Find specific elements by their IDs spatially contained inside a container element |

### 📁 Data Management Tools

| Tool | Description |
|------|-------------|
| `UploadFileToDocs` | Upload files to Autodesk Docs (ACC/BIM 360) |

### 👁️ Viewer Tools

| Tool | Description |
|------|-------------|
| `RenderModel` | Render a design with Autodesk Viewer in a web browser |
| `HighLightElements` | Highlight elements in the Viewer based on their External IDs |

## Usage Examples

### Getting Started

Once configured, you can interact with the server through Claude Desktop using natural language:

```
You: "Get the token first"
Claude: [Calls GetToken tool and authenticates]

You: "Show me all my ACC hubs"
Claude: [Calls GetHubs and displays available hubs]

You: "List the projects in hub [hub_id]"
Claude: [Calls GetProjects with the hub ID]

You: "Show me the element groups in project [project_id]"
Claude: [Calls GetElementGroupsByProject with the project ID]
```

### Querying Elements

```
You: "Find all walls in element group [element_group_id]"
Claude: [Calls GetElementsByElementGroupWithCategoryFilter with category="Walls"]

You: "Show me all doors and windows in that element group"
Claude: [Makes multiple calls to retrieve doors and windows]
```

### Clash Detection

```
You: "Check for clashes between these elements: [element_id_1, element_id_2, element_id_3]"
Claude: [Calls ClashDetectForElements and reports any geometric intersections]
```

### Spatial Containment

```
You: "Find all furniture contained in room [room_element_id]"
Claude: [Calls FindElementsContainedWithin with container and categories]

You: "Check if elements [id1, id2, id3] are inside space [space_id]"
Claude: [Calls FindSpecificElementsContainedWithin and reports containment status]
```

### IFC Export

```
You: "Export all structural columns and beams to an IFC file"
Claude: [Calls ExportIfcForElementGroup with categories and returns file path]
```

### 3D Visualization

```
You: "Render element group [element_group_id] in the viewer"
Claude: [Calls RenderModel and opens browser with 3D model]

You: "Highlight elements with external IDs [ext_id_1, ext_id_2]"
Claude: [Calls HighLightElements to isolate and focus on specific elements]
```

## Architecture

### Project Structure

```
aps-aecdm-mcp-dotnet/
├── mcp-server-aecdm/
│   ├── AuthTools.cs          # OAuth authentication
│   ├── AECDMTools.cs         # AEC Data Model API tools
│   ├── DMTools.cs            # Data Management API tools
│   ├── ViewerTool.cs         # Autodesk Viewer integration
│   ├── Program.cs            # MCP server entry point
│   ├── Global.cs             # Shared state and configuration
│   └── mcp-server-aecdm.csproj
├── README.md
└── LICENSE
```

### Key Technologies

- **[ModelContextProtocol .NET SDK](https://www.nuget.org/packages/ModelContextProtocol/0.1.0-preview.2)** - MCP server implementation
- **[Autodesk.Data](https://www.nuget.org/packages/Autodesk.Data/0.1.5-beta)** - AEC Data Model API client
- **[Autodesk.DataManagement](https://www.nuget.org/packages/Autodesk.DataManagement/2.0.1)** - Data Management API client
- **[GraphQL.Client](https://www.nuget.org/packages/GraphQL.Client/6.1.0)** - GraphQL queries for AEC DM
- **[Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json/13.0.3)** - JSON serialization

### Transport Modes

The server supports two transport modes:

1. **STDIO Mode** (Default) - For Claude Desktop integration via standard input/output
2. **HTTP Mode** - For web-based integration on `http://127.0.0.1:4000/mcp/`

To run in HTTP mode:
```bash
dotnet run --http
```

## Troubleshooting

### Common Issues

#### 1. "Can't find my hub"

**Solution**: Ensure your APS app is [provisioned in your ACC hub](https://get-started.aps.autodesk.com/#provision-access-in-other-products). This must be done by an ACC administrator.

#### 2. Code changes not reflected in Claude

**Problem**: Claude caches the MCP server process.

**Solution**:
1. Completely quit Claude Desktop (check Task Manager/Activity Monitor)
2. Rebuild your project: `dotnet build`
3. Restart Claude Desktop

#### 3. "Token expired" or authentication errors

**Solution**: Call the `GetToken` tool again through Claude to refresh authentication.

#### 4. Port conflicts (8081/8082)

**Problem**: Viewer tools fail if ports are already in use.

**Solution**: Close applications using these ports or modify port numbers in `ViewerTool.cs`.

#### 5. Element IDs vs File Version URNs

**Problem**: Some tools require element group IDs, not file version URNs.

**Solution**: Use the ID from `GetElementGroupsByProject`, not the `fileVersionUrn` field.

### Debug Mode

To see detailed logs, run the server directly:

```bash
cd mcp-server-aecdm
dotnet run
```

Watch the console output for error messages and API responses.

## Video Tutorial

📺 **[Watch the Demo Video](https://youtu.be/GlCYJQfUFWU)** - See the MCP server in action with Claude Desktop.

## Contributing

Contributions are welcome! This is an experimental project and we're looking to expand its capabilities.

### How to Contribute

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Ideas for Contributions

- Additional AEC Data Model API endpoints
- Enhanced spatial analysis algorithms
- Improved error handling and validation
- Unit tests and integration tests
- Support for additional file formats
- Performance optimizations
- Documentation improvements

## License

This sample is licensed under the terms of the [MIT License](LICENSE). Please see the [LICENSE](LICENSE) file for full details.

## Author

**João Martins** - [LinkedIn](https://linkedin.com/in/jpornelas)

## Acknowledgments

- [Mirco Bianchini](https://www.linkedin.com/in/mirco-bianchini-352b2128/) for the initial challenge
- Autodesk Platform Services team for the APIs and SDKs
- Anthropic for the Model Context Protocol specification
- The open-source community for the dependencies used in this project

## Resources

- [Model Context Protocol Documentation](https://modelcontextprotocol.io/)
- [Autodesk Platform Services](https://aps.autodesk.com/)
- [AEC Data Model API Documentation](https://aps.autodesk.com/en/docs/aecdatamodel/v1/)
- [Claude Desktop](https://claude.ai/download)
- [Getting Started with APS](https://get-started.aps.autodesk.com/)