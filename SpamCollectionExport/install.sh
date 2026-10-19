#!/usr/bin/env bash
# Installe BepInEx 5 (Mono x64) + le plugin SpamCollectionExport dans SPAM (Proton/Wine)
# Version: 0.6.0
# Utilisation: ./install.sh [chemin/vers/SPAM]
set -euo pipefail

GAME="${1:-$HOME/.local/share/Steam/steamapps/common/SPAM}"
HERE="$(cd "$(dirname "$0")" && pwd)"

# Couleurs pour les messages
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

log_header() {
    echo -e "${BLUE}========================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}========================================${NC}"
}

# Vérifier que SPAM existe
[ -f "$GAME/SPAM.exe" ] || { log_error "SPAM introuvable dans $GAME"; exit 1; }

log_header "Installation de SpamCollectionExport v0.6.0"
log_info "Dossier du jeu: $GAME"
log_info "Dossier du mod: $HERE"

# Installer BepInEx 5.4.23.5 si nécessaire
if [ ! -f "$GAME/winhttp.dll" ]; then
  log_info "Téléchargement de BepInEx 5.4.23.5..."
  tmp=$(mktemp -d)
  curl -sL -o "$tmp/bep.zip" https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip
  echo "✅ BepInEx téléchargé, installation..."
  (cd "$GAME" && bsdtar -xf "$tmp/bep.zip" 2>/dev/null || unzip -oq "$tmp/bep.zip")
  rm -rf "$tmp"
  log_info "✅ BepInEx installé"
else
  log_info "BepInEx déjà installé"
fi

# Créer les dossiers BepInEx
mkdir -p "$GAME/BepInEx/plugins" "$GAME/BepInEx/config"
log_info "Dossiers BepInEx créés"

# Copier la DLL (cherche dans plusieurs endroits)
DLL=""
# 1. D'abord, cherche un build récent dans src/bin/Release
if [ -f "$HERE/src/bin/Release/SpamCollectionExport.dll" ]; then
    DLL="$HERE/src/bin/Release/SpamCollectionExport.dll"
    log_info "DLL trouvée dans src/bin/Release/"
# 2. Sinon, cherche dans le dossier releases
elif [ -f "$HERE/releases/SpamCollectionExport.dll" ]; then
    DLL="$HERE/releases/SpamCollectionExport.dll"
    log_info "DLL trouvée dans releases/"
# 3. Sinon, cherche dans le dossier racine
elif [ -f "$HERE/SpamCollectionExport.dll" ]; then
    DLL="$HERE/SpamCollectionExport.dll"
    log_info "DLL trouvée dans le dossier racine"
fi

[ -f "$DLL" ] || { log_error "DLL introuvable. Lancez d'abord: ./build.sh"; exit 1; }

# Copier la DLL
cp "$DLL" "$GAME/BepInEx/plugins/"
log_info "✅ SpamCollectionExport.dll copié dans BepInEx/plugins/"

# Vérifier que la DLL est bien copiée
if [ ! -f "$GAME/BepInEx/plugins/SpamCollectionExport.dll" ]; then
    log_error "Échec de la copie de la DLL"
    exit 1
fi

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
log_info "✅ BepInEx configuré"

echo ""
log_header "Installation terminée!"
echo ""
log_info "Pour lancer SPAM avec le mod:"
log_info "  - Steam → SPAM → Propriétés → Options de lancement"
log_info "  - Ajoute: WINEDLLOVERRIDES=\"winhttp=n,b\" %command%"
echo ""
log_info "Raccourcis dans le jeu:"
log_info "  - F8: Ouvrir/fermer le menu du mod"
log_info "  - F9: Démarrer/arrêter la boucle d'entraînement"
echo ""
log_info "Dossier de sortie des exports: $GAME/SpamExport/"
echo ""
log_info "Pour builder le mod: ./build.sh"
