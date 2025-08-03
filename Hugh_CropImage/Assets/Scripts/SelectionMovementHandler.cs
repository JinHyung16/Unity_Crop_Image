using UnityEngine;
using UnityEngine.EventSystems;

namespace HughGame.UI.NCrop
{
    public class SelectionMovementHandler
        : MonoBehaviour
        , IBeginDragHandler
        , IDragHandler
        , IEndDragHandler
        , ISelectHandler
    {
        const float SCROLL_DISTANCE = 5f;

        RectTransform _selectRectTrans;

        Vector2 _initialPosition;
        Vector2 _initialTouchPosition;

        int _draggingPointer;

        CropWindow _window;

        public void Init(CropWindow cropWindow)
        {
            _window = cropWindow;
            _selectRectTrans = cropWindow.Selection;
        }

        public void StopModifySelectionWith()
        {
            if (_window != null)
                _window.StopModifySelectionWith(this);
        }


        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_window == null)
                return;

            if (_window.CanModifySelectionWith(this) == false)
            {
                eventData.pointerDrag = null;
                return;
            }

            _draggingPointer = eventData.pointerId;

            _initialPosition = _selectRectTrans.anchoredPosition;
            _initialTouchPosition = _window.GetTouchPosition(eventData.pressPosition, eventData.pressEventCamera);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_window == null)
                return;

            if (eventData.pointerId != _draggingPointer)
            {
                eventData.pointerDrag = null;
                return;
            }

            _window.UpdateSelection(_initialPosition + _window.GetTouchPosition(eventData.position, eventData.pressEventCamera) - _initialTouchPosition);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_window == null)
                return;

            if (eventData.pointerId == _draggingPointer)
                _window.StopModifySelectionWith(this);
        }

        public void OnUpdate()
        {
            if (_window == null)
                return;

            bool shouldUpdateViewport = false;
            float scale = _window.ImageHolder.localScale.z;

            Vector2 imagePosition = _window.ImageHolder.anchoredPosition;
            Vector2 selectionBottomLeft = imagePosition + _selectRectTrans.anchoredPosition * scale;
            Vector2 selectionTopRight = selectionBottomLeft + _selectRectTrans.sizeDelta * scale;
            Vector2 selectionSize = selectionTopRight - selectionBottomLeft;

            Vector2 viewportSize = _window.ViewportSize;

            if (selectionBottomLeft.x <= SCROLL_DISTANCE)
            {
                imagePosition = _window.ScrollImage(imagePosition, EDirection.Left);
                selectionBottomLeft.x = 0f;

                shouldUpdateViewport = true;
            }
            else if (selectionTopRight.x >= viewportSize.x - SCROLL_DISTANCE)
            {
                imagePosition = _window.ScrollImage(imagePosition, EDirection.Right);
                selectionBottomLeft.x = viewportSize.x - selectionSize.x;

                shouldUpdateViewport = true;
            }

            if (selectionBottomLeft.y <= SCROLL_DISTANCE)
            {
                imagePosition = _window.ScrollImage(imagePosition, EDirection.Bottom);
                selectionBottomLeft.y = 0f;

                shouldUpdateViewport = true;
            }
            else if (selectionTopRight.y >= viewportSize.y - SCROLL_DISTANCE)
            {
                imagePosition = _window.ScrollImage(imagePosition, EDirection.Top);
                selectionBottomLeft.y = viewportSize.y - selectionSize.y;

                shouldUpdateViewport = true;
            }

            if (shouldUpdateViewport)
            {
                _window.ImageHolder.anchoredPosition = imagePosition;
                _window.UpdateSelection((selectionBottomLeft - imagePosition) / scale);
            }
        }

        public void Stop()
        {
            _draggingPointer--;
        }
    }
}