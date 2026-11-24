using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace MedievalCastle
{
    [RequireComponent(typeof(Camera))]
    public sealed class CastleXRView : MonoBehaviour
    {
        [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 0f, -32f);
        [SerializeField] private Transform headsetSpawn;
        [SerializeField] private Transform desktopView;
        [SerializeField, Range(25f, 75f)] private float desktopFieldOfView = 40f;

        private InputAction headPosition;
        private InputAction headRotation;
        private InputAction headTracking;

        private IEnumerator Start()
        {
            if (!XRSettings.isDeviceActive && desktopView && desktopView.gameObject.activeInHierarchy)
            {
                transform.SetPositionAndRotation(desktopView.position, desktopView.rotation);
                GetComponent<Camera>().fieldOfView = desktopFieldOfView;
            }

            yield return new WaitUntil(() => XRSettings.isDeviceActive);

            var originObject = new GameObject("Castle XR Origin");
            originObject.SetActive(false);
            if (headsetSpawn && headsetSpawn.gameObject.activeInHierarchy)
                originObject.transform.SetPositionAndRotation(headsetSpawn.position, headsetSpawn.rotation);
            else
                originObject.transform.position = spawnPosition;
            var floorOffset = new GameObject("Camera Offset");
            floorOffset.transform.SetParent(originObject.transform, false);
            transform.SetParent(floorOffset.transform, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            var camera = GetComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            var poseDriver = gameObject.AddComponent<TrackedPoseDriver>();
            headPosition = new InputAction("Head Position", InputActionType.Value, "<XRHMD>/centerEyePosition");
            headRotation = new InputAction("Head Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation");
            headTracking = new InputAction("Head Tracking", InputActionType.Value, "<XRHMD>/trackingState");
            poseDriver.positionInput = new InputActionProperty(headPosition);
            poseDriver.rotationInput = new InputActionProperty(headRotation);
            poseDriver.trackingStateInput = new InputActionProperty(headTracking);

            var origin = originObject.AddComponent<XROrigin>();
            origin.Camera = camera;
            origin.Origin = originObject;
            origin.CameraFloorOffsetObject = floorOffset;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            origin.CameraYOffset = 1.65f;
            originObject.SetActive(true);
        }

        private void OnDestroy()
        {
            headPosition?.Dispose();
            headRotation?.Dispose();
            headTracking?.Dispose();
        }
    }
}
