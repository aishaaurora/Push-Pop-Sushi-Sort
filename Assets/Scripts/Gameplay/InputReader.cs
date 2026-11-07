using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SushiSort.Gameplay
{
    /// <summary>
    /// Captures player screen inputs (Mouse / Touch) and performs 3D raycasting 
    /// against tubes to notify listeners of player selection.
    /// </summary>
    public class InputReader : MonoBehaviour
    {
        [Header("Raycast Settings")]
        [Tooltip("LayerMask containing the PushPopTube colliders.")]
        [SerializeField] private LayerMask tubeLayerMask = ~0;

        [Tooltip("Target camera for raycasting. Defaults to Camera.main if null.")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("Maximum raycast travel distance.")]
        [SerializeField] private float maxRayDistance = 100f;

        [Tooltip("If true, clicks/touches on UI elements will be ignored.")]
        [SerializeField] private bool ignoreClicksOverUI = true;

        /// <summary>
        /// Event fired when player successfully clicks or taps on a PushPopTube.
        /// </summary>
        public static event Action<PushPopTube> OnTubeClicked;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void Update()
        {
            // Fallback if main camera reference became available after Awake
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null) return;
            }

            // Handle Mouse Click
            if (Input.GetMouseButtonDown(0))
            {
                if (ignoreClicksOverUI && IsPointerOverUI())
                {
                    return;
                }

                ProcessScreenRaycast(Input.mousePosition);
            }
            // Handle Mobile Touch
            else if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    if (ignoreClicksOverUI && IsTouchOverUI(touch.fingerId))
                    {
                        return;
                    }

                    ProcessScreenRaycast(touch.position);
                }
            }
        }

        private void ProcessScreenRaycast(Vector3 screenPosition)
        {
            Ray ray = targetCamera.ScreenPointToRay(screenPosition);

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, tubeLayerMask))
            {
                PushPopTube tube = hit.collider.GetComponentInParent<PushPopTube>();
                if (tube != null)
                {
                    OnTubeClicked?.Invoke(tube);
                }
            }
        }

        private bool IsPointerOverUI()
        {
            if (EventSystem.current == null) return false;
            return EventSystem.current.IsPointerOverGameObject();
        }

        private bool IsTouchOverUI(int fingerId)
        {
            if (EventSystem.current == null) return false;
            return EventSystem.current.IsPointerOverGameObject(fingerId);
        }
    }
}

