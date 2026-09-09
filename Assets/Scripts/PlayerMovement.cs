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
    [SerializeField]private float sprintSpeed=14f;
    [SerializeField]private float crouchSpeed=7f;
    [SerializeField]private float groundAcceleration=45f;
    [SerializeField]private float groundDeceleration=35f;

    [Header("Air Strafing")]
    [SerializeField]private float airAcceleration=18f;
    [SerializeField]private float airStrafeAcceleration=28f;
    [SerializeField]private float maxAirSpeed=22f;

    [Header("Bunny Hop")]
    [SerializeField]private bool allowBunnyHop=true;
    [SerializeField]private bool autoBunnyHop=false;
    [SerializeField]private float bunnyHopAcceleration=10f;
    [SerializeField]private float maxBunnyHopSpeed=22f;

    [Header("Jump & Gravity")]
    [SerializeField]private float gravity=-22f;
    [SerializeField]private float jumpHeight=4.5f;
    [SerializeField]private float wallJumpHeight=4f;
    [SerializeField]private float wallJumpForce=16f;
    [SerializeField]private float wallJumpForwardForce=7f;
    [SerializeField]private float maxFallSpeed=-45f;

    [Header("Crouch")]
    [SerializeField]private float standingHeight=1.5f;
    [SerializeField]private float crouchingHeight=1f;

    [Header("Wall Run")]
    [SerializeField]private float wallCheckDistance=1.5f;
    [SerializeField]private float wallCheckRadius=0.25f;
    [SerializeField]private float wallRunSpeed=12f;
    [SerializeField]private float maxWallRunTime=1.5f;
    [SerializeField]private float wallRunGravity=2f;
    [SerializeField]private float wallRunCooldown=0.15f;
    [SerializeField]private float wallJumpDetachTime=0.5f;
    [SerializeField]private float minimumWallAngle=60f;
    [SerializeField]private float wallRunStickForce=1.5f;
    [SerializeField]private float wallGraceTime=0.15f;

    [Header("Wall Jump")]
    [SerializeField]private float minimumWallJumpSpeed=1f;
    [SerializeField]private float wallJumpSpeedMultiplier=1f;
    [SerializeField]private float wallJumpDirectionBlend=0.7f;
    [SerializeField]private float wallJumpGravityMultiplier=1.8f;
    [SerializeField]private float wallJumpGravityTime=0.45f;

    [Header("Camera")]
    [SerializeField]private Camera playerCamera;
    [SerializeField]private float maxWallRunCameraTilt=10f;
    [SerializeField]private float cameraTiltSpeed=120f;

    [Header("Slide")]
    [SerializeField]private float slideDuration=1.2f;
    [SerializeField]private float slideDeceleration=8f;
    [SerializeField]private float slideMinSpeed=5f;
    [SerializeField]private float slideActivationSpeed=10f;
    [SerializeField]private float slideHeight=0.8f;

    [Header("Slide Visuals")]
    [SerializeField]private Transform playerModel;
    [SerializeField]private float slideModelHeight=0.5f;
    [SerializeField]private float normalModelHeight=1f;

    [Header("Slide Camera")]
    [SerializeField]private float crouchCameraHeight=0.5f;
    [SerializeField]private float slideCameraHeight=0.7f;
    [SerializeField]private float cameraNormalFOV=90f;
    [SerializeField]private float slideCameraFOV=100f;
    [SerializeField]private float cameraSlideSpeed=8f;

    [Header("Dash")]
    [SerializeField]private float dashSpeed=25f;
    [SerializeField]private float dashDuration=0.2f;
    [SerializeField]private float dashCooldown=1.2f;
    [SerializeField]private bool allowAirDash=true;
    [SerializeField]private int maxAirDashes=1;

    [Header("Stuck Recovery")]
    [SerializeField]private float stuckCheckSkin=0.02f;
    [SerializeField]private float maxUnstuckPushPerFrame=5f;

    private CapsuleCollider stuckProbe;

    private Vector3 velocity;
    private Vector3 wallNormal;
    private float wallJumpGravityTimer;
    private float wallRunTimer;
    private float wallRunCooldownTimer;
    private float wallJumpDetachTimer;
    private float wallGraceTimer;
    private float slideTimer;
    private float slideSpeedCurrent;
    private float dashTimer;
    private float dashCooldownTimer;
    private bool isGrounded;
    private bool isCrouching;
    private bool isSprinting;
    private bool isSliding;
    private bool isDashing;
    private int airDashesUsed;

    public bool IsSprinting=>isSprinting;
    public bool IsSliding=>isSliding;
    public CharacterController CharacterController=>controller;
    public bool IsWallRunning{get;private set;}
    public bool IsWallRight{get;private set;}
    public bool IsWallLeft{get;private set;}
    public float WallRunCameraTilt{get;private set;}
    public float MaxWallRunCameraTilt=>maxWallRunCameraTilt;

    private Vector3 cameraNormalLocalPosition;

    private void Start()
    {
        if(controller==null)
            controller=GetComponent<CharacterController>();

        if(orientation==null)
            orientation=transform;

        if(playerCamera!=null)
        {
            cameraNormalLocalPosition=playerCamera.transform.localPosition;
            cameraNormalFOV=playerCamera.fieldOfView;
        }

        SetControllerHeight(standingHeight);
    }

    private void Update()
    {
        if(controller==null||orientation==null)
            return;

        UpdateTimers();
        CheckGround();
        TryUnstuck();
        CheckForWall();
        HandleCrouch();
        HandleDash();
        HandleJump();
        HandleWallRun();
        HandleMovement();
        HandleGravity();
        HandleSlideCamera();
        TryUnstuck();
    }

    private void UpdateTimers()
    {
        if(wallRunCooldownTimer>0f)
            wallRunCooldownTimer-=Time.deltaTime;

        if(wallJumpDetachTimer>0f)
            wallJumpDetachTimer-=Time.deltaTime;

        if(dashCooldownTimer>0f)
            dashCooldownTimer-=Time.deltaTime;

        if(isDashing)
        {
            dashTimer-=Time.deltaTime;

            if(dashTimer<=0f)
                StopDash();
        }
    }

    private void CheckGround()
    {
        isGrounded=controller.isGrounded;

        if(isGrounded)
        {
            airDashesUsed=0;
            wallGraceTimer=0f;

            if(velocity.y<0f)
                velocity.y=-2f;

            if(IsWallRunning)
                StopWallRun();

            wallRunTimer=0f;

            if(wallJumpGravityTimer>0f)
                wallJumpGravityTimer=0f;
        }
    }

    private Vector2 GetMovementInput()
    {
        if(Keyboard.current==null)
            return Vector2.zero;

        float x=0f;
        float z=0f;

        if(Keyboard.current.aKey.isPressed)
            x--;

        if(Keyboard.current.dKey.isPressed)
            x++;

        if(Keyboard.current.wKey.isPressed)
            z++;

        if(Keyboard.current.sKey.isPressed)
            z--;

        Vector2 input=new Vector2(x,z);

        if(input.sqrMagnitude>1f)
            input.Normalize();

        return input;
    }

    private Vector3 GetMovementDirection(Vector2 input)
    {
        Vector3 direction=orientation.right*input.x+orientation.forward*input.y;
        direction.y=0f;

        if(direction.sqrMagnitude>0.001f)
            direction.Normalize();

        return direction;
    }

    private void HandleMovement()
    {
        if(isDashing)
        {
            controller.Move(velocity*Time.deltaTime);
            return;
        }

        Vector2 input=GetMovementInput();
        Vector3 inputDirection=GetMovementDirection(input);

        isSprinting=Keyboard.current!=null&&
                    Keyboard.current.leftShiftKey.isPressed&&
                    !isCrouching&&
                    !isSliding&&
                    input.magnitude>0.01f;

        if(isSliding)
        {
            HandleSlideMovement();
            return;
        }

        if(IsWallRunning)
        {
            Vector3 movement=GetWallRunDirection()*wallRunSpeed;
            movement-=wallNormal*wallRunStickForce;
            movement.y=velocity.y;
            controller.Move(movement*Time.deltaTime);
            return;
        }

        float speed=walkSpeed;

        if(isCrouching)
            speed=crouchSpeed;
        else if(isSprinting)
            speed=sprintSpeed;

        if(isGrounded)
            HandleGroundMovement(inputDirection,input.magnitude,speed);
        else
            HandleAirMovement(inputDirection,input.magnitude);

        controller.Move(velocity*Time.deltaTime);
    }

    private void HandleGroundMovement(Vector3 inputDirection,float inputAmount,float speed)
    {
        Vector3 horizontalVelocity=new Vector3(velocity.x,0f,velocity.z);
        float currentSpeed=horizontalVelocity.magnitude;

        if(inputAmount<=0.01f)
        {
            horizontalVelocity=Vector3.MoveTowards(
                horizontalVelocity,
                Vector3.zero,
                groundDeceleration*Time.deltaTime
            );
        }
        else
        {
            if(currentSpeed>speed)
            {
                Vector3 targetVelocity=inputDirection*speed;

                horizontalVelocity=Vector3.MoveTowards(
                    horizontalVelocity,
                    targetVelocity,
                    groundDeceleration*Time.deltaTime
                );
            }
            else
            {
                float currentDirectionSpeed=Vector3.Dot(
                    horizontalVelocity,
                    inputDirection
                );

                float speedToAdd=speed-currentDirectionSpeed;

                if(speedToAdd>0f)
                {
                    float accelerationAmount=Mathf.Min(
                        groundAcceleration*Time.deltaTime,
                        speedToAdd
                    );

                    horizontalVelocity+=inputDirection*accelerationAmount;
                }

                if(currentDirectionSpeed<0f)
                {
                    horizontalVelocity=Vector3.MoveTowards(
                        horizontalVelocity,
                        inputDirection*speed,
                        groundAcceleration*2f*Time.deltaTime
                    );
                }
            }
        }

        velocity.x=horizontalVelocity.x;
        velocity.z=horizontalVelocity.z;
    }

    private void HandleAirMovement(Vector3 inputDirection,float inputAmount)
    {
        if(inputAmount<=0.01f)
            return;

        Vector3 horizontalVelocity=new Vector3(velocity.x,0f,velocity.z);
        float currentSpeed=Vector3.Dot(horizontalVelocity,inputDirection);
        float acceleration=airAcceleration;

        if(Mathf.Abs(inputDirection.x)>0.1f)
            acceleration=airStrafeAcceleration;

        float speedToAdd=maxAirSpeed-currentSpeed;

        if(speedToAdd<=0f)
            return;

        float accelerationAmount=Mathf.Min(
            acceleration*Time.deltaTime,
            speedToAdd
        );

        horizontalVelocity+=inputDirection*accelerationAmount;

        if(horizontalVelocity.magnitude>maxAirSpeed)
            horizontalVelocity=horizontalVelocity.normalized*maxAirSpeed;

        velocity.x=horizontalVelocity.x;
        velocity.z=horizontalVelocity.z;
    }

    private void HandleSlideMovement()
    {
        slideTimer+=Time.deltaTime;

        Vector3 slideDirection=new Vector3(velocity.x,0f,velocity.z);

        if(slideDirection.sqrMagnitude<0.001f)
        {
            StopSlide();
            return;
        }

        slideDirection.Normalize();

        slideSpeedCurrent=Mathf.MoveTowards(
            slideSpeedCurrent,
            0f,
            slideDeceleration*Time.deltaTime
        );

        velocity.x=slideDirection.x*slideSpeedCurrent;
        velocity.z=slideDirection.z*slideSpeedCurrent;

        controller.Move(velocity*Time.deltaTime);

        if(slideSpeedCurrent<=slideMinSpeed||
           slideTimer>=slideDuration)
        {
            StopSlide();
        }
    }

    private void HandleDash()
    {
        if(Keyboard.current==null||
           !Keyboard.current.qKey.wasPressedThisFrame)
            return;

        if(isDashing||dashCooldownTimer>0f)
            return;

        if(!isGrounded)
        {
            if(!allowAirDash||airDashesUsed>=maxAirDashes)
                return;

            airDashesUsed++;
        }

        StartDash();
    }

    private void StartDash()
    {
        if(isSliding)
            StopSlide();

        Vector2 input=GetMovementInput();
        Vector3 dashDirection=GetMovementDirection(input);

        if(dashDirection.sqrMagnitude<0.001f)
        {
            dashDirection=orientation.forward;
            dashDirection.y=0f;
            dashDirection.Normalize();
        }

        velocity=dashDirection*dashSpeed;
        velocity.y=0f;
        isDashing=true;
        dashTimer=dashDuration;
        dashCooldownTimer=dashCooldown;
    }

    private void StopDash()
    {
        isDashing=false;
    }

    private void HandleJump()
    {
        if(Keyboard.current==null)
            return;

        bool jumpPressed=autoBunnyHop
            ?Keyboard.current.spaceKey.isPressed
            :Keyboard.current.spaceKey.wasPressedThisFrame;

        if(!jumpPressed)
            return;

        if(isGrounded)
        {
            if(isSliding)
                StopSlide();

            if(allowBunnyHop)
                ApplyBunnyHop();

            velocity.y=Mathf.Sqrt(jumpHeight*-2f*gravity);
            return;
        }

        if(wallJumpDetachTimer>0f)
            return;

        if(IsWallRunning)
        {
            WallJump();
            return;
        }

        if(CanWallJump())
            WallJump();
    }

    private void ApplyBunnyHop()
    {
        Vector3 horizontalVelocity=new Vector3(velocity.x,0f,velocity.z);
        Vector2 input=GetMovementInput();

        if(input.sqrMagnitude>0.01f)
        {
            Vector3 inputDirection=GetMovementDirection(input);
            float currentSpeed=Vector3.Dot(horizontalVelocity,inputDirection);
            float speedToAdd=maxBunnyHopSpeed-currentSpeed;

            if(speedToAdd>0f)
            {
                float accelerationAmount=Mathf.Min(
                    bunnyHopAcceleration*Time.deltaTime,
                    speedToAdd
                );

                horizontalVelocity+=inputDirection*accelerationAmount;
            }
        }

        if(horizontalVelocity.magnitude>maxBunnyHopSpeed)
            horizontalVelocity=horizontalVelocity.normalized*maxBunnyHopSpeed;

        velocity.x=horizontalVelocity.x;
        velocity.z=horizontalVelocity.z;
    }

    private bool CanWallJump()
    {
        if(wallNormal==Vector3.zero)
            return false;

        Vector3 horizontalVelocity=new Vector3(velocity.x,0f,velocity.z);

        if(horizontalVelocity.magnitude<minimumWallJumpSpeed)
            return false;

        Vector3 movementDirection=horizontalVelocity.normalized;

        if(Vector3.Dot(movementDirection,-wallNormal)>0.1f)
            return true;

        Vector3 forward=orientation.forward;
        forward.y=0f;

        if(forward.sqrMagnitude<0.001f)
            return false;

        forward.Normalize();

        return Vector3.Dot(forward,-wallNormal)>0.2f;
    }

    private void WallJump()
    {
        if(wallNormal==Vector3.zero)
            return;

        Vector3 currentMomentum=new Vector3(velocity.x,0f,velocity.z);

        float currentSpeed=Mathf.Max(
            currentMomentum.magnitude,
            minimumWallJumpSpeed
        );

        Vector3 forward=orientation.forward;
        forward.y=0f;

        if(forward.sqrMagnitude<0.001f)
            forward=transform.forward;
        else
            forward.Normalize();

        Vector3 awayFromWall=wallNormal;
        awayFromWall.y=0f;

        if(awayFromWall.sqrMagnitude<0.001f)
            return;

        awayFromWall.Normalize();

        Vector3 jumpDirection=Vector3.Lerp(
            awayFromWall,
            (
                awayFromWall*wallJumpForce+
                forward*wallJumpForwardForce
            ).normalized,
            wallJumpDirectionBlend
        ).normalized;

        float wallJumpSpeed=currentSpeed*wallJumpSpeedMultiplier;

        velocity.x=jumpDirection.x*wallJumpSpeed;
        velocity.z=jumpDirection.z*wallJumpSpeed;
        velocity.y=Mathf.Sqrt(wallJumpHeight*-2f*gravity);

        wallJumpGravityTimer=wallJumpGravityTime;
        StopWallRun();
        wallRunCooldownTimer=wallRunCooldown;
        wallJumpDetachTimer=wallJumpDetachTime;
    }

    private void HandleGravity()
    {
        if(isDashing)
        {
            velocity.y=0f;
            return;
        }

        if(IsWallRunning)
        {
            velocity.y=-wallRunGravity;
            return;
        }

        float currentGravity=gravity;

        if(wallJumpGravityTimer>0f)
        {
            currentGravity*=wallJumpGravityMultiplier;
            wallJumpGravityTimer-=Time.deltaTime;
        }

        velocity.y+=currentGravity*Time.deltaTime;
        velocity.y=Mathf.Max(velocity.y,maxFallSpeed);
    }

    private void HandleCrouch()
    {
        if (Keyboard.current == null)
            return;

        bool crouchHeld = Keyboard.current.leftCtrlKey.isPressed;
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        float currentSpeed = horizontalVelocity.magnitude;

        if (isSliding)
        {
            if (!crouchHeld || slideTimer >= slideDuration)
                StopSlide();

            return;
        }

        if (isGrounded && crouchHeld && currentSpeed >= slideActivationSpeed)
        {
            StartSlide();
            return;
        }

        if (crouchHeld)
        {
            if (!isCrouching)
                StartCrouch();
        }
        else if (isCrouching)
        {
            StopCrouch();
        }
    }

    private void StartCrouch()
    {
        if (isSliding)
            return;

        isCrouching = true;
        SetControllerHeight(crouchingHeight);
    }

    private void StopCrouch()
    {
        if(isSliding)
            return;

        if(!CanStandAt(standingHeight))
            return;

        isCrouching=false;
        SetControllerHeight(standingHeight);
    }


    private bool CanStandAt(float height)
    {
        float radius=GetSafeRadius(height);
        float skin=controller.skinWidth+0.02f;
        float checkRadius=Mathf.Max(0.01f,radius-skin);

        float bottom=checkRadius+skin;
        float top=height-checkRadius-skin;

        if(top<bottom)
            top=bottom;

        Vector3 capsuleBottom=transform.position+Vector3.up*bottom;
        Vector3 capsuleTop=transform.position+Vector3.up*top;

        int mask=Physics.AllLayers&~(1<<gameObject.layer);

        return !Physics.CheckCapsule(
            capsuleBottom,
            capsuleTop,
            checkRadius,
            mask,
            QueryTriggerInteraction.Ignore
        );
    }
    private bool IsStuck(out Collider[] overlaps)
    {
        Vector3 worldCenter=transform.position+controller.center;
        float skin=Mathf.Max(stuckCheckSkin,controller.skinWidth*1.5f);
        float radius=Mathf.Max(controller.radius-skin,0.01f);
        float outerHalfExtent=Mathf.Max(controller.height*0.5f-skin,radius);
        float halfHeight=Mathf.Max(outerHalfExtent-radius,0f);

        Vector3 bottom=worldCenter+Vector3.down*halfHeight;
        Vector3 top=worldCenter+Vector3.up*halfHeight;

        Collider[] raw=Physics.OverlapCapsule(bottom,top,radius,Physics.AllLayers,QueryTriggerInteraction.Ignore);

        System.Collections.Generic.List<Collider> filtered=new System.Collections.Generic.List<Collider>(raw.Length);

        foreach(Collider col in raw)
        {
            if(col==null)
                continue;

            if(col.transform==transform||col.transform.IsChildOf(transform))
                continue;

            filtered.Add(col);
        }

        overlaps=filtered.ToArray();
        return overlaps.Length>0;
    }

    private void EnsureStuckProbe()
    {
        if(stuckProbe!=null)
            return;

        GameObject probeObject=new GameObject("StuckProbe");
        probeObject.transform.SetParent(transform,false);
        probeObject.layer=gameObject.layer;

        stuckProbe=probeObject.AddComponent<CapsuleCollider>();
        stuckProbe.isTrigger=true;
    }

    private void SyncStuckProbe()
    {
        stuckProbe.radius=controller.radius;
        stuckProbe.height=controller.height;
        stuckProbe.center=controller.center;
    }

    private void TryUnstuck()
    {
        const int maxIterations=4;

        for(int i=0;i<maxIterations;i++)
        {
            if(!IsStuck(out Collider[] overlaps))
                return;

            EnsureStuckProbe();
            SyncStuckProbe();

            Vector3 totalPush=Vector3.zero;
            bool anyOverlap=false;

            foreach(Collider col in overlaps)
            {
                if(col==null||col==stuckProbe)
                    continue;

                bool overlapped=Physics.ComputePenetration(
                    stuckProbe,transform.position,transform.rotation,
                    col,col.transform.position,col.transform.rotation,
                    out Vector3 direction,out float distance
                );

                if(overlapped)
                {
                    totalPush+=direction*distance;
                    anyOverlap=true;
                }
            }

            if(!anyOverlap||totalPush.sqrMagnitude<0.0000001f)
                return;

            if(totalPush.magnitude>maxUnstuckPushPerFrame)
                totalPush=totalPush.normalized*maxUnstuckPushPerFrame;

            // Direct depenetration correction.
            transform.position+=totalPush;
        }
    }

    private void StartSlide()
    {
        if(!isGrounded||isSliding)
            return;

        Vector3 horizontalVelocity=new Vector3(velocity.x,0f,velocity.z);
        float currentSpeed=horizontalVelocity.magnitude;

        if(currentSpeed<slideActivationSpeed)
            return;

        isSliding=true;
        isCrouching=true;
        slideTimer=0f;
        slideSpeedCurrent=currentSpeed;

        SetControllerHeight(slideHeight);

        if(playerModel!=null)
        {
            playerModel.localScale=new Vector3(
                playerModel.localScale.x,
                slideModelHeight,
                playerModel.localScale.z
            );
        }
    }

    private void StopSlide()
    {
        isSliding=false;
        slideTimer=0f;

        if(playerModel!=null)
        {
            playerModel.localScale=new Vector3(
                playerModel.localScale.x,
                normalModelHeight,
                playerModel.localScale.z
            );
        }

        if(CanStandAt(standingHeight))
        {
            isCrouching=false;
            SetControllerHeight(standingHeight);
        }
        else if(CanStandAt(crouchingHeight))
        {
            isCrouching=true;
            SetControllerHeight(crouchingHeight);
        }
        else
        {
            isCrouching=true;
        }
    }

    private float GetSafeRadius(float height)
    {
        float maxRadius=(height*0.5f)-0.01f;

        if(maxRadius<=0.01f)
            return 0.01f;

        return Mathf.Min(controller.radius,maxRadius);
    }

    private void SetControllerHeight(float height)
    {
        height=Mathf.Max(height,0.05f);

        float safeRadius=GetSafeRadius(height);

        Vector3 oldCenter=controller.center;
        float oldHeight=controller.height;

        float oldBottom=oldCenter.y-(oldHeight*0.5f);

        controller.height=height;

        Vector3 newCenter=controller.center;
        newCenter.y=oldBottom+(height*0.5f);

        controller.center=newCenter;

        if(Mathf.Abs(controller.radius-safeRadius)>0.0001f)
            controller.radius=safeRadius;
    }

    private void HandleSlideCamera()
    {
        if(playerCamera==null)
            return;

        Vector3 targetPosition=cameraNormalLocalPosition;
        float targetFOV=cameraNormalFOV;

        if(isSliding)
        {
            targetPosition.y=cameraNormalLocalPosition.y-slideCameraHeight;
            targetFOV=slideCameraFOV;
        }
        else if(isCrouching)
        {
            targetPosition.y=cameraNormalLocalPosition.y-crouchCameraHeight;
        }

        playerCamera.transform.localPosition=Vector3.Lerp(
            playerCamera.transform.localPosition,
            targetPosition,
            cameraSlideSpeed*Time.deltaTime
        );

        playerCamera.fieldOfView=Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFOV,
            cameraSlideSpeed*Time.deltaTime
        );
    }

    private void CheckForWall()
    {
        if(isGrounded||wallJumpDetachTimer>0f)
        {
            ClearWall();
            return;
        }

        Vector3 origin=controller.bounds.center;
        Vector3 forward=orientation.forward;
        Vector3 right=orientation.right;

        forward.y=0f;
        right.y=0f;

        if(forward.sqrMagnitude<0.001f||
           right.sqrMagnitude<0.001f)
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
            -forward,
            (right+forward).normalized,
            (-right+forward).normalized,
            (right-forward).normalized,
            (-right-forward).normalized
        };

        RaycastHit closestHit=default;
        float closestDistance=wallCheckDistance;
        bool foundWall=false;

        foreach(Vector3 direction in directions)
        {
            if(!Physics.SphereCast(
                origin,
                wallCheckRadius,
                direction,
                out RaycastHit hit,
                wallCheckDistance,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore))
                continue;

            if(hit.collider==controller||
               hit.transform==transform)
                continue;

            float angle=Vector3.Angle(hit.normal,Vector3.up);

            if(angle<minimumWallAngle)
                continue;

            if(hit.distance<closestDistance)
            {
                closestDistance=hit.distance;
                closestHit=hit;
                foundWall=true;
            }
        }

        if(!foundWall)
        {
            if(IsWallRunning)
            {
                wallGraceTimer+=Time.deltaTime;

                if(wallGraceTimer<wallGraceTime)
                    return;
            }

            ClearWall();
            return;
        }

        wallGraceTimer=0f;
        wallNormal=closestHit.normal;

        float normalSide=Vector3.Dot(right,-wallNormal);

        IsWallRight=normalSide>0.1f;
        IsWallLeft=normalSide<-0.1f;
    }

    private void ClearWall()
    {
        if(IsWallRunning)
            StopWallRun();

        wallNormal=Vector3.zero;
        IsWallRight=false;
        IsWallLeft=false;
        wallGraceTimer=0f;
    }

    private void HandleWallRun()
    {
        if(isGrounded)
        {
            StopWallRun();
            return;
        }

        if(wallRunCooldownTimer>0f||
           wallJumpDetachTimer>0f)
            return;

        if(Keyboard.current==null)
            return;

        if(!Keyboard.current.wKey.isPressed)
        {
            if(IsWallRunning)
                StopWallRun();

            return;
        }

        if(wallNormal==Vector3.zero)
        {
            if(IsWallRunning)
                StopWallRun();

            return;
        }

        if(!IsWallRunning)
            StartWallRun();

        if(!IsWallRunning)
            return;

        wallRunTimer+=Time.deltaTime;

        if(wallRunTimer>=maxWallRunTime)
            StopWallRun();
    }

    private void StartWallRun()
    {
        if(IsWallRunning||wallNormal==Vector3.zero)
            return;

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
        Vector3 direction=Vector3.Cross(
            Vector3.up,
            wallNormal
        );

        direction.y=0f;

        if(direction.sqrMagnitude<0.001f)
            return orientation.forward;

        direction.Normalize();

        Vector3 forward=orientation.forward;
        forward.y=0f;

        if(forward.sqrMagnitude<0.001f)
            return direction;

        forward.Normalize();

        if(Vector3.Dot(direction,forward)<0f)
            direction=-direction;

        return direction;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if(hit.gameObject.CompareTag("DeadZone"))
        {
            SceneManager.LoadScene(
                SceneManager.GetActiveScene().buildIndex
            );
            return;
        }

        airDashesUsed=0;
    }

    private void OnDrawGizmosSelected()
    {
        if(controller==null||orientation==null)
            return;

        Vector3 origin=controller.bounds.center;
        Vector3 forward=orientation.forward;
        Vector3 right=orientation.right;

        forward.y=0f;
        right.y=0f;

        if(forward.sqrMagnitude>0.001f)
            forward.Normalize();

        if(right.sqrMagnitude>0.001f)
            right.Normalize();

        Gizmos.color=Color.red;

        Gizmos.DrawLine(origin,origin+right*wallCheckDistance);
        Gizmos.DrawLine(origin,origin-right*wallCheckDistance);
        Gizmos.DrawLine(origin,origin+forward*wallCheckDistance);
        Gizmos.DrawLine(origin,origin-forward*wallCheckDistance);
        Gizmos.DrawLine(origin,origin+(right+forward).normalized*wallCheckDistance);
        Gizmos.DrawLine(origin,origin+(-right+forward).normalized*wallCheckDistance);
        Gizmos.DrawLine(origin,origin+(right-forward).normalized*wallCheckDistance);
        Gizmos.DrawLine(origin,origin+(-right-forward).normalized*wallCheckDistance);

        // Debug wall and capsule checks.
        if(Application.isPlaying)
        {
            DrawStandCheckGizmo(slideHeight,new Color(1f,0.5f,0f));
            DrawStandCheckGizmo(crouchingHeight,Color.yellow);
            DrawStandCheckGizmo(standingHeight,Color.magenta);
            DrawCurrentCapsuleGizmo();
        }
    }

    private void DrawStandCheckGizmo(float targetHeight,Color baseColor)
    {
        bool clear=CanStandAt(targetHeight);
        Gizmos.color=clear?Color.green:Color.red;

        float radius=Mathf.Min(GetSafeRadius(targetHeight)+controller.skinWidth,targetHeight*0.5f);
        Vector3 center=transform.position+new Vector3(0f,targetHeight*0.5f,0f);
        Vector3 bottom=center+Vector3.down*(targetHeight*0.5f-radius);
        Vector3 top=center+Vector3.up*(targetHeight*0.5f-radius);

        Gizmos.DrawWireSphere(bottom,radius);
        Gizmos.DrawWireSphere(top,radius);
        Gizmos.DrawLine(bottom+Vector3.forward*radius,top+Vector3.forward*radius);
        Gizmos.DrawLine(bottom-Vector3.forward*radius,top-Vector3.forward*radius);
        Gizmos.DrawLine(bottom+Vector3.right*radius,top+Vector3.right*radius);
        Gizmos.DrawLine(bottom-Vector3.right*radius,top-Vector3.right*radius);
    }

    private void DrawCurrentCapsuleGizmo()
    {
        Gizmos.color=Color.cyan;

        float radius=controller.radius;
        float height=controller.height;
        Vector3 center=transform.position+controller.center;
        Vector3 bottom=center+Vector3.down*(height*0.5f-radius);
        Vector3 top=center+Vector3.up*(height*0.5f-radius);

        Gizmos.DrawWireSphere(bottom,radius);
        Gizmos.DrawWireSphere(top,radius);
    }
}