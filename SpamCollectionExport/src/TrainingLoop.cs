// TrainingLoop — lance une partie locale (entraînement contre l'IA ou Battleground), la joue,
// et l'abandonne si elle dure trop longtemps ; puis en relance une autre.
//   - une partie qui se termine avant le délai (victoire / défaite) est simplement suivie d'une relance ;
//   - une partie qui dépasse le délai est abandonnée, puis on relance.
// Volontairement limité aux parties locales :
//   - ne touche jamais au matchmaking (aucun appel à StartMatchmaking / JoinMatchQueue) ;
//   - n'abandonne une partie que si GameSettings.IsTraining() ou IsBattleground() est vrai,
//     jamais un match classé / casual / amis / tournoi (garde-fou non désactivable) ;
//   - refuse de démarrer si le joueur est en file d'attente.
using System;
using Spam.MenuV3;
using TcgEngine;
using TcgEngine.Client;
using UnityEngine;

namespace SpamCollectionExport
{
    public enum LoopMode { Training, Battleground, Casual, Ranked }

    internal sealed class TrainingLoop
    {
        private enum Phase { Menu, Launching, InGame, Leaving }

        // Réglages (poussés par Plugin depuis la config BepInEx, modifiables en direct)
        public LoopMode Mode = LoopMode.Training; // type de partie à lancer
        public string DeckId = "";              // "" = deck sélectionné dans le jeu
        public bool RestoreDeckOnStop = true;   // remet le deck d'origine à l'arrêt de la boucle
        public float ResignAfter = 3f;          // durée de jeu max avant d'abandonner (s). 0 = quitter dès que la partie est prête
        public float EndScreenDelay = 2f;       // attente après une fin de partie normale avant de retourner au menu
        public float DelayBetweenGames = 3f;    // pause au menu entre deux parties
        public int MaxGames = 0;                // 0 = illimité
        public int MaxConsecutiveFailures = 5;  // arrêt si le lancement échoue N fois de suite
        public float LaunchTimeout = 15f;       // temps max pour entrer en partie après le lancement
        public float GameReadyTimeout = 60f;    // temps max pour que la partie soit prête
        public float LeaveTimeout = 5f;         // attente après l'abandon avant de forcer le retour menu
        public float RetryInterval = 1f;        // intervalle de ré-essai quand le menu / deck n'est pas prêt
        public float GoMenuCooldown = 5f;       // délai mini entre deux « retour menu » forcés
        public bool AllowOnlineModes = false;   // autorise l'abandon en Casual/Ranked (désactivé par défaut pour éviter les bans)

        public bool Running { get; private set; }
        public int Completed { get; private set; }  // parties terminées (abandonnées + finies)
        public int Abandoned { get; private set; }  // abandonnées car trop longues
        public int Finished { get; private set; }   // terminées normalement avant le délai
        public string Status { get; private set; } = "arrêté";

        private Phase _phase = Phase.Menu;
        private float _nextAction;
        private float _phaseSince;
        private float _readyAt = -1f;
        private float _endedAt = -1f;
        private float _lastGoToMenu = -999f;
        private bool _resigned;
        private int _failures;
        private string _originalDeckId;
        private readonly Action<string> _log;
        private readonly Action<string> _warn;

        public TrainingLoop(Action<string> log, Action<string> warn) { _log = log; _warn = warn; }

        public void Toggle()
        {
            if (Running) Stop("arrêt demandé");
            else Start();
        }

        public void Start()
        {
            Running = true; Completed = 0; Abandoned = 0; Finished = 0; _failures = 0; _resigned = false;
            _readyAt = -1f; _endedAt = -1f;
            _originalDeckId = null;
            if (!string.IsNullOrEmpty(DeckId))
            {
                try { _originalDeckId = Spam.MenuV3.UserData.SelectedDeckId; } catch { }
            }
            _phase = InGameScene() ? Phase.InGame : Phase.Menu;
            _phaseSince = Time.realtimeSinceStartup;
            _nextAction = Time.realtimeSinceStartup + 0.5f;
            Status = "démarrage";
            _log($"boucle : ON (mode {Mode})");
        }

        public void Stop(string why)
        {
            if (!Running) return;
            Running = false; Status = "arrêté";
            RestoreDeck();
            _log($"boucle : OFF ({why}) — {Completed} parties ({Abandoned} abandonnées, {Finished} terminées)");
        }

        private void RestoreDeck()
        {
            if (!RestoreDeckOnStop || string.IsNullOrEmpty(_originalDeckId)) { _originalDeckId = null; return; }
            try
            {
                if (Spam.MenuV3.UserData.SelectedDeckId != _originalDeckId)
                    Spam.MenuV3.UserData.SelectedDeckId = _originalDeckId;
            }
            catch (Exception e) { _warn("impossible de restaurer le deck d'origine : " + e.Message); }
            _originalDeckId = null;
        }

        private static bool InGameScene()
        {
            string s;
            try { s = SceneNav.GetCurrentScene(); } catch { return false; }
            return string.Equals(s, "Game", StringComparison.OrdinalIgnoreCase);
        }

        public void Tick()
        {
            if (!Running) return;
            float now = Time.realtimeSinceStartup;
            bool inGame = InGameScene();

            // Transitions de scène
            if (inGame && (_phase == Phase.Menu || _phase == Phase.Launching))
            {
                _phase = Phase.InGame; _phaseSince = now; _resigned = false; _failures = 0;
                _readyAt = -1f; _endedAt = -1f;
            }
            else if (!inGame && (_phase == Phase.InGame || _phase == Phase.Leaving))
            {
                Completed++;
                if (_resigned) { Abandoned++; _log($"partie #{Completed} abandonnée (trop longue)"); }
                else { Finished++; _log($"partie #{Completed} terminée normalement"); }
                if (MaxGames > 0 && Completed >= MaxGames) { Stop("nombre de parties atteint"); return; }
                _phase = Phase.Menu; _phaseSince = now; _nextAction = now + DelayBetweenGames;
            }

            switch (_phase)
            {
                case Phase.Menu: TickMenu(now); break;
                case Phase.Launching: TickLaunching(now); break;
                case Phase.InGame: TickInGame(now); break;
                case Phase.Leaving: TickLeaving(now); break;
            }
        }

        private void TickMenu(float now)
        {
            Status = "menu";
            if (now < _nextAction) return;

            if (!Spam.MenuV3.UserData.IsConnected)
            { _nextAction = now + RetryInterval; Status = "attente connexion"; return; }

            // Deck choisi pour la boucle (sinon on garde celui sélectionné dans le jeu)
            if (!string.IsNullOrEmpty(DeckId))
            {
                var decks = Spam.MenuV3.UserData.PlayableDecks;
                if (decks == null || decks.Count == 0)
                { _nextAction = now + RetryInterval; Status = "attente decks"; return; }
                bool found = false;
                foreach (var d in decks) { if (d != null && d.Id == DeckId) { found = true; break; } }
                if (!found) { Stop("le deck choisi est introuvable (supprimé ou incomplet ?)"); return; }
                if (Spam.MenuV3.UserData.SelectedDeckId != DeckId) Spam.MenuV3.UserData.SelectedDeckId = DeckId;
            }

            if (Spam.MenuV3.UserData.SelectedDeck == null)
            { _nextAction = now + RetryInterval; Status = "attente deck"; return; }

            var pm = UnityEngine.Object.FindFirstObjectByType<PlayManager>(FindObjectsInactive.Include);
            if (pm == null) { _nextAction = now + RetryInterval; Status = "attente menu Jouer"; return; }
            if (pm.IsInMatchmaking) { _nextAction = now + RetryInterval * 2f; Status = "en file d'attente : pause"; return; }

            _log($"lancement d'une partie ({Mode})");
            switch (Mode)
            {
                case LoopMode.Training: pm.StartTraining(); break;
                case LoopMode.Battleground: pm.StartBattleground(); break;
                case LoopMode.Casual:
                case LoopMode.Ranked:
                    pm.StartMatchmaking();
                    break;
            }
            _phase = Phase.Launching; _phaseSince = now;
            Status = "lancement";
        }

        private void TickLaunching(float now)
        {
            // Le lancement peut refuser sans rien dire (tournoi, pas de deck…)
            if (now - _phaseSince < LaunchTimeout) return;
            _failures++;
            _warn($"le lancement n'a pas abouti ({_failures}/{MaxConsecutiveFailures})");
            if (_failures >= MaxConsecutiveFailures) { Stop("trop d'échecs de lancement"); return; }
            _phase = Phase.Menu; _nextAction = now + RetryInterval * 2f; _phaseSince = now;
        }

        private void TickInGame(float now)
        {
            Status = "en partie";
            var gc = GameClient.Get();
            Game g = null;
            try { g = gc != null ? gc.GetGameData() : null; } catch { }

            if (gc == null || g == null || !gc.IsReady() || !g.HasStarted())
            {
                if (now - _phaseSince > GameReadyTimeout) { _warn("partie jamais prête, retour au menu"); GoMenu(now); }
                return;
            }
            if (gc.IsObserveMode()) return;

            // Garde-fou : on n'abandonne QUE les modes autorisés.
            bool canResign = false;
            try
            {
                if (g.settings != null)
                {
                    if (g.settings.IsTraining() || g.settings.IsBattleground()) canResign = true;
                    else if (AllowOnlineModes && (g.settings.IsCasual() || g.settings.IsRanked())) canResign = true;
                }
            } catch { }
            if (!canResign)
            {
                Stop("la partie en cours n'est pas un mode abandonnable — rien abandonné");
                return;
            }

            if (_readyAt < 0f) _readyAt = now;

            // Si ResignAfter == 0, on quitte immédiatement dès que la partie est prête
            if (ResignAfter <= 0f)
            {
                if (!_resigned)
                {
                    _resigned = true;
                    gc.Resign();
                    _phase = Phase.Leaving; _phaseSince = now;
                    Status = "abandon (timer=0)";
                }
                return;
            }

            // Fin normale (tu as gagné ou perdu avant le délai) : on laisse l'écran de fin, puis menu.
            if (g.HasEnded())
            {
                if (_endedAt < 0f) _endedAt = now;
                Status = "partie terminée";
                if (now - _endedAt >= EndScreenDelay) GoMenu(now);
                return;
            }

            float remaining = ResignAfter - (now - _readyAt);
            if (remaining > 0f) { Status = "en partie — abandon dans " + Mathf.CeilToInt(remaining) + " s"; return; }

            if (!_resigned)
            {
                _resigned = true;
                gc.Resign();
                _phase = Phase.Leaving; _phaseSince = now;
                Status = "abandon";
            }
        }

        private void TickLeaving(float now)
        {
            // Resign() renvoie parfois directement au menu ; sinon on y va après un court délai.
            if (now - _phaseSince >= LeaveTimeout) GoMenu(now);
        }

        private void GoMenu(float now)
        {
            if (now - _lastGoToMenu < GoMenuCooldown) return;
            _lastGoToMenu = now;
            try { SceneNav.GoToMenu(); } catch (Exception e) { _warn("retour menu impossible : " + e.Message); }
        }
    }
}
