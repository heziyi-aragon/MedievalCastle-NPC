using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace MedievalCastle
{
    [RequireComponent(typeof(Camera))]
    public sealed class CastleXRView : MonoBehaviour
    {
        [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 0f, -32f);
        [SerializeField] private Transform headsetSpawn;
        private XROrigin origin;
        private CastlePlayerMotor motor;
        private TrackedPoseDriver poseDriver;
        private InputAction headPosition, headRotation, headTracking;
        private Camera playerCamera;
        private bool headset, configured;

        private void OnEnable() => SceneManager.sceneLoaded += WhenSceneIsReady;
        private void OnDisable() => SceneManager.sceneLoaded -= WhenSceneIsReady;

        private void WhenSceneIsReady(Scene scene, LoadSceneMode mode)
        {
            if (scene == gameObject.scene) InitializeRig();
        }

        // Also supports cameras instantiated into a scene that is already running.
        private void Start()
        {
            if (gameObject.scene.isLoaded) InitializeRig();
        }

        private void InitializeRig()
        {
            if (motor) return;
            playerCamera = GetComponent<Camera>();
            var player = new GameObject("Castle Player");
            player.SetActive(false);
            // The sceneLoaded callback precedes coroutines that may unload an additive scene
            // immediately. The rig must belong to the camera's scene before parenting the camera.
            if (player.scene != gameObject.scene)
            {
                try { SceneManager.MoveGameObjectToScene(player, gameObject.scene); }
                catch (System.ArgumentException)
                {
                    Destroy(player);
                    if (!gameObject.scene.isLoaded) return;
                    throw;
                }
            }
            player.transform.SetPositionAndRotation(headsetSpawn ? headsetSpawn.position : spawnPosition,
                headsetSpawn ? headsetSpawn.rotation : Quaternion.identity);
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(player.transform, false);
            transform.SetParent(offset.transform, false);
            origin = player.AddComponent<XROrigin>();
            origin.Camera = playerCamera;
            origin.Origin = player;
            origin.CameraFloorOffsetObject = offset;
            origin.CameraYOffset = 1.65f;
            origin.enabled = false;
            poseDriver = gameObject.AddComponent<TrackedPoseDriver>();
            headPosition = new InputAction("Head Position", InputActionType.Value, "<XRHMD>/centerEyePosition");
            headRotation = new InputAction("Head Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation");
            headTracking = new InputAction("Head Tracking", InputActionType.Value, "<XRHMD>/trackingState");
            poseDriver.positionInput = new InputActionProperty(headPosition);
            poseDriver.rotationInput = new InputActionProperty(headRotation);
            poseDriver.trackingStateInput = new InputActionProperty(headTracking);
            poseDriver.enabled = false;
            motor = player.AddComponent<CastlePlayerMotor>();
            playerCamera.nearClipPlane = .05f;
            playerCamera.fieldOfView = 65f;
            player.SetActive(true);
            SetHeadset(XRSettings.isDeviceActive);
        }

        private void Update()
        {
            if (motor && headset != XRSettings.isDeviceActive) SetHeadset(XRSettings.isDeviceActive);
        }

        private void SetHeadset(bool value)
        {
            var rig = motor.transform;
            var previousEye = transform.position;
            var previousForward = transform.forward;
            headset = value;
            if (value)
            {
                transform.SetParent(origin.CameraFloorOffsetObject.transform, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                // Negotiate on an active rig every time Link connects, including after desktop play.
                origin.enabled = true;
                origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
                poseDriver.enabled = true;
            }
            else
            {
                poseDriver.enabled = false;
                origin.enabled = false;
                // Desktop height is independent of any late tracking-origin notification.
                transform.SetParent(rig, false);
                transform.localPosition = Vector3.up * 1.65f;
                transform.localRotation = Quaternion.identity;
                if (configured)
                {
                    rig.position = new Vector3(previousEye.x, rig.position.y, previousEye.z);
                    Vector3 flat = Vector3.ProjectOnPlane(previousForward, Vector3.up);
                    if (flat.sqrMagnitude > .001f) rig.rotation = Quaternion.LookRotation(flat);
                    float pitch = -Mathf.Asin(Mathf.Clamp(previousForward.y, -1f, 1f)) * Mathf.Rad2Deg;
                    transform.localRotation = Quaternion.Euler(Mathf.Clamp(pitch, -75f, 75f), 0f, 0f);
                }
            }
            motor.Configure(playerCamera, value);
            configured = true;
        }

        private void OnDestroy()
        {
            headPosition?.Dispose();
            headRotation?.Dispose();
            headTracking?.Dispose();
        }
    }
}
