using OpenSourceTeam6.Customers.Unity;
using OpenSourceTeam6.UI.Core;
using OpenSourceTeam6.UI.Unity;
using UnityEngine;

namespace EmployeeVoice
{
    // Keep the team's NPC simulation inactive outside a playable day.
    [DefaultExecutionOrder(100)]
    public sealed class IntegratedNpcSession : MonoBehaviour
    {
        [SerializeField] private StoreUiSessionController ui;
        [SerializeField] private NpcPopulationController population;
        private bool? lastPaused;
        private int lastDay;
        private StoreInventoryManager inventory;

        private void Start()
        {
            foreach (StoreInventoryManager candidate in FindObjectsByType<StoreInventoryManager>())
                if (candidate.gameObject.scene == gameObject.scene) { inventory = candidate; break; }
        }

        // The team inventory currently tracks counts, not a separate storeroom or shelf visuals.
        public bool TryRestock(string target, out string itemName)
        {
            itemName = null;
            if (inventory == null || !TryCategory(target, out StoreItemCategory category)) return false;
            StoreItemRecord chosen = null;
            foreach (StoreItemRecord item in inventory.Ledger.Items)
                if (item.Category == category && (chosen == null || item.CurrentStock < chosen.CurrentStock))
                    chosen = item;
            if (chosen == null || !inventory.RecordRestock(chosen.ItemId, 3)) return false;
            itemName = chosen.DisplayName;
            return true;
        }

        private static bool TryCategory(string target, out StoreItemCategory category)
        {
            switch (target)
            {
                case "SNACK": category = StoreItemCategory.Snack; return true;
                case "RAMEN": category = StoreItemCategory.Ramen; return true;
                case "DAILY_GOODS": category = StoreItemCategory.Daily; return true;
                case "ICE_CREAM": category = StoreItemCategory.IceCream; return true;
                case "WALK_IN": category = StoreItemCategory.Drink; return true;
                case "HOT_FOOD": category = StoreItemCategory.FreshFood; return true;
                default: category = default; return false;
            }
        }

        private void Update()
        {
            if (ui == null || population == null) return;
            bool paused = ui.CurrentMode != UiScreenMode.InGame || ui.IsPauseMenuOpen ||
                ui.IsSettingsOpen || ui.IsTransitionFading;
            if (lastPaused != paused)
            {
                population.SetPaused(paused);
                lastPaused = paused;
            }
            int day = Mathf.Max(1, ui.CurrentDayNumber);
            if (lastDay != day)
            {
                population.SetDay(day);
                lastDay = day;
            }
        }
    }
}
