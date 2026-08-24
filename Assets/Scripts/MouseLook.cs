using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLook:MonoBehaviour
{
    [SerializeField]private float mouseSensitivity=100f;
    [SerializeField]private Transform playerBody;
    [SerializeField]private Transform orientation;
    [SerializeField]private PlayerMovement playerMovement;

    private float xRotation;
    private float currentTilt;

    private void Start()
    {
        Cursor.lockState=CursorLockMode.Locked;
        Cursor.visible=false;
    }

    private void Update()
    {
        if(Mouse.current==null)return;

        float mouseX=Mouse.current.delta.x.ReadValue()*mouseSensitivity*Time.deltaTime;
        float mouseY=Mouse.current.delta.y.ReadValue()*mouseSensitivity*Time.deltaTime;

        xRotation-=mouseY;
        xRotation=Mathf.Clamp(xRotation,-90f,90f);

        playerBody.Rotate(Vector3.up*mouseX);

        orientation.localRotation=Quaternion.identity;

        HandleCameraTilt();

        transform.localRotation=Quaternion.Euler(
            xRotation,
            0f,
            currentTilt
        );
    }

    private void HandleCameraTilt()
    {
        float targetTilt=playerMovement.IsWallRunning
            ?playerMovement.WallRunCameraTilt
            :0f;

        currentTilt=Mathf.Lerp(
            currentTilt,
            targetTilt,
            10f*Time.deltaTime
        );
    }
}
