// Menu en jeu (IMGUI) : export des decks + réglages de la boucle d'entraînement.
// Toutes les valeurs sont des entrées de config BepInEx : modifiées ici, elles sont
// appliquées tout de suite et sauvegardées dans le .cfg.
using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using Spam.Data;
using Spam.MenuV3;
using UnityEngine;

namespace SpamCollectionExport
{
    public partial class Plugin
    {
        private const int WindowId = 742731;
        private bool _menuOpen;
        private bool _typing;                       // un champ texte du menu a le focus
        private ConfigEntry<KeyCode> _rebinding;    // touche en cours de réassignation
        private Rect _win = new Rect(40f, 40f, 500f, 580f);
        private int _tab;
        private Vector2 _scroll;
        private readonly Dictionary<string, string> _buf = new Dictionary<string, string>();

        private void OnGUI()
        {
            var ev = Event.current;

            // Capture de la prochaine touche pour une réassignation
            if (_rebinding != null && ev != null && ev.type == EventType.KeyDown && ev.keyCode != KeyCode.None)
            {
                if (ev.keyCode != KeyCode.Escape) _rebinding.Value = ev.keyCode;
                _rebinding = null;
                ev.Use();
                return;
            }

            float s = Mathf.Clamp(_menuScale.Value, 0.5f, 3f);
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));

            if (_loop != null && _loop.Running && _loopOverlay.Value)
                GUI.Label(new Rect(10f, 10f, 560f, 24f),
                    "Boucle : " + _loop.Status + " — " + _loop.Completed + " parties (" + _loopHotkey.Value + " pour arrêter)");

            if (_menuOpen)
                _win = GUILayout.Window(WindowId, _win, DrawWindow, "SPAM — export & boucle", GUILayout.Width(500f), GUILayout.Height(580f));

            if (ev != null && ev.type == EventType.Layout)
                _typing = _menuOpen && (GUI.GetNameOfFocusedControl() ?? "").StartsWith("tf_", StringComparison.Ordinal);

            GUI.matrix = old;
        }

        private void DrawWindow(int id)
        {
            _tab = GUILayout.Toolbar(_tab, new[] { "Export", "Boucle d'entraînement", "Interface" });
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(470f));
            if (_tab == 0) DrawExportTab();
            else if (_tab == 1) DrawLoopTab();
            else DrawUiTab();
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Fermer", GUILayout.Width(90f))) _menuOpen = false;
            GUILayout.Label(_menuHotkey.Value + " : ouvrir / fermer le menu");
            GUILayout.EndHorizontal();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        // ---------------------------------------------------------------- onglet Export
        private void DrawExportTab()
        {
            GUILayout.Label("Dernier export : " + _exportStatus);
            GUILayout.Label("Dossier : " + OutDir);
            if (GUILayout.Button("Exporter maintenant (selon les fichiers cochés)")) RunExport();

            GUILayout.Space(8f);
            GUILayout.Label("— Fichiers écrits lors d'un export —");
            BoolRow("collection.txt (cartes possédées)", _expCollection);
            BoolRow("all_cards.txt (toute la base)", _expAllCards);
            BoolRow("decks.txt (tous les decks)", _expDecks);
            BoolRow("collection.json (données brutes)", _expJson);

            GUILayout.Space(8f);
            GUILayout.Label("— Export automatique —");
            BoolRow("Exporter automatiquement quand ça change", _autoExport);
            FloatRow("Vérification toutes les (s)", _autoInterval, 1f, 600f, 1f);
            StringRow("Dossier de sortie (vide = <jeu>/SpamExport)", _outDir);

            GUILayout.Space(8f);
            GUILayout.Label("— Decks (export d'un seul deck → deck_<nom>.txt) —");
            var decks = SafeDecks();
            if (decks == null) { GUILayout.Label("Pas connecté : decks indisponibles."); return; }
            if (decks.Count == 0) GUILayout.Label("Aucun deck.");
            foreach (var d in decks)
            {
                if (d == null) continue;
                int n = d.Cards != null ? d.Cards.Count : 0;
                GUILayout.BeginHorizontal();
                GUILayout.Label(d.Name + " (" + n + " cartes)");
                if (GUILayout.Button("Exporter", GUILayout.Width(80f))) RunExportDeck(d);
                GUILayout.EndHorizontal();
            }
        }

        // ---------------------------------------------------------------- onglet Boucle
        private void DrawLoopTab()
        {
            GUILayout.Label("État : " + _loop.Status);
            GUILayout.Label(_loop.Completed + " parties : " + _loop.Abandoned + " abandonnées (trop longues), " + _loop.Finished + " terminées normalement");
            if (GUILayout.Button(_loop.Running ? "Arrêter la boucle" : "Démarrer la boucle")) _loop.Toggle();
            GUILayout.Label("Parties locales uniquement (jamais de matchmaking). Une partie qui n'est pas locale n'est jamais abandonnée (garde-fou non modifiable).");

            GUILayout.Space(6f);
            KeyRow("Touche démarrer / arrêter", _loopHotkey);

            GUILayout.Space(6f);
            GUILayout.Label("— Mode de jeu —");
            int mode = GUILayout.Toolbar((int)_loopMode.Value, new[] { "Entraînement (vs IA)", "Battleground", "Casual", "Ranked" });
            if (mode != (int)_loopMode.Value) _loopMode.Value = (LoopMode)mode;

            GUILayout.Space(6f);
            GUILayout.Label("— Deck utilisé par la boucle —");
            bool useGameDeck = string.IsNullOrEmpty(_loopDeckId.Value);
            bool pickDefault = GUILayout.Toggle(useGameDeck, "Deck sélectionné dans le jeu");
            if (pickDefault && !useGameDeck) _loopDeckId.Value = "";
            var decks = SafePlayableDecks();
            if (decks == null) GUILayout.Label("Pas connecté : decks indisponibles.");
            else
            {
                foreach (var d in decks)
                {
                    if (d == null) continue;
                    bool selected = !useGameDeck && d.Id == _loopDeckId.Value;
                    bool on = GUILayout.Toggle(selected, d.Name);
                    if (on && !selected) _loopDeckId.Value = d.Id;
                }
                if (!useGameDeck && !decks.Exists(x => x != null && x.Id == _loopDeckId.Value))
                    GUILayout.Label("Le deck choisi n'existe plus : la boucle s'arrêtera au lancement.");
            }
            BoolRow("Remettre le deck d'origine à l'arrêt de la boucle", _loopRestoreDeck);
            BoolRow("Autoriser Casual/Ranked (ATTENTION aux bans)", _loopAllowOnlineModes);

            GUILayout.Space(6f);
            GUILayout.Label("— Durée d'une partie —");
            FloatRow("Abandon si la partie dure plus de (s)", _loopResignAfter, 0f, 7200f, 5f);
            FloatRow("Attente à l'écran de fin normale (s)", _loopEndDelay, 0f, 60f, 0.5f);

            GUILayout.Space(6f);
            GUILayout.Label("— Rythme —");
            FloatRow("Pause au menu entre deux parties (s)", _loopDelay, 0.5f, 300f, 0.5f);
            IntRow("Nombre de parties (0 = illimité)", _loopMaxGames, 0, 100000, 1);

            GUILayout.Space(6f);
            GUILayout.Label("— Délais de sécurité —");
            IntRow("Arrêt après N lancements ratés", _loopMaxFailures, 1, 100, 1);
            FloatRow("Délai max pour entrer en partie (s)", _loopLaunchTimeout, 2f, 300f, 1f);
            FloatRow("Délai max pour partie prête (s)", _loopReadyTimeout, 5f, 600f, 5f);
            FloatRow("Attente avant retour menu forcé (s)", _loopLeaveTimeout, 0.5f, 120f, 0.5f);
            FloatRow("Intervalle de ré-essai (s)", _loopRetry, 0.2f, 30f, 0.2f);
            FloatRow("Délai mini entre deux retours menu (s)", _loopGoMenuCooldown, 1f, 120f, 1f);

            GUILayout.Space(8f);
            if (GUILayout.Button("Remettre les valeurs par défaut de la boucle"))
            {
                ResetEntries(_loopMode, _loopDeckId, _loopRestoreDeck, _loopAllowOnlineModes,
                             _loopResignAfter, _loopEndDelay, _loopDelay,
                             _loopMaxGames, _loopMaxFailures, _loopLaunchTimeout, _loopReadyTimeout,
                             _loopLeaveTimeout, _loopRetry, _loopGoMenuCooldown);
            }
        }

        // ---------------------------------------------------------------- onglet Interface
        private void DrawUiTab()
        {
            KeyRow("Touche du menu", _menuHotkey);
            FloatRow("Taille de l'interface (0.5 à 3)", _menuScale, 0.5f, 3f, 0.1f);
            BoolRow("Compteur de la boucle à l'écran", _loopOverlay);
            GUILayout.Space(8f);
            if (GUILayout.Button("Remettre les valeurs par défaut de l'interface"))
                ResetEntries(_menuHotkey, _menuScale, _loopOverlay);
        }

        // ---------------------------------------------------------------- composants
        private void ResetEntries(params ConfigEntryBase[] entries)
        {
            foreach (var e in entries) e.BoxedValue = e.DefaultValue;
            _buf.Clear();
        }

        private static float Round2(float v) { return Mathf.Round(v * 100f) / 100f; }

        private void BoolRow(string label, ConfigEntry<bool> e)
        {
            bool nv = GUILayout.Toggle(e.Value, label);
            if (nv != e.Value) e.Value = nv;
        }

        private void KeyRow(string label, ConfigEntry<KeyCode> e)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + " : " + e.Value, GUILayout.Width(250f));
            string btn = _rebinding == e ? "Appuie sur une touche… (Échap = annuler)" : "Changer";
            if (GUILayout.Button(btn)) _rebinding = e;
            GUILayout.EndHorizontal();
        }

        private void StringRow(string label, ConfigEntry<string> e)
        {
            GUILayout.Label(label);
            string name = "tf_" + label;
            GUI.SetNextControlName(name);
            string next = GUILayout.TextField(e.Value ?? "");
            if (next != e.Value) e.Value = next;
        }

        private void FloatRow(string label, ConfigEntry<float> e, float min, float max, float step)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(280f));
            if (GUILayout.Button("-", GUILayout.Width(26f))) { e.Value = Mathf.Clamp(Round2(e.Value - step), min, max); _buf.Remove(label); }

            string name = "tf_" + label;
            bool focused = GUI.GetNameOfFocusedControl() == name;
            string typed;
            string cur = (focused && _buf.TryGetValue(label, out typed)) ? typed
                         : e.Value.ToString("0.##", CultureInfo.InvariantCulture);
            GUI.SetNextControlName(name);
            string next = GUILayout.TextField(cur, 8, GUILayout.Width(70f));
            if (next != cur)
            {
                _buf[label] = next;
                float v;
                if (float.TryParse(next.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                    e.Value = Mathf.Clamp(v, min, max);
            }

            if (GUILayout.Button("+", GUILayout.Width(26f))) { e.Value = Mathf.Clamp(Round2(e.Value + step), min, max); _buf.Remove(label); }
            GUILayout.EndHorizontal();
        }

        private void IntRow(string label, ConfigEntry<int> e, int min, int max, int step)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(280f));
            if (GUILayout.Button("-", GUILayout.Width(26f))) { e.Value = Mathf.Clamp(e.Value - step, min, max); _buf.Remove(label); }

            string name = "tf_" + label;
            bool focused = GUI.GetNameOfFocusedControl() == name;
            string typed;
            string cur = (focused && _buf.TryGetValue(label, out typed)) ? typed
                         : e.Value.ToString(CultureInfo.InvariantCulture);
            GUI.SetNextControlName(name);
            string next = GUILayout.TextField(cur, 8, GUILayout.Width(70f));
            if (next != cur)
            {
                _buf[label] = next;
                int v;
                if (int.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                    e.Value = Mathf.Clamp(v, min, max);
            }

            if (GUILayout.Button("+", GUILayout.Width(26f))) { e.Value = Mathf.Clamp(e.Value + step, min, max); _buf.Remove(label); }
            GUILayout.EndHorizontal();
        }
    }
}
