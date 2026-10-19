#!/usr/bin/env bash
# Installe BepInEx 5 (Mono x64) + le plugin SpamCollectionExport dans SPAM (Proton).
set -euo pipefail
GAME="${1:-$HOME/.local/share/Steam/steamapps/common/SPAM}"
HERE="$(cd "$(dirname "$0")" && pwd)"
[ -f "$GAME/SPAM.exe" ] || { echo "SPAM introuvable dans $GAME"; exit 1; }
if [ ! -f "$GAME/winhttp.dll" ]; then
  echo "→ Téléchargement de BepInEx 5.4.23.5"
  tmp=$(mktemp -d)
  curl -sL -o "$tmp/bep.zip" https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip
  (cd "$GAME" && bsdtar -xf "$tmp/bep.zip" 2>/dev/null || unzip -oq "$tmp/bep.zip")
fi
mkdir -p "$GAME/BepInEx/plugins" "$GAME/BepInEx/config"
DLL="$HERE/SpamCollectionExport.dll"
[ -f "$HERE/src/bin/Release/SpamCollectionExport.dll" ] && DLL="$HERE/src/bin/Release/SpamCollectionExport.dll"
[ -f "$DLL" ] || { echo "DLL introuvable : lance d'abord  dotnet build -c Release  dans src/"; exit 1; }
cp "$DLL" "$GAME/BepInEx/plugins/"
# Le jeu change de scène : on cache le manager BepInEx pour qu'il ne soit pas détruit.
CFG="$GAME/BepInEx/config/BepInEx.cfg"
if [ -f "$CFG" ]; then sed -i 's/^HideManagerGameObject = false/HideManagerGameObject = true/' "$CFG"
else printf '[Chainloader]\nHideManagerGameObject = true\n' > "$CFG"; fi
echo "✅ Installé dans $GAME"
echo "Steam → SPAM → Propriétés → Options de lancement :"
echo '    WINEDLLOVERRIDES="winhttp=n,b" %command%'
