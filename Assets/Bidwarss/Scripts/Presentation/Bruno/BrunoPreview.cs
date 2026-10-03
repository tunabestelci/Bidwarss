using UnityEngine;
using UnityEngine.InputSystem;
using Bidwarss.Domain;

namespace Bidwarss
{
    /// <summary>Only attached in the separate character-review scene.</summary>
    [DefaultExecutionOrder(250)]
    public sealed class BrunoPreview : MonoBehaviour
    {
        public BrunoMotion character;
        public Camera reviewCamera;
        float yaw = 25, pitch = 8, distance = 4.4f;
        bool dragging;
        GameObject cutter,prybar,heldBox;
        void Start()
        {
            var knife=Resources.Load<GameObject>("Bruno/BoxCutterTool");var pry=Resources.Load<GameObject>("Bruno/PryBarTool");
            if(knife!=null){cutter=Instantiate(knife,transform);cutter.SetActive(false);}
            if(pry!=null){prybar=Instantiate(pry,transform);prybar.SetActive(false);}
            heldBox=GameObject.CreatePrimitive(PrimitiveType.Cube);heldBox.name="Carry alignment sample";
            heldBox.transform.SetParent(character.transform,false);heldBox.transform.localPosition=new Vector3(0,1.23f,.54f);heldBox.transform.localScale=new Vector3(.42f,.38f,.32f);
            Destroy(heldBox.GetComponent<Collider>());
            var skin=character.GetComponentInChildren<SkinnedMeshRenderer>();
            if(skin!=null)heldBox.GetComponent<Renderer>().sharedMaterial=skin.sharedMaterial;
            heldBox.SetActive(false);
        }
        void Update()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse != null)
            {
                dragging = mouse.rightButton.isPressed;
                if (dragging)
                {
                    var delta = mouse.delta.ReadValue();
                    yaw += delta.x * .2f; pitch = Mathf.Clamp(pitch - delta.y * .2f, -8, 50);
                }
                distance = Mathf.Clamp(distance - mouse.scroll.ReadValue().y * .002f, 1.4f, 7);
            }
            if (keyboard != null && character != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) Set(0, false);
                if (keyboard.digit2Key.wasPressedThisFrame) Set(2.3f, false);
                if (keyboard.digit3Key.wasPressedThisFrame) Set(0, true);
                if (keyboard.digit4Key.wasPressedThisFrame) Set(2.3f, true);
                if(keyboard.digit5Key.wasPressedThisFrame)SetTool(OpeningTool.BoxCutter);
                if(keyboard.digit6Key.wasPressedThisFrame)SetTool(OpeningTool.PryBar);
                if (keyboard.eKey.wasPressedThisFrame) { Set(0, false); character.Greet(); }
            }
            if (reviewCamera != null)
            {
                Vector3 target = new Vector3(0, 1.05f, 0);
                reviewCamera.transform.position = target + Quaternion.Euler(pitch, yaw, 0) * Vector3.forward * distance;
                reviewCamera.transform.LookAt(target);
            }
        }
        void Set(float speed, bool carrying) { character.previewSpeed = speed; character.previewCarrying = carrying;character.previewTool=OpeningTool.None; }
        void SetTool(OpeningTool tool){Set(0,false);character.previewTool=tool;}
        void LateUpdate()
        {
            if(character==null)return;
            if(cutter!=null)cutter.SetActive(character.previewTool==OpeningTool.BoxCutter);
            if(prybar!=null)prybar.SetActive(character.previewTool==OpeningTool.PryBar);
            if(heldBox!=null)heldBox.SetActive(character.previewCarrying);
            var active=character.previewTool==OpeningTool.BoxCutter?cutter:prybar;
            if(active!=null && character.PreviewToolPose(out var p,out var r))active.transform.SetPositionAndRotation(p,r);
        }
        void OnGUI()
        {
            if (character == null) return;
            GUILayout.BeginArea(new Rect(20, 20, 280, 350), GUI.skin.box);
            GUILayout.Label("BRUNO / BIDWARSS");
            GUILayout.Label("Sag tus: dondur | Tekerlek: yaklas");
            if (GUILayout.Button("1 - Bekle")) Set(0, false);
            if (GUILayout.Button("2 - Yuru")) Set(2.3f, false);
            if (GUILayout.Button("3 - Tasi")) Set(0, true);
            if (GUILayout.Button("4 - Tasi ve yuru")) Set(2.3f, true);
            if(GUILayout.Button("5 - Maket bicagi"))SetTool(OpeningTool.BoxCutter);
            if(GUILayout.Button("6 - Civi sokucu"))SetTool(OpeningTool.PryBar);
            if (GUILayout.Button("E - Selam ver")) { Set(0, false); character.Greet(); }
            character.lookAround = GUILayout.Toggle(character.lookAround, "Etrafa bak");
            GUILayout.EndArea();
        }
    }
}
