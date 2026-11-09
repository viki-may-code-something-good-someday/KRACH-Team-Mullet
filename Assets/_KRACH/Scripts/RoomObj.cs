using FMODUnity;
using UnityEngine;

[System.Serializable]
public class RoomObj : MonoBehaviour
{
    public bool isEnemyInThisRoom;
    public bool canEnemySpawnInThisRoom;
    public StudioEventEmitter musicEmitter;
    public bool hasPlayerOpenedThisRoom;
    [SerializeField] public Wall[] wallsInThisRoom;

    private void Awake()
    {
        SetEnemeyIsInThisRoom(false);
    }

    private void Start()
    {
        hasPlayerOpenedThisRoom = false;
        if (musicEmitter) musicEmitter.SetParameter("RoomOcclusion", 1);
    }

    public void AssignWalls(Wall[] walls)
    {
        wallsInThisRoom = walls;
    }

    public void RemoveWallFromRoomArray(Wall wallToRemove)
    {
        SetPlayerHasOpenedThisRoom(true);

        if (wallsInThisRoom == null || wallsInThisRoom.Length == 0) return;

        for (int i = 0; i < wallsInThisRoom.Length; i++)
        {
            if (wallsInThisRoom[i] == wallToRemove)
            {
                // Set the element to null ...
                wallsInThisRoom[i] = null;
                // ... and immediately create a new array without null elements
                wallsInThisRoom = System.Array.FindAll(wallsInThisRoom, w => w != null);
                break;
            }
        }

    }

    public Vector3 GetEnemyPosition()
    {
        return transform.position;
    }

    public void SetEnemeyIsInThisRoom(bool isEnemyInThisRoom)
    {
        this.isEnemyInThisRoom = isEnemyInThisRoom;
        if (TryGetComponent(out MeshRenderer meshRenderer)) meshRenderer.enabled = isEnemyInThisRoom;
    }
    public void SetPlayerHasOpenedThisRoom(bool hasPlayerOpenedThisRoom)
    {
        this.hasPlayerOpenedThisRoom = hasPlayerOpenedThisRoom;
        if (hasPlayerOpenedThisRoom)
            if (musicEmitter != null)
            {
                musicEmitter.SetParameter("RoomOcclusion", 0);
            }
    }
}
