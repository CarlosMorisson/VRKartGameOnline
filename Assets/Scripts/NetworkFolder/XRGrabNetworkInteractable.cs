using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Photon.Pun;
public class XRGrabNetworkInteractable : XRGrabInteractable
{
    private PhotonView _photon;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        _photon = GetComponent<PhotonView>();
    }
    protected override void OnSelectEntered(XRBaseInteractor interactor)
    {
        _photon.RequestOwnership();
        base.OnSelectEntered(interactor);

    }
}
