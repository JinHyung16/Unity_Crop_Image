using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        readonly Dictionary<string, List<string>> _cachedUrlDict = new Dictionary<string, List<string>>();

        const string FolderName = "ProfileImage";
        const string ImageListFolderName = "Image";
        const string ProfileListFileName = "profilelist.txt";
        const string Separator = "::";

        const int CacheDurationDays = 7;

        string _indexFilePath;
        bool _isDirty = false;

        List<string> _removeFileList = new List<string>();

        public void InitAfterLogin()
        {
            try
            {
                var cacheDirectory = Path.Combine(Application.persistentDataPath, FolderName);
                if (Directory.Exists(cacheDirectory) == false)
                {
                    Directory.CreateDirectory(cacheDirectory);
                }

#if UNITY_EDITOR_WIN
                var attrs = File.GetAttributes(cacheDirectory);
                if ((attrs & FileAttributes.Hidden) != 0)
                {
                    File.SetAttributes(cacheDirectory, attrs & ~FileAttributes.Hidden);
                }
#endif
                _indexFilePath = Path.Combine(cacheDirectory, ProfileListFileName);

                if (File.Exists(_indexFilePath) == false)
                {
                    File.Create(_indexFilePath).Close();
                }

                LoadCacheIndexFromFile();
                CleanUpExpiredCache();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[���� ���� ����]: {e.Message}");
            }
        }
        public void Clear()
        {
            RemoveCacheIndexToFile();
            SaveCacheIndexToFile();
            _cachedUrlDict.Clear();
            _removeFileList.Clear();
        }
        public void SaveProfileImage(string accountId, string url, Texture2D texture)
        {
            try
            {
                var imagePath = GetProfileImagePath(accountId, url);
                var imageDir = Path.GetDirectoryName(imagePath);

                if (imageDir != null && Directory.Exists(imageDir) == false)
                {
                    Directory.CreateDirectory(imageDir);
                }

                byte[] bytes = texture.EncodeToJPG(100);
                File.WriteAllBytes(imagePath, bytes);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"������ �̹��� ���� ���� (AccountId: {accountId}): {e.Message}");
            }
        }

        public string GetProfileImagePath(string accountId, string url)
        {
            var imageDir = Path.Combine(Application.persistentDataPath, FolderName, ImageListFolderName);
            var fileName = (accountId + url).Replace("/", "_").Replace("\\", "_");
            return Path.Combine(imageDir, fileName);
        }

        public string GetCachedProfileUrl(string accountId, string url)
        {
            //_cachedProfileURLDict.TryGetValue(accountId, out var url);
            if (_cachedUrlDict.TryGetValue(accountId, out var list))
            {
                if (list == null || list.Count == 0)
                    return string.Empty;

                var findIndex = list.FindIndex(oldUrl => oldUrl == url);
                if (findIndex >= 0)
                {
                    return list[findIndex];
                }
            }
            return string.Empty;
        }


        public void UpdateProfileCache(string accountId, string url)
        {
            if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(url))
                return;

            if (_cachedUrlDict.TryGetValue(accountId, out var list) == false)
            {
                list = new List<string>();
                _cachedUrlDict.Add(accountId, list);
            }

            var findIndex = list.FindIndex(existingUrl => existingUrl == url);
            if (findIndex >= 0)
                return;

            list.Add(url);
            _isDirty = true;
        }

        public void RemoveProfileRegist(string accountId, string url)
        {
            if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(url))
                return;


            if (_cachedUrlDict.TryGetValue(accountId, out var list) == false)
                return;

            var findIndex = list.FindIndex(existingUrl => existingUrl == url);
            if (findIndex < 0)
                return;

            list.RemoveAt(findIndex);

            var profileFilePath = GetProfileImagePath(accountId, url);
            _removeFileList.Add(profileFilePath);
        }


        void CleanUpExpiredCache()
        {
            try
            {
                var imageDir = Path.Combine(Application.persistentDataPath, FolderName, ImageListFolderName);
                if (Directory.Exists(imageDir) == false)
                    return;

                var files = Directory.GetFiles(imageDir);
                var expiredPaths = new HashSet<string>();

                foreach (var filePath in files)
                {
                    var lastWriteTime = File.GetLastWriteTimeUtc(filePath);
                    if ((System.DateTime.UtcNow - lastWriteTime).TotalDays > CacheDurationDays)
                    {
                        // ���� �켱 ����
                        try
                        {
                            File.Delete(filePath);
                            expiredPaths.Add(filePath);
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"����� ������ �̹��� ���� ����: {filePath} - {ex.Message}");
                        }
#if UNITY_EDITOR
                        Debug.Log($"����� ������ �̹��� ����: {filePath}");
#endif
                    }
                }

                if (expiredPaths.Count > 0)
                {
                    // ������ ���Ͽ� ���εǴ� (accountId, url) ���� �ε������� ����
                    var keysToRemove = new List<string>();
                    foreach (var kv in _cachedUrlDict)
                    {
                        var urls = kv.Value;
                        if (urls == null || urls.Count == 0)
                            continue;

                        // ���� accountId�� url ����Ʈ����, ���� ��ΰ� ���� ��Ͽ� �ִ� �׸� ����
                        urls.RemoveAll(u => expiredPaths.Contains(GetProfileImagePath(kv.Key, u)));

                        if (urls.Count == 0)
                            keysToRemove.Add(kv.Key);
                    }

                    foreach (var k in keysToRemove)
                        _cachedUrlDict.Remove(k);

                    _isDirty = true;
#if UNITY_EDITOR
                    Debug.Log($"{expiredPaths.Count}���� ����� ĳ�� ���ϰ� ���� �ε����� �����߽��ϴ�.");
#endif
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"����� ĳ�� ���� ����: {e.Message}");
            }
        }

        void LoadCacheIndexFromFile()
        {
            if (File.Exists(_indexFilePath) == false)
            {
#if UNITY_EDITOR
                Debug.Log($"<color=red>{ProfileListFileName} �������� �ʾ� �ε� �Ұ�</color>");
#endif
                return;
            }

            try
            {
                var lines = File.ReadAllLines(_indexFilePath);
                _cachedUrlDict.Clear();
                //_cachedProfileURLDict.Clear();

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = line.Split(new[] { Separator }, 2, System.StringSplitOptions.None);
                    Debug.Assert(parts.Length == 2, $"{parts} : ������ AccountId::URL ���� Ȯ��");

                    var accountId = parts[0];
                    var url = parts[1];

                    if (string.IsNullOrEmpty(accountId))
                        continue;

                    if (_cachedUrlDict.TryGetValue(accountId, out var urlList) == false)
                    {
                        urlList = new List<string>();
                        _cachedUrlDict.Add(accountId, urlList);
                    }
                    urlList.Add(url);

                    //_cachedProfileURLDict[accountId] = url;
                }
#if UNITY_EDITOR
                Debug.Log($"����Ǿ��ִ� ���� {_cachedUrlDict.Count} �� �ε� ����");
#endif
            }
            catch (System.Exception e)
            {
                _cachedUrlDict.Clear();
                Debug.LogError($"�ε� ����: {e.Message}");
            }
        }

        void SaveCacheIndexToFile()
        {
            if (_isDirty == false)
                return;

            try
            {
                using (var writer = new StreamWriter(_indexFilePath, false))
                {
                    foreach (var entry in _cachedUrlDict)
                    {
                        foreach (var url in entry.Value)
                        {
                            writer.WriteLine($"{entry.Key}{Separator}{url}");
                        }
                    }
                }
                _isDirty = false;
#if UNITY_EDITOR
                var count = _cachedUrlDict.Values == null ? 0 : _cachedUrlDict.Values.Count;
                Debug.Log($"<color=green> {ProfileListFileName}�� {_cachedUrlDict.Count}�� ������ {count}�� URL ���� ����!</color>");
#endif
            }
            catch (System.Exception e)
            {
                Debug.LogError($"���� ���� ����: {e.Message}");
            }
        }

        void RemoveCacheIndexToFile()
        {
            if (_removeFileList.Count == 0)
                return;

            int successCount = 0;
            foreach (var filePath in _removeFileList)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        successCount++;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"������ �̹��� ���� ���� ����: {filePath} - {e.Message}");
                }
            }

#if UNITY_EDITOR
            Debug.Log($"���� ��� ����� ������ �̹��� {successCount}�� ���� �Ϸ�");
#endif
            _removeFileList.Clear();

        }
    }
}
