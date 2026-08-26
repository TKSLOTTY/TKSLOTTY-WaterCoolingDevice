#!/usr/bin/env bash
set -euo pipefail
APP_DIR="$HOME/.local/share/WaterCoolingDevice"
DESKTOP_DIR="$HOME/.local/share/applications"
mkdir -p "$APP_DIR" "$DESKTOP_DIR"
cp "$(dirname "$0")/watercoolingdevice.py" "$APP_DIR/"
chmod +x "$APP_DIR/watercoolingdevice.py"
cat > "$DESKTOP_DIR/watercoolingdevice.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=WaterCoolingDevice
Comment=RP2040 Water Cooling Controller
Exec=python3 $APP_DIR/watercoolingdevice.py
Icon=utilities-system-monitor
Terminal=false
Categories=Utility;HardwareSettings;
EOF
chmod +x "$DESKTOP_DIR/watercoolingdevice.desktop"
echo "Installed. Launch 'WaterCoolingDevice' from the app menu."
echo "For tray temperature, install: sudo apt install gir1.2-ayatanaappindicator3-0.1"
