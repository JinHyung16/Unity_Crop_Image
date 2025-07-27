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

    public class CropWindow : MonoBehaviour
    {
        public interface IListener
        {
            void OnImageCropped(int selectIndex, Texture2D croppedTexture);
            void OnCropCancelled();
        }

        [Header("Properties")]
        Canvas Canvas;
        [SerializeField]
        float _autoZoomInThreshold = 0.5f;
        [SerializeField]
        float _autoZoomOutThreshold = 0.65f;
        [SerializeField]
        float _autoZoomInFillAmount = 0.64f;
        [SerializeField]
        float _autoZoomOutFillAmount = 0.51f;
        [SerializeField]
        AnimationCurve autoZoomCurve;

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

        bool _autoZoomEnabled;
        public bool AutoZoomEnabled
        {
            get 
            { 
                return _autoZoomEnabled; 
            }
            set
            {
                _autoZoomEnabled = value;
                if (_autoZoomEnabled)
                    StartAutoZoom(false);
            }
        }

        bool _pixelPerfectSelection;
        public bool PixelPerfectSelection
        {
            get 
            { 
                return _pixelPerfectSelection; 
            }
            set
            {
                _pixelPerfectSelection = value;
                if (_pixelPerfectSelection)
                    MakePixelPerfectSelection();
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

        IEnumerator _autoZoomCoroutine;
        ISelectHandler _currentSelectionHandler;
        IListener _listener;

        bool _shouldRefreshViewport;

        int _selectIndex;

        float _minImageScale;

        Vector2 _minSize, _maxSize;
        Vector2 _currMinSize, _currMaxSize;

        public void Awake()
        {
            if (Canvas == null)
                Canvas = GetComponent<Canvas>();
        }

        public void Open(int selectIndex, Texture2D texture2D, IListener listener)
        {
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _listener = listener;
            _selectIndex = selectIndex;

            SetupImage(texture2D);

            Init();
            SetDefaultSetting();
            StartCoroutine(OpenWaitOneFrame());
        }

        private System.Collections.IEnumerator OpenWaitOneFrame()
        {
            yield return new WaitForEndOfFrame();
            
            ResetView(false);
        }

        public void OnLateUpdate()
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

        void SetDefaultSetting()
        {
            MarkTextureNonReadable = DefaultSettings.MarkTextureNonReadable;
            OvalSelection = DefaultSettings.OvalSelection;
            GuidelinesVisibility = DefaultSettings.GuidelinesVisibility;
            ImageBackground = DefaultSettings.ImageBackGroundColor;
            AutoZoomEnabled = DefaultSettings.AutoZoomEnabled;
            PixelPerfectSelection = DefaultSettings.PixelPerfectSelection;
        }

        void SetupImage(Texture2D texture2D)
        {
            OriginalImage.texture = texture2D;
            
            // OriginalImage RectTransform은 건들지 않음 (Stretch 유지)
            // _orientedImageTransform = (RectTransform)OriginalImage.transform;

            _originalImageSize = new Vector2(texture2D.width, texture2D.height);
            _orientedImageSize = _originalImageSize;
            
            // OriginalImage는 원본 그대로 유지
            // _orientedImageTransform.sizeDelta = Vector2.zero;
            // _orientedImageTransform.anchoredPosition = Vector2.zero;

            _minSize = new Vector2(100f, 100f);
            var maxPixel = ImageHelper.GetImageMaxPixel();
            _maxSize = new Vector2(maxPixel, maxPixel);

            _currMinSize = _minSize;
            _currMaxSize = _maxSize;

            // ImageHolder만으로 크기/위치 제어
            _imageHolder.sizeDelta = _originalImageSize;
            _imageHolder.anchoredPosition = Vector2.zero;

            Vector2 initialSize = new Vector2(256f, 256f); // 초기 Selection 크기
            UpdateSelection(Vector2.zero, initialSize);
            _selection.anchoredPosition = (_imageHolder.sizeDelta - _selection.sizeDelta) * 0.5f;
            
            if (_pixelPerfectSelection)
                MakePixelPerfectSelection();
        }

        public void ResetView(bool frameSelection)
        {
            if (_orientedImageSize.x <= 0f || _orientedImageSize.y <= 0f)
                return;

            StopAutoZoom();

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
            _imageHolder.anchoredPosition = centerPosition;

            if (frameSelection && _autoZoomEnabled)
                StartAutoZoom(true);
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

        public void StartAutoZoom(bool instantZoom)
        {
            if (!gameObject.activeInHierarchy)
                return;

            StopAutoZoom();

            Vector2 selectionSize = _selection.sizeDelta;
            
            // ImageHolder sizeDelta 기반으로 현재 스케일 계산
            float currentScale = _imageHolder.sizeDelta.x / _orientedImageSize.x;
            Vector2 selectionSizeScaled = selectionSize * currentScale;

            float zoomAmount = -1f;
            float fillRate = Mathf.Max(selectionSizeScaled.x / _viewportSize.x, selectionSizeScaled.y / _viewportSize.y);
            
            if (fillRate <= _autoZoomInThreshold)
            {
                float scaleX = _viewportSize.x * _autoZoomInFillAmount / selectionSize.x;
                float scaleY = _viewportSize.y * _autoZoomInFillAmount / selectionSize.y;

                zoomAmount = Mathf.Min(scaleX, scaleY);
            }
            else if (fillRate >= _autoZoomOutThreshold)
            {
                float scaleX = _viewportSize.x * _autoZoomOutFillAmount / selectionSize.x;
                float scaleY = _viewportSize.y * _autoZoomOutFillAmount / selectionSize.y;

                zoomAmount = Mathf.Min(scaleX, scaleY);
            }
            else
            {
                // ImageHolder 기준으로 Selection 위치 계산
                Vector2 selectionBottomLeft = _imageHolder.anchoredPosition + _selection.anchoredPosition * currentScale;
                Vector2 selectionTopRight = selectionBottomLeft + _selection.sizeDelta * currentScale;

                if (selectionBottomLeft.x < -1E-4f || selectionBottomLeft.y < -1E-4f || selectionTopRight.x > _viewportSize.x + 1E-4f || selectionTopRight.y > _viewportSize.y + 1E-4f)
                    zoomAmount = currentScale;
            }

            if (zoomAmount < 0f)
                return;

            if (zoomAmount < _minImageScale)
                zoomAmount = _minImageScale;

            if (Mathf.Abs(zoomAmount - currentScale) < 0.001f)
                instantZoom = true;

            _autoZoomCoroutine = AutoZoom(zoomAmount, instantZoom);
            StartCoroutine(_autoZoomCoroutine);
        }

        private System.Collections.IEnumerator AutoZoom(float targetScale, bool instantZoom)
        {
            float elapsed = 0f;
            float length = autoZoomCurve.length == 0 ? 0f : autoZoomCurve[autoZoomCurve.length - 1].time;

            // ImageHolder 기준으로 초기값 설정
            Vector2 initialImageSize = _imageHolder.sizeDelta;
            Vector2 initialImagePosition = _imageHolder.anchoredPosition;

            Vector2 finalImageSize = _orientedImageSize * targetScale;
            Vector2 finalImagePosition = _viewportSize * 0.5f - (_selection.anchoredPosition + _selection.sizeDelta * 0.5f) * targetScale;
            finalImagePosition = RestrictImageToViewport(finalImagePosition, finalImageSize);

            if (!instantZoom && elapsed < length)
            {
                Vector2 deltaImagePosition = finalImagePosition - initialImagePosition;
                Vector2 deltaImageSize = finalImageSize - initialImageSize;

                while (elapsed < length)
                {
                    yield return null;
                    elapsed += Time.unscaledDeltaTime;
                    if (elapsed >= length)
                        break;

                    float modifier = autoZoomCurve.Evaluate(elapsed);

                    // ImageHolder만 조정
                    _imageHolder.anchoredPosition = initialImagePosition + deltaImagePosition * modifier;
                    _imageHolder.sizeDelta = initialImageSize + deltaImageSize * modifier;
                }
            }

            // 최종 ImageHolder 위치/크기 설정
            _imageHolder.anchoredPosition = finalImagePosition;
            _imageHolder.sizeDelta = finalImageSize;

            _autoZoomCoroutine = null;
        }

        private void StopAutoZoom()
        {
            if (_autoZoomCoroutine != null)
            {
                StopCoroutine(_autoZoomCoroutine);
                _autoZoomCoroutine = null;
            }

            if (_currentSelectionHandler != null)
            {
                _currentSelectionHandler.Stop();
                _currentSelectionHandler = null;
            }
        }

        public bool CanModifySelectionWith(ISelectHandler handler)
        {
            if (_autoZoomCoroutine != null)
                return false;

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

                if (_pixelPerfectSelection)
                    MakePixelPerfectSelection();

                if (_autoZoomEnabled)
                    StartAutoZoom(false);
            }
        }

        public void MakePixelPerfectSelection()
        {
            Vector2 currentSize = _selection.sizeDelta;
            float squareSize = Mathf.Max(currentSize.x, currentSize.y);
            
            squareSize = Mathf.Round(squareSize);
            squareSize = Mathf.Clamp(squareSize, _currMinSize.x, _currMaxSize.x);
            
            squareSize = Mathf.Min(squareSize, 512f);
            
            Vector2 size = new Vector2(squareSize, squareSize);
            Vector2 position = _selection.anchoredPosition;
            position.x = Mathf.Round(position.x);
            position.y = Mathf.Round(position.y);
            
            Vector2 selectionHalfSize = size * 0.5f;
            Vector2 minPos = -selectionHalfSize;
            Vector2 maxPos = _imageHolder.sizeDelta - selectionHalfSize;

            _selection.anchoredPosition = position.ClampBetween(minPos, maxPos);
            _selection.sizeDelta = size;
        }

        public void UpdateSelection(Vector2 position)
        {
            Vector2 selectionHalfSize = _selection.sizeDelta * 0.5f;
            Vector2 minPos = -selectionHalfSize;
            Vector2 maxPos = _imageHolder.sizeDelta - selectionHalfSize;
            
            _selection.anchoredPosition = position.ClampBetween(minPos, maxPos);
        }

        public void UpdateSelection(Vector2 position, Vector2 size, EDirection pivot = EDirection.None, bool shrinkToFit = true)
        {
            float squareSize = Mathf.Max(size.x, size.y);
            Vector2 newSize = new Vector2(squareSize, squareSize);
            
            newSize = newSize.ClampBetween(_currMinSize, _currMaxSize);
            
            newSize.x = Mathf.Min(newSize.x, 512f);
            newSize.y = Mathf.Min(newSize.y, 512f);
            
            if (newSize.x > 512f || newSize.y > 512f)
            {
                Debug.LogWarning($"Selection size exceeded 512! Requested: {size}, Clamped: {newSize}");
            }

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

            Vector2 selectionHalfSize = size * 0.5f;
            Vector2 minPos = -selectionHalfSize;
            Vector2 maxPos = _imageHolder.sizeDelta - selectionHalfSize;

            _selection.anchoredPosition = position.ClampBetween(minPos, maxPos);
            _selection.sizeDelta = size;
        }

        private Vector2 RestrictImageToViewport(Vector2 position, Vector2 imageSize)
        {
            if (imageSize.x < _viewportSize.x)
                position.x = (_viewportSize.x - imageSize.x) * 0.5f;
            else
                position.x = Mathf.Clamp(position.x, _viewportSize.x - imageSize.x, 0f);

            if (imageSize.y < _viewportSize.y)
                position.y = (_viewportSize.y - imageSize.y) * 0.5f;
            else
                position.y = Mathf.Clamp(position.y, _viewportSize.y - imageSize.y, 0f);

            return position;
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
            // Viewport 기준으로 터치 위치를 먼저 계산
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, screenPos, cam, out localPos);

            // Viewport 좌표를 ImageHolder 좌표로 변환
            Vector2 imageHolderPos = localPos - _imageHolder.anchoredPosition;

            return imageHolderPos;
        }

        public void Close()
        {
            _autoZoomCoroutine = null;
            Destroy(OriginalImage.texture);

            SelectionMovemnetHandler.StopModifySelectionWith();
            foreach (var resizeHandler in SelectionResizeHandler)
            {
                resizeHandler.StopModifySelectionWith();
            }
        }

        void Init()
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

            Canvas.ForceUpdateCanvases();
            OnViewportDimensionsChange(_viewport.rect.size);
        }

        void OnViewportDimensionsChange(Vector2 size)
        {
            _viewportSize = size;
            
            if (_viewportSize.x <= 0f || _viewportSize.y <= 0f)
            {
                _viewportSize = new Vector2(Screen.width, Screen.height);
            }
            
            _shouldRefreshViewport = true;
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

            // ImageHolder 좌표를 OriginalImage 좌표로 변환
            // ImageHolder의 sizeDelta를 기준으로 정규화된 좌표 계산
            Vector2 normalizedPosition = new Vector2(
                _selection.anchoredPosition.x / _imageHolder.sizeDelta.x,
                _selection.anchoredPosition.y / _imageHolder.sizeDelta.y
            );
            Vector2 normalizedSize = new Vector2(
                _selection.sizeDelta.x / _imageHolder.sizeDelta.x,
                _selection.sizeDelta.y / _imageHolder.sizeDelta.y
            );

            // 정규화된 좌표를 OriginalImage의 실제 픽셀 좌표로 변환
            Vector2 pixelPosition = new Vector2(
                normalizedPosition.x * originalTexture.width,
                normalizedPosition.y * originalTexture.height
            );
            Vector2 pixelSize = new Vector2(
                normalizedSize.x * originalTexture.width,
                normalizedSize.y * originalTexture.height
            );

            // 직접 픽셀 기반으로 크롭
            Texture2D croppedTexture = CropTextureDirectly(
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

            _listener.OnImageCropped(_selectIndex, croppedTexture);
        }

        private Texture2D CropTextureDirectly(Texture2D originalTexture, Vector2 pixelPosition, Vector2 pixelSize, int maxCropSize = 512)
        {
            if (originalTexture == null)
                return null;

            // 픽셀 좌표를 정수로 변환하고 범위 제한
            int textureX = Mathf.RoundToInt(pixelPosition.x);
            int textureY = Mathf.RoundToInt(pixelPosition.y);
            int textureWidth = Mathf.RoundToInt(pixelSize.x);
            int textureHeight = Mathf.RoundToInt(pixelSize.y);

            textureX = Mathf.Clamp(textureX, 0, originalTexture.width - 1);
            textureY = Mathf.Clamp(textureY, 0, originalTexture.height - 1);
            textureWidth = Mathf.Clamp(textureWidth, 1, originalTexture.width - textureX);
            textureHeight = Mathf.Clamp(textureHeight, 1, originalTexture.height - textureY);

            Texture2D croppedTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.ARGB32, false);

            Color[] pixels = originalTexture.GetPixels(textureX, textureY, textureWidth, textureHeight);
            croppedTexture.SetPixels(pixels);
            croppedTexture.Apply();

            // 최대 크기 제한 적용
            if (croppedTexture.width > maxCropSize || croppedTexture.height > maxCropSize)
            {
                croppedTexture = ImageHelper.ResizeTexture(croppedTexture, maxCropSize);
            }

            return croppedTexture;
        }
    }

    [Serializable]
    public class ImageCropSetting
    {
        public bool AutoZoomEnabled = false;
        public bool PixelPerfectSelection = false;
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