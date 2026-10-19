// SpamCollectionExport — exporte la collection SPAM en texte lisible + boucle d'entraînement.
// Export : lecture seule, lit l'état déjà chargé par le jeu (UserData.PossessedCards,
// CardData, localisation). Aucune requête réseau, aucune modification.
// Boucle de parties locales (désactivée par défaut) : voir TrainingLoop.cs. Elle ne concerne que
// l'entraînement contre l'IA et le Battleground, jamais le matchmaking.
// Menu en jeu (F8 par défaut) : voir PluginMenu.cs.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using Newtonsoft.Json;
using Spam.Data;
using Spam.MenuV3;
using TcgEngine;
using UnityEngine;
using Loc = Assets.SimpleLocalization.Scripts.LocalizationManager;

namespace SpamCollectionExport
{
    [BepInPlugin("com.yapix.spam.collectionexport", "SPAM Collection Export", "0.5.0")]
    public partial class Plugin : BaseUnityPlugin
    {
        // --- config : interface
        private ConfigEntry<KeyCode> _menuHotkey;
        private ConfigEntry<float> _menuScale;
        private ConfigEntry<bool> _loopOverlay;
        // --- config : export
        private ConfigEntry<string> _outDir;
        private ConfigEntry<bool> _autoExport;
        private ConfigEntry<float> _autoInterval;
        private ConfigEntry<bool> _expCollection, _expAllCards, _expDecks, _expJson;
        // --- config : boucle
        private ConfigEntry<KeyCode> _loopHotkey;
        private ConfigEntry<LoopMode> _loopMode;
        private ConfigEntry<string> _loopDeckId;
        private ConfigEntry<bool> _loopRestoreDeck;
        private ConfigEntry<float> _loopEndDelay;
        private ConfigEntry<float> _loopDelay, _loopResignAfter, _loopLaunchTimeout, _loopReadyTimeout,
                                   _loopLeaveTimeout, _loopRetry, _loopGoMenuCooldown;
        private ConfigEntry<int> _loopMaxGames, _loopMaxFailures;

        private TrainingLoop _loop;
        private string _lastSignature;
        private float _nextCheck;
        private string _exportStatus = "aucun export pour l'instant";

        private void Awake()
        {
            _menuHotkey = Config.Bind("menu", "hotkey", KeyCode.F8, "Touche pour ouvrir/fermer le menu en jeu.");
            _menuScale = Config.Bind("menu", "scale", 1f, "Taille de l'interface du menu (0.5 à 3).");
            _loopOverlay = Config.Bind("menu", "showLoopOverlay", true, "Affiche le compteur de la boucle en haut à gauche pendant qu'elle tourne.");

            _outDir = Config.Bind("export", "outputDir", "", "Dossier de sortie (vide = <jeu>/SpamExport).");
            _autoExport = Config.Bind("export", "autoExport", true, "Exporte automatiquement quand la collection ou les decks changent.");
            _autoInterval = Config.Bind("export", "autoExportInterval", 3f, "Secondes entre deux vérifications de l'export automatique.");
            _expCollection = Config.Bind("export", "writeCollection", true, "Écrire collection.txt (cartes possédées).");
            _expAllCards = Config.Bind("export", "writeAllCards", true, "Écrire all_cards.txt (toute la base, possédée ou non).");
            _expDecks = Config.Bind("export", "writeDecks", true, "Écrire decks.txt (tous les decks).");
            _expJson = Config.Bind("export", "writeJson", true, "Écrire collection.json (données brutes).");

            _loopHotkey = Config.Bind("training-loop", "hotkey", KeyCode.F9, "Touche pour démarrer/arrêter la boucle lancer-abandonner un entraînement (contre l'IA).");
            _loopMode = Config.Bind("training-loop", "mode", LoopMode.Training, "Type de partie lancée par la boucle : Training (contre l'IA) ou Battleground. Parties locales uniquement.");
            _loopDeckId = Config.Bind("training-loop", "deckId", "", "Identifiant du deck utilisé par la boucle (vide = deck sélectionné dans le jeu). Se choisit depuis le menu.");
            _loopRestoreDeck = Config.Bind("training-loop", "restoreDeckOnStop", true, "Remet le deck sélectionné d'origine quand la boucle s'arrête.");
            _loopEndDelay = Config.Bind("training-loop", "endScreenDelay", 2f, "Secondes d'attente à l'écran de fin quand une partie se termine normalement, avant de retourner au menu.");
            _loopDelay = Config.Bind("training-loop", "delayBetweenGames", 3f, "Secondes d'attente au menu entre deux parties.");
            _loopResignAfter = Config.Bind("training-loop", "resignAfterSeconds", 3f, "Durée de jeu maximale (en secondes) : au-delà, la partie est abandonnée puis une nouvelle est lancée. Une partie finie avant ce délai est juste suivie d'une relance.");
            _loopMaxGames = Config.Bind("training-loop", "maxGames", 0, "Nombre de parties avant arrêt automatique (0 = illimité).");
            _loopMaxFailures = Config.Bind("training-loop", "maxConsecutiveFailures", 5, "Arrêt après N lancements ratés de suite.");
            _loopLaunchTimeout = Config.Bind("training-loop", "launchTimeout", 15f, "Secondes max pour entrer en partie après le lancement.");
            _loopReadyTimeout = Config.Bind("training-loop", "gameReadyTimeout", 60f, "Secondes max pour que la partie soit prête avant retour forcé au menu.");
            _loopLeaveTimeout = Config.Bind("training-loop", "leaveTimeout", 5f, "Secondes après l'abandon avant de forcer le retour au menu.");
            _loopRetry = Config.Bind("training-loop", "retryInterval", 1f, "Secondes entre deux essais quand le menu ou le deck n'est pas prêt.");
            _loopGoMenuCooldown = Config.Bind("training-loop", "goMenuCooldown", 5f, "Délai minimum entre deux retours forcés au menu.");

            _loop = new TrainingLoop(m => Logger.LogInfo("training-loop: " + m), m => Logger.LogWarning("training-loop: " + m));
            SyncLoopSettings();
            Logger.LogInfo($"collection-export: prêt, sortie = {OutDir}, menu = {_menuHotkey.Value}, boucle = {_loopHotkey.Value}");
        }

        private string OutDir => string.IsNullOrEmpty(_outDir.Value)
            ? Path.Combine(Paths.GameRootPath, "SpamExport") : _outDir.Value;

        private void SyncLoopSettings()
        {
            _loop.Mode = _loopMode.Value;
            _loop.DeckId = _loopDeckId.Value ?? "";
            _loop.RestoreDeckOnStop = _loopRestoreDeck.Value;
            _loop.EndScreenDelay = Mathf.Max(0f, _loopEndDelay.Value);
            _loop.DelayBetweenGames = Mathf.Max(0.5f, _loopDelay.Value);
            _loop.ResignAfter = Mathf.Max(0f, _loopResignAfter.Value);
            _loop.MaxGames = Mathf.Max(0, _loopMaxGames.Value);
            _loop.MaxConsecutiveFailures = Mathf.Max(1, _loopMaxFailures.Value);
            _loop.LaunchTimeout = Mathf.Max(2f, _loopLaunchTimeout.Value);
            _loop.GameReadyTimeout = Mathf.Max(5f, _loopReadyTimeout.Value);
            _loop.LeaveTimeout = Mathf.Max(0.5f, _loopLeaveTimeout.Value);
            _loop.RetryInterval = Mathf.Max(0.2f, _loopRetry.Value);
            _loop.GoMenuCooldown = Mathf.Max(1f, _loopGoMenuCooldown.Value);
        }

        private void Update()
        {
            try
            {
                SyncLoopSettings();
                if (!_typing && _rebinding == null)
                {
                    if (Input.GetKeyDown(_menuHotkey.Value)) _menuOpen = !_menuOpen;
                    if (Input.GetKeyDown(_loopHotkey.Value)) _loop.Toggle();
                }
                _loop.Tick();
            }
            catch (Exception e) { Logger.LogError("training-loop: " + e); _loop.Stop("erreur"); }

            try
            {
                if (!_autoExport.Value || Time.realtimeSinceStartup < _nextCheck) return;
                _nextCheck = Time.realtimeSinceStartup + Mathf.Max(1f, _autoInterval.Value);
                Export(force: false);
            }
            catch (Exception e) { Logger.LogError("collection-export: " + e); _nextCheck = Time.realtimeSinceStartup + 30f; }
        }

        // Appelé par les boutons du menu : n'écrit rien de plus que Export, mais attrape les erreurs.
        private void RunExport()
        {
            try { Export(force: true); }
            catch (Exception e) { _exportStatus = "erreur : " + e.Message; Logger.LogError("collection-export: " + e); }
        }

        private void RunExportDeck(DeckModel d)
        {
            try
            {
                Directory.CreateDirectory(OutDir);
                var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                var sb = new StringBuilder();
                sb.AppendLine($"# SPAM — deck — {stamp}");
                AppendDeck(sb, d);
                string file = "deck_" + SafeFileName(d.Name) + ".txt";
                Write(file, sb.ToString());
                _exportStatus = $"{DateTime.Now:HH:mm:ss} — deck « {d.Name} » → {file}";
                Logger.LogInfo("collection-export: " + _exportStatus);
            }
            catch (Exception e) { _exportStatus = "erreur : " + e.Message; Logger.LogError("collection-export: " + e); }
        }

        private static string SafeFileName(string name)
        {
            var bad = Path.GetInvalidFileNameChars();
            var s = new string((name ?? "deck").Select(c => bad.Contains(c) ? '_' : c).ToArray()).Trim();
            return s.Length == 0 ? "deck" : s;
        }

        private static List<DeckModel> SafePlayableDecks()
        {
            try { return UserData.IsConnected ? UserData.PlayableDecks : null; } catch { return null; }
        }

        private static List<DeckModel> SafeDecks()
        {
            try { return UserData.IsConnected ? UserData.Decks : null; } catch { return null; }
        }

        private void Export(bool force)
        {
            if (!UserData.IsConnected) { if (force) { _exportStatus = "pas connecté"; Logger.LogWarning("collection-export: pas connecté"); } return; }
            var owned = UserData.PossessedCards;
            if (owned == null || owned.Count == 0) { if (force) _exportStatus = "collection vide ou pas encore chargée"; return; }
            var decks = UserData.Decks ?? new List<DeckModel>();

            string sig = Signature(owned, decks);
            if (!force && sig == _lastSignature) return;
            _lastSignature = sig;

            Directory.CreateDirectory(OutDir);
            var ownedById = new Dictionary<string, CardModel>();
            foreach (var m in owned) if (m?.Id != null && !ownedById.ContainsKey(m.Id)) ownedById[m.Id] = m;

            var all = SafeAllCards();
            var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

            if (_expCollection.Value)
            {
                // collection.txt : seulement les cartes possédées
                var sb = new StringBuilder();
                sb.AppendLine($"# SPAM — collection de {SafeUserName()} — {stamp}");
                sb.AppendLine($"# {ownedById.Count} cartes possédées / {all.Count(IsReleased)} sorties");
                sb.AppendLine();
                foreach (var group in owned.Where(m => m?.CardData != null)
                                           .GroupBy(m => m.CardData.Element ? Safe(() => m.CardData.Element.Name) : "?")
                                           .OrderBy(g => g.Key))
                {
                    sb.AppendLine($"## {group.Key}");
                    foreach (var m in group.OrderBy(m => Safe(() => m.CardData.Name)))
                        AppendCard(sb, m.CardData, m);
                    sb.AppendLine();
                }
                var unknown = owned.Where(m => m != null && m.CardData == null).ToList();
                if (unknown.Count > 0)
                {
                    sb.AppendLine("## Inconnues (id absent des assets)");
                    foreach (var m in unknown) sb.AppendLine("- " + m.Id);
                }
                Write("collection.txt", sb.ToString());
            }

            if (_expAllCards.Value)
            {
                // all_cards.txt : toute la base, marquée possédée ou non
                var sbAll = new StringBuilder();
                sbAll.AppendLine($"# SPAM — toutes les cartes — {stamp}");
                sbAll.AppendLine("# ✅ = possédée, ❌ = non possédée");
                sbAll.AppendLine();
                foreach (var c in all.Where(IsReleased).OrderBy(c => Safe(() => c.Element.Name)).ThenBy(c => Safe(() => c.Name)))
                {
                    ownedById.TryGetValue(c.Id, out var m);
                    sbAll.Append(m != null ? "✅ " : "❌ ");
                    AppendCard(sbAll, c, m);
                }
                Write("all_cards.txt", sbAll.ToString());
            }

            if (_expDecks.Value)
            {
                // decks.txt
                var sbD = new StringBuilder();
                sbD.AppendLine($"# SPAM — decks — {stamp}");
                foreach (var d in decks.Where(d => d != null)) AppendDeck(sbD, d);
                Write("decks.txt", sbD.ToString());
            }

            if (_expJson.Value)
            {
                // collection.json : brut, pour les outils
                Write("collection.json", JsonConvert.SerializeObject(new { exportedAt = stamp, owned, decks }, Formatting.Indented));
            }

            _exportStatus = $"{DateTime.Now:HH:mm:ss} — {ownedById.Count} cartes, {decks.Count} decks → {OutDir}";
            Logger.LogInfo($"collection-export: {ownedById.Count} cartes, {decks.Count} decks → {OutDir}");
        }

        private static void AppendDeck(StringBuilder sbD, DeckModel d)
        {
            sbD.AppendLine();
            sbD.AppendLine($"## {d.Name}");
            foreach (var m in d.Cards ?? new List<CardModel>())
            {
                if (m == null) continue;
                var c = m.CardData;
                sbD.AppendLine(c == null ? "- " + m.Id
                    : $"- {Safe(() => c.Name)} ({Safe(() => c.Element.Name)}) — talent : {TalentName(c, m.Talent)}");
            }
        }

        private void AppendCard(StringBuilder sb, CardData c, CardModel m)
        {
            sb.AppendLine($"**{Safe(() => c.Name)}** — {Safe(() => c.Element.Name)} / {Safe(() => c.Rarity.Name)} — PV {Safe(() => c.Hp.ToString())}, revenu {Safe(() => c.GoldIncome.ToString())}");
            var res = Resistances(c);
            if (res.Length > 0) sb.AppendLine("  Résistances : " + res);
            foreach (var a in c.Abilities ?? (IReadOnlyList<AbilityData>)new List<AbilityData>())
                if (a != null) sb.AppendLine($"  • {Safe(a.GetTitle)} (CD {a.cooldown}) : {Desc(a)}");
            foreach (var p in c.PassiveAbilities ?? (IReadOnlyList<ModificatorStatusData>)new List<ModificatorStatusData>())
                if (p != null) sb.AppendLine($"  ◦ Passif {Safe(p.GetTitle)} : {Clean(Safe(p.GetDesc))}");
            var talents = c.Talents ?? (IReadOnlyList<TalentData>)new List<TalentData>();
            if (talents.Count > 0)
            {
                var parts = new List<string>();
                for (int i = 0; i < talents.Count; i++)
                {
                    var t = talents[i]; if (t == null) continue;
                    int idx = i + 1;
                    bool unlocked = m != null && m.Talents != null && m.Talents.Contains(idx);
                    bool equipped = m != null && m.Talent == idx;
                    string mark = m == null ? "" : equipped ? "★" : unlocked ? "✓" : "✗";
                    parts.Add($"{mark}{Safe(t.GetTitle)}: {Desc(t)}");
                }
                sb.AppendLine("  Talents : " + string.Join(" | ", parts));
            }
            if (m != null && m.Styles != null && m.Styles.Count > 1)
                sb.AppendLine("  Styles : " + string.Join(", ", m.Styles));
        }

        private static string TalentName(CardData c, int talent)
        {
            if (talent <= 0 || c.Talents == null || talent - 1 >= c.Talents.Count) return "aucun";
            return Safe(c.Talents[talent - 1].GetTitle);
        }

        private static string Resistances(CardData c)
        {
            var r = new List<string>();
            void Add(string n, int v) { if (v != 0) r.Add($"{n} {(v > 0 ? "+" : "")}{v}"); }
            Add("Feu", c.FireResistance); Add("Eau", c.WaterResistance); Add("Terre", c.EarthResistance);
            Add("Nature", c.NatureResistance); Add("Ombre", c.ShadowResistance); Add("Lumière", c.LightResistance);
            return string.Join(", ", r);
        }

        private static bool IsReleased(CardData c) => c != null && Safe(() => c.IsReleased && c.IsPlayable ? "1" : "") == "1";

        private static List<CardData> SafeAllCards()
        {
            try { return CardData.All.Where(c => c != null).ToList(); } catch { return new List<CardData>(); }
        }

        private static string SafeUserName()
        {
            try { return UserData.Profile?.Username ?? "?"; } catch { return "?"; }
        }

        private static string Signature(List<CardModel> owned, List<DeckModel> decks)
        {
            var sb = new StringBuilder();
            foreach (var m in owned.Where(x => x != null).OrderBy(x => x.Id))
                sb.Append(m.Id).Append(':').Append(m.Talent).Append(':').Append(string.Join(",", m.Talents ?? new List<int>())).Append(';');
            foreach (var d in decks.Where(x => x != null))
                sb.Append(d.Name).Append('=').Append(string.Join(",", (d.Cards ?? new List<CardModel>()).Select(x => x?.Id + "/" + x?.Talent))).Append(';');
            return sb.ToString();
        }

        // Description avec les valeurs chiffrées (dégâts, soins, %…) ; repli sur le texte brut.
        private static string Desc(AbilityData a)
        {
            string d;
            try { d = a.GetDescWithValue(); } catch { d = null; }
            if (string.IsNullOrEmpty(d)) d = Safe(a.GetDescTranslation);
            return Clean(d);
        }

        private static readonly Regex Dup = new Regex(@"\b(\p{L}+)\[\1\]", RegexOptions.Compiled);
        private static readonly Regex Spaces = new Regex(@"\s{2,}", RegexOptions.Compiled);
        private static readonly Regex Tags = new Regex("<[^>]+>", RegexOptions.Compiled);
        private static string Clean(string s) => Spaces.Replace(Dup.Replace(Tags.Replace(s ?? "", "").Replace("\n", " "), "$1"), " ").Trim();

        private static string Safe(Func<string> f)
        {
            try { return f() ?? "?"; } catch { return "?"; }
        }

        private void Write(string name, string content) =>
            File.WriteAllText(Path.Combine(OutDir, name), content, new UTF8Encoding(false));
    }
}
