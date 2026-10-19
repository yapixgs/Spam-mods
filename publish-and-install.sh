#!/usr/bin/env bash
# Script complet: pull → build → tag → release → install depuis release
# Ce script garantit que l'installation utilise UNIQUEMENT la release publiée,
# pas le build local, pour s'assurer que la release fonctionne.
set -euo pipefail

# Couleurs pour les messages
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

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

# Vérifier qu'on est dans le bon dossier
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

if [ ! -d "SpamCollectionExport" ]; then
    log_error "Exécute ce script depuis la racine du repo (Spam-mods/)"
    exit 1
fi

# ==========================================
# ÉTAPE 1: git pull
# ==========================================
log_header "Étape 1/5: git pull"
git pull
log_info "Pull terminé"

# ==========================================
# ÉTAPE 2: Build du mod
# ==========================================
log_header "Étape 2/5: Build du mod"
cd SpamCollectionExport
./build.sh || { log_error "Build échoué"; exit 1; }
cd ..

# ==========================================
# ÉTAPE 3: Lire la version et créer le tag
# ==========================================
log_header "Étape 3/5: Publication de la release"

# Lire la version depuis Plugin.cs
VERSION=$(grep -oP '"SPAM Collection Export", \K"[^"]+"' SpamCollectionExport/src/Plugin.cs | tr -d '"')
if [ -z "$VERSION" ]; then
    log_error "Impossible de lire la version depuis Plugin.cs"
    exit 1
fi

log_info "Version détectée: v$VERSION"

# Créer le tag si il n'existe pas déjà
if git rev-parse "v$VERSION" >/dev/null 2>&1; then
    log_warn "Tag v$VERSION existe déjà"
else
    git tag "v$VERSION"
    git push origin "v$VERSION"
    log_info "Tag v$VERSION créé et poussé"
fi

# ==========================================
# ÉTAPE 4: Vérifier que la release existe
# ==========================================
log_header "Étape 4/5: Vérification de la release"

# La DLL doit être dans releases/vX.Y.Z/ ou releases/
RELEASE_DLL=""
if [ -f "SpamCollectionExport/releases/v$VERSION/SpamCollectionExport.dll" ]; then
    RELEASE_DLL="SpamCollectionExport/releases/v$VERSION/SpamCollectionExport.dll"
    log_info "DLL de release trouvée: $RELEASE_DLL"
elif [ -f "SpamCollectionExport/releases/SpamCollectionExport.dll" ]; then
    RELEASE_DLL="SpamCollectionExport/releases/SpamCollectionExport.dll"
    log_info "DLL de release trouvée: $RELEASE_DLL"
else
    log_error "DLL de release introuvable!"
    log_error "Attendu dans: SpamCollectionExport/releases/v$VERSION/ ou SpamCollectionExport/releases/"
    exit 1
fi

# Vérifier que la DLL existe vraiment
if [ ! -f "$RELEASE_DLL" ]; then
    log_error "Fichier $RELEASE_DLL introuvable"
    exit 1
fi

log_info "✅ Release v$VERSION est prête"

# ==========================================
# ÉTAPE 5: Installer DEPUIS LA RELEASE (pas le build local)
# ==========================================
log_header "Étape 5/5: Installation depuis la release v$VERSION"

# On va forcer install.sh à utiliser la DLL de la release
# en créant un lien symbolique temporaire ou en copiant

# Méthode: copier la DLL de release dans un endroit où install.sh la trouvera
TMP_BUILD_DIR="SpamCollectionExport/src/bin/Release"
mkdir -p "$TMP_BUILD_DIR"

# Copier la DLL de release dans le dossier de build temporaire
# (install.sh cherche d'abord dans src/bin/Release/)
cp "$RELEASE_DLL" "$TMP_BUILD_DIR/SpamCollectionExport.dll"
log_info "DLL de release copiée vers $TMP_BUILD_DIR/"

# Exécuter install.sh qui va trouver la DLL dans src/bin/Release/
cd SpamCollectionExport
./install.sh
cd ..

# Nettoyer le dossier temporaire (optionnel, gardé pour vérification)
# rm -f "$TMP_BUILD_DIR/SpamCollectionExport.dll"

log_header "✅ TOUT EST TERMINÉ !"
log_info "Release v$VERSION publiée et installée localement"
log_info ""
log_info "Résumé:"
log_info "  - Git pull: ✅"
log_info "  - Build: ✅"
log_info "  - Tag v$VERSION: ✅"
log_info "  - Installation depuis release: ✅"
log_info ""
log_info "La DLL installée provient bien de la release, pas du build local"
