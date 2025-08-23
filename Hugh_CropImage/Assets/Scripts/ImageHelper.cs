using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using HughGame.Managers;

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
            var cachedURL = CachedProfileImageManager.Instance.GetCachedProfileUrl(accountId);

            if (string.IsNullOrEmpty(cachedURL) || cachedURL != url)
            {
                using (var request = UnityWebRequestTexture.GetTexture(url))
                {
                    request.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36");
                    request.SetRequestHeader("Cache-Control", "no-cache, no-store, must-revalidate");
                    request.SetRequestHeader("Pragma", "no-cache");
                    request.SetRequestHeader("Expires", "0");

                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var downloadedTexture = DownloadHandlerTexture.GetContent(request);
                        if (downloadedTexture != null)
                        {
                            SaveProfileImage(accountId, downloadedTexture);
                            UpdateProfileCache(accountId, url);
                            callback?.Invoke(downloadedTexture, true);
                            yield break;
                        }
                    }
#if UNITY_EDITOR
                    Debug.Log($"<color=red>UnityWebRequest 이미지 다운로드 실패" +
                        $"AccountId[{accountId}]" +
                        $"URL[{url}]" +
                        $"Error[{request.error}]</color>");
#endif
                    callback?.Invoke(null, false);
                    yield break;
                }
            }
        }
        private static void SaveProfileImage(string accountId, Texture2D texture)
        {
            CachedProfileImageManager.Instance.SaveProfileImage(accountId, texture);
        }

        private static void UpdateProfileCache(string accountId, string url)
        {
            CachedProfileImageManager.Instance.UpdateProfileCache(accountId, url);
        }

        private static Texture2D ResizeTexture(Texture2D source, int maxWidth, int maxHeight)
        {
            if (source == null) return null;
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
            // UI 좌표계에서 텍스처 좌표계로 변환
            float scaleX = (float)originalTexture.width / originalImageRect.sizeDelta.x;
            float scaleY = (float)originalTexture.height / originalImageRect.sizeDelta.y;

            // OriginalImageRect의 좌상단 위치 계산 -> 중앙 앵커 보정
            Vector2 imageTopLeft = originalImageRect.anchoredPosition - (originalImageRect.sizeDelta * 0.5f);

            // Selection의 ImageHolder 기준 절대 위치를 OriginalImageRect 기준 상대 위치로 변환
            // ImageHolder는 화면 전체이고 중앙 앵커이므로 좌상단으로 변환
            Vector2 selectionAbsolute = selection.anchoredPosition - (selection.parent.GetComponent<RectTransform>().sizeDelta * 0.5f);
            Vector2 selectionRelativeToImage = selectionAbsolute - imageTopLeft;

            // Selection의 픽셀 위치 계산 (Texture2D 좌표에선 원점은 좌하단)
            Vector2 pixelPosition = new Vector2(
                selectionRelativeToImage.x * scaleX,
                originalTexture.height - ((selectionRelativeToImage.y + selection.sizeDelta.y) * scaleY)
            );

            Vector2 pixelSize = new Vector2(
                selection.sizeDelta.x * scaleX,
                selection.sizeDelta.y * scaleY
            );

            return CropTexture(originalTexture, pixelPosition, pixelSize, maxCropPixel);
        }

        static Texture2D CropTexture(Texture2D originalTexture, Vector2 pixelPosition, Vector2 pixelSize, int maxCropPixel)
        {
            if (originalTexture == null)
                return null;
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
            croppedTexture = ResizeTexture(croppedTexture, maxCropPixel);

#if UNITY_EDITOR
            Debug.Log($"<color=green>이미지 Crop 성공" +
                        $"결과 width*heigth : {croppedTexture.width}*{croppedTexture.height}");
#endif
            return croppedTexture;
        }

        public static Vector2 CalculateScaledImageSizeForCropWindow(Texture2D originalTexture)
        {
            var scaledSize = new Vector2(originalTexture.width, originalTexture.height);
            if (originalTexture.width <= Screen.width && originalTexture.height <= Screen.height)
            {
                return AddGapMargin(scaledSize);
            }

            float widthRatio = (float)Screen.width / originalTexture.width;
            float heightRatio = (float)Screen.height / originalTexture.height;
            float scaleFactor = Mathf.Min(widthRatio, heightRatio);
            scaledSize.x *= scaleFactor;
            scaledSize.y *= scaleFactor;

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