using HughGame.Managers;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace HughGame.Helper
{
    public static class ImageHelper
    {
        public delegate void ImageLoadCallback(Texture2D texture, bool success);
        public static void LoadProfileImage(MonoBehaviour runner, string accountId, string url, ImageLoadCallback callback)
        {
            if (runner == null ||
                runner.gameObject.activeInHierarchy == false ||
                string.IsNullOrEmpty(accountId))
            {
                callback?.Invoke(null, false);
                return;
            }

            runner.StartCoroutine(LoadProfileImageCoroutine(accountId, url, callback));
        }
        private static IEnumerator LoadProfileImageCoroutine(string accountId, string url, ImageLoadCallback callback)
        {
            var cachedURL = CachedProfileImageManager.Instance.GetCachedProfileUrl(accountId, url);

            if (string.IsNullOrEmpty(cachedURL) || cachedURL != url)
            {
                using (var request = UnityWebRequestTexture.GetTexture(url))
                {
                    request.timeout = 2;
                    request.useHttpContinue = false;

                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var downloadedTexture = DownloadHandlerTexture.GetContent(request);
                        if (downloadedTexture != null)
                        {
                            var uiTexture = CreateUITexture(downloadedTexture);
                            var maxPixel = GetImageMaxPixel();

                            var textureForSave = uiTexture;
                            if(uiTexture.width != maxPixel || uiTexture.height != maxPixel)
                            {
                                textureForSave = ResizeTextureExactSquare(uiTexture, maxPixel);
                                Object.Destroy(uiTexture);
                            }
                            SaveProfileImage(accountId, url, downloadedTexture);
                            UpdateProfileCache(accountId, url);
                            callback?.Invoke(downloadedTexture, true);
                            yield break;
                        }
                    }

                    Debug.Log($"<color=red>UnityWebRequest 이미지 다운로드 실패" +
                        $"AccountId[{accountId}]" +
                        $"URL[{url}]" +
                        $"Error[{request.error}]</color>");

                    callback?.Invoke(null, false);
                    yield break;
                }
            }

            if(cachedURL == url)
            {
                var imagePath = CachedProfileImageManager.Instance.GetProfileImagePath(accountId, url);
                if (File.Exists(imagePath))
                {
                    byte[] fileData = File.ReadAllBytes(imagePath);
                    var maxPixel = GetImageMaxPixel();
                    Texture2D texture = new Texture2D(maxPixel, maxPixel, TextureFormat.ARGB32, false);
                    if (texture.LoadImage(fileData))
                    {
                        if (texture.width != maxPixel || texture.height != maxPixel)
                        {
                            var resized = ResizeTextureExactSquare(texture, maxPixel);
                            SaveProfileImage(accountId, url, resized);
                            texture = resized;
                            Object.Destroy(resized);
                        }

                        texture.filterMode = FilterMode.Bilinear;
                        texture.anisoLevel = 1;
                        texture.wrapMode = TextureWrapMode.Clamp;
                        callback?.Invoke(texture, true);
                        yield break;
                    }

                    Object.Destroy(texture);
                    Debug.Log($"<color=red>이미지 로드 실패, 파일에 있는지 확인 해보셈" +
                    $"AccountId: : {accountId}</color>\n" +
                    $"이미지 경로 : {imagePath}\n" +
                    $"URL : {url}\n" +
                    $"Cached URL : {cachedURL}");
                }
            }
            callback?.Invoke(null, false);
        }

        private static void SaveProfileImage(string accountId, string url, Texture2D texture)
        {
            CachedProfileImageManager.Instance.SaveProfileImage(accountId, url, texture);
        }

        private static void UpdateProfileCache(string accountId, string url)
        {
            CachedProfileImageManager.Instance.UpdateProfileCache(accountId, url);
        }

        private static Texture2D ResizeTexture(Texture2D source, int maxWidth, int maxHeight)
        {
            if (source == null) 
                return null;
            float ratio = Mathf.Min((float)maxWidth / source.width, (float)maxHeight / source.height);

            if (ratio >= 1.0f)
                return source;

            int newWidth = Mathf.RoundToInt(source.width * ratio);
            int newHeight = Mathf.RoundToInt(source.height * ratio);

            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default);

            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            Texture2D resized = new Texture2D(newWidth, newHeight, source.format, false);
            resized.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            resized.Apply(true, false);

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            return resized;
        }

        private static Texture2D ResizeTextureExactSquare(Texture2D source, int targetSize)
        {
            if (source == null)
                return null;

            float scale = Mathf.Max((float)targetSize / source.width, (float)targetSize / source.height);
            int scaledW = Mathf.CeilToInt(source.width * scale);
            int scaledH = Mathf.CeilToInt(source.height * scale);

            RenderTexture rt = RenderTexture.GetTemporary(scaledW, scaledH, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            Texture2D scaled = new Texture2D(scaledW, scaledH, source.format, false);
            scaled.ReadPixels(new Rect(0, 0, scaledW, scaledH), 0, 0);
            scaled.Apply(false, false);

            int offsetX = (scaledW - targetSize) / 2;
            int offsetY = (scaledH - targetSize) / 2;
            offsetX = Mathf.Max(0, offsetX);
            offsetY = Mathf.Max(0, offsetY);

            Color[] pixels = scaled.GetPixels(offsetX, offsetY, Mathf.Min(targetSize, scaledW), Mathf.Min(targetSize, scaledH));
            Texture2D result = new Texture2D(targetSize, targetSize, TextureFormat.ARGB32, false);
            result.SetPixels(pixels);
            result.Apply(false, false);

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        private static Texture2D CreateUITexture(Texture2D source)
        {
            if (source == null)
                return null;

            bool hasMipmaps = source.mipmapCount > 1;
            if (hasMipmaps)
            {
                var uiTexture = new Texture2D(source.width, source.height, TextureFormat.ARGB32, false);
                uiTexture.SetPixels32(source.GetPixels32());
                uiTexture.Apply(false, false);
                uiTexture.filterMode = FilterMode.Bilinear;
                uiTexture.anisoLevel = 1;
                uiTexture.wrapMode = TextureWrapMode.Clamp;
                return uiTexture;
            }

            source.filterMode = FilterMode.Bilinear;
            source.anisoLevel = 1;
            source.wrapMode = TextureWrapMode.Clamp;
            return source;
        }
        private static TextureFormat GetASTCFormat(int blockSize)
        {
            switch (blockSize)
            {
                case 4: return TextureFormat.ASTC_4x4;
                case 5: return TextureFormat.ASTC_5x5;
                case 6: return TextureFormat.ASTC_6x6;
                case 8: return TextureFormat.ASTC_8x8;
                case 10: return TextureFormat.ASTC_10x10;
                case 12: return TextureFormat.ASTC_12x12;
                default:
                    Debug.Assert(false, $"BlockSize[{blockSize}] 에 대해 case문에 정의해주세요");
                    return TextureFormat.ASTC_4x4;
            }
        }
        public static Texture2D ResizeTexture(Texture2D source, int maxSize)
        {
            if (source.width <= maxSize && source.height <= maxSize)
                return source;

            return ResizeTexture(source, maxSize, maxSize);
        }

        public static Sprite CreateSprite(Texture2D texture)
        {
            if (texture == null)
                return null;

            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }

        public static Texture2D SpriteToTexture2D(Sprite sprite)
        {
            if (sprite == null)
                return null;

            return sprite.texture;
        }

        public static Texture2D CompressTextureToASTC(Texture2D originalTexture, int astcBlockSize = 4)
        {
#if UNITY_EDITOR
            return originalTexture;
#endif
#pragma warning disable CS0162
            TextureFormat astcFormat = GetASTCFormat(astcBlockSize);
#pragma warning restore CS0162
            if (SystemInfo.SupportsTextureFormat(astcFormat) == false)
            {
                Debug.Assert(false, $"ASTC {astcBlockSize}x{astcBlockSize} 포맷은 없음. 지원하는거 쓰셈");
                return originalTexture;
            }

            Texture2D compressedTexture = new Texture2D(originalTexture.width, originalTexture.height, astcFormat, false);

            Color32[] pixels = originalTexture.GetPixels32();
            compressedTexture.SetPixels32(pixels);
            compressedTexture.Apply();

#if UNITY_EDITOR
            Debug.Log($"<color=green>Texture ASTC 압축 완료" +
                        $"BlockSize[{astcBlockSize}x{astcBlockSize}]" +
                        $"압축 텍스쳐 width*height : {compressedTexture.width}x{compressedTexture.height}</color>");
#endif
            return compressedTexture;
        }

        public static bool CheckUploadImageSize(string filePath, long maxSizeBytes = 1000000)
        {
            var fileInfo = new FileInfo(filePath);
            return fileInfo.Length < maxSizeBytes;
        }

        public static bool CheckUploadImagePixelSize(Texture2D texture)
        {
            var maxPixelSize = GetImageMaxPixel();
            if (texture == null) return false;
            return texture.width <= maxPixelSize && texture.height <= maxPixelSize;
        }

        public static int GetImageMaxPixel()
        {
            return 512;
        }

        public static int GetScreenBasedMinPixel()
        {
            var curResolution = GetDeviceResolution();
            return Mathf.Min((int)curResolution.x, (int)curResolution.y);
        }

        public static Texture2D ApplyOvalMask(Texture2D source, Color backgroundColor, bool markNonReadable = true)
        {
            if (source == null)
                return null;
            int width = source.width;
            int height = source.height;
            float centerX = width * 0.5f;
            float centerY = height * 0.5f;
            float radiusX = centerX;
            float radiusY = centerY;
            Texture2D result = new Texture2D(width, height, TextureFormat.ARGB32, false);
            Color[] sourcePixels = source.GetPixels();
            Color[] resultPixels = new Color[sourcePixels.Length];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    float dx = (x - centerX) / radiusX;
                    float dy = (y - centerY) / radiusY;
                    float distance = dx * dx + dy * dy;
                    if (distance <= 1f)
                    {
                        resultPixels[index] = sourcePixels[index];
                    }
                    else
                    {
                        resultPixels[index] = backgroundColor;
                    }
                }
            }
            result.SetPixels(resultPixels);
            result.Apply(false, markNonReadable);
            return result;
        }

        public static Texture2D CropTextureDirectly(Texture2D originalTexture, RectTransform originalImageRect, RectTransform selection, int maxCropPixel)
        {
            if (originalTexture == null || originalImageRect == null || selection == null)
                return null;

            var reference = originalImageRect.parent as RectTransform;
            if (reference == null)
                reference = originalImageRect;

            var imgBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(reference, originalImageRect);
            var selBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(reference, selection);

            Vector2 imgSize = imgBounds.size;
            Vector2 imgMinBL = new Vector2(imgBounds.min.x, imgBounds.min.y);

            float imageAspect = imgSize.x > 0f && imgSize.y > 0f ? imgSize.x / imgSize.y : 1f;
            float texAspect = (float)originalTexture.width / Mathf.Max(1, originalTexture.height);

            Vector2 contentSize = imgSize;
            Vector2 contentOffset = Vector2.zero;
            if (texAspect > imageAspect)
            {
                // 좌우가 꽉 차고 위아래 레터박스
                float scaledH = imgSize.x / texAspect;
                contentOffset.y = (imgSize.y - scaledH) * 0.5f;
                contentSize.y = scaledH;
            }
            else if (texAspect < imageAspect)
            {
                // 위아래가 꽉 차고 좌우 레터박스
                float scaledW = imgSize.y * texAspect;
                contentOffset.x = (imgSize.x - scaledW) * 0.5f;
                contentSize.x = scaledW;
            }

            // 선택 영역을 이미지 표시 영역 좌하단 기준으로 변환
            Vector2 selMinBL = new Vector2(selBounds.min.x, selBounds.min.y);
            Vector2 selectionRelativeToImage = selMinBL - imgMinBL;
            Vector2 selectionRelativeToContent = selectionRelativeToImage - contentOffset;

            // UI 좌표(픽셀 아님) -> 텍스처 픽셀 좌표로 스케일
            float scaleX = contentSize.x > 0f ? (float)originalTexture.width / contentSize.x : 0f;
            float scaleY = contentSize.y > 0f ? (float)originalTexture.height / contentSize.y : 0f;

            // GetPixels은 좌하단 원점 기준. 시작/끝을 분리해 반올림 일관성 확보
            float pxStartX = selectionRelativeToContent.x * scaleX;
            float pxStartY = selectionRelativeToContent.y * scaleY;
            Vector2 uiSelSize = selBounds.size;
            float pxEndX = (selectionRelativeToContent.x + uiSelSize.x) * scaleX;
            float pxEndY = (selectionRelativeToContent.y + uiSelSize.y) * scaleY;

            Vector2 pixelPosition = new Vector2(pxStartX, pxStartY);
            Vector2 pixelSize = new Vector2(pxEndX - pxStartX, pxEndY - pxStartY);

            return CropTexture(originalTexture, pixelPosition, pixelSize, maxCropPixel);
        }

        static Texture2D CropTexture(Texture2D originalTexture, Vector2 pixelPosition, Vector2 pixelSize, int maxCropPixel)
        {
            if (originalTexture == null)
                return null;

            // 반올림 편향 최소화: 시작/끝을 각각 반올림하여 폭/높이 계산
            int reqX = Mathf.RoundToInt(pixelPosition.x);
            int reqY = Mathf.RoundToInt(pixelPosition.y);
            int endX = Mathf.RoundToInt(pixelPosition.x + pixelSize.x);
            int endY = Mathf.RoundToInt(pixelPosition.y + pixelSize.y);
            int reqW = Mathf.Max(1, endX - reqX);
            int reqH = Mathf.Max(1, endY - reqY);

            // 클램프 영역 계산
            int ix = Mathf.Clamp(reqX, 0, originalTexture.width);
            int iy = Mathf.Clamp(reqY, 0, originalTexture.height);
            int ix2 = Mathf.Clamp(reqX + reqW, 0, originalTexture.width);
            int iy2 = Mathf.Clamp(reqY + reqH, 0, originalTexture.height);
            int iw = Mathf.Max(0, ix2 - ix);
            int ih = Mathf.Max(0, iy2 - iy);

            Texture2D result = new Texture2D(reqW, reqH, TextureFormat.ARGB32, false);
            var fill = new Color[reqW * reqH];
            for (int i = 0; i < fill.Length; i++) fill[i] = Color.black;
            result.SetPixels(fill);

            if (iw > 0 && ih > 0)
            {
                Color[] src = originalTexture.GetPixels(ix, iy, iw, ih);
                int dx = ix - reqX;
                int dy = iy - reqY;
                result.SetPixels(dx, dy, iw, ih, src);
            }

            result.Apply(false, false);
            Texture2D croppedTexture = ResizeTexture(result, maxCropPixel);
#if UNITY_EDITOR
            Debug.Log($"<color=green>이미지 Crop 성공 요청:{reqW}x{reqH} 교차:{iw}x{ih} 결과:{croppedTexture.width}x{croppedTexture.height}</color>");
#endif
            if (!ReferenceEquals(result, croppedTexture))
            {
                Object.Destroy(result);
            }

            return croppedTexture;
        }

        public static Vector2 CalculateScaledImageSize(Texture2D originalTexture)
        {
            var resolution = GetDeviceResolution();

            var scaledSize = new Vector2(originalTexture.width, originalTexture.height);
            if (originalTexture.width <= resolution.x && originalTexture.height <= resolution.y)
            {
                return AddGapMargin(scaledSize);
            }

            float widthRatio = resolution.x / originalTexture.width;
            float heightRatio = resolution.y / originalTexture.height;
            float scaleFactor = Mathf.Min(widthRatio, heightRatio);
            scaledSize *= scaleFactor;
            return AddGapMargin(scaledSize);
        }
        static Vector2 AddGapMargin(Vector2 correctionVec2)
        {
            var getDeviceResoultion = GetDeviceResolution();
            var addGapMargin = (int)Mathf.Min(getDeviceResoultion.x - correctionVec2.x, getDeviceResoultion.y - correctionVec2.y);
            correctionVec2.x += addGapMargin;
            correctionVec2.y += addGapMargin;
            return correctionVec2;
        }

        public static Vector2 CalculateScaledImageSize(Texture2D originalTexture, Vector2 containerSize)
        {
            if (originalTexture == null)
                return Vector2.zero;

            var scaledSize = new Vector2(originalTexture.width, originalTexture.height);
            if (scaledSize.x <= 0f || scaledSize.y <= 0f || containerSize.x <= 0f || containerSize.y <= 0f)
                return scaledSize;

            float widthRatio = containerSize.x / scaledSize.x;
            float heightRatio = containerSize.y / scaledSize.y;
            float scale = Mathf.Min(widthRatio, heightRatio);
            scaledSize *= scale;
            return scaledSize;
        }

#if UNITY_IOS
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern int GetNativeScreenWidth();

        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern int GetNativeScreenHeight();

#endif
        static Vector2 GetDeviceResolution()
        {
            Vector2 resolution = new Vector2(Screen.width, Screen.height);

#if UNITY_EDITOR
            return resolution;

#elif UNITY_ANDROID
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var metrics = new AndroidJavaObject("android.util.DisplayMetrics"))
            {
                var windowManager = activity.Call<AndroidJavaObject>("getWindowManager");
                var display = windowManager.Call<AndroidJavaObject>("getDefaultDisplay");
                display.Call("getRealMetrics", metrics);

                var width = metrics.Get<int>("widthPixels");
                var height = metrics.Get<int>("heightPixels");
                
                if(width > 0)
                    resolution.x = width;

                if(height > 0)
                    resolution.y = height;
                    
                return resolution;
            }

#elif UNITY_IOS
            var width = GetNativeScreenWidth();
            if(width > 0)
                resolution.x = width;

            var height = GetNativeScreenHeight();
            if(height > 0)
                resolution.y = height;

            return resolution;
#endif
        }
    }
}