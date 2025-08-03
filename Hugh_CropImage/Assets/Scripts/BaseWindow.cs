using System;
using UnityEngine;

namespace HughGame.UI
{
    public enum EWindowState
    {
        Closed = 0,
        Opened,
    }

    public class BaseWindow : MonoBehaviour
    {
        public Canvas Canvas;

        EWindowState _windowState;

        private void Awake()
        {
            if (Canvas == null)
                Canvas = GetComponent<Canvas>();
        }

        public void OpenInternal(Action enableBefore, Action enableAfter = null)
        {
            enableBefore?.Invoke();
            this.gameObject.SetActive(true);
            enableAfter?.Invoke();
            SetWindowState(EWindowState.Opened);
        }

        public void Close()
        {
            SetWindowState(EWindowState.Closed);
            OnClosed();
            this.gameObject.SetActive(false);
        }

        public bool IsOpend()
        {
            return _windowState != EWindowState.Closed;
        }

        protected virtual void OnClosed()
        {

        }

        protected void SetWindowState(EWindowState windowState)
        {
            _windowState = windowState;
        }
    }
}
