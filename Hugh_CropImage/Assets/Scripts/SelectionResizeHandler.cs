using UnityEngine;
using UnityEngine.EventSystems;

namespace HughGame.UI.NCrop
{
	public class SelectionResizeHandler 
        : MonoBehaviour
        , IBeginDragHandler
        , IDragHandler
        , IEndDragHandler
        , ISelectHandler
	{
		const float SCROLL_DISTANCE = 70f;
		const float SELECTION_MAX_DISTANCE_FOR_SCROLL = 50f;


		[SerializeField]
        EDirection _direction;

		[SerializeField]
		EDirection _secondaryDirection = EDirection.None;
        
        EDirection _directions;
		EDirection _pivot;

		RectTransform _selectRectTrans;

		Vector2 _initialPosition;
		Vector2 _initialTouchPosition;

		Vector2 _initialSelectionPosition;
		Vector2 _initialSelectionSize;

		int _draggingPointer;
        PointerEventData _draggingPointerEventData;
        CropWindow _window;

        public void Init(CropWindow cropWindow)
		{
            _window = cropWindow;
            _selectRectTrans = _window.Selection;

            if (_direction == EDirection.None)
            {
                EDirection temp = _direction;
                _direction = _secondaryDirection;
                _secondaryDirection = temp;
            }

			_directions = _direction | _secondaryDirection;

			_pivot = EDirection.None;
            if ((_directions & EDirection.Left) == EDirection.Left)
                _pivot |= EDirection.Right;
            else if ((_directions & EDirection.Right) == EDirection.Right)
                _pivot |= EDirection.Left;

            if ((_directions & EDirection.Top) == EDirection.Top)
                _pivot |= EDirection.Bottom;
            else if ((_directions & EDirection.Bottom) == EDirection.Bottom)
                _pivot |= EDirection.Top;
		}

		public void StopModifySelectionWith()
		{
            if(_window != null)
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
            _draggingPointerEventData = eventData;

            if ((_directions & EDirection.Left) == EDirection.Left)
                _initialPosition.x = _selectRectTrans.anchoredPosition.x;
            else if ((_directions & EDirection.Right) == EDirection.Right)
                _initialPosition.x = _selectRectTrans.anchoredPosition.x + _selectRectTrans.sizeDelta.x;

            if ((_directions & EDirection.Top) == EDirection.Top)
                _initialPosition.y = _selectRectTrans.anchoredPosition.y + _selectRectTrans.sizeDelta.y;
            else if ((_directions & EDirection.Bottom) == EDirection.Bottom)
                _initialPosition.y = _selectRectTrans.anchoredPosition.y;

            _initialTouchPosition = _window.GetTouchPosition(eventData.pressPosition, eventData.pressEventCamera);

            _initialSelectionPosition = _selectRectTrans.anchoredPosition;
            _initialSelectionSize = _selectRectTrans.sizeDelta;
        }

		public void OnDrag( PointerEventData eventData )
        {
            if (_window == null)
                return;

            if (eventData.pointerId != _draggingPointer)
            {
                eventData.pointerDrag = null;
                return;
            }

            _draggingPointerEventData = eventData;

            Vector2 newPosition = _initialPosition + _window.GetTouchPosition(eventData.position, eventData.pressEventCamera) - _initialTouchPosition;
			Vector2 selectionPosition = _initialSelectionPosition;
			Vector2 selectionSize = _initialSelectionSize;

            if ((_directions & EDirection.Left) == EDirection.Left)
            {
                if (newPosition.x < _window.SelectionSnapToEdgeThreshold)
                    newPosition.x = 0f;

                selectionSize.x -= newPosition.x - selectionPosition.x;
                selectionPosition.x = newPosition.x;
            }
            else if ((_directions & EDirection.Right) == EDirection.Right)
            {
                if (newPosition.x > _window.OrientedImageSize.x - _window.SelectionSnapToEdgeThreshold)
                    newPosition.x = _window.OrientedImageSize.x;

                selectionSize.x = newPosition.x - selectionPosition.x;
            }

            if ((_directions & EDirection.Top) == EDirection.Top)
            {
                if (newPosition.y > _window.OrientedImageSize.y - _window.SelectionSnapToEdgeThreshold)
                    newPosition.y = _window.OrientedImageSize.y;

                selectionSize.y = newPosition.y - selectionPosition.y;
            }
            else if ((_directions & EDirection.Bottom) == EDirection.Bottom)
            {
                if (newPosition.y < _window.SelectionSnapToEdgeThreshold)
                    newPosition.y = 0f;

                selectionSize.y -= newPosition.y - selectionPosition.y;
                selectionPosition.y = newPosition.y;
            }

			bool shouldExpand = false;
            if (_secondaryDirection == EDirection.None)
            {
                if (_direction == EDirection.Left || _direction == EDirection.Right)
                {
                    if (selectionSize.x > _initialSelectionSize.x)
                        shouldExpand = true;
                }
                else
                {
                    if (selectionSize.y > _initialSelectionSize.y)
                        shouldExpand = true;
                }
            }

			_window.UpdateSelection( selectionPosition, selectionSize, _pivot, !shouldExpand );
		}

		public void OnEndDrag( PointerEventData eventData )
        {
            if (_window == null)
                return;

            if (eventData.pointerId == _draggingPointer)
            {
                _draggingPointerEventData = null;
                _window.StopModifySelectionWith(this);
            }
		}

		public void OnUpdate()
        {
            if (_window == null)
                return;

            if (_draggingPointerEventData == null)
                return;

			Vector2 pointerLocalPos;
			RectTransformUtility.ScreenPointToLocalPointInRectangle( _window.Viewport, _draggingPointerEventData.position, _draggingPointerEventData.pressEventCamera, out pointerLocalPos );

			bool shouldUpdateViewport = false;
			float scale = _window.ImageHolder.localScale.z;

			Vector2 imagePosition = _window.ImageHolder.anchoredPosition;
			Vector2 selectionBottomLeft = imagePosition + _selectRectTrans.anchoredPosition * scale;
			Vector2 selectionTopRight = selectionBottomLeft + _selectRectTrans.sizeDelta * scale;

			Vector2 viewportSize = _window.ViewportSize;

            if ((_directions & EDirection.Left) == EDirection.Left || (_directions & EDirection.Right) == EDirection.Right)
            {
                if (pointerLocalPos.x <= SCROLL_DISTANCE && selectionBottomLeft.x <= SELECTION_MAX_DISTANCE_FOR_SCROLL)
                {
                    imagePosition = _window.ScrollImage(imagePosition, EDirection.Left);
                    shouldUpdateViewport = true;
                }
                else if (pointerLocalPos.x >= viewportSize.x - SCROLL_DISTANCE && selectionTopRight.x >= viewportSize.x - SELECTION_MAX_DISTANCE_FOR_SCROLL)
                {
                    imagePosition = _window.ScrollImage(imagePosition, EDirection.Right);
                    shouldUpdateViewport = true;
                }
            }

            if ((_directions & EDirection.Bottom) == EDirection.Bottom || (_directions & EDirection.Top) == EDirection.Top)
            {
                if (pointerLocalPos.y <= SCROLL_DISTANCE && selectionBottomLeft.y <= SELECTION_MAX_DISTANCE_FOR_SCROLL)
                {
                    imagePosition = _window.ScrollImage(imagePosition, EDirection.Bottom);
                    shouldUpdateViewport = true;
                }
                else if (pointerLocalPos.y >= viewportSize.y - SCROLL_DISTANCE && selectionTopRight.y >= viewportSize.y - SELECTION_MAX_DISTANCE_FOR_SCROLL)
                {
                    imagePosition = _window.ScrollImage(imagePosition, EDirection.Top);
                    shouldUpdateViewport = true;
                }
            }

            if (shouldUpdateViewport)
            {
                _window.ImageHolder.anchoredPosition = imagePosition;
                OnDrag(_draggingPointerEventData);
            }
		}

		public void Stop()
		{
			_draggingPointer--;
            _draggingPointerEventData = null;
		}
	}
}