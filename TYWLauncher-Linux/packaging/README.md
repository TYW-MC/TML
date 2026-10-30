# 打包说明

一次生成 5 种常用格式 × 2 种架构，产物统一放在 `dist/`。

## 用法

```bash
# 1. 先发布（在 src/TYW.Desktop 下）
dotnet publish -c Release -r linux-x64   --self-contained true -p:DebugType=none -o out/linux-x64
dotnet publish -c Release -r linux-arm64 --self-contained true -p:DebugType=none -o out/linux-arm64

# 2. 打包（自动探测 out/ 下已有的架构）
./packaging/build.sh 1.0.0

# 或者只打指定架构
./packaging/build.sh 1.0.0 x64
./packaging/build.sh 1.0.0 x64 arm64
```

## 产物

| 文件 | 适用系统 | 安装方式 |
|---|---|---|
| `tyw-launcher_1.0.0_amd64.deb` / `_arm64.deb` | Debian、Ubuntu、Deepin、UOS、麒麟 | `sudo apt install ./tyw-launcher_1.0.0_amd64.deb` |
| `tyw-launcher-1.0.0-1.x86_64.rpm` / `.aarch64.rpm` | Fedora、RHEL、CentOS、openSUSE | `sudo dnf install ./tyw-launcher-1.0.0-1.x86_64.rpm` |
| `tyw-launcher-1.0.0-1-x86_64.pkg.tar.zst` / `-aarch64` | Arch、Manjaro、EndeavourOS | `sudo pacman -U ./tyw-launcher-1.0.0-1-x86_64.pkg.tar.zst` |
| `TYW_Launcher-1.0.0-x86_64.AppImage` / `-aarch64` | 通用，免安装 | `chmod +x` 后直接运行 |
| `tyw-launcher-1.0.0-linux-x64.tar.gz` / `-arm64` | 通用，免安装 | 解压后运行目录里的 `tyw-launcher` |
| `SHA256SUMS` | 校验 | `sha256sum -c SHA256SUMS` |

deb / rpm / pacman 三种安装后会得到：

- `/opt/tyw-launcher/` 程序本体
- `/usr/bin/tyw-launcher` 命令（终端里也能启动）
- `/usr/share/applications/tyw-launcher.desktop` 桌面菜单项
- `/usr/share/icons/hicolor/{32,48,64,128,256}/apps/tyw-launcher.png` 图标

## 依赖

打包脚本按需使用，缺哪个就跳过对应格式（不会中断）：

| 工具 | 用途 | 安装 |
|---|---|---|
| `dpkg-deb` | deb | `apt install dpkg` |
| `rpmbuild` | rpm | `apt install rpm` / `dnf install rpm-build` |
| `zstd` | pkg.tar.zst | `apt install zstd` |
| `appimagetool` | AppImage | 脚本自动下载到 `.tools/`，也可自行放进 PATH |

AppImage 需要 type2 runtime，脚本同样会自动下载到 `.tools/runtime-<arch>`；
国内直连 GitHub release 会超时，脚本内置 `ghproxy.net` 等加速镜像回退。

> 交叉打包 rpm（在 x86_64 上产出 aarch64）时，脚本会临时复制一份 rpmrc 并追加
> `buildarch_compat`，这是 `rpmbuild` 的硬性限制。

## 环境变量

- `TYW_APPIMAGE_COMP=zstd`：改用 zstd 压缩 AppImage（更快，体积略大）。默认 xz。
