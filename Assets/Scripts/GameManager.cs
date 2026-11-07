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

        [Header("Правила")]
        [Tooltip("Якщо true, дилер перевіряє закриту карту, коли відкрита карта 10/В/Д/К (американські правила).")]
        [SerializeField] private bool dealerPeeksOnTen = true;
        [Tooltip("Виплата за природний Блекджек: 1.5 = 3:2, 1.2 = 6:5.")]
        [SerializeField] private float blackjackPayoutRatio = 1.5f;

        private float startingBalance = 1000f;
        private float currentBalance;

        // currentBet - загальна сума ставок на столі (для UI).
        // bet1 / bet2 - ставки окремих рук (bet2 > 0 лише після спліту).
        private float currentBet;
        private float bet1;
        private float bet2;
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
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

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

        // ------------------------------------------------------------------
        // Ставки
        // ------------------------------------------------------------------

        public void StartBettingPhase()
        {
            ChangeState(GameState.Betting);
            ClearTable();
            isSplitMode = false;
            activeHandIndex = 0;
            insuranceBet = 0f;
            bet1 = 0f;
            bet2 = 0f;
            OnSplitStateChanged?.Invoke(false);
            currentBet = 0;
            OnBalanceChanged?.Invoke(currentBalance, currentBet);
            OnScoreChanged?.Invoke(0, 0, 0);
        }

        public void PlaceBet(ChipData chip)
        {
            if (CurrentState != GameState.Betting) return;
            if (currentBalance >= chip.value)
            {
                currentBalance -= chip.value;
                currentBet += chip.value;
                OnBalanceChanged?.Invoke(currentBalance, currentBet);
            }
        }

        public void ClearBet()
        {
            if (CurrentState != GameState.Betting) return;
            if (currentBet == 0) return;
            currentBalance += currentBet;
            currentBet = 0;
            OnBalanceChanged?.Invoke(currentBalance, currentBet);
        }

        public void ConfirmBet()
        {
            if (CurrentState != GameState.Betting) return;
            if (currentBet > 0)
            {
                bet1 = currentBet;
                bet2 = 0f;
                StartDealingPhase();
            }
        }

        // ------------------------------------------------------------------
        // Роздача
        // ------------------------------------------------------------------

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

        // ------------------------------------------------------------------
        // Страховка
        // ------------------------------------------------------------------

        public void OnInsuranceAccept()
        {
            if (CurrentState != GameState.Insurance) return;

            float cost = bet1 / 2f;
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
            if (CurrentState != GameState.Insurance) return;

            insuranceBet = 0f;
            CheckInsuranceOutcome();
        }

        private void CheckInsuranceOutcome()
        {
            if (IsBlackjack(dealerHand))
            {
                RevealDealerHiddenCard();
                BroadcastScores();

                bool playerBlackjack = IsBlackjack(playerHand);
                float payout = 0f;
                string msg;

                // Страховка: ставка повертається + виграш 2:1 (разом x3).
                if (insuranceBet > 0f) payout += insuranceBet * 3f;

                if (playerBlackjack)
                {
                    // Основна ставка - нічия (push).
                    payout += bet1;
                    msg = insuranceBet > 0f
                        ? "Нічия по основній ставці. Страховка виграла!"
                        : "Нічия (Блекджек у обох)";
                }
                else
                {
                    msg = insuranceBet > 0f
                        ? "Страховка виграла! Основну ставку програно."
                        : "У дилера Блекджек! Ви програли.";
                }

                currentBalance += payout;
                FinishGame(msg);
            }
            else
            {
                // Страховка програла (у дилера немає Блекджека).
                insuranceBet = 0f;
                CheckInitialBlackjack();
            }
        }

        // ------------------------------------------------------------------
        // Перевірка Блекджека на старті
        // ------------------------------------------------------------------

        private bool IsBlackjack(List<CardData> hand)
        {
            return hand.Count == 2 && CalculateHandValue(hand) == 21;
        }

        private void CheckInitialBlackjack()
        {
            BroadcastScores();

            bool playerBJ = IsBlackjack(playerHand);
            bool dealerBJ = IsBlackjack(dealerHand);
            bool dealerPeeked = dealerPeeksOnTen && dealerHand[0].rank.GetBlackjackValue() == 10;

            // Дилер дивиться закриту карту під десяткою
            if (dealerPeeked && dealerBJ)
            {
                RevealDealerHiddenCard();
                BroadcastScores();
                if (playerBJ)
                {
                    currentBalance += bet1;
                    FinishGame("Нічия (Блекджек у обох)");
                }
                else FinishGame("У дилера Блекджек! Ви програли.");
                return;
            }

            // Природний Блекджек гравця
            if (playerBJ)
            {
                RevealDealerHiddenCard();
                BroadcastScores();
                if (dealerBJ)
                {
                    currentBalance += bet1;
                    FinishGame("Нічия (Блекджек у обох)");
                }
                else
                {
                    // ставка повертається + виграш за коефіцієнтом (3:2 -> x2.5)
                    currentBalance += bet1 * (1f + blackjackPayoutRatio);
                    FinishGame("Блекджек!");
                }
                return;
            }

            StartPlayerTurn();
        }

        // ------------------------------------------------------------------
        // Хід гравця
        // ------------------------------------------------------------------

        private void StartPlayerTurn()
        {
            ChangeState(GameState.PlayerTurn);
            UpdateTurnOptions();
        }

        private float ActiveBet()
        {
            return activeHandIndex == 0 ? bet1 : bet2;
        }

        private List<CardData> ActiveHand()
        {
            return activeHandIndex == 0 ? playerHand : splitHand;
        }

        private Transform ActiveContainer()
        {
            if (activeHandIndex == 0) return isSplitMode ? splitLeftContainer : playerHandContainer;
            return splitRightContainer;
        }

        private void UpdateTurnOptions()
        {
            List<CardData> currentHand = ActiveHand();
            int handValue = CalculateHandValue(currentHand);
            int count = currentHand.Count;

            bool canDouble = count == 2 && handValue >= 9 && handValue <= 11;
            bool canSplit = !isSplitMode && count == 2 && playerHand[0].rank == playerHand[1].rank;

            // Передаємо ставку активної руки, щоб UI правильно перевіряв, чи вистачає балансу
            OnTurnOptionsUpdated?.Invoke(count, canSplit, canDouble, currentBalance, ActiveBet());
        }

        public void OnHitButton()
        {
            if (CurrentState != GameState.PlayerTurn) return;

            HitHand(ActiveHand(), ActiveContainer(), false);
            BroadcastScores();

            if (CalculateHandValue(ActiveHand()) > 21) HandleHandEnd();
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

            List<CardData> hand = ActiveHand();
            int value = CalculateHandValue(hand);
            if (hand.Count != 2 || value < 9 || value > 11) return;

            float handBet = ActiveBet();
            if (currentBalance < handBet) return;

            // Подвоюється лише ставка активної руки
            currentBalance -= handBet;
            currentBet += handBet;
            if (activeHandIndex == 0) bet1 += handBet;
            else bet2 += handBet;
            OnBalanceChanged?.Invoke(currentBalance, currentBet);

            HitHand(hand, ActiveContainer(), false);
            BroadcastScores();
            HandleHandEnd();
        }

        public void OnSplitButton()
        {
            if (CurrentState != GameState.PlayerTurn) return;
            if (isSplitMode) return;
            if (playerHand.Count != 2 || playerHand[0].rank != playerHand[1].rank) return;
            if (currentBalance < bet1) return;

            currentBalance -= bet1;
            bet2 = bet1;
            currentBet = bet1 + bet2;
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

        // ------------------------------------------------------------------
        // Хід дилера та розрахунок
        // ------------------------------------------------------------------

        private void StartDealerTurn()
        {
            ChangeState(GameState.DealerTurn);
            RevealDealerHiddenCard();
            BroadcastScores();

            bool hand1Bust = CalculateHandValue(playerHand) > 21;
            bool hand2Bust = isSplitMode && CalculateHandValue(splitHand) > 21;

            // Якщо всі руки програли перебором, дилеру тягнути карти не треба
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
            float totalWin = 0f;
            string msg;

            int p1 = CalculateHandValue(playerHand);
            string r1 = GetResult(p1, dScore);
            totalWin += CalculatePayout(r1, bet1);

            if (isSplitMode)
            {
                int p2 = CalculateHandValue(splitHand);
                string r2 = GetResult(p2, dScore);
                totalWin += CalculatePayout(r2, bet2);
                msg = $"1 рука - {r1}\n2 рука - {r2}";
            }
            else msg = r1;

            currentBalance += totalWin;
            FinishGame(msg);
        }

        private string GetResult(int p, int d)
        {
            if (p > 21) return "ПРОГРАШ";
            if (d <= 21 && d > p) return "ПРОГРАШ";
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