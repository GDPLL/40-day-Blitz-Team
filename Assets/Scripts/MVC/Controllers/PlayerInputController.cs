using UnityEngine;

/// <summary>The sole adapter between Unity's Input API and the domain input DTO.</summary>
public sealed class PlayerInputController : MonoBehaviour
{
    public PlayerInput Read()
    {
        return new PlayerInput
        {
            Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
            Fire = Input.GetMouseButton(0),
            Aim = Input.GetMouseButton(1),
            Jump = Input.GetKeyDown(KeyCode.Space),
            Reload = Input.GetKeyDown(KeyCode.R),
            Run = Input.GetKey(KeyCode.LeftShift),
            Crouch = Input.GetKey(KeyCode.LeftControl)
        };
    }

    public Vector2 ReadLookDelta() => new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
}
