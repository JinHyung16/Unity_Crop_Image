using UnityEngine;
using UnityEngine.UI;
using HughGame.UI.NCrop;
using System.Collections;
using System;
using HughGame.Helper;

namespace HughGame.UI
{
    [System.Flags]
    public enum EDirection 
    { 
        None = 0, 
        Left = 1, 
        Top = 2, 
        Right = 4, 
        Bottom = 8
    };

    public enum EVisibility 
    { 
        Hidden = 0, 
        OnDrag = 1, 
        AlwaysVisible = 2 
    };

    public class CropWindow : BaseWindow
    {
        public interface IListener
        {
            void OnImageCropped(Texture2D croppedTexture);
            void OnCropCancelled();
        }

        [SerializeField]
        float _selectionSnapToEdgeThreshold = 5f;
        public float SelectionSnapToEdgeThreshold => _selectionSnapToEdgeThreshold;

        [SerializeField]
        float _viewportScrollSpeed = 512f;

        [SerializeField]
        RectTransform _viewport;
        public RectTransform Viewport => _viewport;

        [SerializeField]
        RectTransform _imageHolder;
        public RectTransform ImageHolder => _imageHolder;

        public RawImage OriginalImage;

        [SerializeField]
        RectTransform _selection;
        public RectTransform Selection => _selection;

        [SerializeField]
        RectTransform _selectionGraphics;
        public RectTransform SelectionGraphics => _selectionGraphics;

        public CropAreaSizeChangeListener SizeChangeListener;
        public SelectionGraphicsSynchronizer SelectionGraphicsSync;
        public SelectionMovementHandler SelectionMovemnetHandler;
        public SelectionCornersFitter SelectionCornerFilter;

        public SelectionResizeHandler[] SelectionResizeHandler;
        public Behaviour[] OvalMaskElements;
        public Behaviour[] Guidelines;

        [Header("Crop Settings")]
        [SerializeField]
        ImageCropSetting defaultSetting;
        private ImageCropSetting DefaultSettings
        {
            get
            {
                if (defaultSetting == null)
                    defaultSetting = new ImageCropSetting();

                return defaultSetting;
            }
        }

        bool _ovalSelection;
        public bool OvalSelection
        {
            get 
            { 
                return _ovalSelection; 
            }
            set
            {
                _ovalSelection = value;
                for (int i = 0; i < OvalMaskElements.Length; i++)
                    OvalMaskElements[i].enabled = _ovalSelection;
            }
        }

        EVisibility _guidelinesVisibility;
        public EVisibility GuidelinesVisibility
        {
            get 
            { 
                return _guidelinesVisibility; 
            }
            set
            {
                _guidelinesVisibility = value;

                bool visible = _guidelinesVisibility == EVisibility.AlwaysVisible;
                for (int i = 0; i < Guidelines.Length; i++)
                    Guidelines[i].enabled = visible;
            }
        }

        public bool MarkTextureNonReadable { get; private set; }

        public Color ImageBackground { get; private set; }

        Vector2 _viewportSize;
        public Vector2 ViewportSize => _viewportSize;

        Vector2 _originalImageSize;
        public Vector2 OriginalImageSize => _originalImageSize;

        Vector2 _orientedImageSize;
        public Vector2 OrientedImageSize => _orientedImageSize;

        public Vector2 SelectionSize => _selection.sizeDelta;

        ISelectHandler _currentSelectionHandler;
        IListener _listener;

        bool _shouldRefreshViewport;
        float _minImageScale;

        Vector2 _minSize, _maxSize;
        Vector2 _currMinSize, _currMaxSize;

        public void Open(Texture2D texture2D, IListener listener)
        {
            OpenInternal(() =>
            {
                Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _listener = listener;
            }, () =>
            {
                Init();
                SetDefaultSetting();
                SetupImage(texture2D);
                Canvas.ForceUpdateCanvases();
            });
        }

        private void LateUpdate()
        {
            if (gameObject.activeInHierarchy)
            {
                // ImageHolder sizeDelta 기반으로 현재 스케일 계산
                float currentScale = _orientedImageSize.x > 0 ? _imageHolder.sizeDelta.x / _orientedImageSize.x : 0f;

                if (_currentSelectionHandler != null && currentScale > _minImageScale + 0.01f)
                    _currentSelectionHandler.OnUpdate();

                if (_shouldRefreshViewport)
                {
                    ResetView(true);
                    _shouldRefreshViewport = false;
                }

                SelectionGraphicsSync.Synchronize();
            }
        }

        public void ResetView(bool frameSelection)
        {
            if (_orientedImageSize.x <= 0f || _orientedImageSize.y <= 0f)
                return;

            if (_currentSelectionHandler != null)
            {
                _currentSelectionHandler.Stop();
                _currentSelectionHandler = null;
            }

            if (_viewportSize.x <= 0f || _viewportSize.y <= 0f)
            {
                _viewportSize = _viewport.rect.size;
                if (_viewportSize.x <= 0f || _viewportSize.y <= 0f)
                    _viewportSize = new Vector2(Screen.width, Screen.height);
            }

            _minImageScale = Mathf.Min(_viewportSize.x / _orientedImageSize.x, _viewportSize.y / _orientedImageSize.y);
            _minImageScale = Mathf.Max(_minImageScale, 0.1f);

            // ImageHolder만으로 크기/위치 제어
            Vector2 scaledImageSize = _orientedImageSize * _minImageScale;
            Vector2 centerPosition = (_viewportSize - scaledImageSize) * 0.5f;

            _imageHolder.sizeDelta = scaledImageSize;
            //_imageHolder.anchoredPosition = centerPosition;

            if (frameSelection)
                _selection.anchoredPosition = (_imageHolder.sizeDelta - _selection.sizeDelta) * 0.5f;
        }
  
        public bool CanModifySelectionWith(ISelectHandler handler)
        {
            if (handler != _currentSelectionHandler)
            {
                if (_currentSelectionHandler != null)
                    _currentSelectionHandler.Stop();

                _currentSelectionHandler = handler;
            }

            if (_guidelinesVisibility == EVisibility.OnDrag)
            {
                for (int i = 0; i < Guidelines.Length; i++)
                    Guidelines[i].enabled = true;
            }

            return true;
        }

        public void StopModifySelectionWith(ISelectHandler handler)
        {
            if (_currentSelectionHandler == handler)
            {
                _currentSelectionHandler = null;

                if (_guidelinesVisibility == EVisibility.OnDrag)
                {
                    for (int i = 0; i < Guidelines.Length; i++)
                        Guidelines[i].enabled = false;
                }
            }
        }

        public void UpdateSelection(Vector2 position)
        {
            _selection.anchoredPosition = ClampSelectionPosition(position, _selection.sizeDelta);
        }

        public void UpdateSelection(Vector2 position, Vector2 size, EDirection pivot = EDirection.None, bool shrinkToFit = true)
        {
            float squareSize = Mathf.Max(size.x, size.y);
            Vector2 newSize = new Vector2(squareSize, squareSize);

            newSize = newSize.ClampBetween(_currMinSize, _currMaxSize);

            var maxPixel = ImageHelper.GetScreenBasedMaxPixel();
            newSize.x = Mathf.Min(newSize.x, maxPixel);
            newSize.y = Mathf.Min(newSize.y, maxPixel);

#if UNITY_EDITOR
            if ((newSize.x > maxPixel || newSize.y > maxPixel))
            {
                Debug.LogWarning($"Selection size exceeded {maxPixel}! Requested: {size}, Clamped: {newSize}");
            }
#endif
            if (size.x != newSize.x)
            {
                if ((pivot & EDirection.Right) == EDirection.Right)
                    position.x -= newSize.x - size.x;
                else if ((pivot & EDirection.Left) != EDirection.Left)
                    position.x -= (newSize.x - size.x) * 0.5f;

                size.x = newSize.x;
            }
            if (size.y != newSize.y)
            {
                if ((pivot & EDirection.Top) == EDirection.Top)
                    position.y -= newSize.y - size.y;
                else if ((pivot & EDirection.Bottom) != EDirection.Bottom)
                    position.y -= (newSize.y - size.y) * 0.5f;

                size.y = newSize.y;
            }

            _selection.anchoredPosition = ClampSelectionPosition(position, size);
            _selection.sizeDelta = size;
        }

        public Vector2 ScrollImage(Vector2 imagePosition, EDirection direction)
        {
            // ImageHolder 크기 기준으로 스크롤
            Vector2 imageSize = _imageHolder.sizeDelta;

            if (direction == EDirection.Left)
            {
                imagePosition.x += _viewportScrollSpeed * Time.unscaledDeltaTime;
                if (imagePosition.x > 0f)
                    imagePosition.x = 0f;
            }
            else if (direction == EDirection.Top)
            {
                imagePosition.y -= _viewportScrollSpeed * Time.unscaledDeltaTime;
                if (imagePosition.y + imageSize.y < _viewportSize.y)
                    imagePosition.y = _viewportSize.y - imageSize.y;
            }
            else if (direction == EDirection.Right)
            {
                imagePosition.x -= _viewportScrollSpeed * Time.unscaledDeltaTime;
                if (imagePosition.x + imageSize.x < _viewportSize.x)
                    imagePosition.x = _viewportSize.x - imageSize.x;
            }
            else
            {
                imagePosition.y += _viewportScrollSpeed * Time.unscaledDeltaTime;
                if (imagePosition.y > 0f)
                    imagePosition.y = 0f;
            }

            // ImageHolder 위치만 업데이트
            _imageHolder.anchoredPosition = imagePosition;

            return imagePosition;
        }

        public Vector2 GetTouchPosition(Vector2 screenPos, Camera cam)
        {
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, screenPos, cam, out localPos);
            Vector2 imageHolderPos = localPos - _imageHolder.anchoredPosition;

            return imageHolderPos;
        } 

        protected override void OnClosed()
        {
            base.OnClosed();
            Destroy(OriginalImage.texture);

            SelectionMovemnetHandler.StopModifySelectionWith();
            foreach (var resizeHandler in SelectionResizeHandler)
            {
                resizeHandler.StopModifySelectionWith();
            }
        }
        private void Init()
        {
            SizeChangeListener.Init();
            SizeChangeListener.onSizeChanged = OnViewportDimensionsChange;

            SelectionMovemnetHandler.Init(this);
            SelectionCornerFilter.Init();
            SelectionGraphicsSync.Init(this);

            foreach (var resizeHandler in SelectionResizeHandler)
            {
                resizeHandler.Init(this);
            }

            OnViewportDimensionsChange(_viewport.rect.size);
        }

        private void SetDefaultSetting()
        {
            MarkTextureNonReadable = DefaultSettings.MarkTextureNonReadable;
            OvalSelection = DefaultSettings.OvalSelection;
            GuidelinesVisibility = DefaultSettings.GuidelinesVisibility;
            ImageBackground = DefaultSettings.ImageBackGroundColor;
        }

        private void SetupImage(Texture2D texture2D)
        {
            OriginalImage.texture = texture2D;

            // OriginalImage RectTransform은 건들지 않음 (Stretch 유지)
            // _orientedImageTransform = (RectTransform)OriginalImage.transform;

            _originalImageSize = new Vector2(texture2D.width, texture2D.height);
            _orientedImageSize = _originalImageSize;

            // OriginalImage는 원본 그대로 유지
            // _orientedImageTransform.sizeDelta = Vector2.zero;
            // _orientedImageTransform.anchoredPosition = Vector2.zero;

            _minSize = new Vector2(64f, 64f);
            var maxPixel = ImageHelper.GetImageMaxPixel();
            _maxSize = new Vector2(maxPixel, maxPixel);

            _currMinSize = _minSize;
            _currMaxSize = _maxSize;

            _imageHolder.sizeDelta = _originalImageSize;
            _imageHolder.anchoredPosition = Vector2.zero;

            _selection.anchoredPosition = Vector2.zero;
            _selection.sizeDelta = _currMaxSize;

            ResetView(false);
            _selection.anchoredPosition = (_imageHolder.sizeDelta - _selection.sizeDelta) * 0.5f;
        }

        private Vector2 ClampSelectionPosition(Vector2 position, Vector2 size)
        {
            Vector2 minPosViewport = -_imageHolder.anchoredPosition;
            Vector2 maxPosViewport = _viewportSize - _imageHolder.anchoredPosition - size;

            Vector2 minPosOverlap = -size;
            Vector2 maxPosOverlap = _imageHolder.sizeDelta;

            Vector2 finalMinPos = new Vector2(Mathf.Max(minPosViewport.x, minPosOverlap.x), Mathf.Max(minPosViewport.y, minPosOverlap.y));
            Vector2 finalMaxPos = new Vector2(Mathf.Min(maxPosViewport.x, maxPosOverlap.x), Mathf.Min(maxPosViewport.y, maxPosOverlap.y));

            return position.ClampBetween(finalMinPos, finalMaxPos);
        }

        private void OnViewportDimensionsChange(Vector2 size)
        {
            _viewportSize = size;        
            if (_viewportSize.x <= 0f || _viewportSize.y <= 0f)
            {
                _viewportSize = new Vector2(Screen.width, Screen.height);
            }
            
            _shouldRefreshViewport = true;
        }
        private Texture2D MakeTextureReadable(Texture2D source)
        {
            RenderTexture renderTex = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
            Graphics.Blit(source, renderTex);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;

            Texture2D readableTexture = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
            readableTexture.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableTexture.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);

            return readableTexture;
        }

        public void OnClickCancel()
        {
            _listener.OnCropCancelled();
        }

        public void OnClickCrop()
        {
            if (OriginalImage.texture == null || OriginalImage.texture is not Texture2D)
            {
                Debug.Assert(false, "갤러리에서 가져온 이미지가 없는데 Crop 기능이 활성화 되었다는건 예외처리가 빠진거. 예외처리 해주세요");
                return;
            }

            Texture2D originalTexture = OriginalImage.texture as Texture2D;

            float scale = (float)originalTexture.width / _imageHolder.sizeDelta.x;

            Vector2 pixelPosition = new Vector2(
                _selection.anchoredPosition.x * scale,
                (_imageHolder.sizeDelta.y - _selection.anchoredPosition.y) * scale - (_selection.sizeDelta.y * scale)
            );

            Vector2 pixelSize = new Vector2(
                _selection.sizeDelta.x * scale,
                _selection.sizeDelta.y * scale
            );

            pixelPosition.x = Mathf.Max(0, pixelPosition.x);
            pixelPosition.y = Mathf.Max(0, pixelPosition.y);
            pixelSize.x = Mathf.Min(pixelSize.x, originalTexture.width - pixelPosition.x);
            pixelSize.y = Mathf.Min(pixelSize.y, originalTexture.height - pixelPosition.y);

            float finalPixelSize = Mathf.Min(pixelSize.x, pixelSize.y);
            pixelSize = new Vector2(finalPixelSize, finalPixelSize);

            Texture2D croppedTexture = ImageHelper.CropTextureDirectly(
                originalTexture,
                pixelPosition,
                pixelSize,
                ImageHelper.GetImageMaxPixel()
            );

            Debug.Assert(croppedTexture != null, "이미지 Crop 실패함");

            if (_ovalSelection)
            {
                Texture2D ovalCroppedTexture = ImageHelper.ApplyOvalMask(
                    croppedTexture,
                    ImageBackground,
                    MarkTextureNonReadable
                );

                DestroyImmediate(croppedTexture);
                croppedTexture = ovalCroppedTexture;
            }

            // Unity 텍스처는 기본적으로 GPU 메모리에 저장되어 CPU에서 접근 불가
            // EncodeTo~ () 하려면 CPU에서 픽셀 데이터 접근해야 하기에 readable 해야함
            if (croppedTexture != null && !croppedTexture.isReadable)
            {
                croppedTexture = MakeTextureReadable(croppedTexture);
            }

            _listener.OnImageCropped(croppedTexture);
        }
    }

    [Serializable]
    public class ImageCropSetting
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