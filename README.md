# OpenClaw Native Tray

> A native Windows tray companion for local OpenClaw runtimes.
>
> 让你的本地 OpenClaw / 多 Agent 运行时常驻托盘、状态可见、入口集中、通知不再顶着 `PowerShell 7`。

[![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D4?logo=windows&logoColor=white)](#)
[![Language](https://img.shields.io/badge/C%23-.NET%20Framework-512BD4?logo=dotnet&logoColor=white)](#)
[![Tray](https://img.shields.io/badge/UX-Native%20Tray-1f883d)](#)
[![License](https://img.shields.io/badge/License-MIT-black)](LICENSE)

如果你已经在 Windows 本机运行 OpenClaw，或者维护一套本地多 Bot / 多 Agent 运行时，这个项目就是给你的：

- 托盘里直接看服务是不是活着
- 右键就能启动、停止、重启、看日志、开控制面板
- 用 JSON 配置覆盖路径和端口
- 自动识别 OpenClaw 安装目录和本地运行时目录
- 尽量让桌面通知显示“应用本身”，而不是 `PowerShell 7`

它不是远程运维面板，也不是 OpenClaw 安装器。  
它是一个专注于 **Windows 本地托管体验** 的原生托盘壳。

---

## Why this is useful

本地跑 Bot 最大的问题，往往不是“跑不起来”，而是：

- 跑起来之后，不知道它是不是还在跑
- 配置和日志分散在多个目录里
- 每次都要切回终端看状态
- 做了开机自启，但没有一个稳定的桌面入口

`OpenClaw Native Tray` 的目标很克制：
**把“状态、控制、入口、排障”这 4 件事，收进一个轻量托盘程序里。**

---

## Features

### Native tray first
- 原生 WinForms 托盘程序，不是简单的脚本弹壳
- 独立 EXE 常驻，避免通知来源被识别成 `PowerShell 7`
- 双击托盘图标可直接打开本地控制面板

### Runtime controls
- 托盘菜单内支持启动 / 停止 / 重启运行时
- 托盘菜单内可直接启用 / 关闭“开机登录自动启动 Tray + OpenClaw”
- 可直接打开状态控制台、日志目录、本地控制面板
- 托盘状态图标会根据运行态切换不同颜色和标记

### Auto-discovery + override
- 自动识别 `runtimeRoot`
- 自动识别 `openClawRoot`
- 识别不到时，可用 `openclawtp.runtime.json` 显式覆盖
- 支持自定义本地网关端口、开机自启任务名称、控制面板路径

### Friendly for local multi-agent setups
- 适合本地长期常驻的多 Agent / 多 Bot 运行时
- 适合你把 OpenClaw 当成“本机后台基础设施”来用
- 适合希望给非命令行用户一个更直观入口的维护者

---

## What this repo contains

这个仓库只发布 **托盘程序本身**，不包含你的私有运行时数据，也不包含任何 API Key、飞书配置、个人路径快照或用户状态数据。

当前仓库内容：

```text
.
├─ src/
│  └─ openclawtp.cs
├─ config/
│  └─ openclawtp.runtime.example.json
├─ scripts/
│  └─ build.ps1
├─ .gitignore
├─ LICENSE
└─ README.md
```

---

## Runtime contract

这个项目默认面向一类“本地运行时目录”工作。运行时目录至少需要具备下面这些脚本约定：

```text
<runtimeRoot>/
├─ env/
│  └─ lobster-teams.local.ps1
├─ scripts/
│  ├─ start-lobster-teams-background.ps1
│  ├─ stop-lobster-teams.ps1
│  ├─ restart-lobster-teams.ps1
│  └─ status-lobster-teams.ps1
└─ state/
   └─ gateway-process.json
```

也就是说：
- 这个托盘程序是一个 **runtime companion**
- 它负责桌面入口和状态承载
- 真正的业务运行时、配置注入、网关启动逻辑，仍由你的本地 runtime 提供

如果你正在使用自己的 OpenClaw 本地运行骨架，只要脚本契约兼容，就可以直接接入。

---

## Quick Start

### 1. Clone

```bash
git clone https://github.com/hellowind777/openclaw-native-tray.git
cd openclaw-native-tray
```

### 2. Prepare config

把示例配置复制为实际配置：

```powershell
Copy-Item .\config\openclawtp.runtime.example.json .\config\openclawtp.runtime.json
```

推荐把路径改成你自己的环境，不要直接照抄任何人的本地目录。

示例：

```json
{
  "runtimeRoot": "D:/Programs/***/lobster-runtime",
  "openClawRoot": "D:/Programs/***/openclaw",
  "gatewayPort": 18789,
  "startupTaskName": "OpenClaw Native Tray",
  "controlPanelPath": "/openclaw/"
}
```

说明：
- 这里用 `***` 明确屏蔽了个人环境信息
- 你也可以直接写成相对路径
- 如果目录结构标准，很多场景下甚至不需要手工填写

### 3. Build

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

构建产物默认输出到：

```text
./dist/openclawtp.exe
```

### 4. Run

把 `dist/openclawtp.exe` 与 `dist/openclawtp.runtime.example.json` 放在一起；首次使用时，把示例配置改名为 `openclawtp.runtime.json`，再双击 `openclawtp.exe`。

---

## Config reference

配置文件名：`openclawtp.runtime.json`

### Supported fields

| Field | Type | Description |
|---|---|---|
| `runtimeRoot` | `string` | 本地运行时目录 |
| `openClawRoot` | `string` | OpenClaw 程序目录 |
| `gatewayPort` | `number` | 本地网关端口，默认 `18789` |
| `startupTaskName` | `string` | Windows 计划任务名称 |
| `controlPanelPath` | `string` | 控制面板路径，默认 `/openclaw/` |

### Auto-detect order

程序会按下面的顺序尝试定位路径：

1. `openclawtp.runtime.json`
2. 环境变量（如 `OPENCLAW_APP_ROOT`）
3. runtime 内的环境脚本（如 `lobster-teams.local.ps1`）
4. 托盘目录附近的常见相对位置
5. 常见默认安装位置

---

## Build notes

### Requirements
- Windows 10 / 11
- .NET Framework C# compiler (`csc.exe`)
- PowerShell 7（运行配套脚本时需要）
- 一个兼容的本地 OpenClaw runtime

### Optional icon build

如果你有自己的 `.ico` 文件，可以这样编译：

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1 -IconPath .\assets\your-icon.ico
```

如果不传 `-IconPath`，脚本会直接编译，不强依赖图标资源。

---

## Distribution checklist

在把它分发给别的用户之前，建议确认这几件事：

- [x] 仓库中不包含任何 API Key
- [x] 仓库中不包含你本地运行时真实状态文件
- [x] 仓库中不包含你的飞书配置、私有群信息、审批策略或用户数据
- [x] 示例路径已脱敏，使用 `***` 或占位路径表示
- [x] 托盘程序可通过 JSON 覆盖路径与端口
- [x] 自动识别失败时，仍可通过显式配置恢复
- [ ] 你自己的 runtime 是否遵循文档里的脚本契约

最后一项是唯一真正取决于用户环境的地方。  
也就是说：**这个仓库本身已经适合公开分发，但它不是“脱离 runtime 就能单独工作”的独立产品。**

---

## FAQ

### Does it install OpenClaw for me?
No. It only provides the native Windows tray layer.

### Does it work without a local runtime?
No. It needs a compatible local runtime root with the expected scripts and state files.

### Why JSON instead of TOML?
因为这个托盘程序本来就已经有 JSON 配置入口，而且 C# / PowerShell 读取都更轻，分发和手工修改也更直观。

### Can I rename the scheduled task?
可以，改 `startupTaskName` 即可。

### Can I move the runtime and OpenClaw folders?
可以，只要改 `openclawtp.runtime.json`，或者保持相对目录关系不变。

---

## Roadmap

- [ ] 增加“打开配置文件”菜单
- [ ] 增加“导出诊断信息”菜单
- [ ] 增加更细粒度的运行状态提示
- [ ] 增加首次启动向导
- [ ] 增加便携版 / Release 发布说明
- [ ] 增加截图和 GIF 演示

---

## Contributing

欢迎提交：
- Bug report
- Runtime compatibility feedback
- Better tray UX ideas
- Build script improvements
- Documentation improvements

如果你想把这个项目一起打磨成更顺手的 Windows 本地托管工具，欢迎提 Issue / PR。

---

## License

MIT. See [`LICENSE`](LICENSE).

---

## Give it a Star

如果这个项目刚好解决了你“本地 OpenClaw 跑着跑着就看不见、日志不好找、状态不透明”的问题，欢迎点个 Star。

这会帮助这个项目继续往前做：
- 更好的托盘 UX
- 更清晰的配置体验
- 更稳定的本地多 Agent 宿主体验

**Star 一下，让更多做本地 AI 工具的人也能少开几个终端窗口。**