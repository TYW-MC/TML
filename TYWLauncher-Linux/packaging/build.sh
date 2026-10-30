#!/usr/bin/env bash
# TYW Launcher Linux 打包脚本：一次生成 tar.gz / deb / AppImage 三种产物。
# 用法：./build.sh [版本号]     例如 ./build.sh 1.0.0
# 依赖：dpkg-deb（deb）、appimagetool（AppImage，缺失时只跳过 AppImage）
set -euo pipefail

VER="${1:-1.0.0}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APP="tyw-launcher"
OUT="$ROOT/dist"
PUB="$ROOT/out/linux-x64"
ICONS="$ROOT/packaging/icons"

rm -rf "$OUT"
mkdir -p "$OUT"

if [ ! -x "$PUB/TYWLauncher" ]; then
  echo "先发布：dotnet publish -c Release -r linux-x64 --self-contained true -o \"$PUB\""
  exit 1
fi

echo "==> tar.gz"
STAGE="$OUT/tar/$APP-$VER"
mkdir -p "$STAGE"
cp -r "$PUB/." "$STAGE/"
cp "$ICONS/tyw-256.png" "$STAGE/tyw-launcher.png"
install -Dm755 /dev/stdin "$STAGE/tyw-launcher" <<'SH'
#!/bin/sh
exec "$(dirname "$(readlink -f "$0")")/TYWLauncher" "$@"
SH
tar -C "$OUT/tar" -czf "$OUT/$APP-$VER-linux-x64.tar.gz" "$APP-$VER"

echo "==> deb"
DEB="$OUT/deb"
mkdir -p "$DEB/DEBIAN" "$DEB/opt/$APP" "$DEB/usr/bin" "$DEB/usr/share/applications" \
         "$DEB/usr/share/icons/hicolor/256x256/apps"
cp -r "$PUB/." "$DEB/opt/$APP/"
cp "$ICONS/tyw-256.png" "$DEB/usr/share/icons/hicolor/256x256/apps/$APP.png"
ln -sf "/opt/$APP/TYWLauncher" "$DEB/usr/bin/tyw-launcher"
cat > "$DEB/usr/share/applications/$APP.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=TYW Launcher
Name[zh_CN]=TYW 启动器
Comment=Minecraft launcher
Comment[zh_CN]=TYW Minecraft 启动器
Exec=/opt/$APP/TYWLauncher %U
Icon=$APP
Terminal=false
Categories=Game;
StartupWMClass=TYWLauncher
EOF
INSTALLED=0
if command -v du >/dev/null; then INSTALLED=$(du -sk "$DEB/opt" | cut -f1); fi
cat > "$DEB/DEBIAN/control" <<EOF
Package: $APP
Version: $VER
Section: games
Priority: optional
Architecture: amd64
Installed-Size: $INSTALLED
Depends: libicu76 | libicu74 | libicu72 | libicu70, libssl3t64 | libssl3, libx11-6, libice6, libsm6, libfontconfig1, libgl1
Maintainer: TYW <support@tywmc.cn>
Description: TYW Launcher for Linux
 TYW Minecraft 启动器的 Linux 版本，界面与 Windows 版一致。
EOF
if command -v dpkg-deb >/dev/null; then
  dpkg-deb --build --root-owner-group "$DEB" "$OUT/${APP}_${VER}_amd64.deb" >/dev/null
  echo "    $OUT/${APP}_${VER}_amd64.deb"
else
  echo "    跳过 deb：没有 dpkg-deb"
fi

echo "==> AppImage"
if command -v appimagetool >/dev/null; then
  APPDIR="$OUT/AppDir"
  mkdir -p "$APPDIR/usr/bin"
  cp -r "$PUB/." "$APPDIR/usr/bin/"
  cp "$ICONS/tyw-256.png" "$APPDIR/$APP.png"
  cat > "$APPDIR/$APP.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=TYW Launcher
Exec=TYWLauncher
Icon=$APP
Terminal=false
Categories=Game;
EOF
  cat > "$APPDIR/AppRun" <<'SH'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/usr/bin/TYWLauncher" "$@"
SH
  chmod +x "$APPDIR/AppRun"
  ARCH=x86_64 appimagetool "$APPDIR" "$OUT/TYW_Launcher-$VER-x86_64.AppImage" >/dev/null
  echo "    $OUT/TYW_Launcher-$VER-x86_64.AppImage"
else
  echo "    跳过 AppImage：没有 appimagetool（apt install appimagetool 或从 GitHub 下载）"
fi

echo
echo "产物目录：$OUT"
ls -lh "$OUT"
