#define USE_RIGIDBODY_VELOCITY
//If you want the mode of operation via rigidbody.velocity, make sure that the line below is commented out,
//and if you want the mode of operation via transform.position, make sure the opposite.
#undef USE_RIGIDBODY_VELOCITY

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class FPSPlayerController : MonoBehaviour
{
    public bool OnGround => groundPoints.Count > 0;

    [Header("If you want smoothness, \nswitch Physics Simulation Mode to Update!")]
#if !USE_RIGIDBODY_VELOCITY
    [Header("For best performance, \nswitch Collision Detection of the Rigidbody to Discrete!")]
#endif

    [Space]
    public bool InputEnable = true;
    public bool NormalizeInput = true;

    [Space]
    [Min(0)]
    public float speed = 10;

    [Space]
    [SerializeField, Range(0, 90)]
    private float slopeLimit = 45;

    [Space]
    [SerializeField, Min(0), Tooltip("Air Resistance (Only works when the character is in the air)")]
    private float airDrag = 0;

    [Space]
    public bool CanJump = true;
    [Min(0)]
    public float jumpHeight = 2;

    [Space]
    public bool CanRun = true;
    [Min(1)]
    public float runSpeedMultiplier = 1.5f;

    [Space]
    public bool canMoveInAir = true;
    [Min(0)]
    public float airborneMotionSensitivity = 1;

    [Header("Camera")]
    public float Sensitivity = 1.0f;

    [Header("References")]
    [SerializeField]
    private Transform cameraTransform;

    new private Rigidbody rigidbody;
    private CapsuleCollider capsuleCollider;
    private Dictionary<Collider, Vector3[]> groundPoints = new();
    private float rotationY = 0.0f;
    private bool jumping = false;
    private bool running = false;
#if !USE_RIGIDBODY_VELOCITY
    private Vector3 velocity = Vector3.zero;
#endif

    private void Update()
    {
        #region Camera
        Vector2 mouseInput = Vector2.zero;
        mouseInput += Vector2.down * Input.GetAxisRaw("Mouse Y");
        mouseInput += Vector2.right * Input.GetAxisRaw("Mouse X");
        mouseInput *= Sensitivity;

        rotationY += mouseInput.y;
        rotationY = Mathf.Clamp(rotationY, -90, 90);
        cameraTransform.localEulerAngles = Vector3.right * rotationY;

        transform.localEulerAngles += Vector3.up * mouseInput.x;
        #endregion

        #region Input
        Vector3 input = Vector3.zero;
        if (InputEnable)
        {
            input += transform.forward * Input.GetAxisRaw("Vertical");
            input += transform.right * Input.GetAxisRaw("Horizontal");
            if (NormalizeInput)
                input = input.normalized;

            running = Input.GetKey(KeyCode.LeftShift) && Vector3.Dot(input.normalized, transform.forward) >= 0.5 && CanRun;

            jumping = Input.GetKeyDown(KeyCode.Space) && CanJump;
        }
        #endregion

        //Velocity recalculation
        if (OnGround)
        {
            input *= speed;

            Vector3 currentGP = groundPoints.Values.First().First();
            //Find the closest point to the input vector
            foreach (var colliderGPs in groundPoints.Values)
            {
                foreach (var newGP in colliderGPs)
                {
                    if (Vector3.Angle(newGP, input) < Vector3.Angle(currentGP, input))
                        currentGP = newGP;
                }
            }

            //Rotate the input vector parallel to the ground
            input = Quaternion.FromToRotation(Vector3.down, currentGP) * input;

            if (running)
                input *= runSpeedMultiplier;

            //Jump
            if (jumping)
            {
                input.y = Mathf.Sqrt(Mathf.Abs(Physics.gravity.y) * jumpHeight * 2);
                jumping = false;
            }

#if USE_RIGIDBODY_VELOCITY
            rigidbody.velocity = input;
        }
        else
        {
            if (canMoveInAir)
                rigidbody.velocity += airborneMotionSensitivity * Time.deltaTime * input;

            //Air drag
            rigidbody.velocity -= airDrag * Time.deltaTime * (rigidbody.velocity - rigidbody.velocity.y * transform.up);
            //Gravity
            rigidbody.velocity += Physics.gravity * Time.deltaTime;
        }
#else
            velocity = input;
        }
        else
        {
            if (canMoveInAir)
                velocity += airborneMotionSensitivity * Time.deltaTime * input;

            //Air drag
            velocity -= airDrag * Time.deltaTime * (velocity - velocity.y * transform.up);
            //Gravity
            velocity += Physics.gravity * Time.deltaTime;
        }

        transform.position += velocity * Time.deltaTime;
#endif
    }

    private void OnCollisionStay(Collision collision)
    {
#if USE_RIGIDBODY_VELOCITY
        List<Vector3> newGroundPoints = new List<Vector3>();

        foreach (var contact in collision.contacts)
        {
            //Predict position
            Vector3 point = contact.point + rigidbody.velocity * Time.deltaTime;
            //Coordinates of the predicted point relative to the center of the lower hemisphere of the collider capsule
            Vector3 localPoint = point - transform.position - (((capsuleCollider.height / 2) - capsuleCollider.radius) * -transform.up);

            //Find the point of contact on the second collision
            Vector3 point2;
            if (collision.collider is BoxCollider || collision.collider is MeshCollider || collision.collider is SphereCollider || collision.collider is CapsuleCollider)
                point2 = collision.collider.ClosestPoint(point);
            else if (Physics.Raycast(point - localPoint, localPoint, out RaycastHit hit, LayerMask.GetMask("Default")))
                point2 = hit.point;
            else
            {
                Debug.DrawLine(point, localPoint + point);
                Debug.LogWarning("Couldn't find a second point of contact");
                continue;
            }

            //If the difference between the points is greater than the limit
            if ((point2 - point).magnitude > Physics.defaultContactOffset)
                continue;

            if (Vector3.Angle(-transform.up, localPoint) > slopeLimit)
                continue;

            newGroundPoints.Add(localPoint);
        }

        if (newGroundPoints.Count() == 0)
        {
            if (groundPoints.Keys.Contains(collision.collider))
                groundPoints.Remove(collision.collider);
            return;
        }

        if (!groundPoints.Keys.Contains(collision.collider))
            groundPoints.Add(collision.collider, newGroundPoints.ToArray());
        else
            groundPoints[collision.collider] = newGroundPoints.ToArray();
#else
        //All collision points are within the constraint
        Vector3[] newGroundPoints = collision.contacts.Select(c => c.point - transform.position - (((capsuleCollider.height / 2) - capsuleCollider.radius) * -transform.up)).Where(p => Vector3.Angle(-transform.up, p) <= slopeLimit).ToArray();

        if (newGroundPoints.Count() == 0)
        {
            if (groundPoints.Keys.Contains(collision.collider))
                groundPoints.Remove(collision.collider);
            return;
        }

        if (!groundPoints.Keys.Contains(collision.collider))
            groundPoints.Add(collision.collider, newGroundPoints);
        else
            groundPoints[collision.collider] = newGroundPoints;
#endif
    }

    private void OnCollisionExit(Collision collision)
    {
        if (groundPoints.Keys.Contains(collision.collider))
            groundPoints.Remove(collision.collider);
    }

    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();
    }
}
