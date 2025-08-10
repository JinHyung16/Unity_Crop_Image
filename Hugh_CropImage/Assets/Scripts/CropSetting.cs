using System;
using UnityEngine;

namespace HughGame.UI
{
    [Serializable]
    public class CropSetting
    {
        public bool OvalSelection = true;
        public bool MarkTextureNonReadable = false;

        public Color ImageBackGroundColor = Color.black;

        public EVisibility GuidelinesVisibility = EVisibility.AlwaysVisible;

        public Vector2 SelectionMinSize = Vector2.zero;
        public Vector2 SelectionMaxSize = Vector2.zero;

        public float SelectionMinAspectRatio = 1f;
        public float SelectionMaxAspectRatio = 1f;

        public float SelectionInitialPaddingLeft = 0.1f;
        public float SelectionInitialPaddingTop = 0.1f;
        public float SelectionInitialPaddingRight = 0.1f;
        public float SelectionInitialPaddingBottom = 0.1f;
    }
}