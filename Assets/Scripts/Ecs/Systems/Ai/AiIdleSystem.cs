using CyberNinja.Config;
using CyberNinja.Ecs.Components.Ai;
using CyberNinja.Ecs.Components.Unit;
using CyberNinja.Models.Enums;
using CyberNinja.Services;
using CyberNinja.Views.Unit;
using Leopotam.EcsLite;
using Leopotam.EcsLite.Di;
using UnityEngine;

namespace CyberNinja.Ecs.Systems.Ai
{
    public class AiIdleSystem : IEcsRunSystem
    {
        private readonly EcsFilterInject<Inc<AiTaskComponent>, Exc<KnockoutComponent>> _filter;
        private readonly EcsWorldInject _world;
        private readonly EcsCustomInject<LayersConfig> _layersConfig;
        private readonly EcsCustomInject<IUnitService> _unitService;
        private readonly EcsPoolInject<AiTaskComponent> _aiTaskPool;
        private readonly EcsPoolInject<AiTargetComponent> _aiTargetPool;
        private readonly EcsPoolInject<SpeedComponent> _speedPool;

        public void Run(IEcsSystems systems)
        {
            foreach (var entity in _filter.Value)
            {
                ref var aiTask = ref _aiTaskPool.Value.Get(entity);
                if (aiTask.Value != EAiTaskType.Idle)
                    continue;

                var unit = _unitService.Value.GetUnit(entity);
                var hitColliders = Physics.OverlapSphere(
                    unit.View.Transform.position,
                    unit.View.LookRadius,
                    _layersConfig.Value.playerLayerMask);

                if (hitColliders.Length > 1)
                {
                    foreach (var hit in hitColliders)
                    {
                        if (hit.transform == unit.View.Transform)
                            continue;

                        var targetTransform = hit.transform;
                        var packed = targetTransform.GetComponent<UnitView>().Entity;
                        if (packed.Unpack(_world.Value, out var targetEntity))
                        {
                            if (_unitService.Value.HasState(targetEntity, EUnitState.Dead))
                                return;

                            var distance = Vector3.Distance(unit.View.Transform.position, targetTransform.position);
                            if (_unitService.Value.IsPlayer(targetEntity))
                            {
                                var aiTarget = new AiTargetComponent
                                {
                                    Distance = distance,
                                    Transform = targetTransform,
                                    Entity = _world.Value.PackEntity(targetEntity)
                                };

                                if (_aiTargetPool.Value.Has(entity))
                                    _aiTargetPool.Value.Get(entity) = aiTarget;
                                else
                                    _aiTargetPool.Value.Add(entity) = aiTarget;

                                aiTask.Value = EAiTaskType.Chase;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    ref var speed = ref _speedPool.Value.Get(entity);
                    speed.SpeedTarget = 0f;
                }
            }
        }
    }
}