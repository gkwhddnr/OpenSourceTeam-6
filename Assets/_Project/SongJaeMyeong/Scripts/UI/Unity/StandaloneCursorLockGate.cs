using System;
using OpenSourceTeam6.UI.Core;
using UnityEngine;

namespace OpenSourceTeam6.UI.Unity
{
    /// <summary>
    /// 개인 검증 씬(단독 실행)에서 외부 PlayerRigController가 주입되지 않았을 때만 사용하는 폴백 잠금 게이트.
    /// 공용 통합 씬에서 외부 ICursorLockGate 어댑터가 연결되면 본 컴포넌트는 커서를 직접 변경하지 않는다.
    /// </summary>
    public sealed class StandaloneCursorLockGate : MonoBehaviour, ICursorLockGate
    {
        [SerializeField] private bool applyUnityCursorState = true;

        private readonly NestedLockCounter counter = new NestedLockCounter();

        public bool IsLocked => counter.IsLocked;

        private void OnEnable()
        {
            counter.LockStateChanged += OnLockStateChanged;
        }

        private void OnDisable()
        {
            counter.LockStateChanged -= OnLockStateChanged;
            counter.ReleaseAll();
        }

        public IDisposable AcquireLock(string reason)
        {
            return counter.Acquire(reason);
        }

        public void SetApplyUnityCursorState(bool enabledState)
        {
            applyUnityCursorState = enabledState;
        }

        private void OnLockStateChanged(bool uiOwnsInput)
        {
            if (!applyUnityCursorState) return;

            if (uiOwnsInput)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
