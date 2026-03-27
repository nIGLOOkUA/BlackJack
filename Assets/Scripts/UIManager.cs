using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Blackjack
{
    // Клас для керування графічним інтерфейсом користувача
    public class UIManager : MonoBehaviour
    {
        // Singleton екземпляр для глобального доступу до UI
        public static UIManager Instance { get; private set; }

        [Header("UI Layers")]
        [SerializeField] private CanvasGroup loginLayer; // Шар інтерфейсу входу/меню
        [SerializeField] private CanvasGroup gameLayer;  // Шар основного ігрового процесу

        [Header("Layouts")]
        [SerializeField] private GameObject singlePlayerLayout; // Розмітка для звичайної гри
        [SerializeField] private GameObject splitPlayerLayout;  // Розмітка для режиму Split (дві руки)

        [Header("Panels")]
        [SerializeField] private GameObject bettingPanel;   // Панель для здійснення ставок
        [SerializeField] private GameObject actionPanel;    // Панель дій гравця (Hit, Stand тощо)
        [SerializeField] private GameObject resultPanel;    // Панель результатів раунду
        [SerializeField] private GameObject insurancePanel; // Панель пропозиції страховки

        [Header("Text Elements")]
        [SerializeField] private TextMeshProUGUI result;            // Текст повідомлення про виграш/програш
        [SerializeField] private TextMeshProUGUI[] playerBalance;   // Масив полів для відображення балансу
        [SerializeField] private TextMeshProUGUI currentBet;        // Поле поточної ставки
        [SerializeField] private TextMeshProUGUI playerScore;       // Очки гравця (основна рука)
        [SerializeField] private TextMeshProUGUI playerSplitScore1; // Очки першої руки (режим Split)
        [SerializeField] private TextMeshProUGUI playerSplitScore2; // Очки другої руки (режим Split)
        [SerializeField] private TextMeshProUGUI dealerScore;       // Очки дилера

        [Header("Buttons")]
        [SerializeField] private Button doubleButton; // Кнопка подвоєння ставки
        [SerializeField] private Button splitButton;  // Кнопка розділення карт

        private void Awake()
        {
            // Реалізація патерну Singleton
            if (Instance != null && Instance != this) Destroy(gameObject);
            else Instance = this;
        }

        private void Start()
        {
            ShowLogin(); // При старті показуємо екран входу
        }

        // Підписка на події GameManager при активації об'єкта
        private void OnEnable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnBalanceChanged += UpdateBalance;
                GameManager.Instance.OnScoreChanged += UpdateScores;
                GameManager.Instance.OnStateChanged += UpdateGameStateUI;
                GameManager.Instance.OnGameResult += ShowResult;
                GameManager.Instance.OnTurnOptionsUpdated += UpdateActionButtons;
                GameManager.Instance.OnSplitStateChanged += UpdateSplitLayout;
            }
        }

        // Відписка від подій при деактивації для уникнення витоків пам'яті
        private void OnDisable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnBalanceChanged -= UpdateBalance;
                GameManager.Instance.OnScoreChanged -= UpdateScores;
                GameManager.Instance.OnStateChanged -= UpdateGameStateUI;
                GameManager.Instance.OnGameResult -= ShowResult;
                GameManager.Instance.OnTurnOptionsUpdated -= UpdateActionButtons;
                GameManager.Instance.OnSplitStateChanged -= UpdateSplitLayout;
            }
        }

        // Обробка натискання кнопки "Прийняти страховку"
        public void OnAcceptInsurance()
        {
            GameManager.Instance.OnInsuranceAccept();
        }

        // Обробка натискання кнопки "Відхилити страховку"
        public void OnDeclineInsurance()
        {
            GameManager.Instance.OnInsuranceDecline();
        }

        // Перемикання візуального макету між звичайним режимом та Split
        private void UpdateSplitLayout(bool isSplitMode)
        {
            if (singlePlayerLayout != null)
            {
                if (isSplitMode) singlePlayerLayout.SetActive(false);
                else singlePlayerLayout.SetActive(true);
            }
            if (splitPlayerLayout != null)
            {
                if (isSplitMode) splitPlayerLayout.SetActive(true);
                else splitPlayerLayout.SetActive(false);
            }
        }

        // Оновлення текстових полів балансу та ставки
        private void UpdateBalance(float balance, float bet)
        {
            if (currentBet != null) currentBet.text = bet.ToString("0.##");
            if (playerBalance != null)
            {
                foreach (var textObj in playerBalance)
                    if (textObj != null) textObj.text = balance.ToString("0.##");
            }
        }

        // Оновлення відображення очок гравця та дилера
        private void UpdateScores(int pScore1, int pScore2, int dScore)
        {
            // Оновлення очок основної руки
            if (playerScore)
            {
                if (pScore1 > 0) playerScore.text = pScore1.ToString();
                else playerScore.text = "";
            }
            // Оновлення очок для лівої руки (Split)
            if (playerSplitScore1)
            {
                if (pScore1 > 0) playerSplitScore1.text = pScore1.ToString();
                else playerSplitScore1.text = "";
            }
            // Оновлення очок для правої руки (Split)
            if (playerSplitScore2)
            {
                if (pScore2 > 0) playerSplitScore2.text = pScore2.ToString();
                else playerSplitScore2.text = "";
            }
            // Оновлення очок дилера
            if (dealerScore)
            {
                if (dScore > 0) dealerScore.text = dScore.ToString();
                else dealerScore.text = "";
            }
        }

        // Відображення панелі результатів з повідомленням
        private void ShowResult(string message)
        {
            if (resultPanel) resultPanel.SetActive(true);
            if (result) result.text = message;
        }

        // Керування активністю панелей залежно від стану гри (State Machine UI)
        private void UpdateGameStateUI(GameState state)
        {
            // Скидання стану панелей
            if (bettingPanel) bettingPanel.SetActive(false);
            if (actionPanel) actionPanel.SetActive(false);
            if (insurancePanel) insurancePanel.SetActive(false);

            // Активація потрібної панелі
            switch (state)
            {
                case GameState.Betting:
                    if (bettingPanel) bettingPanel.SetActive(true);
                    if (resultPanel) resultPanel.SetActive(false);
                    break;
                case GameState.Dealing:
                    break;
                case GameState.Insurance:
                    if (insurancePanel) insurancePanel.SetActive(true);
                    break;
                case GameState.PlayerTurn:
                    if (actionPanel) actionPanel.SetActive(true);
                    break;
                case GameState.GameOver:
                    if (resultPanel) resultPanel.SetActive(true);
                    break;
            }
        }

        // Оновлення доступності кнопок Double та Split (Валідація)
        private void UpdateActionButtons(int cardCount, bool canSplit, bool canDouble, float balance, float bet)
        {
            // Перевірка умов для кнопки Double (правила гри + наявність коштів)
            if (doubleButton)
            {
                if (canDouble)
                {
                    if (balance >= bet) doubleButton.interactable = true;
                    else doubleButton.interactable = false;
                }
                else doubleButton.interactable = false;
            }

            // Перевірка умов для кнопки Split
            if (splitButton)
            {
                if (canSplit)
                {
                    if (balance >= bet) splitButton.interactable = true;
                    else splitButton.interactable = false;
                }
                else splitButton.interactable = false;
            }
        }

        // Показати екран входу
        public void ShowLogin()
        {
            SetLayerActive(loginLayer, true);
            SetLayerActive(gameLayer, false);
        }

        // Почати гру (перехід до ігрового столу)
        public void StartGame()
        {
            SetLayerActive(loginLayer, false);
            SetLayerActive(gameLayer, true);
            GameManager.Instance.StartBettingPhase();
        }

        // Допоміжний метод для керування видимістю та інтерактивністю CanvasGroup
        private void SetLayerActive(CanvasGroup layer, bool isActive)
        {
            if (layer == null) return;
            if (isActive) layer.alpha = 1;
            else layer.alpha = 0;
            layer.interactable = isActive;
            layer.blocksRaycasts = isActive;
            layer.gameObject.SetActive(isActive);
        }
    }
}