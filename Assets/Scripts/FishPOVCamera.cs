using UnityEngine;

[RequireComponent(typeof(CameraController))]
public class FishPOVCamera : MonoBehaviour
{
    [SerializeField] private Flock flock;
    [SerializeField] private Vector3 eyeOffset = new Vector3(0f, 0.2f, 0.8f);

    private CameraController diverController;
    private Transform fish;
    private Vector3 diverPosition;
    private Quaternion diverRotation;
    private bool followingFish;

    private void Awake()
    {
        diverController = GetComponent<CameraController>();
    }

    private void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (followingFish)
                ReturnToDiver();
            else
                FollowRandomFish();
        }

        if (!followingFish)
            return;

        if (fish == null)
        {
            ReturnToDiver();
            return;
        }

        transform.SetPositionAndRotation(
            fish.position + fish.rotation * eyeOffset,
            fish.rotation
        );
    }

    private void FollowRandomFish()
    {
        FlockUnit[] units = flock != null ? flock.allUnits : null;

        if (units == null || units.Length == 0)
        {
            Debug.LogWarning("Asigna Flock en Fish POV Camera y verifica que haya peces.");
            return;
        }

        FlockUnit selected = units[Random.Range(0, units.Length)];
        if (selected == null)
            return;

        diverPosition = transform.position;
        diverRotation = transform.rotation;
        diverController.enabled = false;
        fish = selected.transform;
        followingFish = true;
    }

    private void ReturnToDiver()
    {
        followingFish = false;
        fish = null;
        transform.SetPositionAndRotation(diverPosition, diverRotation);
        diverController.enabled = true;
    }
}