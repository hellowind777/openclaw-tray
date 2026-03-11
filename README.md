# OpenClaw Tray

> Native Windows tray companion for local OpenClaw runtimes.
>
> 只发布可执行文件、配置模板和说明文档，不公开源码。

[![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D4?logo=windows&logoColor=white)](#)
[![Tray](https://img.shields.io/badge/UX-Native%20Tray-1f883d)](#)
[![License](https://img.shields.io/badge/License-MIT-black)](LICENSE)

`OpenClaw Tray` 是一个面向 Windows 的本地托盘程序，用来托管本机 `OpenClaw` 运行时：

- 托盘里直接查看运行状态
- 右键启动 / 停止 / 重启 OpenClaw
- 一键打开控制面板、日志目录、状态窗口
- 支持 `开机启动（Tray + OpenClaw）`
- 自动识别本地 `OpenClaw` 和 runtime 路径
- 配置文件可手动覆盖路径和端口

---

## 仓库内容

这个仓库只保留分发所需文件：

```text
.
├─ .gitignore
├─ LICENSE
├─ openclaw-tray.exe
├─ config.json
└─ README.md
```

不包含源码，不包含构建脚本，也不包含任何私有运行数据或密钥。

---

## 快速使用

### 1. 下载文件

至少拿这两个文件：

- `openclaw-tray.exe`
- `config.json`

### 2. 填写你自己的路径

示例配置：

```json
{
  "runtimeRoot": "<your-runtime-root>",
  "openClawRoot": "<your-openclaw-root>",
  "gatewayPort": 0,
  "startupTaskName": "OpenClaw Tray",
  "controlPanelPath": "/openclaw/"
}
```

说明：

- 尖括号占位符需要替换成你自己的真实路径
- `gatewayPort` 写 `0` 表示使用程序内置默认端口
- 支持绝对路径，也支持相对路径

### 3. 启动

把下面两个文件放在同一个目录：

- `openclaw-tray.exe`
- `config.json`

然后双击 `openclaw-tray.exe` 即可。

---

## 开机启动说明

托盘菜单里的：

- `开机启动（Tray + OpenClaw）`

就是总开关。

启用后会：

- 登录 Windows 自动启动托盘
- 托盘启动后自动拉起 OpenClaw 服务

---

## 运行时目录要求

这个托盘默认对接兼容的本地 runtime 目录。至少需要下面这些文件：

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

- 托盘负责桌面入口和状态承载
- 真实 runtime 负责启动逻辑和配置注入

---

## 配置项

配置文件名：`config.json`

| 字段 | 类型 | 说明 |
|---|---|---|
| `runtimeRoot` | `string` | 本地运行时目录 |
| `openClawRoot` | `string` | OpenClaw 程序目录 |
| `gatewayPort` | `number` | 本地网关端口，填 `0` 表示使用程序内置默认值 |
| `startupTaskName` | `string` | Windows 计划任务名称 |
| `controlPanelPath` | `string` | 控制面板路径，默认 `/openclaw/` |

---

## 目录结构

发布包根目录直接放置可执行文件和配置模板：

```text
openclaw-tray/
├─ openclaw-tray.exe
├─ config.json
├─ README.md
└─ LICENSE
```

如果你自己本地运行时需要真实配置，请复制：

- `config.json`

仓库中的 `config.json` 已做脱敏；如果你在本地填入真实路径或密钥，不要把修改后的 `config.json` 提交回仓库。

---

## FAQ

### 这个仓库公开源码吗？
不公开。这里只保留可运行文件和配置模板。

### 它会帮我安装 OpenClaw 吗？
不会。它只是本地托盘层，不是安装器。

### 它能脱离 runtime 单独工作吗？
不能。它需要兼容的本地 runtime 目录。

### 可以改计划任务名称吗？
可以，改 `startupTaskName` 即可。

---

## License

MIT. See [`LICENSE`](LICENSE).

---

## Star

如果这个项目刚好解决了你“本地 OpenClaw 状态不透明、日志难找、没有托盘入口”的问题，欢迎点个 Star。
