using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HughGame.Managers
{
    public class CachedProfileImageManager
    {
        private static CachedProfileImageManager _instance;
        public static CachedProfileImageManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new CachedProfileImageManager();
                return _instance;
            }
        }

        readonly Dictionary<string, string> _cachedProfileURLDict = new Dictionary<string, string>();

        const string FolderName = ".ProfileImage";
        const string ImageListFolderName = ".Image";
        const string ProfileListFileName = ".profilelist.txt";
        const string Separator = "::";

        const int CacheDurationDays = 7;

        private string _indexFilePath;
        private bool _isDirty = false;

        public void InitAfterLogin()
        {
            try
            {
                var cacheDirectory = Path.Combine(Application.persistentDataPath, FolderName);
                if (Directory.Exists(cacheDirectory) == false)
                {
                    Directory.CreateDirectory(cacheDirectory);

#if UNITY_EDITOR_WIN
                    File.SetAttributes(cacheDirectory, File.GetAttributes(cacheDirectory) | FileAttributes.Hidden);
#endif
                }
                _indexFilePath = Path.Combine(cacheDirectory, ProfileListFileName);

                if (File.Exists(_indexFilePath) == false)
                {
                    File.Create(_indexFilePath).Close();
#if UNITY_EDITOR_WIN
                    File.SetAttributes(cacheDirectory, File.GetAttributes(cacheDirectory) | FileAttributes.Hidden);
#endif
                }

                LoadCacheIndexFromFile();
                CleanUpExpiredCache();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[파일 생성 실패]: {e.Message}");
            }
        }
        public void Clear()
        {
            SaveCacheIndexToFile();
            _cachedProfileURLDict.Clear();
        }

        public void SaveProfileImage(string accountId, Texture2D texture)
        {
            try
            {
                var imagePath = GetProfileImagePath(accountId);
                var imageDir = Path.GetDirectoryName(imagePath);

                if (imageDir != null && Directory.Exists(imageDir) == false)
                {
                    Directory.CreateDirectory(imageDir);
                }

                byte[] bytes = texture.EncodeToJPG(50);
                File.WriteAllBytes(imagePath, bytes);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"프로필 이미지 저장 실패 (AccountId: {accountId}): {e.Message}");
            }
        }

        public string GetProfileImagePath(string accountId)
        {
            var imageDir = Path.Combine(Application.persistentDataPath, FolderName, ImageListFolderName);
            return Path.Combine(imageDir, accountId);
        }

        public string GetCachedProfileUrl(string accountId)
        {
            _cachedProfileURLDict.TryGetValue(accountId, out var url);
            return url;
        }


        public void UpdateProfileCache(string accountId, string url)
        {
            if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(url))
                return;

            if (_cachedProfileURLDict.TryGetValue(accountId, out var oldUrl) && oldUrl == url)
                return;

            _cachedProfileURLDict[accountId] = url;
            _isDirty = true;
        }

        private void CleanUpExpiredCache()
        {
            try
            {
                var imageDir = Path.Combine(Application.persistentDataPath, FolderName, ImageListFolderName);
                if (Directory.Exists(imageDir) == false)
                    return;

                var deletedAccountIds = new List<string>();
                var files = Directory.GetFiles(imageDir);

                foreach (var filePath in files)
                {
                    var lastWriteTime = File.GetLastWriteTimeUtc(filePath);
                    if ((System.DateTime.UtcNow - lastWriteTime).TotalDays > CacheDurationDays)
                    {
                        var accountId = Path.GetFileName(filePath);

                        File.Delete(filePath);
                        deletedAccountIds.Add(accountId);
#if UNITY_EDITOR
                        Debug.Log($"만료된 프로필 이미지 삭제: {filePath}");
#endif
                    }
                }

                if (deletedAccountIds.Count > 0)
                {
                    foreach (var accountId in deletedAccountIds)
                    {
                        _cachedProfileURLDict.Remove(accountId);
                    }
                    _isDirty = true;
#if UNITY_EDITOR
                    Debug.Log($"{deletedAccountIds.Count}개의 만료된 캐시 항목을 정리했습니다.");
#endif
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"만료된 캐시 정리 실패: {e.Message}");
            }
        }

        private void LoadCacheIndexFromFile()
        {
            if (File.Exists(_indexFilePath) == false)
            {
#if UNITY_EDITOR
                Debug.Log($"<color=red>{ProfileListFileName} 생성되지 않아 로드 불가</color>");
#endif
                return;
            }

            try
            {
                var lines = File.ReadAllLines(_indexFilePath);
                _cachedProfileURLDict.Clear();

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = line.Split(new[] { Separator }, 2, System.StringSplitOptions.None);
                    Debug.Assert(parts.Length == 2, $"{parts} : 형식이 AccountId::URL 인지 확인");

                    var accountId = parts[0];
                    var url = parts[1];

                    if (string.IsNullOrEmpty(accountId))
                        continue;

                    _cachedProfileURLDict[accountId] = url;
                }
#if UNITY_EDITOR
                Debug.Log($"저장되어있던 정보 {_cachedProfileURLDict.Count} 개 로드 성공");
#endif
            }
            catch (System.Exception e)
            {
                _cachedProfileURLDict.Clear();
                Debug.LogError($"로드 실패: {e.Message}");
            }
        }

        private void SaveCacheIndexToFile()
        {
            if (_isDirty == false)
                return;

            try
            {
                using (var writer = new StreamWriter(_indexFilePath, false))
                {
                    foreach (var entry in _cachedProfileURLDict)
                    {
                        writer.WriteLine($"{entry.Key}{Separator}{entry.Value}");
                    }
                }

                _isDirty = false;
#if UNITY_EDITOR
                Debug.Log($"<color=green> {ProfileListFileName}에 {_cachedProfileURLDict.Count} 개 저장 성공!</color>");
#endif
            }
            catch (System.Exception e)
            {
                Debug.LogError($"파일 저장 실패: {e.Message}");
            }
        }
    }
}
