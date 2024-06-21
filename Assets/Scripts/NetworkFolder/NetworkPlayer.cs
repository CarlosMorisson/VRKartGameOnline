using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Photon.Pun;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;

public class NetworkPlayer : MonoBehaviour
{
    [Header("Online Transform")]
    [SerializeField]
    Transform Head, LeftHand, RightHand;
    [Header("Online Animation")]
    [SerializeField]
    Animator LeftHandAnimator, RightHandAnimator;
    private PhotonView _photonView;
    [SerializeField]
    private Transform xrOrigin, _headRig, _leftHandRig, _rightHandRig;
    void Start()
    {
        _photonView = GetComponent<PhotonView>();
        xrOrigin = GameObject.FindGameObjectWithTag("Origin").transform;
        _headRig = GameObject.FindGameObjectWithTag("Head").transform; 
        _leftHandRig = GameObject.FindGameObjectWithTag("LeftHand").transform; ;
        _rightHandRig = GameObject.FindGameObjectWithTag("RightHand").transform; 

        if (_photonView.IsMine)
        {
            foreach (var item in GetComponentsInChildren<Renderer>())
            {
                item.enabled = false;
            }
        }
        
    }

    // Update is called once per frame
    void Update()
    {
        if (_photonView.IsMine)
        {

            MapPosition(Head, _headRig);
            MapPosition(LeftHand, _leftHandRig);
            MapPosition(RightHand, _rightHandRig);

            UpdateHandAnimation(InputDevices.GetDeviceAtXRNode(XRNode.LeftHand), LeftHandAnimator);
            UpdateHandAnimation(InputDevices.GetDeviceAtXRNode(XRNode.RightHand), RightHandAnimator);
        }
        
    }
    void UpdateHandAnimation(InputDevice targetDevice, Animator handAnimator)
    {
        if (targetDevice.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue))
        {
            handAnimator.SetFloat("Trigger", triggerValue);
        }
        else
        {
            handAnimator.SetFloat("Trigger", 0);
        }

        if (targetDevice.TryGetFeatureValue(CommonUsages.grip, out float gripValue))
        {
            handAnimator.SetFloat("Grip", gripValue);
        }
        else
        {
            handAnimator.SetFloat("Grip", 0);
        }
    }
    void MapPosition(Transform target, Transform rigTransfrom)
    {
        target.position = rigTransfrom.position;
        target.rotation = rigTransfrom.rotation;
    }
}
