using System.Collections.Generic;
using OpenSourceTeam6.Customers.Unity;
using UnityEngine;

namespace OpenSourceTeam6.Customers.Demo
{
    public sealed class NpcPopulationDemoPanel : MonoBehaviour
    {
        [SerializeField] private NpcPopulationController population;
        [SerializeField] private NpcEmployeeAnimation employee;
        [SerializeField] private NpcEmployeeAnimation cashier;
        [SerializeField] private bool autoPointOnQuestion;

        private readonly List<CheckoutWaitSnapshot> queue = new();
        private Vector2 scroll;
        private GUIStyle title;
        private GUIStyle questionLabel;
        private string latestQuestion;
        private Vector3 standingPosition;
        private Quaternion standingRotation;
        private int employeeCheckoutCustomer = -1;
        private bool employeeCheckoutFinished;

        private void Awake()
        {
            if (employee == null) return;
            standingPosition = employee.transform.position;
            standingRotation = employee.transform.rotation;
        }

        private void OnEnable()
        {
            if (population != null)
                population.CustomerAskedForProduct += ShowQuestion;
            if (cashier != null)
                cashier.CheckoutAnimationFinished += OnEmployeeCheckoutFinished;
        }

        private void OnDisable()
        {
            if (population != null)
                population.CustomerAskedForProduct -= ShowQuestion;
            if (cashier != null)
                cashier.CheckoutAnimationFinished -= OnEmployeeCheckoutFinished;

            latestQuestion = null;
            employeeCheckoutCustomer = -1;
            employeeCheckoutFinished = false;
        }

        private void OnEmployeeCheckoutFinished() => employeeCheckoutFinished = employeeCheckoutCustomer >= 0;

        private void Update()
        {
            if (employeeCheckoutCustomer < 0) return;
            if (employeeCheckoutFinished && population.IsPaused) return;
            if (employeeCheckoutFinished)
                population.TryCompleteCurrentCheckout(employeeCheckoutCustomer);
            else if (cashier != null && cashier.isActiveAndEnabled && cashier.CurrentState == NpcAnimationState.Checkout)
                return;

            employeeCheckoutCustomer = -1;
            employeeCheckoutFinished = false;
        }

        private void ShowQuestion(int customerId, string productName, Transform target)
        {
            if (target == null)
                return;

            latestQuestion = $"Customer {customerId} → {target.name}\nWhere is {productName}?";
            if (autoPointOnQuestion && employee != null && target == employee.transform)
                employee.Play(NpcAnimationState.Point);
        }

        private void OnGUI()
        {
            if (population == null)
                return;

            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
                title.normal.textColor = Color.white;
                questionLabel = new GUIStyle(GUI.skin.label) { wordWrap = true };
            }
            float height = Mathf.Max(100, Mathf.Min(410, Screen.height - 24));
            GUILayout.BeginArea(new Rect(12, 12, Mathf.Min(310, Screen.width - 24), height), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("NPC sandbox", title);
            GUILayout.Label($"Day {population.Day}  |  Active / target");
            GUILayout.BeginHorizontal();
            using (new GuiEnabledScope(population.Day > 1))
                if (GUILayout.Button("< Prev day"))
                    population.SetDay(population.Day - 1);
            using (new GuiEnabledScope(population.Day < 7))
                if (GUILayout.Button("Next day >"))
                    population.SetDay(population.Day + 1);
            GUILayout.EndHorizontal();

            DrawCount("Customers", population.ActiveCustomers, population.CustomerTarget, true);
            DrawCount("Pedestrians", population.ActivePedestrians, population.PedestrianTarget, false);

            GUILayout.Space(5);
            GUILayout.Label("Movement speed (m/s)");
            DrawSpeed("Customers", population.CustomerSpeed, true);
            DrawSpeed("Pedestrians", population.PedestrianSpeed, false);

            if (employee != null)
                DrawEmployeeControls();

            if (!string.IsNullOrEmpty(latestQuestion))
            {
                GUILayout.Space(5);
                GUILayout.Label("Latest product question", title);
                GUILayout.Label(latestQuestion, questionLabel);
            }

            GUILayout.Space(5);
            GUILayout.Label("Checkout queue", title);
            if (population.WaitForExternalCheckout)
                DrawCheckoutControls();
            population.GetQueueSnapshot(queue);
            if (queue.Count == 0)
                GUILayout.Label("No customers at checkout");
            foreach (CheckoutWaitSnapshot entry in queue)
            {
                string state = entry.IsCheckingOut ? "Checkout"
                    : entry.Position == 0 ? "Register" : $"#{entry.Position} in line";
                GUILayout.Label($"{state}   Customer {entry.CustomerId}   {entry.ElapsedSeconds:F1}s wait");
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawCheckoutControls()
        {
            bool ready = population.TryGetCurrentCheckout(out int customerId, out _) &&
                !population.IsPaused && employeeCheckoutCustomer < 0;
            using (new GuiEnabledScope(ready))
            {
                if (GUILayout.Button("Complete player checkout"))
                    population.TryCompleteCurrentCheckout(customerId);
            }
            using (new GuiEnabledScope(ready && cashier != null))
            {
                if (GUILayout.Button("Start employee checkout"))
                    TryStartEmployeeCheckout();
            }
        }

        public bool TryStartEmployeeCheckout()
        {
            if (!isActiveAndEnabled || population == null || population.IsPaused || employeeCheckoutCustomer >= 0 ||
                !population.TryGetCurrentCheckout(out int customerId, out _) ||
                cashier == null || !cashier.CanPlay(NpcAnimationState.Checkout) ||
                !cashier.Play(NpcAnimationState.Checkout))
                return false;

            employeeCheckoutCustomer = customerId;
            return true;
        }

        private void DrawEmployeeControls()
        {
            GUILayout.Space(5);
            GUILayout.Label("Employee animation", title);
            GUILayout.Label($"Current: {employee.CurrentState}");
            GUILayout.BeginHorizontal();
            DrawEmployeeButton("Idle", NpcAnimationState.Idle);
            DrawEmployeeButton("Stock", NpcAnimationState.Stock);
            DrawEmployeeButton("Clean", NpcAnimationState.Clean);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            DrawEmployeeButton("Rest", NpcAnimationState.Rest);
            DrawEmployeeButton("Point", NpcAnimationState.Point);
            DrawEmployeeButton("Anger", NpcAnimationState.Anger);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            DrawEmployeeButton("POS", NpcAnimationState.Checkout);
            if (GUILayout.Button("Sit")) employee.SitAtConfiguredSeat();
            if (GUILayout.Button("Take trash to bin"))
            {
                employee.transform.SetPositionAndRotation(standingPosition, standingRotation);
                employee.PreviewTrashDisposal();
            }
            GUILayout.EndHorizontal();
            autoPointOnQuestion = GUILayout.Toggle(autoPointOnQuestion, "Point when asked about a product");
        }

        private void DrawEmployeeButton(string label, NpcAnimationState state)
        {
            if (!GUILayout.Button(label)) return;
            employee.transform.SetPositionAndRotation(standingPosition, standingRotation);
            employee.Play(state);
        }

        private void DrawCount(string label, int active, int target, bool isCustomer)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}  {active}/{target}", GUILayout.ExpandWidth(true));
            using (new GuiEnabledScope(target > 0))
                if (GUILayout.Button("-", GUILayout.Width(30)))
                    AdjustCount(isCustomer, -1);
            int maximum = isCustomer ? population.MaxCustomers : population.MaxPopulation;
            using (new GuiEnabledScope(target < maximum))
                if (GUILayout.Button("+", GUILayout.Width(30)))
                    AdjustCount(isCustomer, 1);
            GUILayout.EndHorizontal();
        }

        private void AdjustCount(bool isCustomer, int delta)
        {
            if (isCustomer)
                population.AdjustCustomerTarget(delta);
            else
                population.AdjustPedestrianTarget(delta);
        }

        private void DrawSpeed(string label, float current, bool isCustomer)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(82));
            float value = GUILayout.HorizontalSlider(current, NpcPopulationController.MinimumMoveSpeed,
                NpcPopulationController.MaximumMoveSpeed, GUILayout.ExpandWidth(true));
            GUILayout.Label(current.ToString("F1"), GUILayout.Width(28));
            GUILayout.EndHorizontal();
            if (!Mathf.Approximately(value, current))
            {
                if (isCustomer)
                    population.SetCustomerSpeed(value);
                else
                    population.SetPedestrianSpeed(value);
            }
        }

        private readonly struct GuiEnabledScope : System.IDisposable
        {
            private readonly bool previous;

            public GuiEnabledScope(bool enabled)
            {
                previous = GUI.enabled;
                GUI.enabled = previous && enabled;
            }

            public void Dispose() => GUI.enabled = previous;
        }
    }
}
