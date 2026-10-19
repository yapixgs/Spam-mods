#!/usr/bin/env bash
# Script de build pour SpamCollectionExport
# Build le mod et crée une release basée sur le tag git
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC_DIR="$SCRIPT_DIR/src"
OUT_DIR="$SRC_DIR/bin/Release"
RELEASE_DIR="$SCRIPT_DIR/releases"

# Couleurs pour les messages
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Fonction pour afficher les messages
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

# Vérifier que dotnet est installé
if ! command -v dotnet &> /dev/null; then
    log_error "dotnet SDK non trouvé. Installez-le depuis https://dotnet.microsoft.com/download"
    exit 1
fi

log_info "dotnet version: $(dotnet --version)"

# Lire la version du plugin depuis le code source
VERSION=$(grep -oP '"SPAM Collection Export", \K"[^"]+"' "$SRC_DIR/Plugin.cs" | tr -d '"')
log_info "Version détectée: $VERSION"

# Créer le dossier de release
mkdir -p "$RELEASE_DIR"

# Nettoyer les anciens builds
log_info "Nettoyage des anciens builds..."
rm -rf "$OUT_DIR"/*

# Build le projet
log_header "Build du mod SpamCollectionExport v$VERSION"
log_info "Démarrage du build en configuration Release..."

cd "$SRC_DIR"

# Build avec dotnet
if dotnet build -c Release; then
    log_info "Build réussi!"
else
    log_error "Le build a échoué"
    exit 1
fi

# Vérifier que la DLL a été générée
DLL_PATH="$OUT_DIR/SpamCollectionExport.dll"
if [ ! -f "$DLL_PATH" ]; then
    log_error "La DLL n'a pas été générée dans $DLL_PATH"
    exit 1
fi

log_info "DLL trouvée: $DLL_PATH"

# Créer une archive de release
RELEASE_NAME="SpamCollectionExport-v${VERSION}"
RELEASE_ZIP="$RELEASE_DIR/${RELEASE_NAME}.zip"

log_info "Création de l'archive de release..."
cd "$OUT_DIR"
zip -r "$RELEASE_ZIP" SpamCollectionExport.dll
cd "$SCRIPT_DIR"

log_info "Archive créée: $RELEASE_ZIP"

# Vérifier si on a un tag git pour créer une release
CURRENT_TAG=$(git -C "$SCRIPT_DIR" describe --tags --exact-match 2>/dev/null || true)

if [ -n "$CURRENT_TAG" ]; then
    log_info "Tag git détecté: $CURRENT_TAG"
    
    # Créer un dossier pour cette version
    VERSION_DIR="$RELEASE_DIR/$CURRENT_TAG"
    mkdir -p "$VERSION_DIR"
    cp "$DLL_PATH" "$VERSION_DIR/"
    
    log_info "Release $CURRENT_TAG publiée dans $VERSION_DIR/"
    log_info "Fichiers de release:"
    ls -la "$VERSION_DIR/"
else
    log_warn "Aucun tag git détecté. Utilisez 'git tag v$VERSION' puis 'git push --tags' pour créer une release officielle."
    log_info "Le build est disponible dans $OUT_DIR/"
fi

log_header "Build terminé avec succès!"
log_info "DLL: $DLL_PATH"
log_info "Archive: $RELEASE_ZIP"

if [ -n "$CURRENT_TAG" ]; then
    log_info "Release: $VERSION_DIR/"
fi

log_info ""
log_info "Pour installer, exécutez: ./install.sh"
log_info "Ou copiez manuellement $DLL_PATH dans SPAM/BepInEx/plugins/"

exit 0
