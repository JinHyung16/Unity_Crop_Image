using HughGame.Managers;
using HughGame.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HughGame.HScene
{
    public class CropImageScene 
        : MonoBehaviour
        , ProfileWindow.IListener
        , CropWindow.IListener
    {
        [SerializeField]
        private ProfileWindow profileWindow;

        [SerializeField]
        private CropWindow cropWindow;

        private void Awake()
        {
            CachedProfileImageManager.Instance.InitAfterLogin();
        }

        private void Start()
        {
            OpenProfileWindow();
            CloseCropWindow();
        }

        public void OpenProfileWindow()
        {
            profileWindow.Open(this);
        }

        public void SetProfileWindow(Texture2D texture)
        {
            if (profileWindow.IsOpend() == false)
            {
                profileWindow.Open(this);
            }

            profileWindow.SetProfileImage(texture);
        }

        public void CloseProfileWindow()
        {
            profileWindow.Close();
        }

        public void OpenCropWindow(Texture2D texture)
        {
            cropWindow.Open(texture, this);
        }

        public void CloseCropWindow()
        {
            cropWindow.Close();
        }

        #region ProfileWindow.IListener
        void ProfileWindow.IListener.OnImageLoaded(Texture2D texture)
        {
            OpenCropWindow(texture);
        }

        #endregion

        #region CropWindow.IListener

        void CropWindow.IListener.OnCropCancelled()
        {
            CloseCropWindow();
        }

        void CropWindow.IListener.OnImageCropped(Texture2D croppedTexture)
        {
            CloseCropWindow();
            SetProfileWindow(croppedTexture);
        }

        #endregion
    }
}
