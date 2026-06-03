# Changelog

All notable changes to this project will be documented in this file.

## [2.3.3] - 2026-06-03

### Fixed
- **Photoshop COM Object Binding**: Excluded `.1` suffixed ProgIDs (like `Photoshop.Application.200.1` and `Photoshop.Application.130.1`) from the COM search loop. These ProgIDs map to Photoshop's legacy non-scripting interface (CLSID `{6DECC242-87EF-11cf-86B4-444553540000}`) which does not support the `DoJavaScript` scripting method, resulting in `DISP_E_UNKNOWNNAME` (0x80020006) exceptions. The application now successfully binds to the active scripting-capable COM interface (like `Photoshop.Application.130` for CC 2019).
- **Sticky Menu Behavior**: Removed custom `MouseEnter` event overrides on the top menu strip items that opened dropdown menus on hover but got them stuck open. The menus now follow standard Windows UX (open on click, close on focus change/dismiss).
- **Auto-Update System Reliability**: Relocated the background update check from `Program.cs` (triggered prior to application initialization) to the `OnLoad` handler of `MainForm.cs`. This ensures the Velopack update dialog is correctly parented and instantiated on the main STA (Single-Threaded Apartment) UI thread rather than crash-failing silently on an MTA thread pool thread.

### Added
- **Secure Licensing Proxy & Script Protection**: Implemented a secure backend API proxy model. Direct client-side queries to Firebase have been removed. The client now calls a Vercel-compatible Node.js backend.
- **Cryptographic Signature Verification**: Validated authentication response payloads on the client using 2048-bit RSA public/private key signing (RS256) and timestamp verification to prevent replay/spoofing attacks.
- **Dynamic Script Delivery**: Relocated the core Photoshop JSX execution script from the local assembly to the secure backend server. The client receives the encrypted script payload only upon successful server-side license verification, preventing cracking attempts via client-side patch bypasses.
- **Product Key Creator**: Added a local administration script (`npm run create-keys`) to generate batches of 10 keys and upload them directly to Firebase.

## [2.3.2] - 2026-06-03

### Fixed
- **Photoshop COM Connection Reliability**: Implemented a multi-version fallback lookup loop (from CC 2026/v270 down to CS3/v100) to resolve version-specific ProgIDs in the Running Object Table (ROT). This prevents the `CO_E_SERVER_EXEC_FAILURE` error that occurred when Photoshop was already running but not registered under the default `Photoshop.Application` name.
- **Auto-Hide Taskbar Stability**: Replaced the taskbar-flashing Win32 `SW_HIDE` / `SW_SHOWNA` calls with off-screen repositioning (`-32000, -32000`). The window now stays visually hidden during menu and modal interactions while its taskbar icon remains stable and present, eliminating the distracting disappearing/reappearing behavior.
- **Window Restore Bounds**: Added state logic to ensure that if the application is closed while automatically hidden, it correctly saves and restores the original on-screen window bounds rather than saving the off-screen coordinates.
