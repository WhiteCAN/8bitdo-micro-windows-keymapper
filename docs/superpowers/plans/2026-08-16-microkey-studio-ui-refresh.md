# MicroKey Studio UI Refresh Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the developer-only WPF shell with an official-app-inspired button mapping screen for MicroKey Studio.

**Architecture:** Add small display models to the WPF ViewModel and bind them from XAML. Keep BLE transport behavior unchanged and keep experimental hardware writes behind explicit user commands.

**Tech Stack:** C#/.NET 8, WPF XAML, xUnit for view-model display data tests.

## Global Constraints

- App name remains `MicroKey Studio`.
- Language remains C# only.
- Do not add raw captures, APK files, device addresses, phone identifiers, or private paths.
- Do not send BLE writes automatically on app launch.

---

### Task 1: Mapping Display Data

**Files:**
- Modify: `src/MicroKeyStudio.App/ViewModels/MainViewModel.cs`
- Create: `tests/MicroKeyStudio.App.Tests/MicroKeyStudio.App.Tests.csproj`
- Create: `tests/MicroKeyStudio.App.Tests/MainViewModelTests.cs`

**Interfaces:**
- Produces: `ButtonMappingDisplay`, `LeftMappings`, and `RightMappings`.

- [ ] Write a failing test asserting default left and right mappings exist.
- [ ] Run `dotnet test tests/MicroKeyStudio.App.Tests/MicroKeyStudio.App.Tests.csproj` and confirm it fails because the app test project or properties do not exist.
- [ ] Implement display-only mapping data in `MainViewModel`.
- [ ] Run app tests and full tests.

### Task 2: Official-App-Inspired WPF Layout

**Files:**
- Modify: `src/MicroKeyStudio.App/MainWindow.xaml`
- Modify: `src/MicroKeyStudio.App/ViewModels/MainViewModel.cs`

**Interfaces:**
- Consumes: `LeftMappings`, `RightMappings`, BLE commands, and status properties.

- [ ] Replace the basic app shell with a dark layout: title bar, left rail, central controller diagram, mapping rails, and bottom action bar.
- [ ] Preserve `ScanCommand`, `ConnectCommand`, `DisconnectCommand`, and `ReplayCapturedSequenceCommand`.
- [ ] Run `dotnet build src/MicroKeyStudio.App/MicroKeyStudio.App.csproj`.
- [ ] Smoke-run `dotnet run --project src/MicroKeyStudio.App/MicroKeyStudio.App.csproj --no-build`.

### Task 3: Commit

**Files:**
- Add/modify all UI refresh files.

- [ ] Run `dotnet test MicroKeyStudio.sln`.
- [ ] Run `dotnet build MicroKeyStudio.sln`.
- [ ] Commit with `feat: refresh MicroKey Studio mapping UI`.

## Self-Review

- Spec coverage: the plan covers display data, XAML layout, safety-preserving commands, and verification.
- Placeholder scan: no placeholders remain.
- Type consistency: mapping properties are named consistently as `LeftMappings` and `RightMappings`.
