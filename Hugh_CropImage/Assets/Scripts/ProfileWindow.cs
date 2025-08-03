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
                        Debug.Log("<color=red> ������ ���� ���� </color>");
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
                    Debug.Log($"<color=red>�ش� �̹��� ��ΰ� ����.</color>");
                    return;
                }

                LoadImage(path);
            });
        }

        void LoadImage(string path)
        {
            // ���Ŀ� �� �� �־ ����� ������ TRUE return
            if (ImageHelper.CheckUploadImageSize(path) == false)
            {
                Debug.Log("<color=red>�̹��� �뷮�� �ʹ� Ů�ϴ�</color>");
                return;
            }

            var texture2D = NativeGallery.LoadImageAtPath(path, ImageHelper.GetImageMaxPixel(), false);
            if (texture2D == null)
            {
                Debug.Log("<color=red>�̹��� �ε� ����</color>");
                return;
            }

            //if (texture2D.width > 256 || texture2D.height > 256)
            //{
            //    // �ִ� �ȼ� 256*256
            //    PopupManagement.Instance.OpenCenterMessagePopup(LocalKey.NativeGallery_Image_Too_Large);
            //    NLoad.Loader.RestoreSafe(ref texture2D);
            //    return;
            //}

            Debug.Log("<color=green> �̹��� �ε� ����!</color>");
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
                Debug.Log("<color=yellow> �̹����� ���� �־��</color>");
                return;
            }

            RequestPermissionAndOpenGallery();

        }
        #endregion
    }
}
