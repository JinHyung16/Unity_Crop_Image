using UnityEngine;
using UnityEngine.UI;

namespace HughGame.UI.NCrop
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class CropCircleGraphic : MaskableGraphic
    {
        enum EMode
        {
            FillInside = 0,
            FillOutside = 1,
            Edge = 2
        };

#pragma warning disable 0649
        [SerializeField]
        Sprite _renderSprite;

        [SerializeField]
        int _detail = 64;

        [SerializeField]
        EMode _mode;

        [SerializeField]
        float _edgeThickness = 1;
#pragma warning restore 0649

        Vector2 _uv;
        Color32 _color32;

        float _width, _height;
        float _deltaWidth, _deltaHeight;
        float _deltaRadians;

        public override Texture mainTexture 
        { 
            get 
            { 
                return _renderSprite != null ? _renderSprite.texture : s_WhiteTexture; 
            } 
        }

        protected override void Awake()
        {
            base.Awake();

            if (_renderSprite != null)
            {
                Vector4 packedUv = UnityEngine.Sprites.DataUtility.GetOuterUV(_renderSprite);
                _uv = new Vector2(packedUv.x + packedUv.z, packedUv.y + packedUv.w) * 0.5f; // uv center point
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            Rect r = GetPixelAdjustedRect();

            _color32 = color;
            _width = r.width * 0.5f;
            _height = r.height * 0.5f;

            vh.Clear();

            Vector2 pivot = rectTransform.pivot;
            _deltaWidth = r.width * (0.5f - pivot.x);
            _deltaHeight = r.height * (0.5f - pivot.y);

            if (_mode == EMode.FillInside)
            {
                _deltaRadians = 360f / _detail * Mathf.Deg2Rad;
                FillInside(vh);
            }
            else if (_mode == EMode.FillOutside)
            {
                int quarterDetail = (_detail + 3) / 4;
                _deltaRadians = 360f / (quarterDetail * 4) * Mathf.Deg2Rad;

                vh.AddVert(new Vector3(_width + _deltaWidth, _height + _deltaHeight, 0f), _color32, _uv);
                vh.AddVert(new Vector3(-_width + _deltaWidth, _height + _deltaHeight, 0f), _color32, _uv);
                vh.AddVert(new Vector3(-_width + _deltaWidth, -_height + _deltaHeight, 0f), _color32, _uv);
                vh.AddVert(new Vector3(_width + _deltaWidth, -_height + _deltaHeight, 0f), _color32, _uv);

                int triangleIndex = 4;
                FillOutside(vh, new Vector3(_width + _deltaWidth, _deltaHeight, 0f), 0, quarterDetail, ref triangleIndex);
                FillOutside(vh, new Vector3(_deltaWidth, _height + _deltaHeight, 0f), 1, quarterDetail, ref triangleIndex);
                FillOutside(vh, new Vector3(-_width + _deltaWidth, _deltaHeight, 0f), 2, quarterDetail, ref triangleIndex);
                FillOutside(vh, new Vector3(_deltaWidth, -_height + _deltaHeight, 0f), 3, quarterDetail, ref triangleIndex);
            }
            else
            {
                _deltaRadians = 360f / _detail * Mathf.Deg2Rad;
                GenerateEdges(vh);
            }
        }

        public override void Cull(Rect clipRect, bool validRect)
        {
            canvasRenderer.cull = false;
        }

        void FillInside(VertexHelper vh)
        {
            vh.AddVert(new Vector3(_deltaWidth, _deltaHeight, 0f), _color32, _uv);
            vh.AddVert(new Vector3(_width + _deltaWidth, _deltaHeight, 0f), _color32, _uv);

            int triangleIndex = 2;
            for (int i = 1; i < _detail; i++, triangleIndex++)
            {
                float radians = i * _deltaRadians;

                vh.AddVert(new Vector3(Mathf.Cos(radians) * _width + _deltaWidth, Mathf.Sin(radians) * _height + _deltaHeight, 0f), _color32, _uv);
                vh.AddTriangle(triangleIndex, triangleIndex - 1, 0);
            }

            vh.AddTriangle(1, triangleIndex - 1, 0);
        }

        void FillOutside(VertexHelper vh, Vector3 initialPoint, int quarterIndex, int detail, ref int triangleIndex)
        {
            int startIndex = quarterIndex * detail;
            int endIndex = (quarterIndex + 1) * detail;

            vh.AddVert(initialPoint, _color32, _uv);
            triangleIndex++;

            for (int i = startIndex + 1; i <= endIndex; i++, triangleIndex++)
            {
                float radians = i * _deltaRadians;

                vh.AddVert(new Vector3(Mathf.Cos(radians) * _width + _deltaWidth, Mathf.Sin(radians) * _height + _deltaHeight, 0f), _color32, _uv);
                vh.AddTriangle(quarterIndex, triangleIndex - 1, triangleIndex);
            }
        }

        void GenerateEdges(VertexHelper vh)
        {
            float innerWidth = _width - _edgeThickness;
            float innerHeight = _height - _edgeThickness;

            vh.AddVert(new Vector3(_width + _deltaWidth, _deltaHeight, 0f), _color32, _uv);
            vh.AddVert(new Vector3(innerWidth + _deltaWidth, _deltaHeight, 0f), _color32, _uv);

            int triangleIndex = 2;
            for (int i = 1; i < _detail; i++, triangleIndex += 2)
            {
                float radians = i * _deltaRadians;
                float cos = Mathf.Cos(radians);
                float sin = Mathf.Sin(radians);

                vh.AddVert(new Vector3(cos * _width + _deltaWidth, sin * _height + _deltaHeight, 0f), _color32, _uv);
                vh.AddVert(new Vector3(cos * innerWidth + _deltaWidth, sin * innerHeight + _deltaHeight, 0f), _color32, _uv);

                vh.AddTriangle(triangleIndex, triangleIndex - 2, triangleIndex - 1);
                vh.AddTriangle(triangleIndex, triangleIndex - 1, triangleIndex + 1);
            }

            vh.AddTriangle(0, triangleIndex - 2, triangleIndex - 1);
            vh.AddTriangle(0, triangleIndex - 1, 1);
        }
    }
}