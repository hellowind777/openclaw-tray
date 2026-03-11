# OpenClaw Tray

Windows native tray companion for portable OpenClaw deployments.

This repository is a distribution repository. It publishes executables, a sanitized configuration template, and documentation only. Source code, build scripts, local logs, and machine-specific state are not included.

## Included Files

```text
.
├─ LICENSE
├─ README.md
├─ config.json
├─ openclaw-service.exe
├─ openclaw-tray-cli.exe
└─ openclaw-tray.exe
```

## What It Does

- Shows a native Windows tray icon for OpenClaw.
- Supports separate startup controls for:
  - `开机启动（托盘）`
  - `开机启动（OpenClaw，管理员权限）`
- Lets users start, stop, and restart OpenClaw from the tray.
- Supports non-login startup for OpenClaw through a Windows service.
- Provides a CLI entry for automation, remote control, and external invocation.
- Keeps tray UI and CLI separate so the tray no longer shows a console window.
- Supports turning desktop balloon notifications on or off.

## Startup Architecture

- `openclaw-tray.exe`
  - GUI tray process only.
  - Runs without a console window.
- `openclaw-tray-cli.exe`
  - Command-line control entry.
  - Suitable for scripts, automation, or external callers.
- `openclaw-service.exe`
  - `OpenClawTrayStartupService` startup service host.
  - Used for delayed, pre-login startup scenarios.

Startup behavior:

- `开机启动（托盘）`
  - Registers a logon task for the tray UI.
  - Recommended when users only want the tray after login.
- `开机启动（OpenClaw，管理员权限）`
  - Installs the `OpenClawTrayStartupService` startup service.
  - Intended for boot-time delayed startup before login.
  - Requires administrator rights when enabling or disabling.
  - Manual tray start, stop, and restart actions still control OpenClaw itself, not the Windows service.

Default delays:

- OpenClaw startup service delay: `90` seconds
- Tray logon delay: `7` seconds

When OpenClaw startup is enabled from an elevated tray session and OpenClaw is not already running, the tray now:

1. Installs the startup service
2. Starts the service host
3. Starts OpenClaw immediately for the current session

This avoids the previous behavior where startup was enabled but OpenClaw did not start right away.

During tray-triggered startup, the tray icon stays in the yellow starting state and the menu entry changes to `OpenClaw 启动中...`, so users get clear progress feedback instead of a transient failure impression.

## Tray Status Semantics

- Green badge: OpenClaw is running and healthy
- Yellow badge: OpenClaw is starting
- Red badge: OpenClaw start failed or is abnormal
- Gray disabled icon with gray badge: OpenClaw is stopped

## Path Configuration

The published `config.json` is fully sanitized. Replace the placeholder values with your own local paths:

- `runtimeRoot = <runtime-root>`
- `openClawRoot = <openclaw-root>`

Both relative paths and absolute paths are supported.

## Configuration

`config.json`

```json
{
  "runtimeRoot": "<runtime-root>",
  "openClawRoot": "<openclaw-root>",
  "gatewayPort": 0,
  "serviceName": "OpenClawTrayStartupService",
  "trayTaskName": "OpenClaw Tray UI",
  "serviceStartupDelaySeconds": 90,
  "trayLogonDelaySeconds": 7,
  "controlPanelPath": "/openclaw/"
}
```

Fields:

- `runtimeRoot`: runtime directory containing `scripts`, `env`, and `state`
- `openClawRoot`: OpenClaw application directory
- `gatewayPort`: local gateway port, `0` means use the built-in default
- `serviceName`: Windows service name for OpenClaw startup
- `trayTaskName`: scheduled task name for tray logon startup
- `serviceStartupDelaySeconds`: delayed startup time for boot-time OpenClaw service execution
- `trayLogonDelaySeconds`: delayed startup time for tray display after login
- `controlPanelPath`: relative control panel path

Sanitization note:

- The repository version of `config.json` does not contain any real machine path.
- `gatewayPort = 0` means use the built-in default at runtime.

## Runtime Requirements

The runtime directory must contain compatible scripts such as:

```text
<runtimeRoot>\
├─ env\
│  └─ lobster-teams.local.ps1
├─ scripts\
│  ├─ start-lobster-teams-background.ps1
│  ├─ stop-lobster-teams.ps1
│  ├─ restart-lobster-teams.ps1
│  └─ status-lobster-teams.ps1
└─ state\
   └─ gateway-process.json
```

## CLI Examples

```powershell
.\openclaw-tray-cli.exe --status
.\openclaw-tray-cli.exe --status --json
.\openclaw-tray-cli.exe --start-openclaw
.\openclaw-tray-cli.exe --stop-openclaw
.\openclaw-tray-cli.exe --restart-openclaw
.\openclaw-tray-cli.exe --enable-tray-autostart --set-tray-delay 7
.\openclaw-tray-cli.exe --enable-openclaw-autostart --set-openclaw-delay 90
.\openclaw-tray-cli.exe --disable-desktop-notifications
.\openclaw-tray-cli.exe --enable-desktop-notifications
```

## Notes

- This repository intentionally excludes source code and scripts.
- `startup-settings.json` is local machine state and is not committed.
- Administrator rights are required only when installing or uninstalling the OpenClaw startup service.
- The service runs as `LocalService`, not `SYSTEM`, and does not require creating an extra local account.

## License

MIT. See [LICENSE](LICENSE).
