using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using BepInEx.Configuration;
using System;
using System.Globalization;

namespace GridScaler
{
    public class ScalerToolAnchor : MonoBehaviour
    {
        private Color[] axisColors = new Color[] { Color.red, Color.green, Color.blue };
        private Vector3[] axisDirections = new Vector3[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        private int index;
        private int twin;
        private int axis;
        private Vector3 axisDirection;

        public void Initialize(int index)
        {
            //Direction index (0=x, 1=-x, 2=y, 3=-y, 4=z, 5=-z)
            this.index = index;

            //Opposite anchor (0&1)(2&3)(4&5)
            twin = index ^ 1;

            //Axis (x=0,y=1,z=2)
            axis = Mathf.FloorToInt(index / 2f);

            axisDirection = axisDirections[index];

            GetComponent<MeshRenderer>().material.color = axisColors[axis];
            this.gameObject.SetActive(false);
        }

        public int GetIndex() { return index; }
        public int GetTwin() { return twin; }
        public int GetAxis() { return axis; }
        public Vector3 GetAxisDirection() { return axisDirection; }
    }

    public class ScalerTool : MonoBehaviour
    {
        private bool init = false;
        private bool active = false;

        private int layerIndex = 0;

        //Center pivot transform.
        private Transform pivot;
        //Scaling anchors.
        private ScalerToolAnchor[] anchors;
        //Is the scaler visible in the scene?
        private bool visible = false;

        //Scale increment.
        private float step = 1f;
        //Current dimensions of the scaling tool in world space.
        private Vector3 dimensions;
        //The currently grabbed anchor.
        private ScalerToolAnchor selectedAnchor;
        //The length of the currently scaled axis.
        private float anchorValue;
        private string[] stringDimensions = new string[3] { "1", "1", "1" };

        //The target of scaling, the last block in the selection list.
        private List<BlockProperties> selection;
        private BlockProperties target;
        private Vector3 previousTargetPosition;
        private Quaternion previousTargetRotation;

        private Vector3[] CornerVectors = new Vector3[]
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

        private List<Vector3> localSelectionPositions = new List<Vector3>();

        public void Initialize()
        {
            if (init) { return; }

            //Create the pivot point
            pivot = new GameObject("ScalerPivot").transform;
            pivot.transform.parent = this.gameObject.transform;

            //Create the 6 anchors
            anchors = new ScalerToolAnchor[6];
            for (int i = 0; i < 6; i++)
            {
                anchors[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere).AddComponent<ScalerToolAnchor>();
                anchors[i].transform.parent = this.gameObject.transform;
                anchors[i].Initialize(i);
            }

            Place(Vector3.zero, Quaternion.identity, Vector3.one);
            dimensions = Vector3.one;

            GameObject.DontDestroyOnLoad(this.gameObject);

            init = true;
        }

        #region State
        public bool IsActive()
        {
            return active;
        }

        public void Activate()
        {
            active = true;
        }

        public void Deactivate()
        {
            active = false;
        }

        public bool IsVisible()
        {
            return visible;
        }

        public bool IsScaling()
        {
            return selectedAnchor != null;
        }
        #endregion

        #region Tool
        public void Show()
        {
            if (!init) { return; }

            SetAnchorVisibility(true);
            visible = true;
        }

        public void Hide()
        {
            if (!init) { return; }

            SetAnchorVisibility(false);
            visible = false;
        }

        public void Detach()
        {
            if (!init) { return; }

            if (IsVisible())
            {
                TryAnchorRelease();
                Hide();
                localSelectionPositions.Clear();
            }
        }

        private void Place(Vector3 position, Quaternion rotation, Vector3 size)
        {
            pivot.position = position;
            pivot.rotation = rotation;
            dimensions = size;
            stringDimensions[0] = Plugin.RoundToDecimalPlacesString(dimensions.x, 3);
            stringDimensions[1] = Plugin.RoundToDecimalPlacesString(dimensions.y, 3);
            stringDimensions[2] = Plugin.RoundToDecimalPlacesString(dimensions.z, 3);

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
        #endregion

        #region Get
        public float GetAnchorValue()
        {
            return anchorValue;
        }

        public int GetScaledAxis()
        {
            if (IsScaling())
            {
                return selectedAnchor.GetAxis();
            }

            return -1;
        }

        public Vector3 GetDimensions()
        {
            return dimensions;
        }

        public string GetStringDimensions(int axis)
        {
            // Check if the value is a floating point number
            if (stringDimensions[axis].Contains(".") && float.TryParse(stringDimensions[axis], out float floatValue))
            {
                // Convert the floating point number to a string
                string formattedValue = floatValue.ToString();

                // Remove trailing zeroes after the decimal point
                while (formattedValue.EndsWith("0"))
                {
                    formattedValue = formattedValue.Substring(0, formattedValue.Length - 1);
                }

                // Remove the decimal point if all digits after it were zeroes
                if (formattedValue.EndsWith("."))
                {
                    formattedValue = formattedValue.Substring(0, formattedValue.Length - 1);
                }

                // Return the formatted value
                return formattedValue;
            }
            else
            {
                // Return the original value if it's not a floating point number
                return stringDimensions[axis];
            }
        }

        public float GetStep()
        {
            return step;
        }

        public List<BlockProperties> GetSelection()
        {
            return selection;
        }
        #endregion

        #region Set
        public void SetLayer(int layerIndex)
        {
            this.layerIndex = layerIndex;
            gameObject.layer = layerIndex;
            foreach(ScalerToolAnchor anchor in anchors)
            {
                anchor.gameObject.layer = layerIndex;
            }

        }
        public void SetStep(float value, float zeroValue = 0.001f)
        {
            if (value <= 0)
            {
                if (zeroValue <= 0)
                {
                    step = 0.001f;
                }
                else
                {
                    step = zeroValue;
                }
            }
            else
            {
                step = value;
            }
        }

        public void SetSelection(List<BlockProperties> selection)
        {
            this.selection = selection;
            target = selection[selection.Count - 1];
            previousTargetPosition = target.transform.position;
            previousTargetRotation = target.transform.rotation;

            //Bounding box min and max positions.
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

            //Go over each object in the selection
            foreach (BlockProperties bp in selection)
            {
                //Get the extents from the size of the block.
                Vector3 extents = bp.boundingBoxSize * 0.5f;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bp.transform.TransformPoint(Vector3.Scale(CornerVectors[i], extents) + bp.boundingBoxOffset);
                    Vector3 localCorner = target.transform.InverseTransformPoint(corner);
                    min.x = Mathf.Min(localCorner.x, min.x);
                    min.y = Mathf.Min(localCorner.y, min.y);
                    min.z = Mathf.Min(localCorner.z, min.z);
                    max.x = Mathf.Max(localCorner.x, max.x);
                    max.y = Mathf.Max(localCorner.y, max.y);
                    max.z = Mathf.Max(localCorner.z, max.z);
                }
            }

            Vector3 center = target.transform.TransformPoint((max + min) / 2);
            Vector3 size = Vector3.Scale(target.transform.localScale, (max - min));

            Place(center, target.transform.rotation, size);

            localSelectionPositions.Clear();
            foreach (BlockProperties bp in selection)
            {
                localSelectionPositions.Add(GetPercentagePosition(bp.transform.position));
            }
        }
        #endregion

        #region Anchor
        public bool TryAnchorGrab()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 999999f, 1 << layerIndex))
            {
                ScalerToolAnchor anchor = hit.collider.gameObject.GetComponent<ScalerToolAnchor>();
                if (anchor != null)
                {
                    selectedAnchor = anchor;
                    return true;
                }
            }

            return false;
        }

        public bool TryAnchorRelease()
        {
            if (IsScaling())
            {
                selectedAnchor = null;
                return true;
            }

            return false;
        }

        private void SetAnchorVisibility(bool state)
        {
            if (!init) { return; }

            foreach (ScalerToolAnchor anchor in anchors)
            {
                anchor.gameObject.SetActive(state);
            }
        }
        #endregion

        private void Update()
        {
            if (IsVisible())
            {
                if(target == null)
                {
                    Debug.LogError("Grid Scaling Target Destroyed! Reverting...");
                    Plugin.plg.central.selection.DeselectAllBlocks(false, " Gizmo1");
                    return;
                }

                if (IsScaling())
                {
                    Vector3 preDragAnchorPosition = selectedAnchor.transform.position;

                    //Drag the anchor
                    Plane axisPlane = AxisPlane(Camera.main.transform.position, selectedAnchor.GetAxis(), Vector3.zero);
                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

                    Vector3 pos = selectedAnchor.transform.position;
                    Vector3 twinPos = anchors[selectedAnchor.GetTwin()].transform.position;

                    //Calculate the direction from the anchor to the twin to ensure we move the anchor in the correct direction
                    Vector3 dirTwinToAnchor = (pos - twinPos).normalized;

                    if (axisPlane.Raycast(ray, out float distance))
                    {
                        Vector3 targetPoint = ray.GetPoint(distance);
                        Vector3 lineDirection = (twinPos - pos).normalized;
                        Vector3 projectedPoint = pos + Vector3.Project(targetPoint - pos, lineDirection);

                        //Calculate the distance from the twin to the projected point
                        float actualDistance = Vector3.Distance(twinPos, projectedPoint);

                        //Clmap the distance to the nearest multiple of the set step and ensure its never 0.
                        float clampedDistance = Mathf.Round(actualDistance / step) * step;
                        clampedDistance = Mathf.Max(clampedDistance, step);

                        //Calculate the new position for the anchor based on the clamped distance.
                        Vector3 newPosition = twinPos + dirTwinToAnchor * clampedDistance;

                        selectedAnchor.transform.position = newPosition;
                    }

                    int decimals = Plugin.CountDecimalPlaces(step);

                    switch (selectedAnchor.GetAxis())
                    {
                        case 0:
                            anchorValue = dimensions.x;
                            stringDimensions[0] = Plugin.RoundToDecimalPlacesString(dimensions.x, decimals);
                            break;
                        case 1:
                            anchorValue = dimensions.y;
                            stringDimensions[1] = Plugin.RoundToDecimalPlacesString(dimensions.y, decimals);
                            break;
                        case 2:
                            anchorValue = dimensions.z;
                            stringDimensions[2] = Plugin.RoundToDecimalPlacesString(dimensions.z, decimals);
                            break;
                    }

                    if (preDragAnchorPosition != selectedAnchor.transform.position)
                    {
                        Vector3 prevDimension = dimensions;

                        switch (selectedAnchor.GetAxis())
                        {
                            case 0:
                                dimensions.x = Plugin.RoundToDecimalPlaces(Vector3.Distance(anchors[0].transform.position, anchors[1].transform.position), decimals);
                                anchorValue = dimensions.x;
                                break;
                            case 1:
                                dimensions.y = Plugin.RoundToDecimalPlaces(Vector3.Distance(anchors[2].transform.position, anchors[3].transform.position), decimals);
                                anchorValue = dimensions.y;
                                break;
                            case 2:
                                dimensions.z = Plugin.RoundToDecimalPlaces(Vector3.Distance(anchors[4].transform.position, anchors[5].transform.position), decimals);
                                anchorValue = dimensions.z;
                                break;
                        }

                        Vector3 center = twinPos + dirTwinToAnchor * anchorValue * 0.5f;
                        Place(center, pivot.rotation, dimensions);
                        OnScalerChange(prevDimension, pivot.TransformDirection(selectedAnchor.GetAxisDirection()).normalized, selectedAnchor.GetAxis());
                    }
                }
                else if (target.transform.position != previousTargetPosition || target.transform.rotation != previousTargetRotation)
                {
                    previousTargetRotation = target.transform.rotation;
                    previousTargetPosition = target.transform.position;
                    SetSelection(selection);
                }

                foreach (ScalerToolAnchor anchor in anchors)
                {
                    float cameraDistance = Vector3.Distance(Camera.main.transform.position, anchor.transform.position);
                    anchor.transform.localScale = Vector3.one * cameraDistance / 30f;
                }
            }
        }

        private void OnScalerChange(Vector3 prevDimension, Vector3 scaledDirection, int axis)
        {
            Vector3 dimensions = GetDimensions();
            Vector3 dimensionDelta = new Vector3(dimensions.x / prevDimension.x, dimensions.y / prevDimension.y, dimensions.z / prevDimension.z);

            int i = 0;
            foreach (BlockProperties bp in selection)
            {
                Vector3 toScale = Vector3.one;
                int closestAxis = ClosestAxis(bp.transform, scaledDirection);
                toScale[closestAxis] = dimensionDelta[axis];
                bp.transform.localScale = Vector3.Scale(bp.transform.localScale, toScale);
                bp.transform.position = PercentageToWorldPosition(localSelectionPositions[i]);
                bp.SomethingChanged();
                i++;
            }
        }

        //Calculate the plane used for raycasting the new anchor position.
        private Plane AxisPlane(Vector3 cameraPosition, int axis, Vector3 offset)
        {
            Vector3 localCameraPosition = pivot.transform.InverseTransformPoint(cameraPosition).normalized;
            Vector3 normal;
            Vector3 position;

            switch (axis)
            {
                case 0:
                    localCameraPosition.x = 0;
                    break;
                case 1:
                    localCameraPosition.y = 0;
                    break;
                case 2:
                    localCameraPosition.z = 0;
                    break;
            }

            normal = pivot.TransformDirection(localCameraPosition);
            position = pivot.TransformPoint(offset);

            return new Plane(normal, position);
        }

        //Get the position of an object as a percentage (0-1) inside the tools bounding box.
        private Vector3 GetPercentagePosition(Vector3 worldPosition)
        {
            Vector3 localPosition = pivot.InverseTransformPoint(worldPosition);
            Vector3 min = dimensions * -0.5f;
            Vector3 percentage = new Vector3((localPosition.x - min.x) / dimensions.x, (localPosition.y - min.y) / dimensions.y, (localPosition.z - min.z) / dimensions.z);
            return percentage;
        }

        //Get the world position of an object from a percentage position inside the tools bounding box.
        private Vector3 PercentageToWorldPosition(Vector3 percentage)
        {
            Vector3 min = dimensions * -0.5f;
            Vector3 local = new Vector3(min.x + percentage.x * dimensions.x, min.y + percentage.y * dimensions.y, min.z + percentage.z * dimensions.z);
            return pivot.TransformPoint(local);
        }

        //Get the side of a transform that aligns the closest the the given direction in world space.
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
    }

    [BepInPlugin(pluginGUID, pluginName, pluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string pluginGUID = "com.metalted.zeepkist.gridscaler";
        public const string pluginName = "Grid Scaler";
        public const string pluginVersion = "0.7";
        public static Plugin plg;

        public ConfigEntry<KeyCode> activateScaler;
        public ConfigEntry<KeyCode> grabAnchor;
        public ConfigEntry<int> mouseButton;
        public ConfigEntry<int> tooltipWidth;
        public ConfigEntry<int> tooltipHeight;
        public ConfigEntry<int> tooltipFontSize;
        public ConfigEntry<bool> showAllAxis;
        public ConfigEntry<float> zeroValue;
        public ConfigEntry<bool> translationGUI;
        public ConfigEntry<bool> rotationGUI;

        private ScalerTool scalerTool;
        public LEV_LevelEditorCentral central;

        private List<string> undoBefore;
        private Texture2D blackTex;

        private void Awake()
        {
            activateScaler = Config.Bind("Settings", "Activate", KeyCode.Keypad9, "");
            grabAnchor = Config.Bind("Settings", "Grab Anchor", KeyCode.Y, "");
            mouseButton = Config.Bind("Settings", "Mouse Button Grab Anchor", 3, "");
            tooltipWidth = Config.Bind("Settings", "Tooltip Width", 100, "");
            tooltipHeight = Config.Bind("Settings", "Tooltip Height", 30, "");
            tooltipFontSize = Config.Bind("Settings", "Tooltip Text Size", 16, "");
            showAllAxis = Config.Bind("Settings", "Show All Axis", false, "");
            zeroValue = Config.Bind("Settings", "Zero Value", 0.001f, "");
            translationGUI = Config.Bind("Settings", "Show Translation GUI", false, "");
            rotationGUI = Config.Bind("Settings", "Show Rotation GUI", false, "");

            Harmony harmony = new Harmony(pluginGUID);
            harmony.PatchAll();

            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            plg = this;
            blackTex = new Texture2D(1, 1);
            blackTex.SetPixel(0, 0, Color.black);
            blackTex.Apply();
        }

        public void Start()
        {
            scalerTool = new GameObject("Grid Scaler").AddComponent<ScalerTool>();
            scalerTool.Initialize();
        }

        public void SetAnchorLayer(int layerIndex)
        {
            if(scalerTool != null)
            {
                scalerTool.SetLayer(layerIndex);
            }
        }

        public void OnSelectionChange()
        {
            if (central != null)
            {
                if (scalerTool.IsActive())
                {
                    if (scalerTool.IsScaling())
                    {
                        if (scalerTool.TryAnchorRelease())
                        {
                            SaveUndo();
                        }

                        scalerTool.Detach();
                    }

                    if (central.selection.list.Count == 0)
                    {
                        scalerTool.Detach();
                    }
                    else
                    {
                        scalerTool.SetSelection(central.selection.list);
                        scalerTool.Show();
                    }
                }
            }
        }

        private KeyCode GetMouseButton(int i)
        {
            if (i <= 3)
            {
                return KeyCode.Mouse3;
            }
            else if (i == 4)
            {
                return KeyCode.Mouse4;
            }
            else if (i == 5)
            {
                return KeyCode.Mouse5;
            }
            else
            {
                return KeyCode.Mouse6;
            }
        }

        public void Update()
        {
            if (central != null)
            {
                if (scalerTool.IsActive())
                {
                    if (scalerTool.IsVisible())
                    {
                        if (!scalerTool.IsScaling() && (Input.GetKeyDown((KeyCode)grabAnchor.BoxedValue) || Input.GetKeyDown(GetMouseButton((int)mouseButton.BoxedValue))))
                        {
                            if (scalerTool.TryAnchorGrab())
                            {
                                undoBefore = central.undoRedo.ConvertBlockListToJSONList(central.selection.list);
                            }
                        }

                        if (Input.GetKeyUp((KeyCode)grabAnchor.BoxedValue) || Input.GetKeyUp(GetMouseButton((int)mouseButton.BoxedValue)))
                        {
                            if (scalerTool.TryAnchorRelease())
                            {
                                SaveUndo();
                            }
                        }

                        scalerTool.SetStep(central.gizmos.gridXZ, (float)zeroValue.BoxedValue);
                    }

                    if (Input.GetKeyDown((KeyCode)activateScaler.BoxedValue))
                    {
                        if (scalerTool.TryAnchorRelease())
                        {
                            SaveUndo();
                        }

                        scalerTool.Detach();
                        scalerTool.Deactivate();
                    }
                }
                else
                {
                    if (Input.GetKeyDown((KeyCode)activateScaler.BoxedValue))
                    {
                        scalerTool.Activate();
                        OnSelectionChange();
                    }
                }
            }

            if (scalerTool.IsActive())
            {
                if (central == null)
                {
                    scalerTool.Detach();
                    //scalerTool.Deactivate();
                }
            }
        }

        private void SaveUndo()
        {
            List<string> after = central.undoRedo.ConvertBlockListToJSONList(scalerTool.GetSelection());
            List<string> selectionList = central.undoRedo.ConvertSelectionToStringList(scalerTool.GetSelection());
            central.validation.BreakLock(central.undoRedo.ConvertBeforeAndAfterListToCollection(undoBefore, after, scalerTool.GetSelection(), selectionList, selectionList), "Gizmo1");
        }

        private string[] axisNames = new string[] { "X: ", "Y: ", "Z: " };
        private Color lightRed = new Color(1f, 0.4f, 0.4f);
        private Color lightGreen = new Color(0.4f, 1f, 0.4f);
        private Color lightBlue = new Color(0.4f, 0.4f, 1f);

        public void OnGUI()
        {
            if (scalerTool.IsScaling())
            {
                Vector2 size = new Vector2((int)tooltipWidth.BoxedValue, (int)tooltipHeight.BoxedValue);
                GUIStyle style = new GUIStyle(GUI.skin.box);
                style.fontSize = (int)tooltipFontSize.BoxedValue;
                style.normal.background = blackTex;
                style.alignment = TextAnchor.MiddleLeft;
                int scaledAxis = scalerTool.GetScaledAxis();

                if ((bool)showAllAxis.BoxedValue)
                {
                    style.normal.textColor = scaledAxis == 0 ? lightRed : Color.white;
                    GUI.Box(new Rect(new Vector2(Input.mousePosition.x + 30, Screen.height - Input.mousePosition.y - 30 - (2 * size.y)), size), "X: " + (scaledAxis == 0 ? scalerTool.GetDimensions().x : scalerTool.GetStringDimensions(0)), style);
                    style.normal.textColor = scaledAxis == 1 ? lightGreen : Color.white;
                    GUI.Box(new Rect(new Vector2(Input.mousePosition.x + 30, Screen.height - Input.mousePosition.y - 30 - size.y), size), "Y: " + (scaledAxis == 1 ? scalerTool.GetDimensions().y : scalerTool.GetStringDimensions(1)), style);
                    style.normal.textColor = scaledAxis == 2 ? lightBlue : Color.white;
                    GUI.Box(new Rect(new Vector2(Input.mousePosition.x + 30, Screen.height - Input.mousePosition.y - 30), size), "Z: " + (scaledAxis == 2 ? scalerTool.GetDimensions().z : scalerTool.GetStringDimensions(2)), style);
                }
                else
                {
                    style.normal.textColor = scaledAxis == 0 ? lightRed : (scaledAxis == 1 ? lightGreen : lightBlue);
                    GUI.Box(new Rect(new Vector2(Input.mousePosition.x + 30, Screen.height - Input.mousePosition.y - 30), size), (scaledAxis == -1 ? "?: " : axisNames[scaledAxis]) + scalerTool.GetDimensions()[scaledAxis], style);
                }
            }

            if(central != null)
            {
                if(central.gizmos.isDragging)
                {
                    if (central.gizmos.dragButton.isSelected)
                    {
                        if ((bool)translationGUI.BoxedValue)
                        {
                            Vector2 size = new Vector2((int)tooltipWidth.BoxedValue, (int)tooltipHeight.BoxedValue);
                            GUIStyle style = new GUIStyle(GUI.skin.box);
                            style.fontSize = (int)tooltipFontSize.BoxedValue;
                            style.normal.background = blackTex;
                            style.alignment = TextAnchor.MiddleLeft;

                            style.normal.textColor = lightRed;
                            GUI.Box(new Rect(new Vector2(Input.mousePosition.x + 30, Screen.height - Input.mousePosition.y - 30 - (2 * size.y)), size), "X: " + RoundToDecimalPlacesStringGizmo(central.gizmos.translationGizmos.transform.position.x, 5), style);
                            style.normal.textColor = lightGreen;
                            GUI.Box(new Rect(new Vector2(Input.mousePosition.x + 30, Screen.height - Input.mousePosition.y - 30 - size.y), size), "Y: " + RoundToDecimalPlacesStringGizmo(central.gizmos.translationGizmos.transform.position.y, 5), style);
                            style.normal.textColor = lightBlue;
                            GUI.Box(new Rect(new Vector2(Input.mousePosition.x + 30, Screen.height - Input.mousePosition.y - 30), size), "Z: " + RoundToDecimalPlacesStringGizmo(central.gizmos.translationGizmos.transform.position.z, 5), style);
                        }
                    }
                    else if(central.gizmos.rotateButton.isSelected)
                    {
                        if ((bool)rotationGUI.BoxedValue)
                        {
                            Vector2 size = new Vector2((int)tooltipWidth.BoxedValue, (int)tooltipHeight.BoxedValue);
                            GUIStyle style = new GUIStyle(GUI.skin.box);
                            style.fontSize = (int)tooltipFontSize.BoxedValue;
                            style.normal.background = blackTex;
                            style.alignment = TextAnchor.MiddleLeft;

                            style.normal.textColor = lightRed;
                            GUI.Box(new Rect(new Vector2(Input.mousePosition.x + 30, Screen.height - Input.mousePosition.y - 30 - (2 * size.y)), size), "X: " + RoundToDecimalPlacesStringGizmo(central.gizmos.rotationGizmos.transform.eulerAngles.x, 5), style);
                            style.normal.textColor = lightGreen;
                            GUI.Box(new Rect(new Vector2(Input.mousePosition.x + 30, Screen.height - Input.mousePosition.y - 30 - size.y), size), "Y: " + RoundToDecimalPlacesStringGizmo(central.gizmos.rotationGizmos.transform.eulerAngles.y, 5), style);
                            style.normal.textColor = lightBlue;
                            GUI.Box(new Rect(new Vector2(Input.mousePosition.x + 30, Screen.height - Input.mousePosition.y - 30), size), "Z: " + RoundToDecimalPlacesStringGizmo(central.gizmos.rotationGizmos.transform.eulerAngles.z, 5), style);
                        }
                    }
                }
            }
        }

        public static int CountDecimalPlaces(float value)
        {
            // Convert to string with a culture-invariant format to avoid localization issues
            string valueStr = value.ToString(System.Globalization.CultureInfo.InvariantCulture);

            // Check if the number is in scientific notation, which might complicate counting decimal places
            if (valueStr.Contains("E") || valueStr.Contains("e"))
            {
                // Convert to decimal to avoid scientific notation, then to a string
                valueStr = Convert.ToDecimal(value).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            // Check if there's a decimal point in the string
            if (valueStr.Contains("."))
            {
                // Return the length of the substring after the decimal point, ignoring trailing zeros
                return valueStr.Substring(valueStr.IndexOf('.') + 1).TrimEnd('0').Length;
            }

            // If no decimal point, decimal places are 0
            return 0;
        }

        public static float RoundToDecimalPlaces(float value, int decimalPlaces)
        {
            // Use MidpointRounding.AwayFromZero for consistency in rounding midpoint values
            double rounded = Math.Round(value, decimalPlaces, MidpointRounding.AwayFromZero);
            rounded *= Math.Pow(10, decimalPlaces); // Shift the decimal places to the right
            rounded = Math.Floor(rounded); // Remove the fractional part
            rounded /= Math.Pow(10, decimalPlaces); // Shift the decimal places back
            return (float)rounded;
        }

        public static string RoundToDecimalPlacesString(float value, int decimalPlaces)
        {
            // Use MidpointRounding.AwayFromZero for consistency in rounding midpoint values
            double rounded = Math.Round(value, decimalPlaces, MidpointRounding.AwayFromZero);

            // Convert the rounded value to a string with the desired format
            string formattedValue = rounded.ToString($"F{decimalPlaces}", CultureInfo.InvariantCulture);

            return formattedValue;
        }

        public static string RoundToDecimalPlacesStringGizmo(float value, int decimalPlaces)
        {
            // Use MidpointRounding.AwayFromZero for consistency in rounding midpoint values
            double rounded = Math.Round(value, decimalPlaces, MidpointRounding.AwayFromZero);

            // Check for floating-point errors using Mathf.Approximately
            if (Mathf.Approximately((float)rounded, value))
            {
                value = (float)rounded; // Assign the original value to avoid trailing zeros
            }

            // Convert the rounded value to a string with the desired format
            string formattedValue = value.ToString($"F{decimalPlaces}", CultureInfo.InvariantCulture);

            // Remove trailing zeros
            formattedValue = formattedValue.TrimEnd('0');

            // Remove decimal point if all digits after it are zeros
            if (formattedValue.EndsWith("."))
            {
                formattedValue = formattedValue.Substring(0, formattedValue.Length - 1);
            }

            return formattedValue;
        }
    }

    [HarmonyPatch(typeof(LEV_LevelEditorCentral), "Awake")]
    public class LEVCentralPostfix
    {
        public static void Postfix(LEV_LevelEditorCentral __instance)
        {
            Plugin.plg.central = __instance;
            int layerIndex = Mathf.RoundToInt(Mathf.Log(__instance.click.gizmoLayer, 2));
            Plugin.plg.SetAnchorLayer(layerIndex);
        }
    }

    [HarmonyPatch(typeof(LEV_Selection), "ClickBuilding")]
    public class ClickBuildingPostfix
    {
        public static void Prefix()
        {
            Plugin.plg.OnSelectionChange();
        }

        public static void Postfix()
        {
            Plugin.plg.OnSelectionChange();
        }
    }

    [HarmonyPatch(typeof(LEV_Selection), "ClickNothing")]
    public class ClickNothingPrefix
    {
        public static void Prefix()
        {
            Plugin.plg.OnSelectionChange();
        }

        public static void Postfix()
        {
            Plugin.plg.OnSelectionChange();
        }
    }

    [HarmonyPatch(typeof(LEV_Selection), "DeselectAllBlocks")]
    public class DeselectAllBlocksPrefix
    {
        public static void Prefix()
        {
            Plugin.plg.OnSelectionChange();
        }

        public static void Postfix()
        {
            Plugin.plg.OnSelectionChange();
        }
    }

    [HarmonyPatch(typeof(LEV_Selection), "RegisterManualSelectionBreakLock")]
    public class RegisterManualSelectionBreakLockPostfix
    {
        public static void Prefix()
        {
            Plugin.plg.OnSelectionChange();
        }

        public static void Postfix()
        {
            Plugin.plg.OnSelectionChange();
        }
    }

    [HarmonyPatch(typeof(LEV_UndoRedo), "Reselect")]
    public class UndoRedoReselectPostfix
    {
        public static void Postfix()
        {
            Plugin.plg.OnSelectionChange();
        }
    }

    [HarmonyPatch(typeof(LEV_GizmoHandler), "GoOutOfGMode")]
    public class GizmoGoOutOfGMode
    {
        public static void Postfix()
        {
            Plugin.plg.OnSelectionChange();
        }
    }
}