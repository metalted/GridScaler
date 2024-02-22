using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using BepInEx.Configuration;

namespace GridScaler
{
    public class ScalerToolAnchor : MonoBehaviour
    {
        public int index;
        public int twin;
        public int axis;

        public void Initialize(int index)
        {
            this.index = index;
            twin = index ^ 1;
            axis = Mathf.FloorToInt(index / 2f);

            GameObject.DontDestroyOnLoad(this.gameObject);

            //this.gameObject.layer = LayerMask.NameToLayer("Gizmo");

            GetComponent<MeshRenderer>().material.color = new Color[] { Color.red, Color.green, Color.blue }[axis];

            this.gameObject.SetActive(false);
        }
    }

    public class ScalerTool : MonoBehaviour
    {
        private Transform pivot;
        private ScalerToolAnchor[] anchors;
        private bool init = false;
        private bool active = false;
        private float step = 1f;
        private Vector3 dimensions;
        public ScalerToolAnchor selectedAnchor;
        private float anchorValue;
        public BlockProperties target;
        public Vector3 previousTargetPosition;
        public Quaternion previousTargetRotation;

        public delegate void DimensionChange(Vector3 prevDimension, Vector3 scaleDirection, int axis);
        public event DimensionChange OnDimensionChange;

        public void Initialize()
        {
            if (init) { return; }

            //Create the pivot point
            pivot = new GameObject("ScalerPivot").transform;
            GameObject.DontDestroyOnLoad(pivot.gameObject);

            //Create the 6 anchors
            anchors = new ScalerToolAnchor[6];
            for (int i = 0; i < 6; i++)
            {
                anchors[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere).AddComponent<ScalerToolAnchor>();
                anchors[i].Initialize(i);
            }

            Place(Vector3.zero, Quaternion.identity, Vector3.one);
            dimensions = Vector3.one;

            GameObject.DontDestroyOnLoad(this.gameObject);

            init = true;
        }

        public void Show()
        {
            if (!init) { return; }

            SetAnchorVisibility(true);
            active = true;
        }

        public void Hide()
        {
            if (!init) { return; }

            SetAnchorVisibility(false);
            active = false;
        }

        public Vector3 GetScale()
        {
            return dimensions;
        }

        private void SetAnchorVisibility(bool state)
        {
            if (!init) { return; }

            foreach (ScalerToolAnchor anchor in anchors)
            {
                anchor.gameObject.SetActive(state);
            }
        }

        public bool TryAnchorGrab()
        {
            if (!active)
            {
                return false;
            }

            ScalerToolAnchor anchor = RaycastForAnchor();
            if (anchor != null)
            {
                selectedAnchor = anchor;
                return true;
            }

            return false;
        }

        public bool TryAnchorRelease()
        {
            if (!active)
            {
                return false;
            }

            if (selectedAnchor != null)
            {
                selectedAnchor = null;
                return true;
            }

            return false;
        }


        private void Update()
        {
            if (active)
            {
                if (selectedAnchor != null)
                {
                    Vector3 preDragAnchorPosition = selectedAnchor.transform.position;

                    //Drag the anchor
                    Plane axisPlane = AxisPlane(Camera.main.transform.position, selectedAnchor.axis, Vector3.zero);
                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

                    Vector3 pos = selectedAnchor.transform.position;
                    Vector3 tpos = anchors[selectedAnchor.twin].transform.position;

                    //Calculate the direction from the anchor to the twin to ensure we move the anchor in the correct direction
                    Vector3 dirTwinToAnchor = (pos - tpos).normalized;

                    if (axisPlane.Raycast(ray, out float distance))
                    {
                        Vector3 targetPoint = ray.GetPoint(distance);
                        Vector3 lineDirection = (tpos - pos).normalized;
                        Vector3 projectedPoint = pos + Vector3.Project(targetPoint - pos, lineDirection);

                        //Calculate the distance from the twin to the projected point
                        float actualDistance = Vector3.Distance(tpos, projectedPoint);

                        //Clmap the distance to the nearest multiple of the set step and ensure its never 0.
                        float clampedDistance = Mathf.Round(actualDistance / step) * step;
                        clampedDistance = Mathf.Max(clampedDistance, step);                        

                        //Calculate the new position for the anchor based on the clamped distance.
                        Vector3 newPosition = tpos + dirTwinToAnchor * clampedDistance;

                        selectedAnchor.transform.position = newPosition;
                    }

                    switch (selectedAnchor.axis)
                    {
                        case 0:
                            anchorValue = dimensions.x;
                            break;
                        case 1:
                            anchorValue = dimensions.y;
                            break;
                        case 2:
                            anchorValue = dimensions.z;
                            break;
                    }

                    if (preDragAnchorPosition != selectedAnchor.transform.position)
                    {
                        Vector3 prevDimension = dimensions;

                        switch (selectedAnchor.axis)
                        {
                            case 0:
                                dimensions.x = Vector3.Distance(anchors[0].transform.position, anchors[1].transform.position);
                                anchorValue = dimensions.x;
                                break;
                            case 1:
                                dimensions.y = Vector3.Distance(anchors[2].transform.position, anchors[3].transform.position);
                                anchorValue = dimensions.y;
                                break;
                            case 2:
                                dimensions.z = Vector3.Distance(anchors[4].transform.position, anchors[5].transform.position);
                                anchorValue = dimensions.z;
                                break;
                        }

                        Vector3 center = tpos + dirTwinToAnchor * anchorValue * 0.5f;

                        Place(center, pivot.rotation, dimensions);

                        Vector3 scaleDirection = Vector3.zero;
                        switch(selectedAnchor.index)
                        {
                            case 0:
                                scaleDirection = pivot.transform.right;
                                break;
                            case 1:
                                scaleDirection = -pivot.transform.right;
                                break;
                            case 2:
                                scaleDirection = pivot.transform.up;
                                break;
                            case 3:
                                scaleDirection = -pivot.transform.up;
                                break;
                            case 4:
                                scaleDirection = pivot.transform.forward;
                                break;
                            case 5:
                                scaleDirection = -pivot.transform.forward;
                                break;
                        }                        

                        OnDimensionChange?.Invoke(prevDimension, scaleDirection.normalized, selectedAnchor.axis);
                    }
                }
                else if(target.transform.position != previousTargetPosition || target.transform.rotation != previousTargetRotation)
                {
                    previousTargetRotation = target.transform.rotation;
                    previousTargetPosition = target.transform.position;
                    Plugin.plg.OnSelection();
                }

                foreach (ScalerToolAnchor anchor in anchors)
                {
                    float cameraDistance = Vector3.Distance(Camera.main.transform.position, anchor.transform.position);
                    anchor.transform.localScale = Vector3.one * cameraDistance / 30f;
                }
            }
        }

        public float GetAnchorValue()
        {
            return anchorValue;
        }

        private Plane AxisPlane(Vector3 cameraPosition, int axis, Vector3 offset)
        {
            Vector3 localCameraPosition = pivot.transform.InverseTransformPoint(cameraPosition).normalized;
            Vector3 normal;
            Vector3 forward;
            Vector3 position;

            switch (axis)
            {
                case 0:
                    localCameraPosition.x = 0;
                    forward = Vector3.right;
                    break;
                case 1:
                    localCameraPosition.y = 0;
                    forward = Vector3.up;
                    break;
                case 2:
                    localCameraPosition.z = 0;
                    forward = Vector3.forward;
                    break;
            }

            normal = pivot.TransformDirection(localCameraPosition);
            position = pivot.TransformPoint(offset);

            return new Plane(normal, position);
        }

        private ScalerToolAnchor RaycastForAnchor()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                ScalerToolAnchor anchor = hit.collider.gameObject.GetComponent<ScalerToolAnchor>();
                return anchor;
            }

            return null;
        }

        public void Place(Vector3 position, Quaternion rotation, Vector3 size)
        {
            pivot.position = position;
            pivot.rotation = rotation;
            dimensions = size;

            anchors[0].transform.position = pivot.TransformPoint(Vector3.right * size.x / 2f);
            anchors[1].transform.position = pivot.TransformPoint(-Vector3.right * size.x / 2f);
            anchors[2].transform.position = pivot.TransformPoint(Vector3.up * size.y / 2f);
            anchors[3].transform.position = pivot.TransformPoint(-Vector3.up * size.y / 2f);
            anchors[4].transform.position = pivot.TransformPoint(Vector3.forward * size.z / 2f);
            anchors[5].transform.position = pivot.TransformPoint(-Vector3.forward * size.z / 2f);

            foreach (ScalerToolAnchor anchor in anchors)
            {
                anchor.transform.rotation = rotation;
            }
        }

        public bool IsActive()
        {
            return active;
        }

        public void SetStep(float value)
        {
            step = value <= 0 ? 0.001f : value;
        }

        public Vector3 GetPercentagePosition(Vector3 worldPosition)
        {           
            Vector3 localPosition = pivot.InverseTransformPoint(worldPosition);
            Vector3 min = dimensions * -0.5f;
            Vector3 percentage = new Vector3((localPosition.x - min.x) / dimensions.x, (localPosition.y - min.y) / dimensions.y, (localPosition.z - min.z) / dimensions.z);
            return percentage;
        }

        public Vector3 PercentageToWorldPosition(Vector3 percentage)
        {
            Vector3 min = dimensions * -0.5f;
            Vector3 local = new Vector3(min.x + percentage.x * dimensions.x, min.y + percentage.y * dimensions.y, min.z + percentage.z * dimensions.z);
            return pivot.TransformPoint(local);
        }           
    }

    [BepInPlugin(pluginGUID, pluginName, pluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string pluginGUID = "com.metalted.zeepkist.gridscaler";
        public const string pluginName = "Grid Scaler";
        public const string pluginVersion = "0.3";
        public static Plugin plg;

        public ConfigEntry<KeyCode> activateScaler;

        private ScalerTool scalerTool;
        public LEV_LevelEditorCentral central;

        private List<string> undoBefore;

        private List<Vector3> localBlockPositions = new List<Vector3>();

        private void Awake()
        {
            activateScaler = Config.Bind("Settings", "Activate", KeyCode.Y, "");

            Harmony harmony = new Harmony(pluginGUID);
            harmony.PatchAll();

            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            plg = this;
        }

        public void Start()
        {
            scalerTool = new GameObject("Grid Scaler").AddComponent<ScalerTool>();
            scalerTool.OnDimensionChange += OnScalerChange;
            scalerTool.Initialize();
        }

        private void OnScalerChange(Vector3 prevDimension, Vector3 scaledDirection, int axis)
        {
            Vector3 scale = scalerTool.GetScale();
            Vector3 scaleDelta = new Vector3(scale.x / prevDimension.x, scale.y / prevDimension.y, scale.z / prevDimension.z);                   

            int i = 0;
            foreach(BlockProperties bp in central.selection.list)
            {
                Vector3 toScale = Vector3.one;
                int closestAxis = ClosestAxis(bp.transform, scaledDirection);
                toScale[closestAxis] = scaleDelta[axis];
                bp.transform.localScale = Vector3.Scale(bp.transform.localScale, toScale);
                bp.transform.position = scalerTool.PercentageToWorldPosition(localBlockPositions[i]);
                i++;
            }           
        }

        public void OnSelection()
        {
            //Get the last object of the selection list
            BlockProperties lastObject = central.selection.list[central.selection.list.Count - 1];
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);           

            //Go over each object in the selection
            foreach(BlockProperties bp in central.selection.list)
            {
                Vector3 extents = bp.boundingBoxSize * 0.5f;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bp.transform.TransformPoint(Vector3.Scale(CornerVectors[i], extents) + bp.boundingBoxOffset);
                    Vector3 localCorner = lastObject.transform.InverseTransformPoint(corner);
                    min.x = Mathf.Min(localCorner.x, min.x);
                    min.y = Mathf.Min(localCorner.y, min.y);
                    min.z = Mathf.Min(localCorner.z, min.z);
                    max.x = Mathf.Max(localCorner.x, max.x);
                    max.y = Mathf.Max(localCorner.y, max.y);
                    max.z = Mathf.Max(localCorner.z, max.z);
                }                
            }

            Vector3 center = lastObject.transform.TransformPoint((max + min) / 2);
            Vector3 size = Vector3.Scale(lastObject.transform.localScale, (max - min));           

            scalerTool.Place(center, lastObject.transform.rotation, size);            
            
            localBlockPositions.Clear();
            foreach (BlockProperties bp in central.selection.list)
            {
                localBlockPositions.Add(scalerTool.GetPercentagePosition(bp.transform.position));
            }

            scalerTool.target = lastObject;
            scalerTool.previousTargetPosition = lastObject.transform.position;
            scalerTool.previousTargetRotation = lastObject.transform.rotation;
            scalerTool.Show();
        }

        private int ClosestAxis(Transform t, Vector3 direction)
        {
            // Get the local axes of the object
            Vector3 right = t.right.normalized;
            Vector3 up = t.up.normalized;
            Vector3 forward = t.forward.normalized;

            // Calculate the angles between each local axis and the direction vector
            float angleRightPositive = Vector3.Angle(right, direction);
            float angleRightNegative = Vector3.Angle(-right, direction);
            float angleUpPositive = Vector3.Angle(up, direction);
            float angleUpNegative = Vector3.Angle(-up, direction);
            float angleForwardPositive = Vector3.Angle(forward, direction);
            float angleForwardNegative = Vector3.Angle(-forward, direction);

            // Find the axis with the smallest angle
            float minAngle = Mathf.Min(angleRightPositive, Mathf.Min(angleRightNegative,
                                 Mathf.Min(angleUpPositive, Mathf.Min(angleUpNegative,
                                 Mathf.Min(angleForwardPositive, angleForwardNegative)))));

            // Determine which axis is closest aligned with the direction
            if (minAngle == angleRightPositive || minAngle == angleRightNegative)
            {
                return 0;
            }
            else if (minAngle == angleUpPositive || minAngle == angleUpNegative)
            {
                return 1;
            }
            else if (minAngle == angleForwardPositive || minAngle == angleForwardNegative)
            {
                return 2;
            }

            // Default case (shouldn't be reached)
            return -1;
        }

        private static Vector3[] CornerVectors = new Vector3[]
        {
            new Vector3(-1,-1,-1),
            new Vector3(1, -1, -1),
            new Vector3(-1, 1, -1),
            new Vector3(1, 1, -1),
            new Vector3(-1,-1,1),
            new Vector3(1, -1, 1),
            new Vector3(-1, 1, 1),
            new Vector3(1, 1, 1)
        };

        public void Update()
        {
            if (central != null)
            {
                if (scalerTool.IsActive())
                {
                    if (Input.GetKeyDown((KeyCode)activateScaler.BoxedValue))
                    {
                        if (scalerTool.TryAnchorGrab())
                        {
                            undoBefore = central.undoRedo.ConvertBlockListToJSONList(central.selection.list);
                        }
                    }

                    if (Input.GetKeyUp((KeyCode)activateScaler.BoxedValue))
                    {
                        if (scalerTool.TryAnchorRelease())
                        {
                            List<string> after = central.undoRedo.ConvertBlockListToJSONList(central.selection.list);
                            List<string> selectionList = central.undoRedo.ConvertSelectionToStringList(central.selection.list);
                            central.validation.BreakLock(central.undoRedo.ConvertBeforeAndAfterListToCollection(undoBefore, after, central.selection.list, selectionList, selectionList), "Gizmo1");
                        }
                    }

                    scalerTool.SetStep(central.gizmos.gridXZ);
                }
            }

            if(scalerTool.IsActive())
            {
                if(central == null)
                {
                    HideScaler();
                }
            }
        }
       
        public void HideScaler()
        {
            if (scalerTool.IsActive())
            {
                scalerTool.TryAnchorRelease();
                scalerTool.Hide();
                localBlockPositions.Clear();
            }
        }

        public void OnGUI()
        {
            if (scalerTool.selectedAnchor != null)
            {
                string value = "" + scalerTool.GetAnchorValue();

                GUI.Box(new Rect(new Vector2(Input.mousePosition.x + 30, Screen.height - Input.mousePosition.y - 30), new Vector2(100, 30)), value);
            }
        }
    }

    [HarmonyPatch(typeof(LEV_LevelEditorCentral), "Awake")]
    public class LEVCentralPostfix
    {
        public static void Postfix(LEV_LevelEditorCentral __instance)
        {
            Plugin.plg.central = __instance;
        }
    }

    [HarmonyPatch(typeof(LEV_Selection), "ClickBuilding")]
    public class ClickBuildingPrefix
    {
        public static void Postfix(LEV_Selection __instance)
        {
            Plugin.plg.OnSelection();
        }
    }

    [HarmonyPatch(typeof(LEV_Selection), "ClickNothing")]
    public class ClickNothingPrefix
    {
        public static void Prefix()
        {
            Plugin.plg.HideScaler();
        }
    }

    [HarmonyPatch(typeof(LEV_Selection), "DeselectAllBlocks")]
    public class DeselectAllBlocksPrefix
    {
        public static void Prefix()
        {
            Plugin.plg.HideScaler();
        }
    }
}