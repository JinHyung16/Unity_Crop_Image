using HughGame.Helper;
using UnityEngine;
using UnityEngine.UI;

namespace HughGame.UI
{
    public class ProfileWindow : BaseWindow
    {
        public interface IListener
        {
            void OnImageLoaded(Texture2D texture);
        }

        public RawImage RawImg;

        IListener _listener;

        public void Open(IListener listener)
        {
            OpenInternal(() =>
            {
                _listener = listener;
            });
        }

        public void SetProfileImage(Texture2D texture)
        {
            RawImg.texture = texture;
        }

        protected override void OnClosed()
        {
            base.OnClosed();
            Destroy(RawImg.texture);
            _listener = null;
        }

        void RequestPermissionAndOpenGallery()
        {
            Debug.Log("Request Permission");
            bool hasPermission = NativeGallery.CheckPermission(NativeGallery.PermissionType.Read, NativeGallery.MediaType.Image);
            if (hasPermission)
            {
                OpenGallery();
            }
            else
            {
                NativeGallery.RequestPermissionAsync((permission) =>
                {
                    if (permission == NativeGallery.Permission.Granted)
                    {
                        OpenGallery();
                    }
                    else
                    {
                        Debug.Log("<color=red> 갤러리 권한 없음 </color>");
                    }
                }, NativeGallery.PermissionType.Read, NativeGallery.MediaType.Image);
            }
        }

        void OpenGallery()
        {
            NativeGallery.GetImageFromGallery((path) =>
            {
                if (string.IsNullOrEmpty(path))
                {
                    Debug.Log($"<color=red>해당 이미지 경로가 없다.</color>");
                    return;
                }

                LoadImage(path);
            });
        }

        void LoadImage(string path)
        {
            // 추후에 쓸 수 있어서 현재는 무조건 TRUE return
            if (ImageHelper.CheckUploadImageSize(path) == false)
            {
                Debug.Log("<color=red>이미지 용량이 너무 큽니다</color>");
                return;
            }

            var texture2D = NativeGallery.LoadImageAtPath(path, ImageHelper.GetImageMaxPixel(), false);
            if (texture2D == null)
            {
                Debug.Log("<color=red>이미지 로드 실패</color>");
                return;
            }

            //if (texture2D.width > 256 || texture2D.height > 256)
            //{
            //    // 최대 픽셀 256*256
            //    PopupManagement.Instance.OpenCenterMessagePopup(LocalKey.NativeGallery_Image_Too_Large);
            //    NLoad.Loader.RestoreSafe(ref texture2D);
            //    return;
            //}

            Debug.Log("<color=green> 이미지 로드 성공!</color>");
            var result2D = ImageHelper.CompressTextureToASTC(texture2D);
            _listener.OnImageLoaded(result2D);
        }


        #region Button Event Functions
        public void OnClickScreenshot()
        {

        }

        public void OnClickLoadImage()
        {
            if (NativeGallery.IsMediaPickerBusy())
            {
                Debug.Log("<color=yellow> 이미지를 열고 있어요</color>");
                return;
            }

            RequestPermissionAndOpenGallery();

        }
        #endregion
    }
}
