# MicroKey Studio UI Refresh Design

## Goal

Refresh the WPF app shell so the first screen resembles a dedicated 8BitDo Micro button-mapping tool rather than a developer utility.

## Reference

The supplied reference UI uses a dark desktop-app frame, a narrow left navigation rail, a central controller diagram, and symmetrical button mapping controls on both sides. MicroKey Studio should borrow that structure and visual rhythm while keeping its own project name, safety language, and experimental BLE controls.

## Layout

- Use a dark app surface with a top title bar reading `MicroKey Studio`.
- Add a left navigation rail with `Profiles`, `Buttons`, and `Settings`; `Buttons` is selected.
- Put a stylized Micro controller diagram in the middle of the page.
- Put eight mapping rows on the left and eight mapping rows on the right, each with a button label and mapped action chip.
- Keep device actions visible but secondary: scan, connect, disconnect, and developer replay sit in a compact top-right toolbar.
- Add a bottom action bar with `Disable Sleep`, `Reset`, and `Sync to device`.

## Data

Add a display-only mapping row model to the app ViewModel. It is not yet the final editable mapping engine; it exists so the UI has stable button/action data and can later connect to saved profiles.

## Safety

The experimental captured-packet replay remains behind an explicit button. The UI copy must make it clear that sync is experimental.

## Verification

- Unit test that the default button mapping display contains left and right rows.
- Build the WPF app.
- Smoke-run the app for a few seconds to confirm it launches.
