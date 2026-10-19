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
    log_warn "dotnet SDK non trouvé."
    log_info "Essai avec mono/csc..."
    
    # Vérifier mono
    if ! command -v mono &> /dev/null; then
        log_error "Ni dotnet ni mono trouvés."
        log_error "Installez dotnet depuis: https://dotnet.microsoft.com/download"
        log_error "Ou mono depuis: https://www.mono-project.com/download/stable/"
        exit 1
    fi
    
    log_info "mono trouvé: $(mono --version | head -1)"
    
    # Vérifier csc (compilateur C# de mono)
    if ! command -v csc &> /dev/null; then
        csc=$(find /usr -name csc 2>/dev/null | head -1)
        if [ -z "$csc" ]; then
            log_error "csc (compilateur C#) non trouvé."
            log_error "Installez mono-complete: sudo apt install mono-complete"
            exit 1
        fi
        CSC="$csc"
    else
        CSC="csc"
    fi
    
    USE_MONO=true
else
    log_info "dotnet version: $(dotnet --version)"
    USE_MONO=false
fi

# Lire la version du plugin depuis le code source
VERSION=$(grep -oP '"SPAM Collection Export", \K"[^"]+"' "$SRC_DIR/Plugin.cs" | tr -d '"')
log_info "Version détectée: $VERSION"

# Créer le dossier de release
mkdir -p "$RELEASE_DIR"

# Nettoyer les anciens builds
log_info "Nettoyage des anciens builds..."
rm -rf "$OUT_DIR"/*

# Créer le dossier de sortie
mkdir -p "$OUT_DIR"

# Build le projet
log_header "Build du mod SpamCollectionExport v$VERSION"
log_info "Dossier source: $SRC_DIR"
log_info "Dossier de sortie: $OUT_DIR"

if [ "$USE_MONO" = true ]; then
    log_info "Build avec mono/csc..."
    
    # Trouver les références nécessaires
    GAME_MANAGED="$SCRIPT_DIR/../../Assembly-CSharp.dll"
    
    # Si Assembly-CSharp.dll n'est pas dans le repo, on cherche ailleurs
    if [ ! -f "$GAME_MANAGED" ]; then
        # Chercher dans les chemins courants
        if [ -f "$HOME/.local/share/Steam/steamapps/common/SPAM/SPAM_Data/Managed/Assembly-CSharp.dll" ]; then
            GAME_MANAGED="$HOME/.local/share/Steam/steamapps/common/SPAM/SPAM_Data/Managed/Assembly-CSharp.dll"
        elif [ -f "/usr/share/SPAM/SPAM_Data/Managed/Assembly-CSharp.dll" ]; then
            GAME_MANAGED="/usr/share/SPAM/SPAM_Data/Managed/Assembly-CSharp.dll"
        else
            log_error "Assembly-CSharp.dll introuvable."
            log_error "Placez Assembly-CSharp.dll dans le dossier du repo ou installez SPAM."
            exit 1
        fi
    fi
    
    log_info "Utilisation de: $GAME_MANAGED"
    
    # Compiler avec csc
    cd "$SRC_DIR"
    
    $CSC -target:library \
        -out:"$OUT_DIR/SpamCollectionExport.dll" \
        -define:UNITY_2019_4,BEPINEX \
        -langversion:latest \
        -reference:"$GAME_MANAGED" \
        -reference:/usr/lib/mono/4.5/BepInEx.dll \
        -reference:/usr/lib/mono/4.5/Newtonsoft.Json.dll \
        -reference:/usr/lib/mono/4.5/UnityEngine.dll \
        -reference:/usr/lib/mono/4.5/UnityEngine.CoreModule.dll \
        -reference:/usr/lib/mono/4.5/UnityEngine.IMGUIModule.dll \
        -reference:/usr/lib/mono/4.5/UnityEngine.InputLegacyModule.dll \
        -reference:/usr/lib/mono/4.5/UnityEngine.TextRenderingModule.dll \
        Plugin.cs PluginMenu.cs TrainingLoop.cs
    
    if [ $? -eq 0 ]; then
        log_info "Build mono réussi!"
    else
        log_error "Le build mono a échoué"
        exit 1
    fi
else
    log_info "Build avec dotnet..."
    
    cd "$SRC_DIR"
    
    # Build avec dotnet
    if dotnet build -c Release; then
        log_info "Build réussi!"
    else
        log_error "Le build a échoué"
        exit 1
    fi
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
zip -r "$RELEASE_ZIP" SpamCollectionExport.dll 2>/dev/null || {
    log_warn "zip non trouvé, création d'un tarball..."
    tar -czf "$RELEASE_ZIP" SpamCollectionExport.dll
}
cd "$SCRIPT_DIR"

log_info "Archive créée: $RELEASE_ZIP"

# Copier aussi dans le dossier releases
cp "$DLL_PATH" "$RELEASE_DIR/SpamCollectionExport.dll"

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
