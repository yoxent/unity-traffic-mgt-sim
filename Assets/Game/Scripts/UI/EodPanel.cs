using System.Text;
using TrafficSim.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TrafficSim.UI
{
    public sealed class EodPanel : MonoBehaviour
    {
        [SerializeField] GameObject _root;
        [SerializeField] UiTextRef _summaryText;
        [SerializeField] UiTextRef _queueText;
        [SerializeField] Button _continueButton;
        [SerializeField] Toggle _skipToggle;

        RunState _state;
        EodController _eod;

        public void Bind(RunState state, EodController eod)
        {
            if (_eod != null)
                _eod.EodStarted -= OnEodStarted;

            _state = state;
            _eod = eod;

            if (_eod != null)
                _eod.EodStarted += OnEodStarted;
        }

        void Awake()
        {
            if (_root == null)
                _root = gameObject;

            if (_continueButton != null)
                _continueButton.onClick.AddListener(OnContinueClicked);

            if (_skipToggle != null)
                _skipToggle.onValueChanged.AddListener(OnSkipToggleChanged);
        }

        void Start()
        {
            ConfigureSkipToggle();
            SetVisible(false);
        }

        void OnDestroy()
        {
            if (_eod != null)
                _eod.EodStarted -= OnEodStarted;

            if (_continueButton != null)
                _continueButton.onClick.RemoveListener(OnContinueClicked);

            if (_skipToggle != null)
                _skipToggle.onValueChanged.RemoveListener(OnSkipToggleChanged);
        }

        void OnEodStarted()
        {
            if (_state == null)
                return;

            var show = _state.Phase == RunPhase.EodIntervention || _state.Phase == RunPhase.Failed;
            SetVisible(show);
            Refresh();
        }

        void OnContinueClicked()
        {
            if (_state == null || _eod == null)
                return;

            if (_state.Phase == RunPhase.Failed)
            {
                SetVisible(false);
                return;
            }

            _eod.AdvanceDay();
            SetVisible(false);
        }

        void OnSkipToggleChanged(bool enabled)
        {
            if (!TutorialSaveStub.CanSkipEodUi)
                return;

            TutorialSaveStub.SetSkipEodUi(enabled);
        }

        void ConfigureSkipToggle()
        {
            if (_skipToggle == null)
                return;

            _skipToggle.interactable = TutorialSaveStub.CanSkipEodUi;
            _skipToggle.isOn = TutorialSaveStub.ShouldSkipEodUi;
        }

        void Refresh()
        {
            if (_state == null)
                return;

            _summaryText?.SetText(BuildSummary());
            _queueText?.SetText(BuildQueueSummary());

            if (_continueButton != null)
                _continueButton.interactable = _state.Phase != RunPhase.Failed;

            ConfigureSkipToggle();
        }

        string BuildSummary()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Day {_state.DayIndex + 1} Summary");
            builder.AppendLine($"Stars: {_state.CurrentStars:F1}");
            builder.AppendLine($"Money: ${Mathf.FloorToInt(_state.Money)}");
            builder.AppendLine($"Jobs completed: {_state.SuccessfulJobs}");
            builder.Append($"Profit: ${Mathf.FloorToInt(_state.CumulativeProfit)}");

            if (_state.Phase == RunPhase.Failed)
                builder.AppendLine().Append("Run failed.");

            return builder.ToString();
        }

        string BuildQueueSummary()
        {
            var pending = _eod?.Queue?.Pending;
            if (pending == null || pending.Count == 0)
                return "Queued actions: none";

            var builder = new StringBuilder("Queued actions:");
            for (var i = 0; i < pending.Count; i++)
            {
                builder.AppendLine();
                builder.Append($"• ${Mathf.FloorToInt(pending[i].Cost)}");
            }

            return builder.ToString();
        }

        void SetVisible(bool visible)
        {
            if (_root != null)
                _root.SetActive(visible);
        }
    }
}
