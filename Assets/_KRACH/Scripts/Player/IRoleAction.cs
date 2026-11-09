/// <summary>
/// Interface for role-specific actions.
/// Implemented by PlayerInteract (vandalist) and HunterAccuse (hunter).
///
/// Called by PlayerRoleSetup when the role gets activated/deactivated.
/// For new role-specific actions, simply implement this interface and register them
/// in PlayerRoleSetup as an IRoleAction reference.
/// </summary>
public interface IRoleAction
{
    void OnRoleActivated();
    void OnRoleDeactivated();
}
