using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerMovement:MonoBehaviour
{
    [Header("References")]
    [SerializeField]private CharacterController controller;
    [SerializeField]private Transform orientation;

    [Header("Movement")]
    [SerializeField]private float walkSpeed=8f;
    [SerializeField]private float sprintSpeed=12f;
    [SerializeField]private float crouchSpeed=6f;
    [SerializeField]private float airControl=10f;

    [Header("Jump & Gravity")]
    [SerializeField]private float gravity=-14f;
    [SerializeField]private float jumpHeight=5f;
    [SerializeField]private float wallJumpHeight=6f;
    [SerializeField]private float wallJumpForce=16f;
    [SerializeField]private float wallJumpForwardForce=7f;
    [SerializeField]private float maxFallSpeed=-30f;

    [Header("Crouch")]
    [SerializeField]private float standingHeight=3.4f;
    [SerializeField]private float crouchingHeight=1.6f;

    [Header("Wall Run")]
    [SerializeField]private float wallCheckDistance=1.5f;
    [SerializeField]private float wallCheckRadius=.25f;
    [SerializeField]private float wallRunSpeed=12f;
    [SerializeField]private float maxWallRunTime=1.5f;
    [SerializeField]private float wallRunGravity=2f;
    [SerializeField]private float wallRunCooldown=.15f;
    [SerializeField]private float wallJumpDetachTime=.5f;
    [SerializeField]private float minimumWallAngle=60f;
    [SerializeField]private float wallRunStickForce=1.5f;

    [Header("Wall Jump")]
    [SerializeField]private float minimumWallJumpSpeed=1f;
    [SerializeField]private float wallJumpForwardControl=.35f;

    [Header("Camera")]
    [SerializeField]private float maxWallRunCameraTilt=10f;
    [SerializeField]private float cameraTiltSpeed=120f;

    private Vector3 velocity;
    private Vector3 wallNormal;
    private float wallRunTimer;
    private float wallRunCooldownTimer;
    private float wallJumpDetachTimer;
    private bool isGrounded;
    private bool isCrouching;
    private bool isSprinting;

    public bool IsSprinting=>isSprinting;
    public CharacterController CharacterController=>controller;
    public bool IsWallRunning{get;private set;}
    public bool IsWallRight{get;private set;}
    public bool IsWallLeft{get;private set;}
    public float WallRunCameraTilt{get;private set;}

    private void Start()
    {
        if(controller==null)controller=GetComponent<CharacterController>();
    }

    private void Update()
    {
        UpdateTimers();
        CheckGround();
        CheckForWall();
        HandleCrouch();
        HandleJump();
        HandleWallRun();
        HandleMovement();
        HandleGravity();
        HandleCameraTilt();
    }

    private void UpdateTimers()
    {
        if(wallRunCooldownTimer>0f)wallRunCooldownTimer-=Time.deltaTime;
        if(wallJumpDetachTimer>0f)wallJumpDetachTimer-=Time.deltaTime;
    }

    private void CheckGround()
    {
        isGrounded=controller.isGrounded;

        if(isGrounded&&velocity.y<0f)velocity.y=-2f;
        if(isGrounded&&IsWallRunning)StopWallRun();
    }

    private void HandleMovement()
    {
        float x=0f,z=0f;

        if(Keyboard.current.aKey.isPressed)x--;
        if(Keyboard.current.dKey.isPressed)x++;
        if(Keyboard.current.wKey.isPressed)z++;
        if(Keyboard.current.sKey.isPressed)z--;

        Vector3 input=orientation.right*x+orientation.forward*z;
        input.y=0f;
        input=Vector3.ClampMagnitude(input,1f);

        isSprinting=Keyboard.current.leftShiftKey.isPressed&&!isCrouching&&input.magnitude>0f;

        float speed=isCrouching?crouchSpeed:isSprinting?sprintSpeed:walkSpeed;

        if(IsWallRunning)
        {
            Vector3 movement=GetWallRunDirection()*wallRunSpeed;
            movement-=wallNormal*wallRunStickForce;
            movement.y=velocity.y;
            controller.Move(movement*Time.deltaTime);
            return;
        }

        Vector3 targetMovement=input*speed;

        if(isGrounded)
        {
            velocity.x=targetMovement.x;
            velocity.z=targetMovement.z;
        }
        else
        {
            velocity.x=Mathf.MoveTowards(velocity.x,targetMovement.x,airControl*Time.deltaTime);
            velocity.z=Mathf.MoveTowards(velocity.z,targetMovement.z,airControl*Time.deltaTime);
        }

        controller.Move(velocity*Time.deltaTime);
    }

    private void HandleJump()
    {
        if(!Keyboard.current.spaceKey.wasPressedThisFrame)return;

        if(isGrounded)
        {
            velocity.y=Mathf.Sqrt(jumpHeight*-2f*gravity);
            return;
        }

        if(wallJumpDetachTimer>0f)return;

        if(IsWallRunning)
        {
            WallJump();
            return;
        }

        if(CanWallJump())WallJump();
    }

    private bool CanWallJump()
    {
        if(wallNormal==Vector3.zero)return false;

        Vector3 horizontalVelocity=new Vector3(velocity.x,0f,velocity.z);

        if(horizontalVelocity.magnitude<minimumWallJumpSpeed)return false;

        Vector3 movementDirection=horizontalVelocity.normalized;
        float movingIntoWall=Vector3.Dot(movementDirection,-wallNormal);

        if(movingIntoWall>.1f)return true;

        Vector3 forward=orientation.forward;
        forward.y=0f;

        if(forward.sqrMagnitude<.001f)return false;

        forward.Normalize();

        return Vector3.Dot(forward,-wallNormal)>.2f;
    }

    private void WallJump()
    {
        if(wallNormal==Vector3.zero)return;

        Vector3 awayFromWall=wallNormal*wallJumpForce;
        Vector3 forward=orientation.forward;
        forward.y=0f;

        if(forward.sqrMagnitude>.001f)forward.Normalize();

        Vector3 jumpVelocity=awayFromWall+forward*wallJumpForwardForce;

        velocity.x=jumpVelocity.x;
        velocity.z=jumpVelocity.z;
        velocity.y=Mathf.Sqrt(wallJumpHeight*-2f*gravity);

        StopWallRun();
        wallRunCooldownTimer=wallRunCooldown;
        wallJumpDetachTimer=wallJumpDetachTime;
    }

    private void HandleGravity()
    {
        if(IsWallRunning)
        {
            velocity.y=-wallRunGravity;
            return;
        }

        velocity.y+=gravity*Time.deltaTime;
        velocity.y=Mathf.Max(velocity.y,maxFallSpeed);
    }

    private void HandleCrouch()
    {
        if(Keyboard.current.leftCtrlKey.isPressed)
        {
            if(!isCrouching)StartCrouch();
        }
        else if(isCrouching)StopCrouch();
    }

    private void StartCrouch()
    {
        isCrouching=true;
        controller.height=crouchingHeight;
    }

    private void StopCrouch()
    {
        isCrouching=false;
        controller.height=standingHeight;
    }

    private void CheckForWall()
    {
        if(isGrounded)
        {
            ClearWall();
            return;
        }

        if(wallJumpDetachTimer>0f)return;

        Vector3 origin=controller.bounds.center;
        Vector3 forward=orientation.forward;
        Vector3 right=orientation.right;

        forward.y=0f;
        right.y=0f;

        if(forward.sqrMagnitude<.001f||right.sqrMagnitude<.001f)
        {
            ClearWall();
            return;
        }

        forward.Normalize();
        right.Normalize();

        Vector3[] directions=
        {
            right,
            -right,
            forward,
            (right+forward).normalized,
            (-right+forward).normalized
        };

        RaycastHit closestHit=default;
        Vector3 closestDirection=Vector3.zero;
        float closestDistance=wallCheckDistance;
        bool foundWall=false;

        foreach(Vector3 direction in directions)
        {
            if(!Physics.SphereCast(origin,wallCheckRadius,direction,out RaycastHit hit,wallCheckDistance,Physics.AllLayers,QueryTriggerInteraction.Ignore))
                continue;

            if(hit.collider==controller)
                continue;

            float angle=Vector3.Angle(hit.normal,Vector3.up);

            if(angle<minimumWallAngle)
                continue;

            if(hit.distance<closestDistance)
            {
                closestDistance=hit.distance;
                closestHit=hit;
                closestDirection=direction;
                foundWall=true;
            }
        }

        if(!foundWall)
        {
            ClearWall();
            return;
        }

        wallNormal=closestHit.normal;

        float wallSide=Vector3.Dot(right,closestDirection);

        if(Mathf.Abs(wallSide)>.1f)
        {
            IsWallRight=wallSide>0f;
            IsWallLeft=wallSide<0f;
        }
        else
        {
            float normalSide=Vector3.Dot(right,-wallNormal);

            IsWallRight=normalSide>0f;
            IsWallLeft=normalSide<0f;
        }
    }

    private void ClearWall()
    {
        if(IsWallRunning)StopWallRun();

        wallNormal=Vector3.zero;
        IsWallRight=false;
        IsWallLeft=false;
    }

    private void HandleWallRun()
    {
        if(isGrounded)
        {
            StopWallRun();
            return;
        }

        if(wallRunCooldownTimer>0f||wallJumpDetachTimer>0f)return;

        if(!Keyboard.current.wKey.isPressed)
        {
            if(IsWallRunning)StopWallRun();
            return;
        }

        if(wallNormal==Vector3.zero)return;

        if(!IsWallRunning)StartWallRun();

        wallRunTimer+=Time.deltaTime;

        if(wallRunTimer>=maxWallRunTime)
            StopWallRun();
    }

    private void StartWallRun()
    {
        if(IsWallRunning||wallNormal==Vector3.zero)return;

        IsWallRunning=true;
        wallRunTimer=0f;
        velocity.y=0f;
    }

    private void StopWallRun()
    {
        IsWallRunning=false;
        wallRunTimer=0f;
    }

    private Vector3 GetWallRunDirection()
    {
        Vector3 direction=Vector3.Cross(Vector3.up,wallNormal);
        direction.y=0f;

        if(direction.sqrMagnitude<.001f)
            return orientation.forward;

        direction.Normalize();

        Vector3 forward=orientation.forward;
        forward.y=0f;

        if(forward.sqrMagnitude<.001f)
            return direction;

        forward.Normalize();

        if(Vector3.Dot(direction,forward)<0f)
            direction=-direction;

        return direction;
    }

    private void HandleCameraTilt()
    {
        float targetTilt=0f;

        if(IsWallRunning)
        {
            if(IsWallRight)
                targetTilt=maxWallRunCameraTilt;
            else if(IsWallLeft)
                targetTilt=-maxWallRunCameraTilt;
        }

        WallRunCameraTilt=Mathf.MoveTowards(
            WallRunCameraTilt,
            targetTilt,
            cameraTiltSpeed*Time.deltaTime
        );
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if(hit.gameObject.CompareTag("DeadZone"))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if(controller==null||orientation==null)return;

        Vector3 origin=controller.bounds.center;
        Vector3 forward=orientation.forward;
        Vector3 right=orientation.right;

        forward.y=0f;
        right.y=0f;

        if(forward.sqrMagnitude>.001f)forward.Normalize();
        if(right.sqrMagnitude>.001f)right.Normalize();

        Gizmos.color=Color.red;
        Gizmos.DrawLine(origin,origin+right*wallCheckDistance);
        Gizmos.DrawLine(origin,origin-right*wallCheckDistance);
        Gizmos.DrawLine(origin,origin+forward*wallCheckDistance);
        Gizmos.DrawLine(origin,origin+(right+forward).normalized*wallCheckDistance);
        Gizmos.DrawLine(origin,origin+(-right+forward).normalized*wallCheckDistance);
    }
}
