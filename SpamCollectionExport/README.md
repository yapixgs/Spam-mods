# SPAM Collection Export + Training Loop

Un mod BepInEx pour **SPAM** (Steam) qui permet :

- **📤 Export de la collection** : Exporte automatiquement tes cartes, decks et données de collection dans des fichiers texte/JSON
- **🔄 Boucle d'entraînement** : Lance automatiquement des parties en boucle avec abandon après un timer

## ✨ Fonctionnalités

### Export de collection
- `collection.txt` : Liste de toutes tes cartes possédées, organisées par élément
- `all_cards.txt` : Toute la base de données du jeu, avec indication de possession (✅/❌)
- `decks.txt` : Tous tes decks sauvegardés
- `collection.json` : Données brutes au format JSON pour outils externes
- Export automatique quand ta collection change
- Export manuel via le menu en jeu (F8)

### Boucle d'entraînement / Farming
- **4 modes disponibles** : Training (vs IA), Battleground, Casual, Ranked
- **Timer d'abandon** : Quitte automatiquement la partie après un délai configurable
- **Timer = 0** : Quitte immédiatement dès que la partie est prête (parfait pour farmer)
- **Nombre de parties illimité** ou limité
- **Sélection du deck** à utiliser pour la boucle
- **Restauration du deck** original à l'arrêt
- **Gestion des échecs** : Arrêt automatique après N échecs de lancement consécutifs

## ⚠️ Attention

- **Casual/Ranked** : L'abandon automatique dans ces modes est **désactivé par défaut** car cela peut entraîner des **bans**
- Active **AllowOnlineModes** dans les paramètres UNIQUEMENT si tu comprends les risques
- Training et Battleground (modes locaux) fonctionnent toujours sans restriction

## 🎮 Utilisation

### Installation
1. Installe [BepInEx 5.x](https://github.com/BepInEx/BepInEx/releases) pour SPAM (Mono x64)
2. Place `SpamCollectionExport.dll` dans `BepInEx/plugins/`
3. Lance le jeu

### Raccourcis par défaut
- **F8** : Ouvre/ferme le menu du mod
- **F9** : Démarre/arrête la boucle d'entraînement

### Menu en jeu
Appuie sur **F8** pour ouvrir le menu avec 3 onglets :

- **Export** : Gère les exports, dossier de sortie, export manuel
- **Boucle d'entraînement** : Configure et lance la boucle
- **Interface** : Modifie les raccourcis et l'apparence

## 📝 Configuration

Tous les paramètres sont sauvegardés dans `BepInEx/config/com.yapix.spam.collectionexport.cfg`

### Paramètres principaux de la boucle
- `mode` : Type de partie (Training, Battleground, Casual, Ranked)
- `resignAfterSeconds` : Temps avant abandon (0 = quitter immédiatement)
- `delayBetweenGames` : Pause entre deux parties au menu
- `maxGames` : Nombre max de parties (0 = illimité)
- `allowOnlineModes` : Autorise Casual/Ranked (⚠️ risque de ban)

## 🛠️ Build

```bash
cd SpamCollectionExport/src
dotnet build -c Release
```

La DLL sera générée dans `src/bin/Release/SpamCollectionExport.dll`

## 📦 Structure du projet

```
SpamCollectionExport/
├── install.sh          # Script d'installation
├── README.md           # Ce fichier
└── src/
    ├── SpamCollectionExport.csproj
    ├── Plugin.cs        # Logique principale + export
    ├── PluginMenu.cs    # Interface IMGUI
    └── TrainingLoop.cs  # Logique de la boucle
```

## 📊 Changelog

### v0.6.0
- ✅ Ajout des modes **Casual** et **Ranked** pour la boucle
- ✅ Option `AllowOnlineModes` pour autoriser l'abandon en modes en ligne (⚠️ désactivé par défaut)
- ✅ Support de `ResignAfter = 0` : quitte immédiatement dès que la partie est prête
- ✅ Meilleure gestion des erreurs et messages de status
- 📝 Documentation mise à jour

### v0.5.0
- Version initiale avec export de collection et boucle Training/Battleground

## 💬 Support

Ouvre une issue sur GitHub si tu rencontres un problème.

---

**Auteur** : yapixgs
**Licence** : MIT (ou équivalent)
