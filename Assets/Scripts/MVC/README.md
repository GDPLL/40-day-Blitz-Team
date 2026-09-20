# MVC ownership

`Models/` contains serializable game state and rules only. These classes do not reference `MonoBehaviour`, `Input`, `Transform`, `Rigidbody`, Animator, particles, audio, or UI.

`Controllers/` are the only layer that reads player input, uses physics queries, changes a model, and decides which view command to issue.

`Views/` own Unity scene objects and presentation. They do not query input or decide gameplay rules.

Runtime composition is deliberately one-way:

`PlayerInputController -> Player_Control -> PlayerModel -> PlayerView / Player_animation / Player_camera / Gun_Control -> WeaponView`

`Gun_Control -> WeaponModel -> WeaponView`

`Object_System -> HealthModel`

The fields labelled **Migration bindings** remain on the existing controller components only so the original prefabs keep their serialized references. In `Awake`, those references are injected into a view. Once the prefabs are re-saved in the Unity editor, they can be moved into the view inspectors and removed from the controller without changing runtime behaviour.
