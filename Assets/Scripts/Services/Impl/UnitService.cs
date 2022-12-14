using System;
using System.Collections.Generic;
using CyberNinja.Config;
using CyberNinja.Ecs.Components.Ai;
using CyberNinja.Ecs.Components.Unit;
using CyberNinja.Models.Enums;
using CyberNinja.Utils;
using CyberNinja.Views;
using CyberNinja.Views.Unit;
using Leopotam.EcsLite;
using UnityEngine;

namespace CyberNinja.Services.Impl
{
    public class UnitService : IUnitService
    {
        private readonly EcsWorld _world;
        private readonly UnitConfig _unitConfig;
        private readonly CanvasView _canvasView;
        private readonly IVfxService _vfxService;
        private readonly EcsPool<StunComponent> _stunPool;
        private readonly EcsPool<KnockoutComponent> _knockoutPool;
        private readonly EcsPool<DeadComponent> _deadPool;
        private readonly EcsPool<HealthComponent> _healthPool;
        private readonly EcsPool<UnitComponent> _unitPool;
        private readonly EcsPool<DamageFactorComponent> _damageFactorPool;
        private readonly EcsPool<DashComponent> _dashPool;
        private readonly EcsPool<StationaryComponent> _stationaryPool;
        private readonly EcsPool<PlayerComponent> _playerPool;
        private readonly EcsPool<EnergyComponent> _energyPool;
        private readonly EcsPool<VectorsComponent> _vectorsPool;
        private readonly EcsPool<MoveVectorComponent> _moveVectorPool;

        public UnitService(EcsWorld world, UnitConfig unitConfig, CanvasView canvasView, IVfxService vfxService)
        {
            _world = world;
            _unitConfig = unitConfig;
            _canvasView = canvasView;
            _vfxService = vfxService;

            _stunPool = _world.GetPool<StunComponent>();
            _knockoutPool = _world.GetPool<KnockoutComponent>();
            _deadPool = _world.GetPool<DeadComponent>();
            _healthPool = _world.GetPool<HealthComponent>();
            _unitPool = _world.GetPool<UnitComponent>();
            _damageFactorPool = _world.GetPool<DamageFactorComponent>();
            _dashPool = _world.GetPool<DashComponent>();
            _stationaryPool = _world.GetPool<StationaryComponent>();
            _playerPool = _world.GetPool<PlayerComponent>();
            _energyPool = _world.GetPool<EnergyComponent>();
            _vectorsPool = _world.GetPool<VectorsComponent>();
            _moveVectorPool = _world.GetPool<MoveVectorComponent>();
        }

        public int CreateUnit(UnitView view)
        {
            var entity = _world.NewEntity();

            ref var unit = ref _unitPool.Add(entity);
            unit.View = view;
            unit.ControlType = view.ControlType;

            ref var health = ref _healthPool.Add(entity);
            health.Current = view.MaxHealth;
            health.Max = view.MaxHealth;

            var healthRegenerationPool = _world.GetPool<HealthRegenerationComponent>();
            ref var healthRegeneration = ref healthRegenerationPool.Add(entity);
            healthRegeneration.Value = view.HealthRegeneration;
            
            var energyPool = _world.GetPool<EnergyComponent>();
            ref var energy = ref energyPool.Add(entity);
            energy.Current = view.MaxEnergy;
            energy.Max = view.MaxEnergy;

            var energyRegenerationPool = _world.GetPool<EnergyRegenerationComponent>();
            ref var energyRegeneration = ref energyRegenerationPool.Add(entity);
            energyRegeneration.Value = view.EnergyRegeneration;

            var damageFactorPool = _world.GetPool<DamageFactorComponent>();
            ref var damageFactor = ref damageFactorPool.Add(entity);
            damageFactor.ImpactList = new List<float>();
            damageFactor.PhysicalFactor = 0;

            var movementPool = _world.GetPool<SpeedComponent>();
            ref var movement = ref movementPool.Add(entity);
            movement.SpeedMoveMax = view.MoveSpeed;
            movement.SpeedCurrent = 0;
            movement.SpeedTarget = 0;

            var vectorsPool = _world.GetPool<VectorsComponent>();
            ref var vectors = ref vectorsPool.Add(entity);
            vectors.IsActiveVectorLook = true;
            vectors.VectorLook = Vector3.zero;
            vectors.VectorDifference = Vector3.zero;

            view.NavMeshAgent.speed = movement.SpeedCurrent;
            view.NavMeshAgent.stoppingDistance = view.AttackDistance - 0.1f;
            view.NavMeshAgent.updateRotation = false;

            view.Entity = _world.PackEntity(entity);

            return entity;
        }

        public void AddDamage(int entity, float damage, Transform damageOrigin)
        {
            ref var health = ref _healthPool.Get(entity);
            var unit = _unitPool.Get(entity);
            var damageFactor = _damageFactorPool.Get(entity);

            var damageMath = damage - damage * damageFactor.PhysicalFactor / 100;
            var newHealth = Mathf.Clamp(health.Current - damageMath, 0f, health.Max);

            UpdateHealth(entity, newHealth);

            var damageClamped = Mathf.Clamp01(damageMath / _unitConfig.maxDamage);
            var healthClamped = Mathf.Clamp01(1 - health.Current / health.Max);

            var abilityData = unit.View.AbilityDamageConfig;
            if (damageMath > 0)
            {
                if (abilityData.ANIMATOR)
                    unit.View.Animator.TriggerAnimations(abilityData);
                if (abilityData.VFX)
                    _vfxService.SpawnVfx(entity, abilityData, true, damageClamped, healthClamped, damageOrigin.position);

                var damageClampedLayer = Mathf.Clamp(damageClamped, _unitConfig.minLayerHit, 1); // limit min layer weight
                unit.View.Animator.SetLayerWeight(2, damageClampedLayer);
            }

            if (health.Current <= 0)
                Dead(entity);
        }

        private void Dead(int entity)
        {
            AddState(entity, EUnitState.Dead);
            AddState(entity, EUnitState.Knockout);

            RemoveState(entity, EUnitState.Stun);
            RemoveState(entity, EUnitState.Dash);
            RemoveState(entity, EUnitState.Stationary);

            var unit = _unitPool.Get(entity);

            unit.View.NavMeshAgent.enabled = false;

            if (unit.View.AbilityDeadConfig.ANIMATOR)
                unit.View.Animator.TriggerAnimations(unit.View.AbilityDeadConfig);
            if (unit.View.AbilityDeadConfig.VFX)
                _vfxService.SpawnVfx(entity, unit.View.AbilityDeadConfig);

            if (unit.ControlType == EControlType.AI)
            {
                ref var aiTask = ref _world.GetPool<AiTaskComponent>().Get(entity);
                aiTask.Value = EAiTaskType.Dead;
                if (_world.GetPool<AiTargetComponent>().Has(entity))
                    _world.GetPool<AiTargetComponent>().Del(entity);
            }
        }

        public bool AddState(int entity, EUnitState state, float value = 0)
        {
            switch (state)
            {
                case EUnitState.Stun:
                    if (_stunPool.Has(entity))
                        return false;
                    _stunPool.Add(entity);
                    ToggleStun(entity);
                    return true;
                case EUnitState.Knockout:
                    if (_knockoutPool.Has(entity))
                        return false;
                    _knockoutPool.Add(entity);
                    return true;
                case EUnitState.Dead:
                    if (_deadPool.Has(entity))
                        return false;
                    _deadPool.Add(entity);
                    return true;
                case EUnitState.Dash:
                    if (_dashPool.Has(entity))
                        return false;
                    _dashPool.Add(entity);
                    return true;
                case EUnitState.Stationary:
                    if (_stationaryPool.Has(entity))
                        return false;
                    _stationaryPool.Add(entity).Time = value;
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        public bool HasState(int entity, EUnitState state)
        {
            switch (state)
            {
                case EUnitState.Stun:
                    return _stunPool.Has(entity);
                case EUnitState.Knockout:
                    return _knockoutPool.Has(entity);
                case EUnitState.Dead:
                    return _deadPool.Has(entity);
                case EUnitState.Dash:
                    return _dashPool.Has(entity);
                case EUnitState.Stationary:
                    return _stationaryPool.Has(entity);
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        public bool RemoveState(int entity, EUnitState state)
        {
            switch (state)
            {
                case EUnitState.Stun:
                    if (!_stunPool.Has(entity))
                        return false;
                    _stunPool.Del(entity);
                    ToggleStun(entity);
                    return true;
                case EUnitState.Knockout:
                    if (!_knockoutPool.Has(entity))
                        return false;
                    _knockoutPool.Del(entity);
                    return true;
                case EUnitState.Dead:
                    if (!_deadPool.Has(entity))
                        return false;
                    _deadPool.Del(entity);
                    return true;
                case EUnitState.Dash:
                    if (!_dashPool.Has(entity))
                        return false;
                    _dashPool.Del(entity);
                    return true;
                case EUnitState.Stationary:
                    if (!_stationaryPool.Has(entity))
                        return false;
                    _stationaryPool.Del(entity);
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        public bool IsPlayer(int entity) => _playerPool.Has(entity);

        public HealthComponent GetHealth(int entity)
        {
            if (_healthPool.Has(entity))
                return _healthPool.Get(entity);
            throw new Exception("Entity hasn't health component");
        }

        public void UpdateHealth(int entity, float value)
        {
            ref var health = ref _healthPool.Get(entity);
            health.Current = value;
            
            if (IsPlayer(entity))
            {
                var finalHealthScaleX = Mathf.Clamp01(health.Current / health.Max);
                _canvasView.PlayerHealthBar.transform.localScale = new Vector3(finalHealthScaleX, 1, 1);

                var finalHealthValue = Mathf.Clamp(health.Current, 0, health.Max);
                _canvasView.PlayerHealthText.text = finalHealthValue.ToString("F0") + "/" + health.Max;
            }
        }

        public void UpdateEnergy(int entity, float value)
        {
            ref var energy = ref _energyPool.Get(entity);
            energy.Current = value;
            
            if (IsPlayer(entity))
            {
                var finalEnergyScaleX = Mathf.Clamp01(energy.Current / energy.Max);
                _canvasView.PlayerEnergyBar.transform.localScale = new Vector3(finalEnergyScaleX, 1, 1);

                var finalEnergyValue = Mathf.Clamp(energy.Current, 0, energy.Max);
                _canvasView.PlayerEnergyText.text = finalEnergyValue.ToString("F0") + "/" + energy.Current;
            }
        }

        public UnitComponent GetUnit(int entity)
        {
            if (_unitPool.Has(entity))
                return _unitPool.Get(entity);
            throw new Exception("Entity hasn't unit component");
        }

        public void TryDash(int entity, AbilityConfig abilityConfig, bool hit, Vector3 hitVector)
        {
            if (abilityConfig.DASH)
            {
                var time = abilityConfig.dashTime;
                var distance = abilityConfig.dashDistance;
                var useLook = abilityConfig.dashUseLook;

                if (HasState(entity, EUnitState.Dash))
                {
                    ref var dash = ref _dashPool.Get(entity);
                    if (time > dash.TimeLeft)
                    {
                        dash.Time = time;
                        dash.TimeLeft = time;
                        dash.Distance = distance;
                        dash.UseLook = useLook;
                    }
                }
                else
                {
                    Vector3 vector;
                    if (!hit)
                    {
                        var vectors = _vectorsPool.Get(entity);
                        var moveVector = _moveVectorPool.Get(entity);
                        var unit = GetUnit(entity);

                        if (abilityConfig.dashUseLook)
                            vector = vectors.VectorLook;
                        else
                            vector = moveVector.Value;

                        if (vector == Vector3.zero)
                            vector = unit.View.Transform.forward;
                    }
                    else
                    {
                        vector = hitVector;
                        Debug.Log("bam");
                    }

                    _dashPool.Add(entity) = new DashComponent
                    {
                        Time = time,
                        TimeLeft = time,
                        Distance = distance,
                        Vector = vector,
                        UseLook = useLook,
                        VectorActivated = true
                    };
                }
            }
        }

        private void ToggleStun(int entity)
        {
            var unit = _unitPool.Get(entity);
            var abilityData = unit.View.AbilityDamageConfig;

            if (unit.View.AbilityStunOnConfig.ANIMATOR)
            {
                if (HasState(entity, EUnitState.Stun))
                {
                    AddState(entity, EUnitState.Knockout);
                    unit.View.Animator.TriggerAnimations(unit.View.AbilityStunOnConfig);
                }
                else
                {
                    RemoveState(entity, EUnitState.Knockout);
                    unit.View.Animator.TriggerAnimations(unit.View.AbilityStunOffConfig);
                }
            }


            if (abilityData.VFX)
                _vfxService.SpawnVfx(entity, abilityData);
        }
    }
}