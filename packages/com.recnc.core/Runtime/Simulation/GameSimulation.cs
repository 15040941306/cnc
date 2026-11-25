using System;
using System.Collections.Generic;
using ReCnc.Core.Data;
using ReCnc.Core.Map;

namespace ReCnc.Core.Simulation
{
    public class GameSimulation
    {
        public readonly RulesCatalog Rules;
        public readonly GridMap Map;
        private readonly List<FactionState> _factions = new List<FactionState>();
        private readonly List<EntityState> _entities = new List<EntityState>();
        private readonly Dictionary<int, EntityState> _entityById = new Dictionary<int, EntityState>();
        private readonly List<ProjectileState> _projectiles = new List<ProjectileState>();
        private readonly List<SimEvent> _events = new List<SimEvent>();

        private int _nextEntityId = 1;
        private int _nextProjectileId = 1;
        private float _simTime;
        private float _aiDecisionTimer = 2.0f;
        private float _aiAttackWaveTimer = 65.0f;
        private float _oreRegrowTimer = 15.0f;
        private uint _rngState = 0x9E3779B9u;
        public int WinnerFactionId = -1;

        private int NextSimInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            uint x = _rngState;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _rngState = x;
            return minInclusive + (int)(x % (uint)(maxExclusive - minInclusive));
        }

        public IReadOnlyList<FactionState> Factions { get { return _factions; } }
        public IReadOnlyList<EntityState> Entities { get { return _entities; } }
        public IReadOnlyList<ProjectileState> Projectiles { get { return _projectiles; } }
        public float SimTime { get { return _simTime; } }

        public GameSimulation(RulesCatalog rules, MapBundleData mapBundle)
        {
            Rules = rules;
            Map = new GridMap(mapBundle);
        }

        public static GameSimulation CreateStandardSkirmish(RulesCatalog rules, MapBundleData mapData, string playerSide = "Allied", int startingCredits = 10000)
        {
            var sim = new GameSimulation(rules, mapData);
            string aiSide = string.Equals(playerSide, "Soviet", StringComparison.OrdinalIgnoreCase) ? "Allied" : "Soviet";

            int clampedCredits = Math.Clamp(startingCredits, 2500, 50000);
            var player = new FactionState(0, "指挥官 (玩家)", playerSide, false) { Credits = clampedCredits };
            var ai = new FactionState(1, "电脑遭遇战 AI", aiSide, true) { Credits = clampedCredits };
            sim._factions.Add(player);
            sim._factions.Add(ai);

            CellCoord p0Start = new CellCoord(10, 10);
            CellCoord p1Start = new CellCoord(sim.Map.Width - 15, sim.Map.Height - 15);
            if (mapData.Waypoints != null && mapData.Waypoints.Count >= 2)
            {
                p0Start = new CellCoord(
                    Math.Clamp(mapData.Waypoints[0].X, 5, sim.Map.Width - 10),
                    Math.Clamp(mapData.Waypoints[0].Y, 5, sim.Map.Height - 10));
                p1Start = new CellCoord(
                    Math.Clamp(mapData.Waypoints[1].X, 5, sim.Map.Width - 10),
                    Math.Clamp(mapData.Waypoints[1].Y, 5, sim.Map.Height - 10));
            }

            // Clear spawn clearings so MCV + initial tanks always fit cleanly
            sim.ClearSpawnArea(p0Start, 8);
            sim.ClearSpawnArea(p1Start, 8);

            string pMcv = playerSide == "Allied" ? "AMCV" : "SMCV";
            string pTank = playerSide == "Allied" ? "MTNK" : "HTNK";
            string pInf = playerSide == "Allied" ? "E1" : "E2";

            sim.SpawnUnit(0, pMcv, p0Start);
            sim.SpawnUnit(0, pTank, new CellCoord(p0Start.X + 3, p0Start.Y));
            sim.SpawnUnit(0, pTank, new CellCoord(p0Start.X, p0Start.Y + 3));
            sim.SpawnUnit(0, pInf, new CellCoord(p0Start.X + 2, p0Start.Y + 2));
            sim.SpawnUnit(0, pInf, new CellCoord(p0Start.X + 3, p0Start.Y + 2));
            sim.SpawnUnit(0, pInf, new CellCoord(p0Start.X + 2, p0Start.Y + 3));

            // Spawn AI base yard + escort
            string aiYard = aiSide == "Soviet" ? "NACNST" : "GACNST";
            string aiTank = aiSide == "Soviet" ? "HTNK" : "MTNK";
            string aiInf = aiSide == "Soviet" ? "E2" : "E1";
            sim.TryForcePlaceBuilding(1, aiYard, p1Start);
            sim.SpawnUnit(1, aiTank, new CellCoord(p1Start.X - 2, p1Start.Y));
            sim.SpawnUnit(1, aiTank, new CellCoord(p1Start.X, p1Start.Y - 2));
            sim.SpawnUnit(1, aiInf, new CellCoord(p1Start.X - 2, p1Start.Y - 2));
            sim.SpawnUnit(1, aiInf, new CellCoord(p1Start.X - 3, p1Start.Y - 2));

            sim.RecalculatePower();
            return sim;
        }

        private void ClearSpawnArea(CellCoord center, int radius)
        {
            if (Map.IsNativeIsometricGrid)
            {
                // Official Westwood RA2 maps already have flat, balanced spawn clearings!
                // Only clear tree/wall props in the immediate 4-cell radius so MCV deployment never collides with a tree.
                for (int dy = -4; dy <= 4; dy++)
                {
                    for (int dx = -4; dx <= 4; dx++)
                    {
                        var cell = Map.GetCell(center.X + dx, center.Y + dy);
                        if (cell != null)
                        {
                            cell.HasProp = false;
                            cell.HasBlockingProp = false;
                        }
                    }
                }
                return;
            }

            int baseHeight = 2;
            var centerCell = Map.GetCell(center);
            if (centerCell != null && centerCell.Terrain != TerrainKind.Water)
            {
                baseHeight = centerCell.HeightLevel;
            }

            // Generous buildable base pad (radius 8) + smooth 4-cell sloped apron (radius 9..12) so spawns on hills never create cliff walls!
            int innerPad = Math.Max(7, radius);
            int outerApron = innerPad + 4;
            for (int dy = -outerApron; dy <= outerApron; dy++)
            {
                for (int dx = -outerApron; dx <= outerApron; dx++)
                {
                    var cell = Map.GetCell(center.X + dx, center.Y + dy);
                    if (cell == null) continue;

                    int cheb = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    if (cheb <= innerPad)
                    {
                        cell.Terrain = TerrainKind.Clear;
                        cell.HeightLevel = baseHeight;
                        cell.RampType = 0;
                        if (cheb <= 5)
                        {
                            cell.OreAmount = 0;
                            cell.OreType = 0;
                        }
                    }
                    else if (cell.HeightLevel != baseHeight)
                    {
                        if (cell.Terrain == TerrainKind.Rock || cell.Terrain == TerrainKind.Water)
                        {
                            cell.Terrain = TerrainKind.Clear;
                        }
                        float t = (cheb - innerPad) / (float)(outerApron - innerPad + 1);
                        cell.HeightLevel = (int)Math.Round(baseHeight * (1f - t) + cell.HeightLevel * t);
                        cell.RampType = 1;
                    }
                }
            }
        }

        public FactionState GetFaction(int factionId)
        {
            for (int i = 0; i < _factions.Count; i++)
            {
                if (_factions[i].FactionId == factionId) return _factions[i];
            }
            return null;
        }

        public EntityState GetEntity(int id)
        {
            EntityState e;
            return _entityById.TryGetValue(id, out e) ? e : null;
        }

        public void DrainEvents(List<SimEvent> output)
        {
            output.Clear();
            output.AddRange(_events);
            _events.Clear();
        }

        public List<ActorDefData> GetAvailableBuildOptions(int factionId, string category)
        {
            var result = new List<ActorDefData>();
            var faction = GetFaction(factionId);
            if (faction == null) return result;

            var ownedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _entities.Count; i++)
            {
                var e = _entities[i];
                if (e.IsAlive && e.FactionId == factionId && e.Def.IsBuilding)
                {
                    ownedIds.Add(e.Def.Id);
                }
            }

            var all = Rules.AllActors;
            for (int i = 0; i < all.Count; i++)
            {
                var def = all[i];
                if (!string.Equals(def.Side, faction.Side, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.Equals(def.Category, category, StringComparison.OrdinalIgnoreCase)) continue;
                if (def.Id == "GACNST" || def.Id == "NACNST") continue; // Built via MCV deploy

                bool prereqsMet = true;
                if (def.Prerequisites != null)
                {
                    for (int p = 0; p < def.Prerequisites.Count; p++)
                    {
                        if (!ownedIds.Contains(def.Prerequisites[p]))
                        {
                            prereqsMet = false;
                            break;
                        }
                    }
                }

                if (prereqsMet)
                {
                    result.Add(def);
                }
            }
            return result;
        }

        public EntityState SpawnUnit(int factionId, string actorId, CellCoord cell)
        {
            var def = Rules.GetActor(actorId);
            if (def == null) return null;
            CellCoord spawnCell = Map.FindNearestPassable(cell, 8);
            var entity = new EntityState
            {
                Id = _nextEntityId++,
                FactionId = factionId,
                Def = def,
                Hp = def.MaxHp,
                Position = SimVec2.FromCellCenter(spawnCell),
                BodyYawDeg = factionId == 0 ? 45f : 225f,
                TurretYawDeg = factionId == 0 ? 45f : 225f
            };
            _entities.Add(entity);
            _entityById[entity.Id] = entity;
            return entity;
        }

        public bool CanPlaceBuilding(int factionId, string actorId, CellCoord topLeft, bool checkAdjacency = true)
        {
            var def = Rules.GetActor(actorId);
            if (def == null || !def.IsBuilding) return false;
            if (!Map.CanPlaceBuildingFootprint(topLeft, def.FootprintX, def.FootprintY)) return false;
            if (!checkAdjacency) return true;

            SimVec2 candidateCenter = new SimVec2(topLeft.X + def.FootprintX * 0.5f, topLeft.Y + def.FootprintY * 0.5f);
            for (int i = 0; i < _entities.Count; i++)
            {
                var e = _entities[i];
                if (e.IsAlive && e.FactionId == factionId && e.Def.IsBuilding)
                {
                    if (e.Position.DistanceTo(candidateCenter) <= 10.5f)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public EntityState TryForcePlaceBuilding(int factionId, string actorId, CellCoord topLeft)
        {
            var def = Rules.GetActor(actorId);
            if (def == null || !def.IsBuilding) return null;
            if (!Map.CanPlaceBuildingFootprint(topLeft, def.FootprintX, def.FootprintY)) return null;

            var entity = new EntityState
            {
                Id = _nextEntityId++,
                FactionId = factionId,
                Def = def,
                Hp = def.MaxHp,
                TopLeftCell = topLeft,
                Position = new SimVec2(topLeft.X + def.FootprintX * 0.5f, topLeft.Y + def.FootprintY * 0.5f),
                RallyPoint = new CellCoord(topLeft.X + def.FootprintX / 2, Math.Max(0, topLeft.Y - 2))
            };
            Map.SetBuildingFootprint(topLeft, def.FootprintX, def.FootprintY, entity.Id);
            _entities.Add(entity);
            _entityById[entity.Id] = entity;
            RecalculatePower();

            if (!string.IsNullOrEmpty(def.FreeUnit))
            {
                CellCoord dock = GetBuildingExitCell(entity);
                SpawnUnit(factionId, def.FreeUnit, dock);
            }

            _events.Add(new SimEvent { Kind = SimEventKind.BuildingPlaced, FactionId = factionId, SourceEntityId = entity.Id, Position = entity.Position, Tag = def.Id });
            return entity;
        }

        public bool CommandDeployMcv(int entityId)
        {
            var mcv = GetEntity(entityId);
            if (mcv == null || !mcv.IsAlive || string.IsNullOrEmpty(mcv.Def.DeploysInto)) return false;
            var yardDef = Rules.GetActor(mcv.Def.DeploysInto);
            if (yardDef == null) return false;

            CellCoord center = mcv.Position.ToCell();
            CellCoord topLeft = new CellCoord(center.X - yardDef.FootprintX / 2, center.Y - yardDef.FootprintY / 2);

            // Try small search around MCV so deployment feels forgiving
            for (int r = 0; r <= 2; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        var candidate = new CellCoord(topLeft.X + dx, topLeft.Y + dy);
                        if (Map.CanPlaceBuildingFootprint(candidate, yardDef.FootprintX, yardDef.FootprintY))
                        {
                            int factionId = mcv.FactionId;
                            RemoveEntity(mcv);
                            TryForcePlaceBuilding(factionId, yardDef.Id, candidate);
                            _events.Add(new SimEvent { Kind = SimEventKind.EvaAnnouncement, FactionId = factionId, Position = SimVec2.FromCellCenter(candidate), Tag = "ceva048" });
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public bool CommandStartProduction(int factionId, string actorId)
        {
            var faction = GetFaction(factionId);
            var def = Rules.GetActor(actorId);
            if (faction == null || def == null) return false;

            ProductionQueueState queue;
            if (!faction.Queues.TryGetValue(def.Category, out queue)) return false;
            if (!string.IsNullOrEmpty(queue.ActiveActorId)) return false;

            var available = GetAvailableBuildOptions(factionId, def.Category);
            bool unlocked = false;
            for (int i = 0; i < available.Count; i++)
            {
                if (string.Equals(available[i].Id, actorId, StringComparison.OrdinalIgnoreCase))
                {
                    unlocked = true;
                    break;
                }
            }
            if (!unlocked) return false;

            queue.ActiveActorId = def.Id;
            queue.TotalCost = Math.Max(50, def.Cost);
            queue.PaidCost = 0;
            queue.IsReadyToPlace = false;
            return true;
        }

        public void CommandCancelProduction(int factionId, string category)
        {
            var faction = GetFaction(factionId);
            if (faction == null) return;
            ProductionQueueState queue;
            if (faction.Queues.TryGetValue(category, out queue) && !string.IsNullOrEmpty(queue.ActiveActorId))
            {
                faction.Credits += queue.PaidCost;
                queue.ActiveActorId = null;
                queue.TotalCost = 0;
                queue.PaidCost = 0;
                queue.IsReadyToPlace = false;
            }
        }

        public bool CommandPlaceReadyBuilding(int factionId, string category, CellCoord topLeft)
        {
            var faction = GetFaction(factionId);
            if (faction == null) return false;
            ProductionQueueState queue;
            if (!faction.Queues.TryGetValue(category, out queue) || !queue.IsReadyToPlace) return false;

            if (!CanPlaceBuilding(factionId, queue.ActiveActorId, topLeft, true)) return false;

            string actorId = queue.ActiveActorId;
            queue.ActiveActorId = null;
            queue.TotalCost = 0;
            queue.PaidCost = 0;
            queue.IsReadyToPlace = false;

            return TryForcePlaceBuilding(factionId, actorId, topLeft) != null;
        }

        public bool CommandSellBuilding(int factionId, int buildingId)
        {
            var b = GetEntity(buildingId);
            var faction = GetFaction(factionId);
            if (b == null || faction == null || !b.IsAlive || b.FactionId != factionId || !b.Def.IsBuilding) return false;

            faction.Credits += b.Def.Cost / 2;
            RemoveEntity(b);
            RecalculatePower();
            return true;
        }

        public void CommandMove(IReadOnlyList<int> entityIds, CellCoord destination, bool attackMove)
        {
            if (entityIds == null || entityIds.Count == 0) return;
            int cols = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(entityIds.Count)));

            for (int i = 0; i < entityIds.Count; i++)
            {
                var e = GetEntity(entityIds[i]);
                if (e == null || !e.IsAlive || e.Def.IsBuilding) continue;

                int ox = (i % cols) - cols / 2;
                int oy = (i / cols) - cols / 2;
                CellCoord subGoal = Map.FindNearestPassable(new CellCoord(destination.X + ox, destination.Y + oy), 6);

                e.CurrentOrder = attackMove ? OrderKind.AttackMove : OrderKind.Move;
                e.TargetEntityId = 0;
                e.TargetCell = subGoal;
                RebuildPath(e, subGoal);
            }
        }

        public void CommandAttack(IReadOnlyList<int> entityIds, int targetEntityId)
        {
            var target = GetEntity(targetEntityId);
            if (target == null || !target.IsAlive || entityIds == null) return;

            for (int i = 0; i < entityIds.Count; i++)
            {
                var e = GetEntity(entityIds[i]);
                if (e == null || !e.IsAlive || e.FactionId == target.FactionId) continue;
                if (string.IsNullOrEmpty(e.Def.PrimaryWeapon)) continue;

                e.CurrentOrder = OrderKind.AttackTarget;
                e.TargetEntityId = targetEntityId;
                if (!e.Def.IsBuilding)
                {
                    RebuildPath(e, target.Position.ToCell());
                }
            }
        }

        public void CommandHarvest(IReadOnlyList<int> entityIds, CellCoord oreCell)
        {
            if (entityIds == null) return;
            for (int i = 0; i < entityIds.Count; i++)
            {
                var e = GetEntity(entityIds[i]);
                if (e == null || !e.IsAlive || e.Def.Role != "Harvester") continue;
                e.CurrentOrder = OrderKind.Harvest;
                e.TargetCell = oreCell;
                RebuildPath(e, oreCell);
            }
        }

        public void Step(float dt)
        {
            if (dt <= 0f) return;
            _simTime += dt;

            UpdateProductionQueues(dt);
            UpdateEntities(dt);
            UpdateProjectiles(dt);
            UpdateOreRegrowth(dt);
            UpdateAi(dt);
            CheckVictory();
        }

        private void UpdateProductionQueues(float dt)
        {
            // Base spend rate: ~400 credits/sec at normal power, 160 credits/sec on low power
            for (int f = 0; f < _factions.Count; f++)
            {
                var faction = _factions[f];
                bool wasLowPower = faction.IsLowPower;
                float buildSpeed = wasLowPower ? 160f : 420f;
                int maxStepSpend = Math.Max(1, (int)Math.Round(buildSpeed * dt));

                foreach (var kv in faction.Queues)
                {
                    var q = kv.Value;
                    if (string.IsNullOrEmpty(q.ActiveActorId) || q.IsReadyToPlace) continue;

                    int remaining = q.TotalCost - q.PaidCost;
                    int spend = Math.Min(remaining, Math.Min(faction.Credits, maxStepSpend));
                    if (spend > 0)
                    {
                        faction.Credits -= spend;
                        q.PaidCost += spend;
                    }

                    if (q.PaidCost >= q.TotalCost)
                    {
                        var def = Rules.GetActor(q.ActiveActorId);
                        if (def != null && def.IsBuilding)
                        {
                            q.IsReadyToPlace = true;
                            _events.Add(new SimEvent { Kind = SimEventKind.EvaAnnouncement, FactionId = faction.FactionId, Tag = "ceva048" }); // Construction complete
                        }
                        else if (def != null)
                        {
                            SpawnProducedUnit(faction.FactionId, def);
                            q.ActiveActorId = null;
                            q.TotalCost = 0;
                            q.PaidCost = 0;
                            q.IsReadyToPlace = false;
                        }
                    }
                }
            }
        }

        private void SpawnProducedUnit(int factionId, ActorDefData unitDef)
        {
            EntityState producer = null;
            for (int i = 0; i < _entities.Count; i++)
            {
                var e = _entities[i];
                if (!e.IsAlive || e.FactionId != factionId || !e.Def.IsBuilding) continue;
                if (unitDef.Category == "Infantry" && (e.Def.Id == "GAPILE" || e.Def.Id == "NAHAND"))
                {
                    producer = e;
                    break;
                }
                if (unitDef.Category == "Vehicle" && (e.Def.Id == "GAWEAP" || e.Def.Id == "NAWEAP"))
                {
                    producer = e;
                    break;
                }
            }

            CellCoord spawnCell = producer != null ? GetBuildingExitCell(producer) : new CellCoord(12, 12);
            var spawned = SpawnUnit(factionId, unitDef.Id, spawnCell);
            if (spawned != null && producer != null)
            {
                CellCoord rally = Map.FindNearestPassable(producer.RallyPoint, 4);
                spawned.CurrentOrder = OrderKind.AttackMove;
                spawned.TargetCell = rally;
                RebuildPath(spawned, rally);
                _events.Add(new SimEvent { Kind = SimEventKind.UnitProduced, FactionId = factionId, SourceEntityId = spawned.Id, Position = spawned.Position, Tag = unitDef.Id });
                _events.Add(new SimEvent { Kind = SimEventKind.EvaAnnouncement, FactionId = factionId, Position = spawned.Position, Tag = "ceva062" }); // Unit ready
            }
        }

        private CellCoord GetBuildingExitCell(EntityState building)
        {
            var below = new CellCoord(building.TopLeftCell.X + building.Def.FootprintX / 2, building.TopLeftCell.Y - 1);
            return Map.FindNearestPassable(below, 6);
        }

        private void UpdateEntities(float dt)
        {
            for (int i = 0; i < _entities.Count; i++)
            {
                var e = _entities[i];
                if (!e.IsAlive) continue;

                if (e.WeaponCooldown > 0f)
                {
                    e.WeaponCooldown = Math.Max(0f, e.WeaponCooldown - dt);
                }

                if (e.Def.Role == "Harvester")
                {
                    UpdateHarvester(e, dt);
                }

                if (!e.Def.IsBuilding)
                {
                    FollowPath(e, dt);
                }

                UpdateCombat(e, dt);
            }
        }

        private void UpdateHarvester(EntityState harv, float dt)
        {
            if (harv.CurrentOrder == OrderKind.Move) return; // Respect manual player move until arrival

            if (harv.CurrentOrder == OrderKind.Idle)
            {
                if (harv.TotalCarriedLoad >= EntityState.MaxHarvesterCapacity)
                {
                    SendHarvesterToRefinery(harv);
                }
                else
                {
                    CellCoord oreCell;
                    if (Map.TryFindNearestOre(harv.Position.ToCell(), 48, out oreCell))
                    {
                        harv.CurrentOrder = OrderKind.Harvest;
                        harv.TargetCell = oreCell;
                        RebuildPath(harv, oreCell);
                    }
                    else if (harv.TotalCarriedLoad > 0)
                    {
                        SendHarvesterToRefinery(harv);
                    }
                }
                return;
            }

            if (harv.CurrentOrder == OrderKind.Harvest)
            {
                if (harv.Path.Count == 0)
                {
                    var cell = Map.GetCell(harv.Position.ToCell());
                    if (cell != null && cell.OreAmount > 0)
                    {
                        harv.HarvestTimer += dt;
                        if (harv.HarvestTimer >= 0.45f)
                        {
                            harv.HarvestTimer = 0f;
                            cell.OreAmount--;
                            if (cell.OreType == 2) harv.CarriedGemUnits += 4;
                            else harv.CarriedOreUnits += 4;

                            if (cell.OreAmount <= 0)
                            {
                                cell.OreType = 0;
                            }
                        }

                        if (harv.TotalCarriedLoad >= EntityState.MaxHarvesterCapacity)
                        {
                            SendHarvesterToRefinery(harv);
                        }
                    }
                    else
                    {
                        harv.CurrentOrder = OrderKind.Idle;
                    }
                }
                return;
            }

            if (harv.CurrentOrder == OrderKind.ReturnOre && harv.Path.Count == 0)
            {
                var faction = GetFaction(harv.FactionId);
                if (faction != null && harv.TotalCarriedLoad > 0)
                {
                    int payout = harv.CarriedOreUnits * 30 + harv.CarriedGemUnits * 60;
                    faction.Credits += payout;
                    harv.CarriedOreUnits = 0;
                    harv.CarriedGemUnits = 0;
                    _events.Add(new SimEvent { Kind = SimEventKind.OreUnloaded, FactionId = harv.FactionId, SourceEntityId = harv.Id, Position = harv.Position, Value = payout });
                }
                harv.CurrentOrder = OrderKind.Idle;
            }
        }

        private void SendHarvesterToRefinery(EntityState harv)
        {
            EntityState bestRefinery = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _entities.Count; i++)
            {
                var e = _entities[i];
                if (e.IsAlive && e.FactionId == harv.FactionId && (e.Def.Id == "GAREFN" || e.Def.Id == "NAREFN"))
                {
                    float d = e.Position.DistanceTo(harv.Position);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestRefinery = e;
                    }
                }
            }

            if (bestRefinery != null)
            {
                CellCoord dock = GetBuildingExitCell(bestRefinery);
                harv.CurrentOrder = OrderKind.ReturnOre;
                harv.TargetCell = dock;
                RebuildPath(harv, dock);
            }
            else
            {
                harv.CurrentOrder = OrderKind.Idle;
            }
        }

        private void FollowPath(EntityState unit, float dt)
        {
            if (unit.Path.Count == 0)
            {
                if (unit.CurrentOrder == OrderKind.Move || unit.CurrentOrder == OrderKind.AttackMove)
                {
                    unit.CurrentOrder = OrderKind.Idle;
                }
                ApplyFriendlySeparation(unit, dt);
                return;
            }

            SimVec2 waypoint = SimVec2.FromCellCenter(unit.Path[0]);
            float dx = waypoint.X - unit.Position.X;
            float dy = waypoint.Y - unit.Position.Y;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);

            if (dist <= 0.12f)
            {
                unit.Position = waypoint;
                unit.Path.RemoveAt(0);
                return;
            }

            float desiredYaw = (float)(Math.Atan2(dx, dy) * (180.0 / Math.PI));
            unit.BodyYawDeg = MoveTowardsAngle(unit.BodyYawDeg, desiredYaw, 360f * dt);
            if (!unit.Def.HasTurret)
            {
                unit.TurretYawDeg = unit.BodyYawDeg;
            }

            float step = Math.Min(dist, Math.Max(1.0f, unit.Def.Speed) * dt);
            unit.Position = new SimVec2(unit.Position.X + (dx / dist) * step, unit.Position.Y + (dy / dist) * step);
        }

        private void ApplyFriendlySeparation(EntityState unit, float dt)
        {
            float pushX = 0f;
            float pushY = 0f;
            float minSep = unit.Def.Role == "Infantry" ? 0.42f : 0.78f;

            for (int i = 0; i < _entities.Count; i++)
            {
                var other = _entities[i];
                if (other.Id == unit.Id || !other.IsAlive || other.Def.IsBuilding) continue;
                float dx = unit.Position.X - other.Position.X;
                float dy = unit.Position.Y - other.Position.Y;
                float d2 = dx * dx + dy * dy;
                if (d2 < minSep * minSep && d2 > 0.0001f)
                {
                    float d = (float)Math.Sqrt(d2);
                    float strength = (minSep - d) / minSep;
                    pushX += (dx / d) * strength;
                    pushY += (dy / d) * strength;
                }
            }

            if (Math.Abs(pushX) > 0.001f || Math.Abs(pushY) > 0.001f)
            {
                SimVec2 candidate = new SimVec2(unit.Position.X + pushX * 1.6f * dt, unit.Position.Y + pushY * 1.6f * dt);
                var cell = Map.GetCell(candidate.ToCell());
                if (cell != null && cell.IsLandPassable)
                {
                    unit.Position = candidate;
                }
            }
        }

        private void UpdateCombat(EntityState attacker, float dt)
        {
            if (string.IsNullOrEmpty(attacker.Def.PrimaryWeapon)) return;
            var weapon = Rules.GetWeapon(attacker.Def.PrimaryWeapon);
            if (weapon == null) return;

            if (attacker.Def.IsBuilding && attacker.Def.Powered)
            {
                var faction = GetFaction(attacker.FactionId);
                if (faction != null && faction.IsLowPower) return;
            }

            EntityState target = null;
            if (attacker.CurrentOrder == OrderKind.AttackTarget && attacker.TargetEntityId != 0)
            {
                target = GetEntity(attacker.TargetEntityId);
                if (target == null || !target.IsAlive)
                {
                    attacker.TargetEntityId = 0;
                    attacker.CurrentOrder = OrderKind.Idle;
                    target = null;
                }
            }

            // Pure move order ignores auto-combat while moving unless AttackMove or Idle
            if (target == null && attacker.CurrentOrder != OrderKind.Move && attacker.CurrentOrder != OrderKind.Harvest && attacker.CurrentOrder != OrderKind.ReturnOre)
            {
                target = FindNearestEnemyInRange(attacker, weapon.Range * 1.15f);
            }

            if (target == null)
            {
                if (attacker.Def.HasTurret)
                {
                    attacker.TurretYawDeg = MoveTowardsAngle(attacker.TurretYawDeg, attacker.BodyYawDeg, 180f * dt);
                }
                return;
            }

            float dx = target.Position.X - attacker.Position.X;
            float dy = target.Position.Y - attacker.Position.Y;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            float targetEdgeDist = target.Def.IsBuilding ? Math.Max(0f, dist - Math.Max(target.Def.FootprintX, target.Def.FootprintY) * 0.45f) : dist;

            if (targetEdgeDist > weapon.Range)
            {
                if (!attacker.Def.IsBuilding && (attacker.CurrentOrder == OrderKind.AttackTarget || attacker.CurrentOrder == OrderKind.AttackMove))
                {
                    if (attacker.Path.Count == 0)
                    {
                        RebuildPath(attacker, target.Position.ToCell());
                    }
                }
                return;
            }

            // In range! Stop if explicit AttackTarget so tank holds firing range cleanly
            if (!attacker.Def.IsBuilding && attacker.CurrentOrder == OrderKind.AttackTarget)
            {
                attacker.Path.Clear();
            }

            float aimYaw = (float)(Math.Atan2(dx, dy) * (180.0 / Math.PI));
            if (attacker.Def.HasTurret)
            {
                attacker.TurretYawDeg = MoveTowardsAngle(attacker.TurretYawDeg, aimYaw, 280f * dt);
                if (Math.Abs(DeltaAngle(attacker.TurretYawDeg, aimYaw)) > 12f) return;
            }
            else if (!attacker.Def.IsBuilding)
            {
                attacker.BodyYawDeg = MoveTowardsAngle(attacker.BodyYawDeg, aimYaw, 320f * dt);
                attacker.TurretYawDeg = attacker.BodyYawDeg;
            }

            if (attacker.WeaponCooldown <= 0f)
            {
                attacker.WeaponCooldown = Math.Max(0.2f, weapon.ReloadSeconds);
                FireProjectile(attacker, target, weapon);
            }
        }

        private void FireProjectile(EntityState attacker, EntityState target, WeaponDefData weapon)
        {
            float dist = Math.Max(0.2f, attacker.Position.DistanceTo(target.Position));
            float speed = Math.Max(6f, weapon.ProjectileSpeed);
            var proj = new ProjectileState
            {
                Id = _nextProjectileId++,
                FactionId = attacker.FactionId,
                SourceEntityId = attacker.Id,
                TargetEntityId = target.Id,
                Weapon = weapon,
                StartPos = attacker.Position,
                CurrentPos = attacker.Position,
                TargetPos = target.Position,
                Progress = 0f,
                Duration = Math.Max(0.06f, dist / speed)
            };
            _projectiles.Add(proj);
            _events.Add(new SimEvent
            {
                Kind = SimEventKind.WeaponFired,
                FactionId = attacker.FactionId,
                SourceEntityId = attacker.Id,
                Position = attacker.Position,
                Tag = weapon.ReportSound ?? weapon.Id
            });
        }

        private void UpdateProjectiles(float dt)
        {
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                var p = _projectiles[i];
                var target = GetEntity(p.TargetEntityId);
                if (target != null && target.IsAlive)
                {
                    p.TargetPos = target.Position;
                }

                p.Progress += dt / p.Duration;
                if (p.Progress >= 1f)
                {
                    p.CurrentPos = p.TargetPos;
                    ResolveProjectileImpact(p);
                    _projectiles.RemoveAt(i);
                }
                else
                {
                    p.CurrentPos = new SimVec2(
                        p.StartPos.X + (p.TargetPos.X - p.StartPos.X) * p.Progress,
                        p.StartPos.Y + (p.TargetPos.Y - p.StartPos.Y) * p.Progress);
                }
            }
        }

        private void ResolveProjectileImpact(ProjectileState proj)
        {
            _events.Add(new SimEvent
            {
                Kind = SimEventKind.ProjectileImpact,
                FactionId = proj.FactionId,
                SourceEntityId = proj.SourceEntityId,
                Position = proj.TargetPos,
                Tag = proj.Weapon.ProjectileStyle
            });

            var directTarget = GetEntity(proj.TargetEntityId);
            if (directTarget != null && directTarget.IsAlive)
            {
                ApplyDamage(directTarget, proj.Weapon, 1.0f);
            }

            if (proj.Weapon.SplashRadius > 0.25f)
            {
                for (int i = 0; i < _entities.Count; i++)
                {
                    var e = _entities[i];
                    if (!e.IsAlive || (directTarget != null && e.Id == directTarget.Id)) continue;
                    if (e.FactionId == proj.FactionId) continue;
                    float d = e.Position.DistanceTo(proj.TargetPos);
                    if (d <= proj.Weapon.SplashRadius)
                    {
                        float falloff = Math.Max(0.25f, 1f - (d / proj.Weapon.SplashRadius) * 0.65f);
                        ApplyDamage(e, proj.Weapon, falloff);
                    }
                }
            }
        }

        private void ApplyDamage(EntityState victim, WeaponDefData weapon, float scale)
        {
            float armorMul = GetArmorMultiplier(weapon, victim.Def.Armor);
            int dmg = Math.Max(1, (int)Math.Round(weapon.Damage * armorMul * scale));
            victim.Hp -= dmg;
            if (victim.Hp <= 0)
            {
                victim.Hp = 0;
                _events.Add(new SimEvent
                {
                    Kind = SimEventKind.EntityDestroyed,
                    FactionId = victim.FactionId,
                    SourceEntityId = victim.Id,
                    Position = victim.Position,
                    Tag = victim.Def.Id
                });
                bool wasBuilding = victim.Def.IsBuilding;
                RemoveEntity(victim);
                if (wasBuilding) RecalculatePower();
            }
        }

        private static float GetArmorMultiplier(WeaponDefData w, string armor)
        {
            if (string.IsNullOrEmpty(armor)) return 1f;
            switch (armor.ToLowerInvariant())
            {
                case "none": return w.VsNone;
                case "flak": return w.VsFlak;
                case "plate": return w.VsPlate;
                case "light": return w.VsLight;
                case "medium": return w.VsMedium;
                case "heavy": return w.VsHeavy;
                case "wood": return w.VsWood;
                case "steel": return w.VsSteel;
                case "concrete": return w.VsConcrete;
                default: return 1f;
            }
        }

        private EntityState FindNearestEnemyInRange(EntityState seeker, float range)
        {
            EntityState best = null;
            float bestDist = range;
            for (int i = 0; i < _entities.Count; i++)
            {
                var other = _entities[i];
                if (!other.IsAlive || other.FactionId == seeker.FactionId) continue;
                float d = seeker.Position.DistanceTo(other.Position);
                if (other.Def.IsBuilding)
                {
                    d = Math.Max(0f, d - Math.Max(other.Def.FootprintX, other.Def.FootprintY) * 0.45f);
                }
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = other;
                }
            }
            return best;
        }

        private void UpdateOreRegrowth(float dt)
        {
            _oreRegrowTimer -= dt;
            if (_oreRegrowTimer > 0f) return;
            _oreRegrowTimer = 10f;

            // 1. Existing Gold Ore (Riparius, OreType==1) slowly matures; Gems (Cruentus, OreType==2) do not regrow per RA2 rules!
            for (int y = 1; y < Map.Height - 1; y++)
            {
                for (int x = 1; x < Map.Width - 1; x++)
                {
                    var cell = Map.GetCell(x, y);
                    if (cell.OreType == 1 && cell.OreAmount > 0 && cell.OreAmount < 12 && NextSimInt(0, 4) == 0)
                    {
                        cell.OreAmount++;
                    }
                }
            }

            // 2. Ore Spawner Drills (TIBTRE01..03, Prop.Kind == 1) actively pump & spread new Gold Ore in a 3-cell radius!
            if (Map.Props != null)
            {
                for (int i = 0; i < Map.Props.Count; i++)
                {
                    var prop = Map.Props[i];
                    if (prop.Kind != 1) continue;

                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        int dx = NextSimInt(-3, 4);
                        int dy = NextSimInt(-3, 4);
                        if (dx == 0 && dy == 0) continue;

                        var target = Map.GetCell(prop.X + dx, prop.Y + dy);
                        if (target == null || !target.IsLandPassable || target.HasBridge) continue;
                        if (target.Terrain != TerrainKind.Clear && target.Terrain != TerrainKind.Rough) continue;

                        if (target.OreAmount == 0)
                        {
                            target.OreType = 1;
                            target.OreAmount = 3;
                            break;
                        }
                        if (target.OreType == 1 && target.OreAmount < 12)
                        {
                            target.OreAmount = Math.Min(12, target.OreAmount + 2);
                            break;
                        }
                    }
                }
            }
        }

        private void UpdateAi(float dt)
        {
            _aiDecisionTimer -= dt;
            _aiAttackWaveTimer -= dt;
            if (_aiDecisionTimer > 0f) return;
            _aiDecisionTimer = 2.5f;

            for (int f = 0; f < _factions.Count; f++)
            {
                var ai = _factions[f];
                if (!ai.IsAiControlled) continue;

                RunAiBuildOrder(ai);

                if (_aiAttackWaveTimer <= 0f)
                {
                    _aiAttackWaveTimer = 60f;
                    LaunchAiAttackWave(ai);
                }
            }
        }

        private void RunAiBuildOrder(FactionState ai)
        {
            string yardId = ai.Side == "Soviet" ? "NACNST" : "GACNST";
            string powerId = ai.Side == "Soviet" ? "NAPOWR" : "GAPOWR";
            string refnId = ai.Side == "Soviet" ? "NAREFN" : "GAREFN";
            string barrId = ai.Side == "Soviet" ? "NAHAND" : "GAPILE";
            string weapId = ai.Side == "Soviet" ? "NAWEAP" : "GAWEAP";
            string defId = ai.Side == "Soviet" ? "TESLA" : "GAPILL";
            string infId = ai.Side == "Soviet" ? "E2" : "E1";
            string tankId = ai.Side == "Soviet" ? "HTNK" : "MTNK";

            EntityState yard = null;
            int powerCount = 0, refnCount = 0, barrCount = 0, weapCount = 0, defCount = 0;
            for (int i = 0; i < _entities.Count; i++)
            {
                var e = _entities[i];
                if (!e.IsAlive || e.FactionId != ai.FactionId) continue;
                if (e.Def.Id == yardId) yard = e;
                else if (e.Def.Id == powerId) powerCount++;
                else if (e.Def.Id == refnId) refnCount++;
                else if (e.Def.Id == barrId) barrCount++;
                else if (e.Def.Id == weapId) weapCount++;
                else if (e.Def.Id == defId) defCount++;
            }

            if (yard == null) return;

            // Place ready buildings automatically near AI yard
            var mainQ = ai.Queues["MainBuilding"];
            if (mainQ.IsReadyToPlace)
            {
                TryAiAutoPlace(ai.FactionId, "MainBuilding", yard.TopLeftCell);
            }
            else if (string.IsNullOrEmpty(mainQ.ActiveActorId))
            {
                if (powerCount == 0 || ai.IsLowPower) CommandStartProduction(ai.FactionId, powerId);
                else if (refnCount == 0) CommandStartProduction(ai.FactionId, refnId);
                else if (barrCount == 0) CommandStartProduction(ai.FactionId, barrId);
                else if (weapCount == 0) CommandStartProduction(ai.FactionId, weapId);
                else if (refnCount < 2 && ai.Credits > 1800) CommandStartProduction(ai.FactionId, refnId);
            }

            var defQ = ai.Queues["DefenseBuilding"];
            if (defQ.IsReadyToPlace)
            {
                TryAiAutoPlace(ai.FactionId, "DefenseBuilding", yard.TopLeftCell);
            }
            else if (string.IsNullOrEmpty(defQ.ActiveActorId) && barrCount > 0 && defCount < 2 && ai.Credits > 1200)
            {
                CommandStartProduction(ai.FactionId, defId);
            }

            var infQ = ai.Queues["Infantry"];
            if (barrCount > 0 && string.IsNullOrEmpty(infQ.ActiveActorId) && ai.Credits >= 400)
            {
                CommandStartProduction(ai.FactionId, infId);
            }

            var vehQ = ai.Queues["Vehicle"];
            if (weapCount > 0 && string.IsNullOrEmpty(vehQ.ActiveActorId) && ai.Credits >= 900)
            {
                CommandStartProduction(ai.FactionId, tankId);
            }
        }

        private void TryAiAutoPlace(int factionId, string category, CellCoord anchor)
        {
            for (int r = 3; r <= 10; r++)
            {
                for (int dy = -r; dy <= r; dy += 2)
                {
                    for (int dx = -r; dx <= r; dx += 2)
                    {
                        var candidate = new CellCoord(anchor.X + dx, anchor.Y + dy);
                        if (CommandPlaceReadyBuilding(factionId, category, candidate))
                        {
                            return;
                        }
                    }
                }
            }
            CommandCancelProduction(factionId, category);
        }

        private void LaunchAiAttackWave(FactionState ai)
        {
            EntityState playerTarget = null;
            for (int i = 0; i < _entities.Count; i++)
            {
                var e = _entities[i];
                if (e.IsAlive && e.FactionId != ai.FactionId)
                {
                    playerTarget = e;
                    if (e.Def.IsBuilding) break;
                }
            }
            if (playerTarget == null) return;

            var attackers = new List<int>();
            for (int i = 0; i < _entities.Count; i++)
            {
                var e = _entities[i];
                if (e.IsAlive && e.FactionId == ai.FactionId && !e.Def.IsBuilding && e.Def.Role != "Harvester" && e.Def.Role != "Mcv")
                {
                    attackers.Add(e.Id);
                }
            }

            if (attackers.Count >= 4)
            {
                CommandMove(attackers, playerTarget.Position.ToCell(), true);
            }
        }

        private void CheckVictory()
        {
            if (WinnerFactionId >= 0) return;
            bool p0Alive = false;
            bool p1Alive = false;
            for (int i = 0; i < _entities.Count; i++)
            {
                var e = _entities[i];
                if (!e.IsAlive) continue;
                if (e.FactionId == 0) p0Alive = true;
                else if (e.FactionId == 1) p1Alive = true;
            }
            if (p0Alive && !p1Alive) WinnerFactionId = 0;
            else if (!p0Alive && p1Alive) WinnerFactionId = 1;
        }

        public void RecalculatePower()
        {
            for (int f = 0; f < _factions.Count; f++)
            {
                _factions[f].PowerProduced = 0;
                _factions[f].PowerConsumed = 0;
            }

            for (int i = 0; i < _entities.Count; i++)
            {
                var e = _entities[i];
                if (!e.IsAlive || !e.Def.IsBuilding) continue;
                var faction = GetFaction(e.FactionId);
                if (faction == null) continue;
                if (e.Def.Power > 0) faction.PowerProduced += e.Def.Power;
                else if (e.Def.Power < 0) faction.PowerConsumed += -e.Def.Power;
            }
        }

        private void RebuildPath(EntityState entity, CellCoord goal)
        {
            entity.Path.Clear();
            var computed = AStarPathfinder.FindPath(Map, entity.Position.ToCell(), goal);
            entity.Path.AddRange(computed);
        }

        private void RemoveEntity(EntityState entity)
        {
            if (entity.Def.IsBuilding)
            {
                Map.SetBuildingFootprint(entity.TopLeftCell, entity.Def.FootprintX, entity.Def.FootprintY, 0);
            }
            entity.Hp = 0;
            _entityById.Remove(entity.Id);
            _entities.Remove(entity);
        }

        private static float DeltaAngle(float current, float target)
        {
            float delta = (target - current) % 360f;
            if (delta > 180f) delta -= 360f;
            if (delta < -180f) delta += 360f;
            return delta;
        }

        private static float MoveTowardsAngle(float current, float target, float maxDelta)
        {
            float delta = DeltaAngle(current, target);
            if (Math.Abs(delta) <= maxDelta) return target;
            return current + Math.Sign(delta) * maxDelta;
        }
    }
}
