# Spam-mods

Collection de mods pour le jeu **SPAM** (Steam) utilisant **BepInEx**.

## 📦 Mods disponibles

### [SpamCollectionExport](SpamCollectionExport/README.md) - v0.6.0
Mod multi-fonctions pour SPAM :
- 📤 **Export de collection** : Exporte automatiquement cartes, decks et données de collection
- 🔄 **Boucle d'entraînement/Farming** : Lance des parties en boucle avec abandon automatique
- ⏱️ **Timer configurable** : Quitte après un délai (même 0 = quitte immédiatement)
- 🎯 **4 modes supportés** : Training, Battleground, Casual, Ranked

## 🛠️ Installation générale

1. **Installe BepInEx 5.x** (Mono x64) dans le dossier de SPAM
2. **Place les DLL** des mods dans `BepInEx/plugins/`
3. **Configure** les options via les fichiers `.cfg` dans `BepInEx/config/`
4. **Lance SPAM** via Steam avec l'option de lancement :
   ```
   WINEDLLOVERRIDES="winhttp=n,b" %command%
   ```

## 📂 Structure du dépôt

```
Spam-mods/
├── README.md                    # Ce fichier
└── SpamCollectionExport/        # Mod principal
    ├── README.md                # Documentation complète
    ├── install.sh               # Script d'installation
    └── src/                     # Code source
        ├── Plugin.cs           # Logique principale
        ├── PluginMenu.cs       # Interface utilisateur
        ├── TrainingLoop.cs     # Boucle d'entraînement
        └── SpamCollectionExport.csproj
```

## 💡 Contribuer

1. Fork le dépôt
2. Crée une branche pour ta fonctionnalité (`git checkout -b feature/ma-fonctionnalite`)
3. Commit tes changements (`git commit -m 'Ajout de ma fonctionnalité'`)
4. Push vers la branche (`git push origin feature/ma-fonctionnalite`)
5. Ouvre une Pull Request

## 📜 Licence

Tous les mods sont sous licence **MIT** (ou équivalent).

---

*Créé par yapixgs*
