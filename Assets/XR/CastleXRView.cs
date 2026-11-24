using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

[assembly: InternalsVisibleTo("CastleNPC.Exploration.PlayModeTests")]

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
        internal const float EyeHeight = 1.75f;
        private Transform heightOffset;
        private bool calibrated;
        private readonly List<XRInputSubsystem> inputSubsystems = new List<XRInputSubsystem>();
        private XRInputSubsystem inputSubsystem;
        private TrackingOriginModeFlags sampledMode;
        private float minHeight, maxHeight, stableSeconds, heightSum, sampledFloorOffset;
        private int stableSamples;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += WhenSceneIsReady;
            InputDevices.deviceConnected += OnDeviceConnected;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= WhenSceneIsReady;
            InputDevices.deviceConnected -= OnDeviceConnected;
        }

        private void OnDeviceConnected(UnityEngine.XR.InputDevice device)
        {
            if (headset && (device.characteristics & InputDeviceCharacteristics.HeadMounted) != 0)
                ResetCalibration();
        }

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
            heightOffset = new GameObject("Eye height calibration").transform;
            heightOffset.SetParent(offset.transform, false);
            transform.SetParent(heightOffset, false);
            origin = player.AddComponent<XROrigin>();
            origin.Camera = playerCamera;
            origin.Origin = player;
            origin.CameraFloorOffsetObject = offset;
            origin.CameraYOffset = EyeHeight;
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
            if (!headset) return;
            if (inputSubsystem == null || !inputSubsystem.running)
            {
                SubsystemManager.GetInstances(inputSubsystems);
                inputSubsystem = inputSubsystems.Find(s => s.running);
            }
            // Query the running subsystem even if the requested Floor mode was already active
            // and the runtime never sent a trackingOriginUpdated event to XROrigin.
            var mode = inputSubsystem != null ? inputSubsystem.GetTrackingOriginMode() : origin.CurrentTrackingOriginMode;
            bool tracked = (headTracking.ReadValue<int>() & (int)InputTrackingState.Position) != 0;
            SampleEyeHeight(tracked, headPosition.ReadValue<Vector3>().y, mode, Time.unscaledDeltaTime);
        }

        internal void SetHeadset(bool value)
        {
            var rig = motor.transform;
            var previousEye = transform.position;
            var previousForward = transform.forward;
            headset = value;
            ResetCalibration();
            if (value)
            {
                transform.SetParent(heightOffset, false);
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
                transform.localPosition = Vector3.up * EyeHeight;
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

        private void ResetCalibration()
        {
            calibrated = false;
            stableSamples = 0;
            sampledMode = TrackingOriginModeFlags.Unknown;
            heightOffset.localPosition = Vector3.zero;
        }

        internal void SampleEyeHeight(bool tracked, float rawHeight, TrackingOriginModeFlags mode, float dt)
        {
            float floorOffset = heightOffset.parent.localPosition.y;
            if (mode != sampledMode || !Mathf.Approximately(sampledFloorOffset, floorOffset))
            {
                ResetCalibration();
                sampledMode = mode;
                sampledFloorOffset = floorOffset;
            }
            if (calibrated || !headset) return;
            // Core Utils owns the parent offset (zero for Floor, nominal height for Device).
            // Calibrate a separate node once so later leaning or crouching is still 1:1.
            float height = floorOffset + rawHeight;
            if (!tracked || mode == TrackingOriginModeFlags.Unknown || !(height >= .30f && height <= 2.65f))
            {
                stableSamples = 0;
                return;
            }
            if (stableSamples == 0 || height < maxHeight - .035f || height > minHeight + .035f)
            {
                stableSamples = 0;
                stableSeconds = heightSum = 0;
                minHeight = maxHeight = height;
            }
            minHeight = Mathf.Min(minHeight, height);
            maxHeight = Mathf.Max(maxHeight, height);
            heightSum += height;
            stableSamples++;
            stableSeconds += Mathf.Min(.05f, dt);
            if (stableSamples >= 6 && stableSeconds >= .30f)
            {
                heightOffset.localPosition = Vector3.up * (EyeHeight - heightSum / stableSamples);
                calibrated = true;
            }
        }

        private void OnDestroy()
        {
            headPosition?.Dispose();
            headRotation?.Dispose();
            headTracking?.Dispose();
        }
    }
}
