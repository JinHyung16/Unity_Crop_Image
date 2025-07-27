using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace HughGame.Helper
{
    public static class ImageHelper
    {
        public delegate void ImageLoadCallback(Texture2D texture, bool success);

        static Texture2D ResizeTexture(Texture2D source, int maxWidth, int maxHeight)
        {
            float ratio = Mathf.Min((float)maxWidth / source.width, (float)maxHeight / source.height);
            int newWidth = Mathf.RoundToInt(source.width * ratio);
            int newHeight = Mathf.RoundToInt(source.height * ratio);

            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
            Graphics.Blit(source, rt);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            
            Texture2D resized = new Texture2D(newWidth, newHeight);
            resized.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            resized.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return resized;
        }


        public static Texture2D ResizeTexture(Texture2D source, int maxSize)
        {
            if (source.width <= maxSize && source.height <= maxSize)
                return source;

            float ratio = Mathf.Min((float)maxSize / source.width, (float)maxSize / source.height);
            var newWidth = Mathf.RoundToInt(source.width * ratio);
            var newHeight = Mathf.RoundToInt(source.height * ratio);

            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
            Graphics.Blit(source, rt);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            
            Texture2D resized = new Texture2D(newWidth, newHeight);
            resized.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            resized.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);    
            return resized;
        }

        public static Sprite CreateSprite(Texture2D texture)
        {
            if (texture == null)
                return null;

            var compressTexture = ImageHelper.CompressTextureToASTC(texture);
            return Sprite.Create(compressTexture, new Rect(0, 0, compressTexture.width, compressTexture.height), new Vector2(0.5f, 0.5f));
        }

        public static Texture2D CompressTextureToASTC(Texture2D originalTexture, int astcBlockSize = 4)
        {
            TextureFormat astcFormat = GetASTCFormat(astcBlockSize);
            if (SystemInfo.SupportsTextureFormat(astcFormat) == false)
            {
                Debug.LogWarning($"ASTC {astcBlockSize}x{astcBlockSize} format not supported on this device. Using original texture.");
                return originalTexture;
            }

            Texture2D compressedTexture = new Texture2D(originalTexture.width, originalTexture.height, astcFormat, false);

            Color32[] pixels = originalTexture.GetPixels32();
            compressedTexture.SetPixels32(pixels);
            compressedTexture.Apply();

            Debug.Log($"Texture compressed to ASTC {astcBlockSize}x{astcBlockSize}: {compressedTexture.width}x{compressedTexture.height}");
            return compressedTexture;
        }


        static TextureFormat GetASTCFormat(int blockSize)
        {
            switch (blockSize)
            {
                case 4: 
                    return TextureFormat.ASTC_4x4;

                case 5: 
                    return TextureFormat.ASTC_5x5;

                case 6: 
                    return TextureFormat.ASTC_6x6;

                case 8: 
                    return TextureFormat.ASTC_8x8;

                case 10: 
                    return TextureFormat.ASTC_10x10;

                case 12: 
                    return TextureFormat.ASTC_12x12;

                default: 
                    Debug.LogWarning($"Unsupported ASTC block size: {blockSize}. Using 4x4.");
                    return TextureFormat.ASTC_4x4;
            }
        }

        public static bool CheckUploadImageSize(string filePath, long maxSizeBytes = 1000000)
        {
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
                return false;
                
            System.IO.FileInfo fileInfo = new System.IO.FileInfo(filePath);
            return fileInfo.Length <= maxSizeBytes;
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


        public static Texture2D CropTexture(Texture2D originalTexture, RectTransform imageRect, Vector2 cropPosition, Vector2 cropSize, int maxCropSize = 512)
        {
            if (originalTexture == null)
                return null;

            float imageWidth = imageRect.rect.width;
            float imageHeight = imageRect.rect.height;

            float normalizedX = cropPosition.x / imageWidth;
            float normalizedY = cropPosition.y / imageHeight;
            float normalizedWidth = cropSize.x / imageWidth;
            float normalizedHeight = cropSize.y / imageHeight;

            int textureX = Mathf.RoundToInt(normalizedX * originalTexture.width);
            int textureY = Mathf.RoundToInt(normalizedY * originalTexture.height);
            int textureWidth = Mathf.RoundToInt(normalizedWidth * originalTexture.width);
            int textureHeight = Mathf.RoundToInt(normalizedHeight * originalTexture.height);

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
                croppedTexture = ResizeTexture(croppedTexture, maxCropSize);
            }

            return croppedTexture;
        }

    }
} 