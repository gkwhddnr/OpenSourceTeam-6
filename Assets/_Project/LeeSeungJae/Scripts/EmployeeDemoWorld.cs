using System;
using System.Collections;
using UnityEngine;

namespace EmployeeVoice
{
    // Runtime-only demo objects; the eventual game owns its own employee and table assets.
    public sealed class DemoTable : MonoBehaviour
    {
        public int Number { get; private set; }
        public bool IsRemoved { get; private set; }
        public bool IsClean { get; private set; }
        private EmployeeDemoActor owner;
        private Renderer tableRenderer;
        private MaterialPropertyBlock colors;

        public void Initialize(int number)
        {
            Number = number;
            tableRenderer = GetComponent<Renderer>();
            colors = new MaterialPropertyBlock();
            SetColor(new Color(0.55f, 0.29f, 0.12f));
        }

        public bool TryReserve(EmployeeDemoActor actor)
        {
            if (actor == null || IsRemoved || owner != null) return false;
            owner = actor;
            return true;
        }

        public void Release(EmployeeDemoActor actor)
        {
            if (owner == actor) owner = null;
        }

        public bool Complete(EmployeeDemoActor actor, EmployeeAction action)
        {
            if (owner != actor || IsRemoved) return false;
            if (action == EmployeeAction.CLEAN_TABLE)
            {
                IsClean = true;
                SetColor(new Color(0.35f, 0.75f, 0.48f));
            }
            else if (action == EmployeeAction.THROW_AWAY_TABLE)
            {
                IsRemoved = true;
                transform.position = new Vector3(6f, 0.4f, 1.5f);
                SetColor(new Color(0.35f, 0.35f, 0.35f));
            }
            else return false;
            Release(actor);
            return true;
        }

        private void SetColor(Color color)
        {
            tableRenderer.GetPropertyBlock(colors);
            colors.SetColor("_BaseColor", color);
            colors.SetColor("_Color", color);
            tableRenderer.SetPropertyBlock(colors);
        }
    }

    public sealed class EmployeeDemoActor : MonoBehaviour
    {
        public EmployeePersonality Personality { get; private set; }
        public bool IsBusy { get; private set; }
        private DemoTable reservedTable;
        private Coroutine currentWork;
        private Action<string> onStatus;
        private Action<EmployeeDemoActor, EmployeeAction> onCompleted;

        public void Initialize(EmployeePersonality personality, Action<string> status, Action<EmployeeDemoActor, EmployeeAction> completed)
        {
            Personality = personality ?? throw new ArgumentNullException(nameof(personality));
            onStatus = status;
            onCompleted = completed;
        }

        public bool TryStart(EmployeeAction action, DemoTable table)
        {
            if (IsBusy || table == null || table.IsRemoved || !EmployeeVoiceDemo.IsTableAction(action)) return false;
            if ((action == EmployeeAction.CLEAN_TABLE || action == EmployeeAction.THROW_AWAY_TABLE) && !table.TryReserve(this)) return false;
            reservedTable = action == EmployeeAction.CLEAN_TABLE || action == EmployeeAction.THROW_AWAY_TABLE ? table : null;
            IsBusy = true;
            currentWork = StartCoroutine(Perform(action, table));
            return true;
        }

        private IEnumerator Perform(EmployeeAction action, DemoTable table)
        {
            onStatus?.Invoke(Personality.employeeName + " started " + action + " for table " + table.Number);
            if (reservedTable != null)
            {
                Vector3 destination = table.transform.position + new Vector3(-1.2f, 0.6f, 0f);
                while (table != null && reservedTable != null && (transform.position - destination).sqrMagnitude > 0.0025f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, destination, 2f * Time.deltaTime);
                    yield return null;
                }
                yield return new WaitForSeconds(0.7f);
                if (table == null || !table.Complete(this, action))
                    onStatus?.Invoke(Personality.employeeName + " could not finish the table task.");
                else
                {
                    onStatus?.Invoke(Personality.employeeName + " completed " + action + " for table " + table.Number);
                    onCompleted?.Invoke(this, action);
                }
            }
            else
            {
                yield return new WaitForSeconds(0.7f);
                onStatus?.Invoke(Personality.employeeName + " reacted: " + action);
                onCompleted?.Invoke(this, action);
            }
            Finish();
        }

        private void Finish()
        {
            if (reservedTable != null) reservedTable.Release(this);
            reservedTable = null;
            currentWork = null;
            IsBusy = false;
        }

        private void OnDisable()
        {
            if (currentWork != null) StopCoroutine(currentWork);
            Finish();
        }
    }
}
