// Hizli test icin basit FP oyuncu. Kendi oyuncu kontrolcun hazirsa bunu silebilirsin.
// Eski Input Manager ve yeni Input System ile calisir.
// Sol tik: imleci kilitle | Esc: birak | WASD | Shift: kos | Space: zipla | E: etkilesim
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DepoLevel
{
    [RequireComponent(typeof(CharacterController))]
    public class DepoTestPlayer : MonoBehaviour
    {
        public Transform cameraPivot;
        public float walkSpeed = 4.2f;
        public float runSpeed = 8.0f;
        public float mouseSensitivity = 0.12f;
        public float jumpHeight = 0.9f;
        public float gravity = -20f;
        public float interactDistance = 2.6f;

        CharacterController cc;
        float pitch, yaw, vy;
        string hint;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (cameraPivot == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) cameraPivot = cam.transform;
            }
            yaw = transform.eulerAngles.y;
        }

        void Start()
        {
            if (DepoPlayerSpawn.Current != null) DepoPlayerSpawn.Current.Place(transform);
            yaw = transform.eulerAngles.y;
        }

        // ---- girdi soyutlamasi
        Vector2 MoveInput()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current; if (k == null) return Vector2.zero;
            float x = (k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0);
            float y = (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0);
            return new Vector2(x, y);
#else
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
        }
        Vector2 LookInput()
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current; return m == null ? Vector2.zero : m.delta.ReadValue();
#else
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
#endif
        }
        bool Run()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
#else
            return Input.GetKey(KeyCode.LeftShift);
#endif
        }
        bool JumpDown()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Space);
#endif
        }
        bool InteractDown()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }
        bool ClickDown()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }
        bool EscDown()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        void Update()
        {
            if (ClickDown()) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (EscDown()) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 look = LookInput() * mouseSensitivity;
                yaw += look.x;
                pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
            }
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            Vector2 mv = Vector2.ClampMagnitude(MoveInput(), 1f);
            Vector3 move = (transform.right * mv.x + transform.forward * mv.y) * (Run() ? runSpeed : walkSpeed);

            if (cc.isGrounded)
            {
                vy = -2f;
                if (JumpDown()) vy = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            vy += gravity * Time.deltaTime;
            move.y = vy;
            cc.Move(move * Time.deltaTime);

            UpdateInteract();
        }

        void UpdateInteract()
        {
            hint = null;
            if (cameraPivot == null) return;
            var ray = new Ray(cameraPivot.position, cameraPivot.forward);
            if (Physics.Raycast(ray, out var hit, interactDistance, ~0, QueryTriggerInteraction.Collide))
            {
                var t = hit.collider.GetComponent<DepoTrigger>();
                if (t != null && t.type == DepoTriggerType.Interact)
                {
                    hint = "[E] " + t.Prompt;
                    if (InteractDown()) { t.Interact(gameObject); Debug.Log("[Depo] Etkilesim: " + t.interactId); }
                }
                else
                {
                    var slot = hit.collider.GetComponent<DepoShelfSlot>();
                    if (slot != null) hint = slot.zoneId + " / " + slot.slotType + " / kat " + slot.level + (slot.IsFree ? " (bos)" : " (dolu)");
                }
            }
        }

        void OnGUI()
        {
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            GUI.Box(new Rect(cx - 2, cy - 2, 4, 4), GUIContent.none);
            if (!string.IsNullOrEmpty(hint))
            {
                var st = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold };
                GUI.Label(new Rect(cx - 200, cy + 18, 400, 26), hint, st);
            }
            if (Cursor.lockState != CursorLockMode.Locked)
                GUI.Label(new Rect(10, 10, 420, 24), "Tikla: fareyi kilitle  |  WASD, Shift kos, Space zipla, E etkilesim");
        }
    }
}
