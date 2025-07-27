using UnityEngine;

namespace HughGame.UI.NCrop
{
	public class SelectionCornersFitter : MonoBehaviour
	{
		[SerializeField]
		RectTransform selection;

		[SerializeField]
		RectTransform bottomLeft;

		[SerializeField]
		RectTransform bottomRight;

		[SerializeField]
		RectTransform topLeft;

		[SerializeField]
		RectTransform topRight;

		[SerializeField]
		float preferredCornerSize = 30f;

		[SerializeField]
		float cornerSizeMaxRatio = 0.3f;

		Vector2 _inset;

		public void Init()
		{
			_inset = ( (RectTransform) transform ).sizeDelta * 0.5f;
            OnRectTransformDimensionsChange();
		}

        void OnRectTransformDimensionsChange()
        {
            if (!gameObject.activeInHierarchy)
                return;

            Vector2 cornerSize;
            Vector2 maxCornerSize = selection.rect.size * cornerSizeMaxRatio + _inset;
            if (preferredCornerSize <= maxCornerSize.x && preferredCornerSize <= maxCornerSize.y)
                cornerSize = new Vector2(preferredCornerSize, preferredCornerSize);
            else
                cornerSize = Vector2.one * Mathf.Min(maxCornerSize.x, maxCornerSize.y);

            float halfCornerSize = cornerSize.x * 0.5f;

            bottomLeft.anchoredPosition = new Vector2(halfCornerSize, halfCornerSize);
            bottomLeft.sizeDelta = cornerSize;

            bottomRight.anchoredPosition = new Vector2(-halfCornerSize, halfCornerSize);
            bottomRight.sizeDelta = cornerSize;

            topLeft.anchoredPosition = new Vector2(halfCornerSize, -halfCornerSize);
            topLeft.sizeDelta = cornerSize;

            topRight.anchoredPosition = new Vector2(-halfCornerSize, -halfCornerSize);
            topRight.sizeDelta = cornerSize;
        }
	}
}