using System.Collections.Generic;
using ReCnc.Core.Data;
using ReCnc.Core.Map;
using ReCnc.Core.Simulation;
using ReCnc.Unity.CameraControl;
using ReCnc.Unity.View;
using ReCnc.Unity.World;
using UnityEngine;

namespace ReCnc.Unity.InputControl
{
    public class RtsInputAndHudController : MonoBehaviour
    {
        private const float SidebarWidth = 236f;

        public Ra2GameDirector Director;
        private readonly List<int> _selectedIds = new List<int>();
        private readonly Dictionary<int, List<int>> _controlGroups = new Dictionary<int, List<int>>();

        private string _activeCategory = "MainBuilding";
        private string _placingCategory;
        private ActorDefData _placingBuildingDef;
        private GameObject _placementGhost;
        private Renderer[] _placementGhostRenderers;
        private Material _ghostValidMat;
        private Material _ghostInvalidMat;

        private bool _isDraggingBox;
        private Vector3 _dragStartScreen;
        private float _lastClickTime;
        private int _lastClickedEntityId;

        private Texture2D _whiteTex;
        private Texture2D _panelBgTex;
        private Texture2D _buttonBgTex;
        private Texture2D _buttonHoverTex;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _tabBtnStyle;
        private GUIStyle _tabActiveStyle;

        private void Start()
        {
            if (Director == null) Director = FindObjectOfType<Ra2GameDirector>();
            _whiteTex = CreateSolidTex(Color.white);
            _panelBgTex = CreateSolidTex(new Color(0.08f, 0.11f, 0.15f, 0.94f));
            _buttonBgTex = CreateSolidTex(new Color(0.15f, 0.20f, 0.27f, 0.96f));
            _buttonHoverTex = CreateSolidTex(new Color(0.24f, 0.34f, 0.46f, 0.98f));

            _ghostValidMat = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.18f, 0.95f, 0.35f, 0.52f) };
            _ghostInvalidMat = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.95f, 0.20f, 0.20f, 0.52f) };
        }

        private void Update()
        {
            if (Director == null || Director.Simulation == null) return;

            PruneDeadSelections();
            HandleHotkeys();

            bool pointerInSidebar = Input.mousePosition.x >= Screen.width - SidebarWidth;
            UpdatePlacementGhost(pointerInSidebar);

            if (!string.IsNullOrEmpty(_placingCategory) && _placingBuildingDef != null)
            {
                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                {
                    ExitPlacementMode();
                    return;
                }
                if (!pointerInSidebar && Input.GetMouseButtonDown(0))
                {
                    CellCoord hitCell;
                    if (TryRaycastGroundCell(out hitCell))
                    {
                        var topLeft = new CellCoord(hitCell.X - _placingBuildingDef.FootprintX / 2, hitCell.Y - _placingBuildingDef.FootprintY / 2);
                        if (Director.Simulation.CommandPlaceReadyBuilding(0, _placingCategory, topLeft))
                        {
                            ExitPlacementMode();
                        }
                        else
                        {
                            Director.PlayClip2D("ceva055", 0.8f); // Cannot deploy here
                        }
                    }
                }
                return;
            }

            // Left Mouse Selection / Drag Box
            if (!pointerInSidebar && Input.GetMouseButtonDown(0))
            {
                _isDraggingBox = true;
                _dragStartScreen = Input.mousePosition;
            }

            if (_isDraggingBox && Input.GetMouseButtonUp(0))
            {
                _isDraggingBox = false;
                float dragDist = Vector2.Distance(_dragStartScreen, Input.mousePosition);
                if (dragDist < 10f)
                {
                    HandleSingleClickSelection();
                }
                else
                {
                    HandleBoxSelection(_dragStartScreen, Input.mousePosition);
                }
            }

            // Right Mouse Order (Move / Attack / Harvest)
            if (!pointerInSidebar && Input.GetMouseButtonDown(1) && _selectedIds.Count > 0)
            {
                HandleRightClickCommand();
            }
        }

        private void HandleHotkeys()
        {
            var sim = Director.Simulation;

            // V: Toggle building/infantry style between Hybrid RA2 Sprite + 3D vs Pure 3D Blockout
            if (Input.GetKeyDown(KeyCode.V))
            {
                Director.ToggleStructureVisualMode();
            }

            // G or Tab: Deploy selected MCV (note: D is camera right pan, so G = Deploy like OpenRA/RA2 remaster, plus double-click!)
            if (Input.GetKeyDown(KeyCode.G))
            {
                TryDeploySelectedMcv();
            }

            // H: Jump camera to player base / MCV
            if (Input.GetKeyDown(KeyCode.H))
            {
                var cam = FindObjectOfType<RtsCamera3DController>();
                if (cam != null)
                {
                    for (int i = 0; i < sim.Entities.Count; i++)
                    {
                        var e = sim.Entities[i];
                        if (e.IsAlive && e.FactionId == 0 && (e.Def.IsBuilding || e.Def.Role == "Mcv"))
                        {
                            cam.JumpTo(WorldCoords.SimToWorld(sim.Map, e.Position));
                            break;
                        }
                    }
                }
            }

            // X: Sell selected friendly building
            if (Input.GetKeyDown(KeyCode.X) && _selectedIds.Count == 1)
            {
                var e = sim.GetEntity(_selectedIds[0]);
                if (e != null && e.FactionId == 0 && e.Def.IsBuilding)
                {
                    sim.CommandSellBuilding(0, e.Id);
                    ClearSelection();
                }
            }

            // Control Groups 1..5
            for (int g = 1; g <= 5; g++)
            {
                KeyCode key = (KeyCode)((int)KeyCode.Alpha0 + g);
                if (Input.GetKeyDown(key))
                {
                    if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                    {
                        _controlGroups[g] = new List<int>(_selectedIds);
                    }
                    else if (_controlGroups.TryGetValue(g, out var saved) && saved.Count > 0)
                    {
                        SetSelection(saved);
                    }
                }
            }
        }

        private void TryDeploySelectedMcv()
        {
            for (int i = 0; i < _selectedIds.Count; i++)
            {
                var e = Director.Simulation.GetEntity(_selectedIds[i]);
                if (e != null && e.IsAlive && e.FactionId == 0 && !string.IsNullOrEmpty(e.Def.DeploysInto))
                {
                    if (Director.Simulation.CommandDeployMcv(e.Id))
                    {
                        ClearSelection();
                        return;
                    }
                }
            }
        }

        private void HandleSingleClickSelection()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 250f))
            {
                var tag = hit.collider.GetComponentInParent<EntitySelectableTag>();
                if (tag != null)
                {
                    var entity = Director.Simulation.GetEntity(tag.EntityId);
                    if (entity != null && entity.IsAlive && entity.FactionId == 0)
                    {
                        // Check double-click on MCV to deploy!
                        if (tag.EntityId == _lastClickedEntityId && (Time.unscaledTime - _lastClickTime) < 0.35f && !string.IsNullOrEmpty(entity.Def.DeploysInto))
                        {
                            Director.Simulation.CommandDeployMcv(entity.Id);
                            ClearSelection();
                            return;
                        }

                        _lastClickedEntityId = tag.EntityId;
                        _lastClickTime = Time.unscaledTime;
                        SetSelection(new List<int> { entity.Id });
                        Director.PlayUnitCommandFeedback(entity, false, true);
                        return;
                    }
                }
            }

            if (!Input.GetKey(KeyCode.LeftShift))
            {
                ClearSelection();
            }
        }

        private void HandleBoxSelection(Vector3 screenA, Vector3 screenB)
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            float minX = Mathf.Min(screenA.x, screenB.x);
            float maxX = Mathf.Max(screenA.x, screenB.x);
            float minY = Mathf.Min(screenA.y, screenB.y);
            float maxY = Mathf.Max(screenA.y, screenB.y);

            var pickedUnits = new List<int>();
            var entities = Director.Simulation.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                if (!e.IsAlive || e.FactionId != 0 || e.Def.IsBuilding) continue;

                Vector3 worldPos = WorldCoords.SimToWorld(Director.Simulation.Map, e.Position) + Vector3.up * 0.6f;
                Vector3 sp = cam.WorldToScreenPoint(worldPos);
                if (sp.z > 0f && sp.x >= minX && sp.x <= maxX && sp.y >= minY && sp.y <= maxY)
                {
                    pickedUnits.Add(e.Id);
                }
            }

            if (pickedUnits.Count > 0)
            {
                SetSelection(pickedUnits);
                var first = Director.Simulation.GetEntity(pickedUnits[0]);
                Director.PlayUnitCommandFeedback(first, false, true);
            }
        }

        private void HandleRightClickCommand()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 300f)) return;

            var sim = Director.Simulation;
            var tag = hit.collider.GetComponentInParent<EntitySelectableTag>();
            if (tag != null)
            {
                var targetEntity = sim.GetEntity(tag.EntityId);
                if (targetEntity != null && targetEntity.IsAlive && targetEntity.FactionId != 0)
                {
                    sim.CommandAttack(_selectedIds, targetEntity.Id);
                    SpawnMoveMarker(hit.point, new Color(1f, 0.25f, 0.25f));
                    var leader = sim.GetEntity(_selectedIds[0]);
                    Director.PlayUnitCommandFeedback(leader, true, false);
                    return;
                }
            }

            CellCoord cell = WorldCoords.WorldToCell(hit.point);
            var gridCell = sim.Map.GetCell(cell);

            // Check if Harvester clicked on Ore/Gem cell
            bool anyHarvester = false;
            for (int i = 0; i < _selectedIds.Count; i++)
            {
                var u = sim.GetEntity(_selectedIds[i]);
                if (u != null && u.Def.Role == "Harvester") { anyHarvester = true; break; }
            }

            if (anyHarvester && gridCell != null && gridCell.OreAmount > 0)
            {
                sim.CommandHarvest(_selectedIds, cell);
                SpawnMoveMarker(hit.point, new Color(1f, 0.85f, 0.2f));
            }
            else
            {
                bool attackMove = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                sim.CommandMove(_selectedIds, cell, attackMove);
                SpawnMoveMarker(hit.point, attackMove ? new Color(1f, 0.55f, 0.15f) : new Color(0.25f, 1f, 0.4f));
            }

            var firstSelected = sim.GetEntity(_selectedIds[0]);
            Director.PlayUnitCommandFeedback(firstSelected, false, false);
        }

        private void SpawnMoveMarker(Vector3 worldPos, Color col)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(go.GetComponent<Collider>());
            go.transform.position = worldPos + Vector3.up * 0.08f;
            go.transform.localScale = new Vector3(1.1f, 0.03f, 1.1f);
            var mat = new Material(Shader.Find("Sprites/Default")) { color = col };
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var fx = go.AddComponent<TransientBurstVfx>();
            fx.MaxScale = 1.4f;
            fx.BaseColor = col;
            fx.MatInstance = mat;
        }

        private bool TryRaycastGroundCell(out CellCoord cell)
        {
            cell = new CellCoord(0, 0);
            Camera cam = Camera.main;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 300f))
            {
                cell = WorldCoords.WorldToCell(hit.point);
                return Director.Simulation.Map.InBounds(cell);
            }
            return false;
        }

        private void EnterPlacementMode(string category, ActorDefData def)
        {
            ExitPlacementMode();
            _placingCategory = category;
            _placingBuildingDef = def;

            _placementGhost = new GameObject("BuildingPlacementGhost");
            GameObject remPrefab = Director.AssetRegistry != null ? Director.AssetRegistry.GetRemasteredModel(def.Id) : null;
            if (remPrefab != null)
            {
                var inst = Instantiate(remPrefab, _placementGhost.transform, false);
                var cols = inst.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < cols.Length; i++) Destroy(cols[i]);
            }
            else
            {
                float wx = def.FootprintX * WorldCoords.CellSize * 0.94f;
                float wz = def.FootprintY * WorldCoords.CellSize * 0.94f;
                float wy = Mathf.Max(1.2f, def.Height);

                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(box.GetComponent<Collider>());
                box.transform.SetParent(_placementGhost.transform, false);
                box.transform.localPosition = new Vector3(0f, wy * 0.5f, 0f);
                box.transform.localScale = new Vector3(wx, wy, wz);
            }
            _placementGhostRenderers = _placementGhost.GetComponentsInChildren<Renderer>(true);
        }

        private void ExitPlacementMode()
        {
            _placingCategory = null;
            _placingBuildingDef = null;
            if (_placementGhost != null)
            {
                Destroy(_placementGhost);
                _placementGhost = null;
            }
        }

        private void UpdatePlacementGhost(bool pointerInSidebar)
        {
            if (_placementGhost == null || _placingBuildingDef == null) return;
            if (pointerInSidebar)
            {
                _placementGhost.SetActive(false);
                return;
            }

            CellCoord hitCell;
            if (!TryRaycastGroundCell(out hitCell))
            {
                _placementGhost.SetActive(false);
                return;
            }

            _placementGhost.SetActive(true);
            var topLeft = new CellCoord(hitCell.X - _placingBuildingDef.FootprintX / 2, hitCell.Y - _placingBuildingDef.FootprintY / 2);
            SimVec2 centerSim = new SimVec2(topLeft.X + _placingBuildingDef.FootprintX * 0.5f, topLeft.Y + _placingBuildingDef.FootprintY * 0.5f);
            _placementGhost.transform.position = WorldCoords.SimToWorld(Director.Simulation.Map, centerSim);

            bool valid = Director.Simulation.CanPlaceBuilding(0, _placingBuildingDef.Id, topLeft, true);
            Material mat = valid ? _ghostValidMat : _ghostInvalidMat;
            for (int i = 0; i < _placementGhostRenderers.Length; i++)
            {
                _placementGhostRenderers[i].sharedMaterial = mat;
            }
        }

        private void PruneDeadSelections()
        {
            for (int i = _selectedIds.Count - 1; i >= 0; i--)
            {
                var e = Director.Simulation.GetEntity(_selectedIds[i]);
                if (e == null || !e.IsAlive)
                {
                    _selectedIds.RemoveAt(i);
                }
            }
        }

        private void ClearSelection()
        {
            for (int i = 0; i < _selectedIds.Count; i++)
            {
                var v = Director.GetEntityView(_selectedIds[i]);
                if (v != null) v.SetSelected(false);
            }
            _selectedIds.Clear();
        }

        private void SetSelection(List<int> ids)
        {
            ClearSelection();
            for (int i = 0; i < ids.Count; i++)
            {
                var v = Director.GetEntityView(ids[i]);
                if (v != null)
                {
                    v.SetSelected(true);
                    _selectedIds.Add(ids[i]);
                }
            }
        }

        private void EnsureStyles()
        {
            if (_headerStyle != null) return;
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.85f, 0.35f) }
            };
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = Color.white }
            };
            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = new Color(0.78f, 0.84f, 0.90f) }
            };
            _tabBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                normal = { background = _buttonBgTex, textColor = new Color(0.82f, 0.88f, 0.95f) },
                hover = { background = _buttonHoverTex, textColor = Color.white }
            };
            _tabActiveStyle = new GUIStyle(_tabBtnStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { background = _buttonHoverTex, textColor = new Color(1f, 0.9f, 0.35f) }
            };
        }

        private void OnGUI()
        {
            if (Director == null || Director.Simulation == null) return;
            EnsureStyles();

            DrawWorldHealthBars();
            DrawSelectionBoxGui();
            DrawTopLeftHelpOverlay();
            DrawRightSidebar();
            DrawVictoryOverlay();
        }

        private void DrawWorldHealthBars()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            var selectedSet = new HashSet<int>(_selectedIds);
            var entities = Director.Simulation.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                if (!e.IsAlive) continue;
                bool isSelected = selectedSet.Contains(e.Id);
                if (!isSelected && e.Hp >= e.Def.MaxHp) continue;

                Vector3 worldTop = WorldCoords.SimToWorld(Director.Simulation.Map, e.Position) + Vector3.up * (e.Def.Height + 0.55f);
                Vector3 sp = cam.WorldToScreenPoint(worldTop);
                if (sp.z <= 0f || sp.x > Screen.width - SidebarWidth) continue;

                float barW = e.Def.IsBuilding ? 64f : 40f;
                float barH = 5f;
                float guiX = sp.x - barW * 0.5f;
                float guiY = Screen.height - sp.y - barH;

                GUI.color = new Color(0f, 0f, 0f, 0.78f);
                GUI.DrawTexture(new Rect(guiX - 1f, guiY - 1f, barW + 2f, barH + 2f), _whiteTex);

                float ratio = Mathf.Clamp01((float)e.Hp / Mathf.Max(1, e.Def.MaxHp));
                GUI.color = ratio > 0.5f ? new Color(0.2f, 0.95f, 0.3f) : (ratio > 0.25f ? new Color(0.95f, 0.8f, 0.15f) : new Color(0.95f, 0.2f, 0.15f));
                GUI.DrawTexture(new Rect(guiX, guiY, barW * ratio, barH), _whiteTex);
            }
            GUI.color = Color.white;
        }

        private void DrawSelectionBoxGui()
        {
            if (!_isDraggingBox) return;
            Vector3 cur = Input.mousePosition;
            if (Vector2.Distance(_dragStartScreen, cur) < 8f) return;

            float xMin = Mathf.Min(_dragStartScreen.x, cur.x);
            float xMax = Mathf.Max(_dragStartScreen.x, cur.x);
            float yMin = Mathf.Min(Screen.height - _dragStartScreen.y, Screen.height - cur.y);
            float yMax = Mathf.Max(Screen.height - _dragStartScreen.y, Screen.height - cur.y);
            Rect rect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);

            GUI.color = new Color(0.2f, 0.95f, 0.4f, 0.16f);
            GUI.DrawTexture(rect, _whiteTex);
            GUI.color = new Color(0.3f, 1f, 0.45f, 0.9f);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, 1.5f), _whiteTex);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - 1.5f, rect.width, 1.5f), _whiteTex);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, 1.5f, rect.height), _whiteTex);
            GUI.DrawTexture(new Rect(rect.xMax - 1.5f, rect.yMin, 1.5f, rect.height), _whiteTex);
            GUI.color = Color.white;
        }

        private void DrawTopLeftHelpOverlay()
        {
            Rect box = new Rect(12f, 12f, 360f, 108f);
            GUI.DrawTexture(box, _panelBgTex);
            GUILayout.BeginArea(new Rect(box.x + 10f, box.y + 8f, box.width - 20f, box.height - 14f));
            GUILayout.Label("红色警戒2 Unity 全3D重制原型", _headerStyle);
            GUILayout.Label("镜头: WASD平移 | 滚轮缩放 | Q/E或Alt+中键旋转 | H回基地", _smallStyle);
            GUILayout.Label("操作: 左键点选/框选 | 双击基地车(或按G)展开 | 右键移动/攻击/采矿", _smallStyle);
            string modeLabel = Director.Pure3DStructuresMode ? "原版复古模式(VXL+立绘)" : "Blender 3D翻新精模(PBR)";
            if (GUILayout.Button("[V] 切换画质模式 (当前: " + modeLabel + ")", _tabBtnStyle, GUILayout.Height(22f)))
            {
                Director.ToggleStructureVisualMode();
            }
            GUILayout.EndArea();
        }

        private void DrawRightSidebar()
        {
            var sim = Director.Simulation;
            var player = sim.GetFaction(0);
            if (player == null) return;

            Rect panel = new Rect(Screen.width - SidebarWidth, 0f, SidebarWidth, Screen.height);
            GUI.DrawTexture(panel, _panelBgTex);

            GUILayout.BeginArea(new Rect(panel.x + 10f, 12f, SidebarWidth - 20f, Screen.height - 24f));

            // 1. Credits & Power
            GUILayout.Label("资金: $" + player.Credits, _headerStyle);
            Color pwrCol = player.IsLowPower ? new Color(1f, 0.32f, 0.28f) : new Color(0.4f, 0.95f, 0.5f);
            var pwrStyle = new GUIStyle(_labelStyle) { normal = { textColor = pwrCol } };
            string pwrStatus = player.IsLowPower ? " (电力不足!)" : "";
            GUILayout.Label("电力: " + player.PowerProduced + " / " + player.PowerConsumed + pwrStatus, pwrStyle);
            GUILayout.Space(8f);

            // 2. Category Tabs
            GUILayout.BeginHorizontal();
            DrawCategoryTab("主建筑", "MainBuilding");
            DrawCategoryTab("防御", "DefenseBuilding");
            DrawCategoryTab("步兵", "Infantry");
            DrawCategoryTab("战车", "Vehicle");
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);

            // 3. Active Queue Status
            var activeQ = player.Queues[_activeCategory];
            if (!string.IsNullOrEmpty(activeQ.ActiveActorId))
            {
                var qDef = sim.Rules.GetActor(activeQ.ActiveActorId);
                string qName = qDef != null ? qDef.Name : activeQ.ActiveActorId;
                if (activeQ.IsReadyToPlace)
                {
                    GUILayout.Label("已就绪: " + qName, _headerStyle);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("放置建筑", _tabActiveStyle, GUILayout.Height(26f)))
                    {
                        EnterPlacementMode(_activeCategory, qDef);
                    }
                    if (GUILayout.Button("取消", _tabBtnStyle, GUILayout.Width(54f), GUILayout.Height(26f)))
                    {
                        ExitPlacementMode();
                        sim.CommandCancelProduction(0, _activeCategory);
                    }
                    GUILayout.EndHorizontal();
                }
                else
                {
                    int pct = Mathf.RoundToInt(activeQ.ProgressNormalized * 100f);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("建造中: " + qName + " (" + pct + "%)", _labelStyle);
                    if (GUILayout.Button("取消", _tabBtnStyle, GUILayout.Width(48f)))
                    {
                        sim.CommandCancelProduction(0, _activeCategory);
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.Space(6f);
            }

            // 4. Available Build Buttons with real RA2 Cameos
            var options = sim.GetAvailableBuildOptions(0, _activeCategory);
            if (options.Count == 0)
            {
                GUILayout.Label("提示: 请先选中盟军基地车，双击或按 [G] 键展开为盟军建造厂以解锁建造科技树！", _smallStyle);
            }
            else
            {
                for (int i = 0; i < options.Count; i++)
                {
                    DrawBuildCard(options[i], activeQ);
                }
            }

            GUILayout.FlexibleSpace();

            // 5. Selected Unit/Building Context Panel
            if (_selectedIds.Count > 0)
            {
                var first = sim.GetEntity(_selectedIds[0]);
                if (first != null)
                {
                    GUILayout.Label("当前选中 (" + _selectedIds.Count + "):", _headerStyle);
                    GUILayout.Label(first.Def.Name + "  HP: " + first.Hp + "/" + first.Def.MaxHp, _labelStyle);
                    if (!string.IsNullOrEmpty(first.Def.DeploysInto))
                    {
                        if (GUILayout.Button("[G] 展开基地车建立基地", _tabActiveStyle, GUILayout.Height(30f)))
                        {
                            TryDeploySelectedMcv();
                        }
                    }
                    else if (first.Def.IsBuilding && first.FactionId == 0)
                    {
                        if (GUILayout.Button("[X] 变卖建筑 (+$" + (first.Def.Cost / 2) + ")", _tabBtnStyle, GUILayout.Height(26f)))
                        {
                            sim.CommandSellBuilding(0, first.Id);
                            ClearSelection();
                        }
                    }
                }
            }

            GUILayout.EndArea();
        }

        private void DrawCategoryTab(string label, string categoryKey)
        {
            GUIStyle style = _activeCategory == categoryKey ? _tabActiveStyle : _tabBtnStyle;
            if (GUILayout.Button(label, style, GUILayout.Height(26f)))
            {
                _activeCategory = categoryKey;
                Director.PlayClip2D("utab", 0.6f);
            }
        }

        private void DrawBuildCard(ActorDefData def, ProductionQueueState queue)
        {
            Rect row = GUILayoutUtility.GetRect(SidebarWidth - 20f, 54f);
            bool hovered = row.Contains(Event.current.mousePosition);
            GUI.DrawTexture(row, hovered ? _buttonHoverTex : _buttonBgTex);

            // Draw real converted RA2 Cameo icon (60x48)!
            Sprite cameo = Director.AssetRegistry != null ? Director.AssetRegistry.GetCameo(def.Id) : null;
            Rect iconRect = new Rect(row.x + 4f, row.y + 3f, 60f, 48f);
            if (cameo != null && cameo.texture != null)
            {
                GUI.DrawTexture(iconRect, cameo.texture, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.color = new Color(0.25f, 0.45f, 0.7f);
                GUI.DrawTexture(iconRect, _whiteTex);
                GUI.color = Color.white;
            }

            // Progress bar overlay on active item
            bool isThisActive = queue != null && queue.ActiveActorId == def.Id;
            if (isThisActive)
            {
                GUI.color = new Color(0.2f, 0.95f, 0.4f, 0.35f);
                GUI.DrawTexture(new Rect(row.x, row.y, row.width * queue.ProgressNormalized, row.height), _whiteTex);
                GUI.color = Color.white;
            }

            GUI.Label(new Rect(row.x + 70f, row.y + 6f, row.width - 74f, 20f), def.Name, _labelStyle);
            string sub = "$" + def.Cost + (def.Power != 0 ? (" | 电力 " + (def.Power > 0 ? "+" : "") + def.Power) : "");
            if (isThisActive && queue.IsReadyToPlace) sub = "就绪 - 点击放置!";
            GUI.Label(new Rect(row.x + 70f, row.y + 27f, row.width - 74f, 20f), sub, _smallStyle);

            if (Event.current.type == EventType.MouseDown && hovered)
            {
                if (Event.current.button == 0)
                {
                    if (isThisActive && queue.IsReadyToPlace)
                    {
                        EnterPlacementMode(_activeCategory, def);
                    }
                    else if (string.IsNullOrEmpty(queue.ActiveActorId))
                    {
                        if (Director.Simulation.CommandStartProduction(0, def.Id))
                        {
                            Director.PlayClip2D("ceva051", 0.8f); // Building
                        }
                    }
                }
                else if (Event.current.button == 1 && isThisActive)
                {
                    ExitPlacementMode();
                    Director.Simulation.CommandCancelProduction(0, _activeCategory);
                }
                Event.current.Use();
            }

            GUILayout.Space(4f);
        }

        private void DrawVictoryOverlay()
        {
            int winner = Director.Simulation.WinnerFactionId;
            if (winner < 0) return;

            Rect modal = new Rect(Screen.width * 0.5f - 180f, Screen.height * 0.5f - 70f, 360f, 140f);
            GUI.DrawTexture(modal, _panelBgTex);
            GUILayout.BeginArea(new Rect(modal.x + 20f, modal.y + 20f, modal.width - 40f, modal.height - 40f));
            GUILayout.Label(winner == 0 ? "战役胜利！敌军已被全歼！" : "基地失守，作战失败！", _headerStyle);
            GUILayout.Space(16f);
            if (GUILayout.Button("重新开始遭遇战", _tabActiveStyle, GUILayout.Height(36f)))
            {
                ClearSelection();
                Director.InitializeMatch();
            }
            GUILayout.EndArea();
        }

        private static Texture2D CreateSolidTex(Color col)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, col);
            t.Apply();
            return t;
        }
    }
}
