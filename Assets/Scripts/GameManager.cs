using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blackjack
{
    public enum GameState { Betting, Dealing, Insurance, PlayerTurn, DealerTurn, Resolution, GameOver }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public event Action<float, float> OnBalanceChanged;
        public event Action<int, int, int> OnScoreChanged;
        public event Action<GameState> OnStateChanged;
        public event Action<string> OnGameResult;
        public event Action<int, bool, bool, float, float> OnTurnOptionsUpdated;
        public event Action<bool> OnSplitStateChanged;

        [SerializeField] private DeckManager deckManager;
        [SerializeField] private Transform playerHandContainer;
        [SerializeField] private Transform splitLeftContainer;
        [SerializeField] private Transform splitRightContainer;
        [SerializeField] private Transform dealerHandContainer;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private Sprite cardBackSprite;

        private float startingBalance = 1000f;
        private float currentBalance;
        private float currentBet;
        private float insuranceBet = 0f;
        private float actionDelay = 0.5f;

        private List<CardData> playerHand = new List<CardData>();
        private List<CardData> splitHand = new List<CardData>();
        private bool isSplitMode = false;
        private int activeHandIndex = 0;

        private List<CardData> dealerHand = new List<CardData>();
        private List<CardDisplay> dealerCardDisplays = new List<CardDisplay>();

        public GameState CurrentState { get; private set; }
        private float timer = 0f;
        private int dealStep = 0;
        private const string BalanceKey = "PlayerBalance";

        private void Awake()
        {
            if (Instance != null && Instance != this) Destroy(gameObject);
            else Instance = this;
            if (PlayerPrefs.HasKey(BalanceKey)) currentBalance = PlayerPrefs.GetFloat(BalanceKey);
            else currentBalance = startingBalance;
        }

        private void Start()
        {
            currentBet = 0;
            OnBalanceChanged?.Invoke(currentBalance, currentBet);
            if (deckManager != null) deckManager.InitializeDeck();
            StartBettingPhase();
        }

        private void Update()
        {
            if (timer > 0f) { timer -= Time.deltaTime; return; }
            switch (CurrentState)
            {
                case GameState.Dealing: HandleDealingState(); break;
                case GameState.DealerTurn: HandleDealerTurnState(); break;
                case GameState.GameOver: StartBettingPhase(); break;
            }
        }

        public void StartBettingPhase()
        {
            ChangeState(GameState.Betting);
            ClearTable();
            isSplitMode = false;
            activeHandIndex = 0;
            insuranceBet = 0f;
            OnSplitStateChanged?.Invoke(false);
            currentBet = 0;
            OnBalanceChanged?.Invoke(currentBalance, currentBet);
            OnScoreChanged?.Invoke(0, 0, 0);
        }

        public void PlaceBet(ChipData chip)
        {
            if (currentBalance >= chip.value)
            {
                currentBalance -= chip.value;
                currentBet += chip.value;
                OnBalanceChanged?.Invoke(currentBalance, currentBet);
            }
        }

        public void ClearBet()
        {
            if (currentBet == 0) return;
            currentBalance += currentBet;
            currentBet = 0;
            OnBalanceChanged?.Invoke(currentBalance, currentBet);
        }

        public void ConfirmBet()
        {
            if (currentBet > 0) StartDealingPhase();
        }

        private void StartDealingPhase()
        {
            ChangeState(GameState.Dealing);
            dealStep = 0;
            timer = actionDelay;
        }

        private void HandleDealingState()
        {
            dealStep++;
            switch (dealStep)
            {
                case 1: HitHand(playerHand, playerHandContainer, false); break;
                case 2: HitDealer(false); break;
                case 3: HitHand(playerHand, playerHandContainer, false); break;
                case 4:
                    HitDealer(true);
                    if (dealerHand[0].rank == CardRank.Ace)
                    {
                        ChangeState(GameState.Insurance);
                        BroadcastScores();
                        return;
                    }
                    CheckInitialBlackjack();
                    return;
            }
            if (dealStep < 4) timer = actionDelay;
        }

        public void OnInsuranceAccept()
        {
            float cost = currentBet / 2f;
            if (currentBalance >= cost)
            {
                currentBalance -= cost;
                insuranceBet = cost;
                OnBalanceChanged?.Invoke(currentBalance, currentBet);
                CheckInsuranceOutcome();
            }
            else OnInsuranceDecline();
        }

        public void OnInsuranceDecline()
        {
            insuranceBet = 0f;
            CheckInsuranceOutcome();
        }

        private void CheckInsuranceOutcome()
        {
            if (CalculateHandValue(dealerHand) == 21)
            {
                RevealDealerHiddenCard();
                BroadcastScores();
                if (insuranceBet > 0)
                {
                    float payout = insuranceBet * 3f;
                    currentBalance += payout;
                    FinishGame("Страховка виграла!");
                }
                else
                {
                    if (CalculateHandValue(playerHand) == 21)
                    {
                        currentBalance += currentBet;
                        FinishGame("Нічия (Блекджек у обох)");
                    }
                    else FinishGame("У дилера Блекджек! Ви програли.");
                }
            }
            else
            {
                if (insuranceBet > 0) insuranceBet = 0f;
                CheckInitialBlackjack();
            }
        }

        private void CheckInitialBlackjack()
        {
            BroadcastScores();
            if (CalculateHandValue(playerHand) == 21)
            {
                RevealDealerHiddenCard();
                BroadcastScores();
                ResolveGame();
            }
            else StartPlayerTurn();
        }

        private void StartPlayerTurn()
        {
            ChangeState(GameState.PlayerTurn);
            UpdateTurnOptions();
        }

        private void UpdateTurnOptions()
        {
            List<CardData> currentHand;
            if (activeHandIndex == 0) currentHand = playerHand;
            else currentHand = splitHand;

            int handValue = CalculateHandValue(currentHand);
            int count = currentHand.Count;
            bool canDouble = false;

            if (count == 2)
            {
                if (handValue >= 9)
                    if (handValue <= 11) canDouble = true;
            }

            bool canSplit = !isSplitMode && count == 2 && playerHand[0].rank == playerHand[1].rank;
            OnTurnOptionsUpdated?.Invoke(count, canSplit, canDouble, currentBalance, currentBet);
        }

        public void OnHitButton()
        {
            if (CurrentState != GameState.PlayerTurn) return;

            if (activeHandIndex == 0)
            {
                Transform container;
                if (isSplitMode) container = splitLeftContainer;
                else container = playerHandContainer;
                HitHand(playerHand, container, false);
            }
            else HitHand(splitHand, splitRightContainer, false);

            BroadcastScores();
            List<CardData> currentHand;
            if (activeHandIndex == 0) currentHand = playerHand;
            else currentHand = splitHand;

            if (CalculateHandValue(currentHand) > 21) HandleHandEnd();
            else UpdateTurnOptions();
        }

        public void OnStandButton()
        {
            if (CurrentState != GameState.PlayerTurn) return;
            HandleHandEnd();
        }

        private void HandleHandEnd()
        {
            if (isSplitMode && activeHandIndex == 0)
            {
                activeHandIndex = 1;
                BroadcastScores();
                UpdateTurnOptions();
            }
            else StartDealerTurn();
        }

        public void OnDoubleButton()
        {
            if (CurrentState != GameState.PlayerTurn) return;
            if (currentBalance >= currentBet)
            {
                currentBalance -= currentBet;
                currentBet += currentBet;
                OnBalanceChanged?.Invoke(currentBalance, currentBet);

                if (activeHandIndex == 0)
                {
                    Transform container;
                    if (isSplitMode) container = splitLeftContainer;
                    else container = playerHandContainer;
                    HitHand(playerHand, container, false);
                }
                else HitHand(splitHand, splitRightContainer, false);
                HandleHandEnd();
            }
        }

        public void OnSplitButton()
        {
            if (CurrentState != GameState.PlayerTurn) return;
            if (currentBalance < currentBet) return;

            currentBalance -= currentBet;
            currentBet *= 2;
            OnBalanceChanged?.Invoke(currentBalance, currentBet);

            isSplitMode = true;
            activeHandIndex = 0;
            OnSplitStateChanged?.Invoke(true);

            CardData cardToMove = playerHand[1];
            playerHand.RemoveAt(1);
            splitHand.Add(cardToMove);

            RedrawCardsForSplit();
            HitHand(playerHand, splitLeftContainer, false);
            HitHand(splitHand, splitRightContainer, false);

            BroadcastScores();
            UpdateTurnOptions();
        }

        private void RedrawCardsForSplit()
        {
            foreach (Transform t in playerHandContainer) Destroy(t.gameObject);
            foreach (Transform t in splitLeftContainer) Destroy(t.gameObject);
            foreach (Transform t in splitRightContainer) Destroy(t.gameObject);
            foreach (var card in playerHand) CreateCardVisual(card, splitLeftContainer, false);
            foreach (var card in splitHand) CreateCardVisual(card, splitRightContainer, false);
        }

        private void StartDealerTurn()
        {
            ChangeState(GameState.DealerTurn);
            RevealDealerHiddenCard();
            BroadcastScores();
            bool hand1Bust = CalculateHandValue(playerHand) > 21;
            bool hand2Bust = isSplitMode && (CalculateHandValue(splitHand) > 21);
            if ((!isSplitMode && hand1Bust) || (isSplitMode && hand1Bust && hand2Bust)) ResolveGame();
            else timer = actionDelay;
        }

        private void HandleDealerTurnState()
        {
            int dScore = CalculateHandValue(dealerHand);
            if (dScore < 17)
            {
                HitDealer(false);
                BroadcastScores();
                timer = 1.0f;
            }
            else ResolveGame();
        }

        private void ResolveGame()
        {
            if (CurrentState == GameState.Resolution || CurrentState == GameState.GameOver) return;
            ChangeState(GameState.Resolution);

            RevealDealerHiddenCard();
            BroadcastScores();

            int dScore = CalculateHandValue(dealerHand);
            float betPerHand;
            if (isSplitMode) betPerHand = currentBet / 2f;
            else betPerHand = currentBet;

            float totalWin = 0f;
            string msg = "";

            int p1 = CalculateHandValue(playerHand);
            string r1 = GetResult(p1, dScore);
            totalWin += CalculatePayout(r1, betPerHand);

            if (isSplitMode)
            {
                int p2 = CalculateHandValue(splitHand);
                string r2 = GetResult(p2, dScore);
                totalWin += CalculatePayout(r2, betPerHand);
                msg = $"1 рука - {r1}\n2 рука - {r2}";
            }
            else msg = r1;

            currentBalance += totalWin;
            FinishGame(msg);
        }

        private string GetResult(int p, int d)
        {
            if (p > 21 || (d <= 21 && d > p)) return "ПРОГРАШ"; 
            if (p == d) return "НІЧИЯ"; 
            return "ПЕРЕМОГА"; 
        }

        private float CalculatePayout(string res, float bet)
        {
            if (res == "ПЕРЕМОГА") return bet * 2f;
            if (res == "НІЧИЯ") return bet;
            return 0f;
        }

        private void FinishGame(string outcome)
        {
            OnGameResult?.Invoke(outcome);
            OnBalanceChanged?.Invoke(currentBalance, 0);
            PlayerPrefs.SetFloat(BalanceKey, currentBalance);
            PlayerPrefs.Save();
            ChangeState(GameState.GameOver);
            timer = 5f;
        }

        private void BroadcastScores()
        {
            int p1Score = CalculateHandValue(playerHand);
            int p2Score = 0;
            if (isSplitMode) p2Score = CalculateHandValue(splitHand);
            int dScore = 0;

            if (CurrentState == GameState.Dealing || CurrentState == GameState.PlayerTurn || CurrentState == GameState.Insurance)
            {
                if (dealerHand.Count > 0) dScore = dealerHand[0].rank.GetBlackjackValue();
            }
            else dScore = CalculateHandValue(dealerHand);
            OnScoreChanged?.Invoke(p1Score, p2Score, dScore);
        }

        private void HitHand(List<CardData> hand, Transform container, bool faceDown) => SpawnCard(hand, container, false, faceDown);
        private void HitDealer(bool faceDown) => SpawnCard(dealerHand, dealerHandContainer, true, faceDown);

        private void SpawnCard(List<CardData> hand, Transform container, bool isDealer, bool faceDown)
        {
            if (deckManager == null) return;
            CardData card = deckManager.DrawCard();
            if (card == null) return;
            hand.Add(card);
            CreateCardVisual(card, container, faceDown, isDealer);
        }

        private void CreateCardVisual(CardData card, Transform container, bool faceDown, bool isDealer = false)
        {
            GameObject obj = Instantiate(cardPrefab, container);
            CardDisplay display = obj.GetComponent<CardDisplay>();
            if (display != null)
            {
                display.Setup(card, cardBackSprite, faceDown);
                if (isDealer) dealerCardDisplays.Add(display);
            }
        }

        private void RevealDealerHiddenCard()
        {
            foreach (var cd in dealerCardDisplays) if (cd.IsFaceDown) cd.Flip();
        }

        private int CalculateHandValue(List<CardData> hand)
        {
            int total = 0, aces = 0;
            foreach (var c in hand)
            {
                int val = c.rank.GetBlackjackValue();
                total += val;
                if (c.rank == CardRank.Ace) aces++;
            }
            while (total > 21 && aces > 0) { total -= 10; aces--; }
            return total;
        }

        private void ClearTable()
        {
            if (deckManager != null) { deckManager.AddToDiscard(playerHand); deckManager.AddToDiscard(splitHand); deckManager.AddToDiscard(dealerHand); }
            playerHand.Clear(); splitHand.Clear(); dealerHand.Clear(); dealerCardDisplays.Clear();
            foreach (Transform t in playerHandContainer) Destroy(t.gameObject);
            foreach (Transform t in splitLeftContainer) Destroy(t.gameObject);
            foreach (Transform t in splitRightContainer) Destroy(t.gameObject);
            foreach (Transform t in dealerHandContainer) Destroy(t.gameObject);
        }

        private void ChangeState(GameState newState)
        {
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);
        }
    }
}