#!/usr/bin/env bash
# Installe BepInEx 5 (Mono x64) + le plugin SpamCollectionExport dans SPAM (Proton/Wine)
# Version: 0.6.0
set -euo pipefail

GAME="${1:-$HOME/.local/share/Steam/steamapps/common/SPAM}"
HERE="$(cd "$(dirname "$0")" && pwd)"

# Vérifier que SPAM existe
[ -f "$GAME/SPAM.exe" ] || { echo "❌ SPAM introuvable dans $GAME"; exit 1; }

echo "🎮 SPAM trouvé dans: $GAME"

# Installer BepInEx 5.4.23.5 si nécessaire
if [ ! -f "$GAME/winhttp.dll" ]; then
  echo "⬇️  Téléchargement de BepInEx 5.4.23.5..."
  tmp=$(mktemp -d)
  curl -sL -o "$tmp/bep.zip" https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip
  echo "✅ BepInEx téléchargé, installation..."
  (cd "$GAME" && bsdtar -xf "$tmp/bep.zip" 2>/dev/null || unzip -oq "$tmp/bep.zip")
  rm -rf "$tmp"
  echo "✅ BepInEx installé"
fi

# Créer les dossiers BepInEx
mkdir -p "$GAME/BepInEx/plugins" "$GAME/BepInEx/config"

# Copier la DLL
DLL="$HERE/src/bin/Release/SpamCollectionExport.dll"
[ -f "$DLL" ] || { echo "❌ DLL introuvable: lance d'abord 'dotnet build -c Release' dans src/"; exit 1; }
cp "$DLL" "$GAME/BepInEx/plugins/"
echo "✅ SpamCollectionExport.dll copié dans BepInEx/plugins/"

# Configurer BepInEx pour cacher le GameObject (nécessaire pour SPAM)
CFG="$GAME/BepInEx/config/BepInEx.cfg"
if [ -f "$CFG" ]; then
  # Remplacer ou ajouter la ligne
  if grep -q "HideManagerGameObject" "$CFG"; then
    sed -i 's/^HideManagerGameObject = .*/HideManagerGameObject = true/' "$CFG"
  else
    echo "HideManagerGameObject = true" >> "$CFG"
  fi
else
  printf '[Chainloader]\nHideManagerGameObject = true\n' > "$CFG"
fi
echo "✅ BepInEx configuré"

echo ""
echo "✨ Installation terminée!"
echo ""
echo "Pour lancer SPAM avec le mod:"
echo "  - Steam → SPAM → Propriétés → Options de lancement"
echo "  - Ajoute: WINEDLLOVERRIDES=\"winhttp=n,b\" %command%"
echo ""
echo "Raccourcis dans le jeu:"
echo "  - F8: Ouvrir/fermer le menu du mod"
echo "  - F9: Démarrer/arrêter la boucle d'entraînement"
echo ""
echo "Dossier de sortie des exports: $GAME/SpamExport/"
