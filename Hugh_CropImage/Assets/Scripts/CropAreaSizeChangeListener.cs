using UnityEngine;

namespace HughGame.UI.NCrop
{
	public class CropAreaSizeChangeListener : MonoBehaviour
	{
	    RectTransform _rectTransform;

		public System.Action<Vector2> onSizeChanged;

		public void Init()
		{
            _rectTransform = (RectTransform)transform;
            OnRectTransformDimensionsChange();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (onSizeChanged != null && _rectTransform != null)
                onSizeChanged(_rectTransform.rect.size);
        }
	}
}