using UnityEngine;

namespace HughGame.UI.NCrop
{
	public class SelectionGraphicsSynchronizer : MonoBehaviour
	{
		[SerializeField]
		RectTransform selectionBottomLeft;

		[SerializeField]
		RectTransform selectionTopRight;

		RectTransform _viewport;
		RectTransform _selectionGraphics;

		Vector2 _bottomLeftPrevPosition, _topRightPrevPosition;
        CropWindow _window;

        public void Init(CropWindow cropWindow)
        {
            _window = cropWindow;
            _viewport = cropWindow.Viewport;
            _selectionGraphics = _window.SelectionGraphics;

            _bottomLeftPrevPosition = selectionBottomLeft.position;
            _topRightPrevPosition = selectionTopRight.position;
            Synchronize(selectionBottomLeft.position, selectionTopRight.position);
        }

        public void Synchronize()
        {
            if (_window == null)
                return;

            Vector2 bottomLeftPosition = selectionBottomLeft.position;
            Vector2 topRightPosition = selectionTopRight.position;

            if (bottomLeftPosition != _bottomLeftPrevPosition || topRightPosition != _topRightPrevPosition)
                Synchronize(bottomLeftPosition, topRightPosition);
        }

        void Synchronize(Vector2 bottomLeft, Vector2 topRight)
        {
            Vector2 position = _viewport.InverseTransformPoint(bottomLeft);
            Vector2 size = (Vector2)_viewport.InverseTransformPoint(topRight) - position;

            _selectionGraphics.anchoredPosition = position;
            _selectionGraphics.sizeDelta = size;

            _bottomLeftPrevPosition = bottomLeft;
            _topRightPrevPosition = topRight;
        }
	}
}