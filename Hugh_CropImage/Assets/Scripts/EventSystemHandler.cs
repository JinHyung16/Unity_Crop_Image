using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem.UI;
#endif

namespace HughGame.UI.NCrop
{
    [DefaultExecutionOrder(1000)]
    public class EventSystemHandler : MonoBehaviour
    {
#pragma warning disable 0649
        [SerializeField]
        GameObject _embeddedEventSystem;
#pragma warning restore 0649

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
		private void Awake()
		{
			StandaloneInputModule legacyInputModule = _embeddedEventSystem.GetComponent<StandaloneInputModule>();
			if( legacyInputModule )
			{
				DestroyImmediate( legacyInputModule );
				_embeddedEventSystem.AddComponent<InputSystemUIInputModule>();
			}
		}
#endif

        private void OnEnable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;

            ActivateEventSystemIfNeeded();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;

            DeactivateEventSystem();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
#if UNITY_2017_2_OR_NEWER
            DeactivateEventSystem();
#endif
            ActivateEventSystemIfNeeded();
        }

        private void OnSceneUnloaded(Scene current)
        {
            DeactivateEventSystem();
        }

        private void ActivateEventSystemIfNeeded()
        {
            if (_embeddedEventSystem && !EventSystem.current)
                _embeddedEventSystem.SetActive(true);
        }

        private void DeactivateEventSystem()
        {
            if (_embeddedEventSystem)
                _embeddedEventSystem.SetActive(false);
        }
    }
}