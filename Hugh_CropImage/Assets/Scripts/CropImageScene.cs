using HughGame.Managers;
using HughGame.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HughGame.HScene
{
    public class CropImageScene : MonoBehaviour
    {
        [SerializeField]
        private CropWindow CropWindow;

        private void Awake()
        {
            CachedProfileImageManager.Instance.InitAfterLogin();
        }

        private void Start()
        {
            
        }
    }
}
