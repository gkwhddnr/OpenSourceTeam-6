using System.Collections.Generic;
using OpenSourceTeam6.Customers.Unity;
using UnityEngine;

namespace OpenSourceTeam6.Customers.Demo
{
    public sealed class NpcPopulationDemoPanel : MonoBehaviour
    {
        [SerializeField] private NpcPopulationController population;

        private readonly List<CheckoutWaitSnapshot> queue = new();
        private Vector2 scroll;
        private GUIStyle title;

        private void OnGUI()
        {
            if (population == null)
                return;

            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
                title.normal.textColor = Color.white;
            }
            float height = Mathf.Max(100, Mathf.Min(340, Screen.height - 24));
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

            DrawCount("Customers (red)", population.ActiveCustomers, population.CustomerTarget, true);
            DrawCount("Pedestrians (blue)", population.ActivePedestrians, population.PedestrianTarget, false);

            GUILayout.Space(5);
            GUILayout.Label("Checkout queue", title);
            population.GetQueueSnapshot(queue);
            if (queue.Count == 0)
                GUILayout.Label("No customers at checkout");
            foreach (CheckoutWaitSnapshot entry in queue)
            {
                string state = entry.IsCheckingOut ? "Checkout"
                    : entry.Position == 0 ? "Going to register" : $"#{entry.Position} in line";
                GUILayout.Label($"{state}   Customer {entry.CustomerId}   {entry.ElapsedSeconds:F1}s wait");
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
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
