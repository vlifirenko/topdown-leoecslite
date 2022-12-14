using CyberNinja.Ecs.Components;
using CyberNinja.Ecs.Components.Unit;
using CyberNinja.Services;
using CyberNinja.Services.Impl;
using CyberNinja.Views;
using Leopotam.EcsLite;
using Leopotam.EcsLite.Di;

namespace CyberNinja.Ecs.Systems.Ability
{
    public class InitAbilitiesSystem : IEcsInitSystem
    {
        private readonly EcsFilterInject<Inc<PlayerComponent>> _playerFilter;
        private readonly EcsFilterInject<Inc<EnemyComponent>> _enemyFilter;
        private readonly EcsCustomInject<IAbilityService> _abilityService;
        private readonly EcsCustomInject<IUnitService> _unitService;
        private readonly EcsCustomInject<CanvasView> _canvasView;
        private readonly EcsPoolInject<PlayerComponent> _playerPool;
        private readonly EcsPoolInject<EnemyComponent> _enemyPool;

        public void Init(IEcsSystems systems)
        {
            InitControls();
            AddPlayerAbilities();
            AddEnemiesAbilities();
        }

        private void InitControls()
        {
            foreach (var entity in _playerFilter.Value)
            {
                var player = _playerPool.Value.Get(entity);

                player.Controls._Player.Ability01_Tap.started
                    += ctx => _abilityService.Value.TryActivateAbility(0, entity);
                player.Controls._Player.Ability02_Tap.started
                    += ctx => _abilityService.Value.TryActivateAbility(1, entity);
                player.Controls._Player.Ability03_Tap.started
                    += ctx => _abilityService.Value.TryActivateAbility(2, entity);
                player.Controls._Player.Ability04_Tap.started
                    += ctx => _abilityService.Value.TryActivateAbility(3, entity);
                player.Controls._Player.Action01_Tap.started
                    += ctx => _abilityService.Value.TryActivateAbility(4, entity);
                player.Controls._Player.Action02_Tap.started
                    += ctx => _abilityService.Value.TryActivateAbility(5, entity);
                player.Controls._Player.Action03_Tap.started
                    += ctx => _abilityService.Value.TryActivateAbility(6, entity);
                player.Controls._Player.Action04_Tap.started
                    += ctx => _abilityService.Value.TryActivateAbility(7, entity);
            }
        }

        private void AddPlayerAbilities()
        {
            foreach (var entity in _playerFilter.Value)
            {
                var unit = _unitService.Value.GetUnit(entity);

                foreach (var abilityItem in unit.View.Abilities)
                {
                    _abilityService.Value.CreateAbility(abilityItem, entity);

                    _canvasView.Value.AbilityImages[abilityItem.slotIndex].sprite = abilityItem.abilityConfig.icon;
                }
            }
        }

        private void AddEnemiesAbilities()
        {
            foreach (var entity in _enemyFilter.Value)
            {
                var unit = _unitService.Value.GetUnit(entity);
                
                foreach (var abilityItem in unit.View.Abilities)
                    _abilityService.Value.CreateAbility(abilityItem, entity);
            }
        }
    }
}