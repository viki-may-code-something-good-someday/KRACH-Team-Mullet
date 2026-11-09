using UnityEngine;

/// <summary>
/// Places objects without physics on the ground.
///
/// The spawn points in the level are set up for real players: they fall down after spawning
/// and get placed on the ground by their CharacterController. A test dummy has neither a
/// CharacterController nor a Rigidbody and would stay in the air exactly at spawn point
/// height – out of reach for a hunter standing on the ground.
/// </summary>
public static class SpawnPlacement
{
    /// <summary>
    /// Moves <paramref name="position"/> vertically so that the bottom of
    /// <paramref name="body"/> touches the ground below. If the raycast finds no ground,
    /// the position stays unchanged.
    /// </summary>
    public static Vector3 DropToGround(
        Vector3 position, CapsuleCollider body, LayerMask groundMask, float searchHeight = 30f)
    {
        if (body == null) return position;

        // Start slightly above, so a spawn point that sits barely inside the ground still hits.
        Vector3 rayStart = position + Vector3.up * 0.5f;

        if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit,
                             searchHeight, groundMask, QueryTriggerInteraction.Ignore))
        {
            Debug.LogWarning($"[SpawnPlacement] No ground found below {position} " +
                             $"(groundMask={groundMask.value}) – position kept unchanged.");
            return position;
        }

        float bottomOffset = body.center.y - body.height * 0.5f;
        return new Vector3(position.x, hit.point.y - bottomOffset, position.z);
    }
}
