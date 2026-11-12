using System.Collections.Generic;
using UnityEngine;

public class FlockUnit : MonoBehaviour
{
    [SerializeField] private float FOVAngle;
    [SerializeField] private float smoothDamp;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private Vector3[] directionsToCheckWhenAvoidingObstacles;

    private List<FlockUnit> cohesionNeighbours = new List<FlockUnit>();
    private List<FlockUnit> avoidanceNeighbours = new List<FlockUnit>();
    private List<FlockUnit> aligementNeighbours = new List<FlockUnit>();
    private Flock assignedFlock;
    private Vector3 currentVelocity;
    private Vector3 currentObstacleAvoidanceVector;
    private float speed;

    public Transform myTransform { get; set; }

    private void Awake()
    {
        myTransform = transform;
    }

    public void AssignFlock(Flock flock)
    {
        assignedFlock = flock;
    }

    public void InitializeSpeed(float speed)
    {
        this.speed = speed;
    }

    public void MoveUnit()
    {
        FindNeighbours();
        CalculateSpeed();

        var cohesionVector = CalculateCohesionVector() * assignedFlock.cohesionWeight;
        var avoidanceVector = CalculateAvoidanceVector() * assignedFlock.avoidanceWeight;
        var aligementVector = CalculateAligementVector() * assignedFlock.aligementWeight;
        var boundsVector = CalculateBoundsVector() * assignedFlock.boundsWeight;
        var obstacleVector = CalculateObstacleVector() * assignedFlock.obstacleWeight;

        var moveVector = cohesionVector + avoidanceVector + aligementVector
            + boundsVector + obstacleVector;

        moveVector = Vector3.SmoothDamp(
            myTransform.forward, moveVector, ref currentVelocity, smoothDamp
        );
        moveVector = moveVector.normalized * speed;

        if (moveVector == Vector3.zero)
            moveVector = transform.forward;

        myTransform.forward = moveVector;
        myTransform.position += moveVector * Time.deltaTime;
    }

    private void FindNeighbours()
    {
        cohesionNeighbours.Clear();
        avoidanceNeighbours.Clear();
        aligementNeighbours.Clear();

        var allUnits = assignedFlock.allUnits;

        for (int i = 0; i < allUnits.Length; i++)
        {
            var currentUnit = allUnits[i];

            if (currentUnit == this)
                continue;

            float distanceSqr = Vector3.SqrMagnitude(
                currentUnit.myTransform.position - myTransform.position
            );

            if (distanceSqr <= assignedFlock.cohesionDistance * assignedFlock.cohesionDistance)
                cohesionNeighbours.Add(currentUnit);

            if (distanceSqr <= assignedFlock.avoidanceDistance * assignedFlock.avoidanceDistance)
                avoidanceNeighbours.Add(currentUnit);

            if (distanceSqr <= assignedFlock.aligementDistance * assignedFlock.aligementDistance)
                aligementNeighbours.Add(currentUnit);
        }
    }

    private void CalculateSpeed()
    {
        if (cohesionNeighbours.Count == 0)
            return;

        speed = 0f;

        for (int i = 0; i < cohesionNeighbours.Count; i++)
            speed += cohesionNeighbours[i].speed;

        speed /= cohesionNeighbours.Count;
        speed = Mathf.Clamp(speed, assignedFlock.minSpeed, assignedFlock.maxSpeed);
    }

    private Vector3 CalculateCohesionVector()
    {
        var cohesionVector = Vector3.zero;

        if (cohesionNeighbours.Count == 0)
            return Vector3.zero;

        int neighboursInFOV = 0;

        for (int i = 0; i < cohesionNeighbours.Count; i++)
        {
            Vector3 neighbourPosition = cohesionNeighbours[i].myTransform.position;

            if (IsInFOV(neighbourPosition))
            {
                neighboursInFOV++;
                cohesionVector += neighbourPosition;
            }
        }

        // Sin vecinos visibles, no hay un centro hacia el cual nadar.
        if (neighboursInFOV == 0)
            return Vector3.zero;

        cohesionVector /= neighboursInFOV;
        cohesionVector -= myTransform.position;
        return cohesionVector.normalized;
    }

    private Vector3 CalculateAligementVector()
    {
        var aligementVector = myTransform.forward;

        if (aligementNeighbours.Count == 0)
            return myTransform.forward;

        int neighboursInFOV = 0;

        for (int i = 0; i < aligementNeighbours.Count; i++)
        {
            Transform neighbour = aligementNeighbours[i].myTransform;

            if (IsInFOV(neighbour.position))
            {
                neighboursInFOV++;
                aligementVector += neighbour.forward;
            }
        }

        // Sin vecinos visibles, el pez conserva su dirección.
        if (neighboursInFOV == 0)
            return myTransform.forward;

        aligementVector /= neighboursInFOV;
        return aligementVector.normalized;
    }

    private Vector3 CalculateAvoidanceVector()
    {
        var avoidanceVector = Vector3.zero;

        // La separación consulta su propia lista de vecinos.
        if (avoidanceNeighbours.Count == 0)
            return Vector3.zero;

        int neighboursInFOV = 0;

        for (int i = 0; i < avoidanceNeighbours.Count; i++)
        {
            Vector3 neighbourPosition = avoidanceNeighbours[i].myTransform.position;

            if (IsInFOV(neighbourPosition))
            {
                neighboursInFOV++;
                avoidanceVector += myTransform.position - neighbourPosition;
            }
        }

        // Sin vecinos visibles cerca, no se aplica separación.
        if (neighboursInFOV == 0)
            return Vector3.zero;

        avoidanceVector /= neighboursInFOV;
        return avoidanceVector.normalized;
    }

    private Vector3 CalculateBoundsVector()
    {
        var offsetToCenter = assignedFlock.transform.position - myTransform.position;
        bool nearBoundary = offsetToCenter.magnitude >= assignedFlock.boundsDistance * 0.9f;

        return nearBoundary ? offsetToCenter.normalized : Vector3.zero;
    }

    private Vector3 CalculateObstacleVector()
    {
        if (Physics.Raycast(
            myTransform.position, myTransform.forward, out _,
            assignedFlock.obstacleDistance, obstacleMask
        ))
        {
            return FindBestDirectionToAvoidObstacle();
        }

        currentObstacleAvoidanceVector = Vector3.zero;
        return Vector3.zero;
    }

    private Vector3 FindBestDirectionToAvoidObstacle()
    {
        if (currentObstacleAvoidanceVector != Vector3.zero)
        {
            if (!Physics.Raycast(
                myTransform.position, myTransform.forward, out _,
                assignedFlock.obstacleDistance, obstacleMask
            ))
            {
                return currentObstacleAvoidanceVector;
            }
        }

        float maxDistance = int.MinValue;
        var selectedDirection = Vector3.zero;

        for (int i = 0; i < directionsToCheckWhenAvoidingObstacles.Length; i++)
        {
            var currentDirection = myTransform.TransformDirection(
                directionsToCheckWhenAvoidingObstacles[i].normalized
            );

            if (Physics.Raycast(
                myTransform.position, currentDirection, out RaycastHit hit,
                assignedFlock.obstacleDistance, obstacleMask
            ))
            {
                float currentDistance = (hit.point - myTransform.position).sqrMagnitude;

                if (currentDistance > maxDistance)
                {
                    maxDistance = currentDistance;
                    selectedDirection = currentDirection;
                }
            }
            else
            {
                currentObstacleAvoidanceVector = currentDirection.normalized;
                return currentObstacleAvoidanceVector;
            }
        }

        return selectedDirection.normalized;
    }

    private bool IsInFOV(Vector3 position)
    {
        return Vector3.Angle(myTransform.forward, position - myTransform.position) <= FOVAngle;
    }
}