#!/usr/bin/env bash
set -e

# Directories
ICON_DIR="${HOME}/.local/share/icons/hicolor/512x512/apps"
MIME_DIR="${HOME}/.local/share/mime/packages"
APP_DIR="${HOME}/.local/share/applications"

mkdir -p "${ICON_DIR}" "${MIME_DIR}" "${APP_DIR}"

# Copy icon
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "${SCRIPT_DIR}")"
cp "${REPO_ROOT}/assets/icon.png" "${ICON_DIR}/zafiro-avalonia-showme.png"

# Copy MIME definition
cat << 'EOF' > "${MIME_DIR}/zafiro-avalonia-showme.xml"
<?xml version="1.0" encoding="UTF-8"?>
<mime-info xmlns="http://www.freedesktop.org/standards/shared-mime-info">
  <mime-type type="application/x-axaml">
    <comment>Avalonia XAML Document</comment>
    <comment xml:lang="es">Documento XAML de Avalonia</comment>
    <glob pattern="*.axaml"/>
    <glob pattern="*.AXAML"/>
    <sub-class-of type="application/xml"/>
    <generic-icon name="zafiro-avalonia-showme"/>
  </mime-type>
  <mime-type type="application/x-xaml">
    <comment>XAML Document</comment>
    <comment xml:lang="es">Documento XAML</comment>
    <glob pattern="*.xaml"/>
    <glob pattern="*.XAML"/>
    <sub-class-of type="application/xml"/>
    <generic-icon name="zafiro-avalonia-showme"/>
  </mime-type>
</mime-info>
EOF

# Find executable path
TOOL_EXEC="$(which zafiro-avalonia-showme 2>/dev/null || echo "${HOME}/.dotnet/tools/zafiro-avalonia-showme")"

# Desktop entry
cat << EOF > "${APP_DIR}/zafiro-avalonia-showme.desktop"
[Desktop Entry]
Version=1.0
Type=Application
Name=Zafiro ShowMe
GenericName=Avalonia XAML Previewer
GenericName[es]=Previsualizador XAML de Avalonia
Comment=Preview arbitrary Avalonia XAML using the host app context and official previewer
Comment[es]=Previsualiza XAML/AXAML de Avalonia en su contexto de ejecución
Exec=${TOOL_EXEC} %f
Icon=zafiro-avalonia-showme
Terminal=false
Categories=Development;GUIDesigner;Utility;
MimeType=application/x-axaml;application/x-xaml;
StartupNotify=true
StartupWMClass=zafiro-avalonia-showme
Keywords=avalonia;xaml;axaml;previewer;designer;
EOF

chmod +x "${APP_DIR}/zafiro-avalonia-showme.desktop"

# Update databases
update-mime-database "${HOME}/.local/share/mime"
update-desktop-database "${APP_DIR}"
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -f -t "${HOME}/.local/share/icons/hicolor" 2>/dev/null || true
fi

# Set default associations
xdg-mime default zafiro-avalonia-showme.desktop application/x-axaml
xdg-mime default zafiro-avalonia-showme.desktop application/x-xaml

echo "✅ Zafiro ShowMe desktop integration configured successfully!"
